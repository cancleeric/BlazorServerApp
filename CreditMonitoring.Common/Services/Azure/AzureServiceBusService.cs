using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using CreditMonitoring.Common.Models;
using System.Runtime.CompilerServices;

namespace CreditMonitoring.Common.Services.Azure
{
    /// <summary>
    /// Azure Service Bus 訊息服務
    /// 用於處理信貸警報和通知的異步訊息傳遞
    /// </summary>
    public interface IAzureServiceBusService
    {
        Task SendCreditAlertAsync(CreditAlert alert);
        Task SendNotificationAsync(object notification);
        IAsyncEnumerable<CreditAlert> ReceiveCreditAlertsAsync(CancellationToken cancellationToken = default);
    }

    public class AzureServiceBusService : IAzureServiceBusService, IAsyncDisposable
    {
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ServiceBusSender _creditAlertsSender;
        private readonly ServiceBusSender _notificationsSender;
        private readonly ServiceBusReceiver _creditAlertsReceiver;
        private readonly ILogger<AzureServiceBusService> _logger;
        private readonly string _creditAlertsQueueName;
        private readonly string _notificationsQueueName;

        public AzureServiceBusService(
            IConfiguration configuration,
            ILogger<AzureServiceBusService> logger)
        {
            _logger = logger;
            var connectionString = configuration.GetConnectionString("ServiceBusConnection") 
                ?? throw new InvalidOperationException("Service Bus connection string not found");
            
            _creditAlertsQueueName = configuration["ServiceBus:CreditAlertsQueueName"] ?? "credit-alerts";
            _notificationsQueueName = configuration["ServiceBus:NotificationsQueueName"] ?? "notifications";

            _serviceBusClient = new ServiceBusClient(connectionString);
            _creditAlertsSender = _serviceBusClient.CreateSender(_creditAlertsQueueName);
            _notificationsSender = _serviceBusClient.CreateSender(_notificationsQueueName);
            _creditAlertsReceiver = _serviceBusClient.CreateReceiver(_creditAlertsQueueName);
        }

        /// <summary>
        /// 發送信貸警報到 Service Bus 佇列
        /// </summary>
        public async Task SendCreditAlertAsync(CreditAlert alert)
        {
            try
            {
                var messageBody = JsonSerializer.Serialize(alert);
                var message = new ServiceBusMessage(messageBody)
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Subject = "CreditAlert",
                    ContentType = "application/json"
                };

                // 添加自定義屬性
                message.ApplicationProperties.Add("AlertType", alert.AlertType);
                message.ApplicationProperties.Add("Severity", alert.Severity.ToString());
                message.ApplicationProperties.Add("LoanAccountId", alert.LoanAccountId);
                message.ApplicationProperties.Add("Timestamp", alert.CreatedAt);

                await _creditAlertsSender.SendMessageAsync(message);
                
                _logger.LogInformation(
                    "信貸警報已發送到 Service Bus: AlertId={AlertId}, Type={AlertType}, Severity={Severity}",
                    alert.Id, alert.AlertType, alert.Severity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "發送信貸警報到 Service Bus 時發生錯誤: AlertId={AlertId}", alert.Id);
                throw;
            }
        }

        /// <summary>
        /// 發送通知訊息到 Service Bus 佇列
        /// </summary>
        public async Task SendNotificationAsync(object notification)
        {
            try
            {
                var messageBody = JsonSerializer.Serialize(notification);
                var message = new ServiceBusMessage(messageBody)
                {
                    MessageId = Guid.NewGuid().ToString(),
                    Subject = "Notification",
                    ContentType = "application/json"
                };

                await _notificationsSender.SendMessageAsync(message);
                
                _logger.LogInformation("通知已發送到 Service Bus: {Notification}", messageBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "發送通知到 Service Bus 時發生錯誤");
                throw;
            }
        }

        /// <summary>
        /// 接收信貸警報訊息
        /// </summary>
        public async IAsyncEnumerable<CreditAlert> ReceiveCreditAlertsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var processedAlerts = new List<CreditAlert>();
                
                try
                {
                    var messages = await _creditAlertsReceiver.ReceiveMessagesAsync(
                        maxMessages: 10, 
                        maxWaitTime: TimeSpan.FromSeconds(30),
                        cancellationToken);

                    if (!messages.Any())
                    {
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }

                    foreach (var message in messages)
                    {
                        CreditAlert? alert = null;
                        try
                        {
                            var messageBody = message.Body.ToString();
                            alert = JsonSerializer.Deserialize<CreditAlert>(messageBody);
                            
                            if (alert != null)
                            {
                                processedAlerts.Add(alert);
                                await _creditAlertsReceiver.CompleteMessageAsync(message, cancellationToken);
                                
                                _logger.LogInformation(
                                    "已處理信貸警報訊息: AlertId={AlertId}, MessageId={MessageId}",
                                    alert.Id, message.MessageId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "處理信貸警報訊息時發生錯誤: MessageId={MessageId}", message.MessageId);
                            await _creditAlertsReceiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "接收信貸警報訊息時發生錯誤");
                    await Task.Delay(5000, cancellationToken);
                }

                // 在 try-catch 外面返回處理好的警報
                foreach (var alert in processedAlerts)
                {
                    yield return alert;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _creditAlertsSender.DisposeAsync();
            await _notificationsSender.DisposeAsync();
            await _creditAlertsReceiver.DisposeAsync();
            await _serviceBusClient.DisposeAsync();
        }
    }
}
