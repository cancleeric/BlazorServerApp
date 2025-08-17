#!/usr/bin/env bash

# Enterprise Identity Server 自動化故障回應系統
# 作者: Enterprise Auth Team
# 版本: 1.0.0

set -e

# 顏色定義
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# 腳本配置
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
LOG_FILE="/var/log/enterprise-ids-auto-response.log"
ALERT_WEBHOOK_URL="${ALERT_WEBHOOK_URL:-http://localhost:9093/api/v1/alerts}"
SERVICE_NAME="enterprise-identity-server"

# 日誌函數
log_event() {
    local level="$1"
    local message="$2"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[$timestamp] [$level] $message" >> "$LOG_FILE"
    echo -e "${BLUE}[$timestamp]${NC} ${level}: $message"
}

log_info() {
    log_event "INFO" "$1"
}

log_success() {
    log_event "SUCCESS" "$1"
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    log_event "WARNING" "$1"
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    log_event "ERROR" "$1"
    echo -e "${RED}[ERROR]${NC} $1"
}

# 發送通知到 Slack/Teams
send_notification() {
    local title="$1"
    local message="$2"
    local severity="${3:-info}"
    local color="good"
    
    case "$severity" in
        "critical") color="danger" ;;
        "warning") color="warning" ;;
        "info") color="good" ;;
    esac
    
    log_info "發送通知: $title - $message"
    
    # 這裡實作實際的通知邏輯 (Slack, Teams, Email 等)
    # curl -X POST "$SLACK_WEBHOOK_URL" \
    #   -H 'Content-type: application/json' \
    #   --data "{\"text\":\"$title\", \"attachments\":[{\"color\":\"$color\", \"text\":\"$message\"}]}"
}

# 檢查服務狀態
check_service_health() {
    local service_url="${1:-http://localhost:5000/health}"
    
    if curl -s -f "$service_url" >/dev/null 2>&1; then
        return 0
    else
        return 1
    fi
}

# 重啟應用服務
restart_application() {
    log_info "開始重啟 Enterprise Identity Server 應用程式..."
    
    # 檢查是否為 Docker 部署
    if docker ps | grep -q "$SERVICE_NAME"; then
        log_info "偵測到 Docker 部署，重啟容器..."
        docker restart "$SERVICE_NAME" || {
            log_error "Docker 容器重啟失敗"
            return 1
        }
    elif systemctl is-active --quiet "$SERVICE_NAME"; then
        log_info "偵測到 Systemd 服務，重啟服務..."
        sudo systemctl restart "$SERVICE_NAME" || {
            log_error "Systemd 服務重啟失敗"
            return 1
        }
    else
        log_error "無法確定服務部署方式，無法自動重啟"
        return 1
    fi
    
    # 等待服務恢復
    local retry_count=0
    local max_retries=30
    
    while [ $retry_count -lt $max_retries ]; do
        if check_service_health; then
            log_success "服務重啟成功，健康檢查通過"
            send_notification "🔄 服務重啟成功" "Enterprise Identity Server 已成功重啟並恢復正常" "info"
            return 0
        fi
        
        sleep 10
        retry_count=$((retry_count + 1))
        log_info "等待服務恢復... ($retry_count/$max_retries)"
    done
    
    log_error "服務重啟後無法恢復正常狀態"
    send_notification "❌ 服務重啟失敗" "Enterprise Identity Server 重啟後無法恢復正常，需要人工介入" "critical"
    return 1
}

# 清理記憶體
cleanup_memory() {
    log_info "開始記憶體清理作業..."
    
    # 清理系統緩存
    sync
    echo 3 > /proc/sys/vm/drop_caches 2>/dev/null || {
        log_warning "無法清理系統緩存 (需要 root 權限)"
    }
    
    # 觸發 .NET GC
    if pgrep -f "dotnet.*EnterpriseIDS" > /dev/null; then
        log_info "觸發 .NET 垃圾回收..."
        # 這裡可以通過 API 端點觸發 GC
        curl -X POST "http://localhost:5000/api/maintenance/gc" 2>/dev/null || {
            log_warning "無法觸發 .NET GC"
        }
    fi
    
    log_success "記憶體清理完成"
    send_notification "🧹 記憶體清理" "已執行記憶體清理作業" "info"
}

