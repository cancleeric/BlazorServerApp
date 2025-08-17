#!/usr/bin/env bash

# Enterprise Identity Server 監控系統測試腳本
# 作者: Enterprise Auth Team
# 版本: 1.0.0

set -e

# 顏色定義
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# 測試配置
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
TEST_REPORT_FILE="test-results-$(date +%Y%m%d-%H%M%S).json"

# 服務端點
ENTERPRISE_IDS_URL="http://localhost:5000"
PROMETHEUS_URL="http://localhost:9090"
GRAFANA_URL="http://localhost:3000"
KIBANA_URL="http://localhost:5601"
ELASTICSEARCH_URL="http://localhost:9200"
ALERTMANAGER_URL="http://localhost:9093"

# 測試結果追蹤
declare -A test_results
total_tests=0
passed_tests=0
failed_tests=0

# 日誌函數
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[PASS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[FAIL]${NC} $1"
}

# 執行測試並記錄結果
run_test() {
    local test_name="$1"
    local test_command="$2"
    local test_description="$3"
    
    total_tests=$((total_tests + 1))
    
    log_info "執行測試: $test_name - $test_description"
    
    if eval "$test_command" >/dev/null 2>&1; then
        log_success "$test_name"
        test_results["$test_name"]="PASS"
        passed_tests=$((passed_tests + 1))
        return 0
    else
        log_error "$test_name"
        test_results["$test_name"]="FAIL"
        failed_tests=$((failed_tests + 1))
        return 1
    fi
}

# 測試 HTTP 端點可達性
test_endpoint_reachability() {
    log_info "=== 測試端點可達性 ==="
    
    run_test "enterprise_ids_health" \
        "curl -s -f $ENTERPRISE_IDS_URL/health" \
        "Enterprise Identity Server 健康檢查端點"
    
    run_test "enterprise_ids_metrics" \
        "curl -s -f $ENTERPRISE_IDS_URL/metrics" \
        "Enterprise Identity Server Prometheus 指標端點"
    
    run_test "prometheus_api" \
        "curl -s -f $PROMETHEUS_URL/api/v1/query?query=up" \
        "Prometheus API 查詢端點"
    
    run_test "grafana_api" \
        "curl -s -f $GRAFANA_URL/api/health" \
        "Grafana 健康檢查端點"
    
    run_test "elasticsearch_health" \
        "curl -s -f $ELASTICSEARCH_URL/_cluster/health" \
        "Elasticsearch 叢集健康狀態"
    
    run_test "kibana_status" \
        "curl -s -f $KIBANA_URL/api/status" \
        "Kibana 狀態端點"
    
    run_test "alertmanager_status" \
        "curl -s -f $ALERTMANAGER_URL/-/healthy" \
        "AlertManager 健康狀態"
}

# 測試指標收集
test_metrics_collection() {
    log_info "=== 測試指標收集 ==="
    
    # 測試 Enterprise IDS 指標
    run_test "enterprise_ids_system_metrics" \
        "curl -s $ENTERPRISE_IDS_URL/metrics | grep -q 'enterprise_ids_memory_usage_bytes'" \
        "系統記憶體使用量指標"
    
    run_test "enterprise_ids_http_metrics" \
        "curl -s $ENTERPRISE_IDS_URL/metrics | grep -q 'enterprise_ids_http_requests_total'" \
        "HTTP 請求總數指標"
    
    run_test "enterprise_ids_auth_metrics" \
        "curl -s $ENTERPRISE_IDS_URL/metrics | grep -q 'enterprise_ids_authentication_attempts_total'" \
        "認證嘗試總數指標"
    
    # 測試 Prometheus 指標收集
    run_test "prometheus_scrape_enterprise_ids" \
        "curl -s '$PROMETHEUS_URL/api/v1/query?query=up{job=\"enterprise-identity-server\"}' | grep -q '\"result\"'" \
        "Prometheus 收集 Enterprise IDS 指標"
    
    run_test "prometheus_scrape_node_exporter" \
        "curl -s '$PROMETHEUS_URL/api/v1/query?query=up{job=\"node-exporter\"}' | grep -q '\"result\"'" \
        "Prometheus 收集 Node Exporter 指標"
}

# 測試健康檢查
test_health_checks() {
    log_info "=== 測試健康檢查 ==="
    
    run_test "enterprise_ids_overall_health" \
        "curl -s $ENTERPRISE_IDS_URL/health | jq -e '.Status == \"Healthy\"'" \
        "整體健康狀態"
    
    run_test "enterprise_ids_readiness" \
        "curl -s $ENTERPRISE_IDS_URL/health/ready | jq -e '.Status == \"Healthy\"'" \
        "就緒狀態檢查"
    
    run_test "enterprise_ids_liveness" \
        "curl -s $ENTERPRISE_IDS_URL/health/live | jq -e '.Status == \"Healthy\"'" \
        "存活狀態檢查"
    
    run_test "enterprise_ids_component_health" \
        "curl -s $ENTERPRISE_IDS_URL/health/component/database | jq -e '.Status'" \
        "組件健康檢查"
    
    run_test "enterprise_ids_health_summary" \
        "curl -s $ENTERPRISE_IDS_URL/health/summary | jq -e '.OverallStatus'" \
        "健康檢查摘要"
}

