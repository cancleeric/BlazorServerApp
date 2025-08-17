#!/usr/bin/env bash

# Enterprise Identity Server 監控堆疊管理腳本
# 作者: Enterprise Auth Team
# 版本: 1.0.0

set -e

# 顏色定義
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# 腳本配置
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
DOCKER_COMPOSE_FILE="$PROJECT_ROOT/docker-compose.monitoring.yml"

# 日誌函數
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# 檢查必要工具
check_prerequisites() {
    log_info "檢查必要工具..."
    
    if ! command -v docker &> /dev/null; then
        log_error "Docker 未安裝或不在 PATH 中"
        exit 1
    fi
    
    if ! command -v docker-compose &> /dev/null; then
        log_error "Docker Compose 未安裝或不在 PATH 中"
        exit 1
    fi
    
    if [ ! -f "$DOCKER_COMPOSE_FILE" ]; then
        log_error "找不到 Docker Compose 檔案: $DOCKER_COMPOSE_FILE"
        exit 1
    fi
    
    log_success "必要工具檢查完成"
}

# 啟動監控堆疊
start_monitoring() {
    log_info "啟動 Enterprise Identity Server 監控堆疊..."
    
    cd "$PROJECT_ROOT"
    
    # 創建必要的目錄
    log_info "創建必要的目錄..."
    mkdir -p logs
    mkdir -p data/{prometheus,grafana,elasticsearch,alertmanager}
    
    # 設定目錄權限
    log_info "設定目錄權限..."
    chmod 777 data/elasticsearch
    chmod 777 data/grafana
    
    # 啟動服務
    log_info "啟動 Docker 容器..."
    docker-compose -f "$DOCKER_COMPOSE_FILE" up -d
    
    # 等待服務啟動
    log_info "等待服務啟動..."
    sleep 30
    
    # 檢查服務狀態
    check_services_health
    
    # 顯示訪問資訊
    show_access_info
    
    log_success "監控堆疊啟動完成！"
}

# 停止監控堆疊
stop_monitoring() {
    log_info "停止 Enterprise Identity Server 監控堆疊..."
    
    cd "$PROJECT_ROOT"
    docker-compose -f "$DOCKER_COMPOSE_FILE" down
    
    log_success "監控堆疊已停止"
}

# 重啟監控堆疊
restart_monitoring() {
    log_info "重啟 Enterprise Identity Server 監控堆疊..."
    stop_monitoring
    sleep 5
    start_monitoring
}

# 檢查服務健康狀態
check_services_health() {
    local services=(
        "Prometheus:http://localhost:9090/-/healthy"
        "Grafana:http://localhost:3000/api/health"
        "Elasticsearch:http://localhost:9200/_cluster/health"
        "Kibana:http://localhost:5601/api/status"
        "AlertManager:http://localhost:9093/-/healthy"
    )
    
    log_info "檢查服務健康狀態..."
    
    for service in "${services[@]}"; do
        IFS=':' read -ra ADDR <<< "$service"
        service_name="${ADDR[0]}"
        health_url="${ADDR[1]}:${ADDR[2]}"
        
        printf "檢查 %-15s ... " "$service_name"
        
        if curl -s "$health_url" > /dev/null 2>&1; then
            echo -e "${GREEN}健康${NC}"
        else
            echo -e "${RED}不健康${NC}"
        fi
    done
}

# 顯示訪問資訊
show_access_info() {
    log_info "服務訪問資訊:"
    echo "----------------------------------------"
    echo "🔍 Prometheus:    http://localhost:9090"
    echo "📊 Grafana:       http://localhost:3000 (admin/admin123)"
    echo "📋 Kibana:        http://localhost:5601"
    echo "🚨 AlertManager:  http://localhost:9093"
    echo "🔍 Elasticsearch: http://localhost:9200"
    echo "📈 Node Exporter: http://localhost:9100"
    echo "🔴 Redis:         localhost:6379"
    echo "----------------------------------------"
    echo ""
    echo "📚 Enterprise Identity Server 指標端點:"
    echo "   http://localhost:5000/metrics"
    echo "   http://localhost:5000/health"
    echo ""
}

# 查看服務日誌
view_logs() {
    local service="$1"
    
    if [ -z "$service" ]; then
        log_info "可用的服務:"
        docker-compose -f "$DOCKER_COMPOSE_FILE" ps --services
        return
    fi
    
    log_info "查看 $service 服務日誌..."
    cd "$PROJECT_ROOT"
    docker-compose -f "$DOCKER_COMPOSE_FILE" logs -f "$service"
}

# 清理資料
cleanup_data() {
    log_warning "這將刪除所有監控資料，包括:"
    echo "  - Prometheus 指標資料"
    echo "  - Grafana 儀表板設定"
    echo "  - Elasticsearch 日誌資料"
    echo "  - AlertManager 設定"
    echo ""
    
    read -p "確定要繼續嗎? (y/N): " -n 1 -r
    echo
    
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        log_info "停止服務..."
        stop_monitoring
        
        log_info "清理資料..."
        cd "$PROJECT_ROOT"
        docker-compose -f "$DOCKER_COMPOSE_FILE" down -v
        sudo rm -rf data/
        
        log_success "資料清理完成"
    else
        log_info "取消清理操作"
    fi
}

# 備份設定
backup_config() {
    local backup_dir="backup/monitoring-$(date +%Y%m%d-%H%M%S)"
    
    log_info "備份監控設定到 $backup_dir..."
    
    mkdir -p "$backup_dir"
    cp -r monitoring/ "$backup_dir/"
    cp docker-compose.monitoring.yml "$backup_dir/"
    
    log_success "設定備份完成: $backup_dir"
}

# 更新監控堆疊
update_stack() {
    log_info "更新監控堆疊..."
    
    # 備份設定
    backup_config
    
    # 拉取最新映像
    cd "$PROJECT_ROOT"
    docker-compose -f "$DOCKER_COMPOSE_FILE" pull
    
    # 重啟服務
    restart_monitoring
    
    log_success "監控堆疊更新完成"
}

# 顯示使用說明
show_help() {
    echo "Enterprise Identity Server 監控堆疊管理工具"
    echo ""
    echo "使用方式: $0 [command]"
    echo ""
    echo "可用命令:"
    echo "  start      啟動監控堆疊"
    echo "  stop       停止監控堆疊"
    echo "  restart    重啟監控堆疊"
    echo "  status     檢查服務狀態"
    echo "  logs       查看服務日誌 [service_name]"
    echo "  cleanup    清理所有資料"
    echo "  backup     備份設定"
    echo "  update     更新監控堆疊"
    echo "  help       顯示此說明"
    echo ""
    echo "範例:"
    echo "  $0 start           # 啟動監控堆疊"
    echo "  $0 logs grafana    # 查看 Grafana 日誌"
    echo "  $0 status          # 檢查所有服務狀態"
}

# 主函數
main() {
    case "${1:-help}" in
        "start")
            check_prerequisites
            start_monitoring
            ;;
        "stop")
            stop_monitoring
            ;;
        "restart")
            check_prerequisites
            restart_monitoring
            ;;
        "status")
            check_services_health
            ;;
        "logs")
            view_logs "$2"
            ;;
        "cleanup")
            cleanup_data
            ;;
        "backup")
            backup_config
            ;;
        "update")
            check_prerequisites
            update_stack
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