# 擴展服務實例
scale_service() {
    local target_instances="${1:-2}"
    
    log_info "開始擴展服務實例到 $target_instances 個..."
    
    if command -v docker-compose &> /dev/null; then
        docker-compose scale "$SERVICE_NAME=$target_instances" || {
            log_error "服務擴展失敗"
            return 1
        }
    else
        log_error "未找到 docker-compose，無法自動擴展"
        return 1
    fi
    
    log_success "服務已擴展到 $target_instances 個實例"
    send_notification "📈 服務擴展" "已將 Enterprise Identity Server 擴展到 $target_instances 個實例" "info"
}

# 修復 LDAP 連線
fix_ldap_connection() {
    log_info "開始修復 LDAP 連線..."
    
    # 重置 LDAP 連線池
    curl -X POST "http://localhost:5000/api/maintenance/ldap/reset" 2>/dev/null || {
        log_warning "無法重置 LDAP 連線池"
    }
    
    # 測試 LDAP 連線
    if curl -s "http://localhost:5000/health/component/ldap" | grep -q "Healthy"; then
        log_success "LDAP 連線修復成功"
        send_notification "🔗 LDAP 修復成功" "LDAP 連線已恢復正常" "info"
        return 0
    else
        log_error "LDAP 連線修復失敗"
        send_notification "❌ LDAP 修復失敗" "LDAP 連線無法恢復，需要人工檢查" "critical"
        return 1
    fi
}

# 清理日誌檔案
cleanup_logs() {
    local max_age_days="${1:-7}"
    
    log_info "開始清理超過 $max_age_days 天的日誌檔案..."
    
    # 清理應用程式日誌
    find "/var/log/enterprise-ids" -name "*.log" -mtime +$max_age_days -delete 2>/dev/null || {
        log_warning "清理應用程式日誌時發生錯誤"
    }
    
    # 清理 Docker 日誌
    if command -v docker &> /dev/null; then
        docker system prune -f --filter "until=${max_age_days}d" >/dev/null 2>&1 || {
            log_warning "清理 Docker 日誌時發生錯誤"
        }
    fi
    
    log_success "日誌清理完成"
    send_notification "🗑️ 日誌清理" "已清理超過 $max_age_days 天的日誌檔案" "info"
}

# 檢查磁碟空間並清理
check_disk_space() {
    local threshold="${1:-90}"
    local disk_usage=$(df / | awk 'NR==2 {print $5}' | sed 's/%//')
    
    if [ "$disk_usage" -gt "$threshold" ]; then
        log_warning "磁碟使用率達到 ${disk_usage}%，開始清理..."
        
        # 清理日誌
        cleanup_logs 3
        
        # 清理臨時檔案
        find /tmp -type f -mtime +1 -delete 2>/dev/null || true
        
        # 清理 APT 快取 (如果是 Ubuntu/Debian)
        if command -v apt-get &> /dev/null; then
            apt-get clean 2>/dev/null || true
        fi
        
        # 重新檢查磁碟使用率
        disk_usage=$(df / | awk 'NR==2 {print $5}' | sed 's/%//')
        log_info "清理後磁碟使用率: ${disk_usage}%"
        
        send_notification "💾 磁碟清理" "磁碟使用率從 ${threshold}% 降到 ${disk_usage}%" "info"
    fi
}