# 測試 Grafana 儀表板
test_grafana_dashboards() {
    log_info "=== 測試 Grafana 儀表板 ==="
    
    # 使用基本認證 (admin/admin123)
    local auth="admin:admin123"
    
    run_test "grafana_datasource" \
        "curl -s -u $auth $GRAFANA_URL/api/datasources | jq -e '.[0].name == \"Prometheus\"'" \
        "Prometheus 資料源配置"
    
    run_test "grafana_dashboard_list" \
        "curl -s -u $auth $GRAFANA_URL/api/search | jq -e 'length > 0'" \
        "儀表板列表"
    
    run_test "grafana_metrics_query" \
        "curl -s -u $auth -H 'Content-Type: application/json' -d '{\"queries\":[{\"expr\":\"up\",\"refId\":\"A\"}]}' $GRAFANA_URL/api/ds/query" \
        "指標查詢功能"
}

# 測試告警規則
test_alerting_rules() {
    log_info "=== 測試告警規則 ==="
    
    run_test "prometheus_rules_loaded" \
        "curl -s $PROMETHEUS_URL/api/v1/rules | jq -e '.data.groups | length > 0'" \
        "Prometheus 告警規則載入"
    
    run_test "alertmanager_config" \
        "curl -s $ALERTMANAGER_URL/api/v1/status | jq -e '.data.configYAML'" \
        "AlertManager 配置"
    
    run_test "prometheus_alerts" \
        "curl -s $PROMETHEUS_URL/api/v1/alerts" \
        "Prometheus 告警狀態"
}

# 測試 ELK Stack
test_elk_stack() {
    log_info "=== 測試 ELK Stack ==="
    
    run_test "elasticsearch_cluster" \
        "curl -s $ELASTICSEARCH_URL/_cluster/health | jq -e '.status == \"yellow\" or .status == \"green\"'" \
        "Elasticsearch 叢集狀態"
    
    run_test "elasticsearch_indices" \
        "curl -s $ELASTICSEARCH_URL/_cat/indices | grep -q enterprise-ids" \
        "Enterprise IDS 日誌索引"
    
    run_test "kibana_saved_objects" \
        "curl -s $KIBANA_URL/api/saved_objects/_find?type=index-pattern" \
        "Kibana 索引模式"
    
    run_test "logstash_pipeline" \
        "curl -s http://localhost:9600/_node/stats/pipeline" \
        "Logstash 管道狀態"
}

# 測試自動化回應
test_automated_response() {
    log_info "=== 測試自動化回應 ==="
    
    local response_script="$SCRIPT_DIR/automated-response.sh"
    
    run_test "response_script_exists" \
        "test -x $response_script" \
        "自動回應腳本存在且可執行"
    
    run_test "response_script_help" \
        "$response_script help" \
        "自動回應腳本說明功能"
    
    # 測試記憶體清理功能 (非破壞性)
    run_test "response_cleanup_memory" \
        "$response_script cleanup-memory" \
        "記憶體清理功能"
}

# 效能測試
test_performance() {
    log_info "=== 測試系統效能 ==="
    
    # 測試健康檢查回應時間
    local health_response_time=$(curl -s -w "%{time_total}" -o /dev/null $ENTERPRISE_IDS_URL/health)
    if (( $(echo "$health_response_time < 1.0" | bc -l) )); then
        log_success "健康檢查回應時間: ${health_response_time}s (< 1s)"
        test_results["health_response_time"]="PASS"
        passed_tests=$((passed_tests + 1))
    else
        log_error "健康檢查回應時間: ${health_response_time}s (>= 1s)"
        test_results["health_response_time"]="FAIL"
        failed_tests=$((failed_tests + 1))
    fi
    total_tests=$((total_tests + 1))
    
    # 測試指標端點回應時間
    local metrics_response_time=$(curl -s -w "%{time_total}" -o /dev/null $ENTERPRISE_IDS_URL/metrics)
    if (( $(echo "$metrics_response_time < 2.0" | bc -l) )); then
        log_success "指標端點回應時間: ${metrics_response_time}s (< 2s)"
        test_results["metrics_response_time"]="PASS"
        passed_tests=$((passed_tests + 1))
    else
        log_error "指標端點回應時間: ${metrics_response_time}s (>= 2s)"
        test_results["metrics_response_time"]="FAIL"
        failed_tests=$((failed_tests + 1))
    fi
    total_tests=$((total_tests + 1))
}

