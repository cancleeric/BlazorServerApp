using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using CreditMonitoring.Common.Models;

namespace CreditMonitoring.Common.Services.Azure
{
    /// <summary>
    /// Azure Application Insights 監控服務
    /// 提供自定義的應用程式監控和遙測功能
    /// </summary>
    public interface IAzureMonitoringService
    {
        void TrackCreditAlert(CreditAlert alert);
        void TrackLoanAccountActivity(int accountId, string activity, Dictionary<string, string>? properties = null);
        void TrackUserAction(string userId, string action, Dictionary<string, string>? properties = null);
        void TrackApiCall(string apiEndpoint, TimeSpan duration, bool success, string? errorMessage = null);
        void TrackCustomEvent(string eventName, Dictionary<string, string>? properties = null, Dictionary<string, double>? metrics = null);
        IDisposable StartOperation(string operationName);
    }

    public class AzureMonitoringService : IAzureMonitoringService
    {
        private readonly TelemetryClient _telemetryClient;
        private readonly ILogger<AzureMonitoringService> _logger;

        public AzureMonitoringService(
            TelemetryClient telemetryClient,
            ILogger<AzureMonitoringService> logger)
        {
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        /// <summary>
        /// 追蹤信貸警報事件
        /// </summary>
        public void TrackCreditAlert(CreditAlert alert)
        {
            var properties = new Dictionary<string, string>
            {
                ["AlertId"] = alert.Id.ToString(),
                ["AlertType"] = alert.AlertType,
                ["Severity"] = alert.Severity.ToString(),
                ["LoanAccountId"] = alert.LoanAccountId.ToString(),
                ["IsResolved"] = alert.IsResolved.ToString(),
                ["CreatedAt"] = alert.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            };

            if (alert.VoucherId.HasValue)
            {
                properties["VoucherId"] = alert.VoucherId.Value.ToString();
            }

            var metrics = new Dictionary<string, double>
            {
                ["AlertCount"] = 1
            };

            // 根據嚴重程度設定不同的事件類型
            var eventName = alert.Severity switch
            {
                AlertSeverity.Critical => "CriticalCreditAlert",
                AlertSeverity.High => "HighCreditAlert",
                AlertSeverity.Medium => "MediumCreditAlert",
                AlertSeverity.Low => "LowCreditAlert",
                _ => "CreditAlert"
            };

            _telemetryClient.TrackEvent(eventName, properties, metrics);

            _logger.LogInformation(
                "已追蹤信貸警報到 Application Insights: AlertId={AlertId}, Type={AlertType}, Severity={Severity}",
                alert.Id, alert.AlertType, alert.Severity);
        }

        /// <summary>
        /// 追蹤貸款帳戶活動
        /// </summary>
        public void TrackLoanAccountActivity(int accountId, string activity, Dictionary<string, string>? properties = null)
        {
            var eventProperties = new Dictionary<string, string>
            {
                ["LoanAccountId"] = accountId.ToString(),
                ["Activity"] = activity,
                ["Timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };

            if (properties != null)
            {
                foreach (var prop in properties)
                {
                    eventProperties[prop.Key] = prop.Value;
                }
            }

            _telemetryClient.TrackEvent("LoanAccountActivity", eventProperties);

            _logger.LogInformation(
                "已追蹤貸款帳戶活動到 Application Insights: AccountId={AccountId}, Activity={Activity}",
                accountId, activity);
        }

        /// <summary>
        /// 追蹤用戶操作
        /// </summary>
        public void TrackUserAction(string userId, string action, Dictionary<string, string>? properties = null)
        {
            var eventProperties = new Dictionary<string, string>
            {
                ["UserId"] = userId,
                ["Action"] = action,
                ["Timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };

            if (properties != null)
            {
                foreach (var prop in properties)
                {
                    eventProperties[prop.Key] = prop.Value;
                }
            }

            _telemetryClient.TrackEvent("UserAction", eventProperties);

            _logger.LogDebug(
                "已追蹤用戶操作到 Application Insights: UserId={UserId}, Action={Action}",
                userId, action);
        }

        /// <summary>
        /// 追蹤 API 呼叫效能
        /// </summary>
        public void TrackApiCall(string apiEndpoint, TimeSpan duration, bool success, string? errorMessage = null)
        {
            var dependency = new DependencyTelemetry
            {
                Name = apiEndpoint,
                Type = "HTTP",
                Data = apiEndpoint,
                Duration = duration,
                Success = success,
                Timestamp = DateTimeOffset.UtcNow
            };

            if (!success && !string.IsNullOrEmpty(errorMessage))
            {
                dependency.Properties["ErrorMessage"] = errorMessage;
            }

            _telemetryClient.TrackDependency(dependency);

            var properties = new Dictionary<string, string>
            {
                ["ApiEndpoint"] = apiEndpoint,
                ["Success"] = success.ToString(),
                ["Duration"] = duration.TotalMilliseconds.ToString("F2")
            };

            if (!string.IsNullOrEmpty(errorMessage))
            {
                properties["ErrorMessage"] = errorMessage;
            }

            _telemetryClient.TrackEvent("ApiCall", properties);

            _logger.LogInformation(
                "已追蹤 API 呼叫到 Application Insights: Endpoint={Endpoint}, Duration={Duration}ms, Success={Success}",
                apiEndpoint, duration.TotalMilliseconds, success);
        }

        /// <summary>
        /// 追蹤自定義事件
        /// </summary>
        public void TrackCustomEvent(string eventName, Dictionary<string, string>? properties = null, Dictionary<string, double>? metrics = null)
        {
            _telemetryClient.TrackEvent(eventName, properties, metrics);

            _logger.LogDebug("已追蹤自定義事件到 Application Insights: {EventName}", eventName);
        }

        /// <summary>
        /// 開始追蹤操作
        /// </summary>
        public IDisposable StartOperation(string operationName)
        {
            var operation = _telemetryClient.StartOperation<RequestTelemetry>(operationName);
            
            _logger.LogDebug("已開始追蹤操作: {OperationName}", operationName);
            
            return operation;
        }
    }

    /// <summary>
    /// 效能監控裝飾器
    /// 用於自動追蹤方法執行時間
    /// </summary>
    public class PerformanceMonitoringDecorator<T> : DispatchProxy
    {
        private T _target = default!;
        private IAzureMonitoringService _monitoringService = default!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var stopwatch = Stopwatch.StartNew();
            var methodName = $"{typeof(T).Name}.{targetMethod?.Name}";

            try
            {
                var result = targetMethod?.Invoke(_target, args);
                
                stopwatch.Stop();
                
                _monitoringService.TrackCustomEvent(
                    "MethodExecution",
                    new Dictionary<string, string>
                    {
                        ["MethodName"] = methodName,
                        ["Success"] = "true"
                    },
                    new Dictionary<string, double>
                    {
                        ["ExecutionTime"] = stopwatch.ElapsedMilliseconds
                    });

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                _monitoringService.TrackCustomEvent(
                    "MethodExecution",
                    new Dictionary<string, string>
                    {
                        ["MethodName"] = methodName,
                        ["Success"] = "false",
                        ["ErrorMessage"] = ex.Message,
                        ["ExceptionType"] = ex.GetType().Name
                    },
                    new Dictionary<string, double>
                    {
                        ["ExecutionTime"] = stopwatch.ElapsedMilliseconds
                    });

                throw;
            }
        }

        public static T Create(T target, IAzureMonitoringService monitoringService)
        {
            var proxy = Create<T, PerformanceMonitoringDecorator<T>>() as PerformanceMonitoringDecorator<T>;
            proxy!._target = target;
            proxy._monitoringService = monitoringService;
            return (T)(object)proxy;
        }
    }
}
