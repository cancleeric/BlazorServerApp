#!/bin/bash
# NGINX 負載平衡器健康檢查腳本
# 檢查所有上游伺服器的健康狀態

set -e

# 配置變數
NGINX_STATUS_URL="http://localhost:8080/nginx_status"
HEALTH_CHECK_URL="http://localhost:8080/load_balancer_health"
LOG_FILE="/var/log/nginx/health_check.log"
ALERT_EMAIL="admin@enterpriseids.local"

# 顏色碼
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 日誌函數
log_message() {
    echo "$(date '+%Y-%m-%d %H:%M:%S') - $1" | tee -a "$LOG_FILE"
}

# 檢查 NGINX 狀態
check_nginx_status() {
    log_message "檢查 NGINX 狀態..."
    
    if curl -f -s "$NGINX_STATUS_URL" > /dev/null; then
        echo -e "${GREEN}✓ NGINX 狀態：正常${NC}"
        return 0
    else
        echo -e "${RED}✗ NGINX 狀態：異常${NC}"
        return 1
    fi
}

# 檢查上游伺服器健康狀態
check_upstream_health() {
    log_message "檢查上游伺服器健康狀態..."
    
    local upstream_servers=(
        "app1.enterpriseids.local:5000"
        "app2.enterpriseids.local:5000"
        "app3.enterpriseids.local:5000"
        "app4.enterpriseids.local:5000"
    )
    
    local healthy_count=0
    local total_count=${#upstream_servers[@]}
    
    for server in "${upstream_servers[@]}"; do
        if curl -f -s -m 5 "http://$server/health" > /dev/null; then
            echo -e "${GREEN}✓ $server：健康${NC}"
            ((healthy_count++))
        else
            echo -e "${RED}✗ $server：不健康${NC}"
            log_message "WARNING: Upstream server $server is not healthy"
        fi
    done
    
    echo "健康伺服器數量：$healthy_count/$total_count"
    
    # 如果健康的伺服器少於 50%，發出警告
    if [ $((healthy_count * 2)) -lt $total_count ]; then
        echo -e "${RED}警告：健康伺服器數量不足 50%${NC}"
        log_message "CRITICAL: Less than 50% of upstream servers are healthy ($healthy_count/$total_count)"
        return 1
    fi
    
    return 0
}

# 檢查負載平衡器配置
check_load_balancer_config() {
    log_message "檢查負載平衡器配置..."
    
    # 檢查 NGINX 配置語法
    if nginx -t 2>/dev/null; then
        echo -e "${GREEN}✓ NGINX 配置語法：正確${NC}"
    else
        echo -e "${RED}✗ NGINX 配置語法：錯誤${NC}"
        log_message "ERROR: NGINX configuration syntax error"
        return 1
    fi
    
    # 檢查配置檔案是否存在
    local config_files=(
        "/etc/nginx/nginx.conf"
        "/etc/nginx/conf.d/upstream_status.conf"
    )
    
    for config_file in "${config_files[@]}"; do
        if [ -f "$config_file" ]; then
            echo -e "${GREEN}✓ 配置檔案存在：$config_file${NC}"
        else
            echo -e "${RED}✗ 配置檔案遺失：$config_file${NC}"
            log_message "ERROR: Configuration file missing: $config_file"
            return 1
        fi
    done
    
    return 0
}

# 效能檢查
check_performance_metrics() {
    log_message "檢查效能指標..."
    
    # 檢查連線數
    local active_connections=$(curl -s "$NGINX_STATUS_URL" | grep "Active connections" | awk '{print $3}')
    echo "活躍連線數：$active_connections"
    
    if [ "$active_connections" -gt 1000 ]; then
        echo -e "${YELLOW}警告：活躍連線數較高 ($active_connections)${NC}"
        log_message "WARNING: High number of active connections: $active_connections"
    fi
    
    # 檢查請求速率
    local requests=$(curl -s "$NGINX_STATUS_URL" | grep "server accepts handled requests" | awk '{print $3}')
    echo "總請求數：$requests"
    
    return 0
}

# SSL 憑證檢查
check_ssl_certificates() {
    log_message "檢查 SSL 憑證..."
    
    local cert_file="/etc/nginx/ssl/enterpriseids.local.crt"
    
    if [ -f "$cert_file" ]; then
        # 檢查憑證到期日
        local expiry_date=$(openssl x509 -in "$cert_file" -noout -enddate | cut -d= -f2)
        local expiry_timestamp=$(date -d "$expiry_date" +%s)
        local current_timestamp=$(date +%s)
        local days_until_expiry=$(( (expiry_timestamp - current_timestamp) / 86400 ))
        
        echo "SSL 憑證到期日：$expiry_date"
        echo "剩餘天數：$days_until_expiry 天"
        
        if [ $days_until_expiry -lt 30 ]; then
            echo -e "${YELLOW}警告：SSL 憑證將在 30 天內到期${NC}"
            log_message "WARNING: SSL certificate expires in $days_until_expiry days"
        elif [ $days_until_expiry -lt 7 ]; then
            echo -e "${RED}緊急：SSL 憑證將在 7 天內到期${NC}"
            log_message "CRITICAL: SSL certificate expires in $days_until_expiry days"
        else
            echo -e "${GREEN}✓ SSL 憑證狀態：正常${NC}"
        fi
    else
        echo -e "${RED}✗ SSL 憑證檔案不存在${NC}"
        log_message "ERROR: SSL certificate file not found: $cert_file"
        return 1
    fi
    
    return 0
}

# 主要健康檢查函數
main_health_check() {
    echo "開始 Enterprise IDS 負載平衡器健康檢查..."
    echo "檢查時間：$(date)"
    echo "=================================="
    
    local overall_status=0
    
    # 執行各項檢查
    check_nginx_status || overall_status=1
    echo ""
    
    check_upstream_health || overall_status=1
    echo ""
    
    check_load_balancer_config || overall_status=1
    echo ""
    
    check_performance_metrics || overall_status=1
    echo ""
    
    check_ssl_certificates || overall_status=1
    echo ""
    
    # 總結
    echo "=================================="
    if [ $overall_status -eq 0 ]; then
        echo -e "${GREEN}✓ 整體健康狀態：良好${NC}"
        log_message "INFO: Overall health check passed"
    else
        echo -e "${RED}✗ 整體健康狀態：有問題${NC}"
        log_message "ERROR: Overall health check failed"
    fi
    
    return $overall_status
}

# 產生健康報告
generate_health_report() {
    local report_file="/var/log/nginx/health_report_$(date +%Y%m%d_%H%M%S).json"
    
    cat > "$report_file" << EOF
{
    "timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
    "load_balancer": {
        "nginx_status": "$(curl -s $NGINX_STATUS_URL | head -n1 || echo 'ERROR')",
        "configuration_valid": $(nginx -t &>/dev/null && echo 'true' || echo 'false'),
        "ssl_certificate_days_remaining": $days_until_expiry
    },
    "upstream_servers": {
        "total": 4,
        "healthy": $healthy_count,
        "health_percentage": $(( healthy_count * 100 / 4 ))
    },
    "performance": {
        "active_connections": $active_connections,
        "total_requests": $requests
    },
    "overall_status": "$([ $overall_status -eq 0 ] && echo 'healthy' || echo 'unhealthy')"
}
EOF
    
    echo "健康報告已儲存至：$report_file"
}

# 腳本入口點
if [ "${1:-}" = "--report" ]; then
    main_health_check
    generate_health_report
elif [ "${1:-}" = "--quiet" ]; then
    main_health_check > /dev/null 2>&1
    exit $?
else
    main_health_check
fi

exit $overall_status