# 產生負載測試指標
generate_test_metrics() {
    log_info "=== 產生測試指標 ==="
    
    # 產生測試 HTTP 請求
    for i in {1..10}; do
        curl -s "$ENTERPRISE_IDS_URL/health" >/dev/null &
        curl -s "$ENTERPRISE_IDS_URL/health/ready" >/dev/null &
        curl -s "$ENTERPRISE_IDS_URL/api/metrics/summary" >/dev/null &
    done
    wait
    
    # 產生測試指標
    local test_data='{
        "AuthResult": "success",
        "TokenType": "access_token", 
        "TenantId": "test-tenant",
        "ActiveSessions": 42,
        "ActiveConnections": 15
    }'
    
    curl -s -H "Content-Type: application/json" \
         -d "$test_data" \
         "$ENTERPRISE_IDS_URL/api/metrics/test" >/dev/null
    
    log_success "測試指標生成完成"
    sleep 5 # 等待指標收集
}

# 生成測試報告
generate_test_report() {
    log_info "=== 生成測試報告 ==="
    
    local report_json="{
        \"test_run\": {
            \"timestamp\": \"$(date -Iseconds)\",
            \"total_tests\": $total_tests,
            \"passed_tests\": $passed_tests,
            \"failed_tests\": $failed_tests,
            \"success_rate\": \"$(echo "scale=2; $passed_tests * 100 / $total_tests" | bc)%\"
        },
        \"test_results\": {"
    
    local first=true
    for test_name in "${!test_results[@]}"; do
        if [ "$first" = false ]; then
            report_json+=","
        fi
        report_json+="\"$test_name\": \"${test_results[$test_name]}\""
        first=false
    done
    
    report_json+="}}"
    
    echo "$report_json" | jq '.' > "$TEST_REPORT_FILE"
    
    log_success "測試報告已生成: $TEST_REPORT_FILE"
}

# 顯示測試摘要
show_test_summary() {
    echo ""
    echo "========================================"
    echo "           測試結果摘要"
    echo "========================================"
    echo "總測試數量: $total_tests"
    echo "通過測試: $passed_tests"
    echo "失敗測試: $failed_tests"
    echo "成功率: $(echo "scale=2; $passed_tests * 100 / $total_tests" | bc)%"
    echo "========================================"
    
    if [ $failed_tests -eq 0 ]; then
        log_success "🎉 所有測試都通過了！"
        return 0
    else
        log_error "❌ 有 $failed_tests 個測試失敗"
        
        echo ""
        echo "失敗的測試:"
        for test_name in "${!test_results[@]}"; do
            if [ "${test_results[$test_name]}" = "FAIL" ]; then
                echo "  - $test_name"
            fi
        done
        return 1
    fi
}

# 主測試函數
run_all_tests() {
    log_info "開始 Enterprise Identity Server 監控系統測試..."
    echo ""
    
    # 檢查必要工具
    if ! command -v jq &> /dev/null; then
        log_error "需要安裝 jq 工具來解析 JSON"
        exit 1
    fi
    
    if ! command -v bc &> /dev/null; then
        log_error "需要安裝 bc 工具來進行數學計算"
        exit 1
    fi
    
    # 執行所有測試
    generate_test_metrics
    test_endpoint_reachability
    test_metrics_collection
    test_health_checks
    test_grafana_dashboards
    test_alerting_rules
    test_elk_stack
    test_automated_response
    test_performance
    
    # 生成報告和摘要
    generate_test_report
    show_test_summary
}

# 顯示使用說明
show_help() {
    echo "Enterprise Identity Server 監控系統測試工具"
    echo ""
    echo "使用方式: $0 [command]"
    echo ""
    echo "可用命令:"
    echo "  all                執行所有測試"
    echo "  endpoints          測試端點可達性"
    echo "  metrics            測試指標收集"
    echo "  health             測試健康檢查"
    echo "  grafana            測試 Grafana 儀表板"
    echo "  alerts             測試告警規則"
    echo "  elk                測試 ELK Stack"
    echo "  response           測試自動化回應"
    echo "  performance        測試系統效能"
    echo "  generate-metrics   生成測試指標"
    echo "  help               顯示此說明"
    echo ""
    echo "範例:"
    echo "  $0 all             # 執行完整測試套件"
    echo "  $0 endpoints       # 只測試端點可達性"
    echo "  $0 performance     # 只測試效能"
}

# 主函數
main() {
    case "${1:-all}" in
        "all")
            run_all_tests
            ;;
        "endpoints")
            test_endpoint_reachability
            show_test_summary
            ;;
        "metrics")
            generate_test_metrics
            test_metrics_collection
            show_test_summary
            ;;
        "health")
            test_health_checks
            show_test_summary
            ;;
        "grafana")
            test_grafana_dashboards
            show_test_summary
            ;;
        "alerts")
            test_alerting_rules
            show_test_summary
            ;;
        "elk")
            test_elk_stack
            show_test_summary
            ;;
        "response")
            test_automated_response
            show_test_summary
            ;;
        "performance")
            test_performance
            show_test_summary
            ;;
        "generate-metrics")
            generate_test_metrics
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