# 處理特定告警
handle_alert() {
    local alert_name="$1"
    local alert_instance="${2:-unknown}"
    local alert_severity="${3:-warning}"
    
    log_info "處理告警: $alert_name (實例: $alert_instance, 嚴重程度: $alert_severity)"
    
    case "$alert_name" in
        "ServiceDown")
            log_info "處理服務下線告警..."
            restart_application
            ;;
        "HighMemoryUsage")
            log_info "處理高記憶體使用率告警..."
            cleanup_memory
            if [ "$alert_severity" = "critical" ]; then
                scale_service 3
            fi
            ;;
        "HighCpuUsage")
            log_info "處理高 CPU 使用率告警..."
            scale_service 2
            ;;
        "LdapConnectionFailure")
            log_info "處理 LDAP 連線失敗告警..."
            fix_ldap_connection
            ;;
        "DiskSpaceLow")
            log_info "處理磁碟空間不足告警..."
            check_disk_space 80
            ;;
        "HighErrorRate")
            log_info "處理高錯誤率告警..."
            cleanup_memory
            if ! check_service_health; then
                restart_application
            fi
            ;;
        *)
            log_warning "未知的告警類型: $alert_name，跳過自動處理"
            ;;
    esac
}

# Webhook 處理器 (從 AlertManager 接收告警)
webhook_handler() {
    local webhook_data="$1"
    
    # 解析 AlertManager webhook 資料
    # 這裡應該實作實際的 JSON 解析邏輯
    local alert_name=$(echo "$webhook_data" | jq -r '.alerts[0].labels.alertname // "unknown"' 2>/dev/null || echo "unknown")
    local alert_instance=$(echo "$webhook_data" | jq -r '.alerts[0].labels.instance // "unknown"' 2>/dev/null || echo "unknown")
    local alert_severity=$(echo "$webhook_data" | jq -r '.alerts[0].labels.severity // "warning"' 2>/dev/null || echo "warning")
    
    handle_alert "$alert_name" "$alert_instance" "$alert_severity"
}

# 健康檢查循環
health_check_loop() {
    local check_interval="${1:-60}"
    
    log_info "開始健康檢查循環 (間隔: ${check_interval}秒)..."
    
    while true; do
        if ! check_service_health; then
            log_warning "服務健康檢查失敗，觸發自動修復..."
            handle_alert "ServiceDown" "localhost" "critical"
        fi
        
        # 檢查磁碟空間
        check_disk_space 85
        
        sleep "$check_interval"
    done
}

# 顯示使用說明
show_help() {
    echo "Enterprise Identity Server 自動化故障回應工具"
    echo ""
    echo "使用方式: $0 [command] [options]"
    echo ""
    echo "可用命令:"
    echo "  restart-app           重啟應用程式"
    echo "  cleanup-memory        清理記憶體"
    echo "  scale [instances]     擴展服務實例"
    echo "  fix-ldap             修復 LDAP 連線"
    echo "  cleanup-logs [days]   清理日誌檔案"
    echo "  check-disk [threshold] 檢查磁碟空間"
    echo "  handle-alert [name] [instance] [severity] 處理特定告警"
    echo "  webhook [json]        處理 webhook 告警"
    echo "  health-loop [interval] 開始健康檢查循環"
    echo "  help                 顯示此說明"
    echo ""
    echo "範例:"
    echo "  $0 restart-app                    # 重啟應用程式"
    echo "  $0 scale 3                        # 擴展到 3 個實例"
    echo "  $0 cleanup-logs 7                 # 清理 7 天前的日誌"
    echo "  $0 handle-alert ServiceDown       # 處理服務下線告警"
    echo "  $0 health-loop 30                 # 每 30 秒執行健康檢查"
}

# 主函數
main() {
    # 確保日誌目錄存在
    mkdir -p "$(dirname "$LOG_FILE")"
    
    case "${1:-help}" in
        "restart-app")
            restart_application
            ;;
        "cleanup-memory")
            cleanup_memory
            ;;
        "scale")
            scale_service "${2:-2}"
            ;;
        "fix-ldap")
            fix_ldap_connection
            ;;
        "cleanup-logs")
            cleanup_logs "${2:-7}"
            ;;
        "check-disk")
            check_disk_space "${2:-90}"
            ;;
        "handle-alert")
            handle_alert "$2" "$3" "$4"
            ;;
        "webhook")
            webhook_handler "$2"
            ;;
        "health-loop")
            health_check_loop "${2:-60}"
            ;;
        "help"|"--help"|"-h")
            show_help
            ;;
        *)
            log_error "未知命令: $1"
            show_help
            exit 1
            ;;
    esac
}

# 執行主函數
main "$@"