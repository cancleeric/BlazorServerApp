using Microsoft.AspNetCore.SignalR;
using CreditMonitoring.Common.Interfaces;
using CreditMonitoring.Common.Models;
using CreditMonitoring.Web.Hubs;

namespace CreditMonitoring.Web.Services
{
    /// <summary>
    /// SignalR通知服務實現
    /// </summary>
    public class SignalRNotificationService : ISignalRNotificationService
    {
        private readonly IHubContext<CreditMonitoringHub> _hubContext;
        private readonly ILogger<SignalRNotificationService> _logger;

        public SignalRNotificationService(
            IHubContext<CreditMonitoringHub> hubContext,
            ILogger<SignalRNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// 發送信用警報通知
        /// </summary>
        public async Task SendCreditAlertAsync(CreditAlert alert, string? userId = null)
        {
            try
            {
                var alertData = new
                {
                    Id = alert.Id,
                    AccountId = alert.AccountId,
                    Severity = alert.Severity,
                    Message = alert.Message,
                    CreatedAt = alert.CreatedAt,
                    IsResolved = alert.IsResolved
                };

                if (!string.IsNullOrEmpty(userId))
                {
                    // 發送給特定用戶
                    await _hubContext.Clients.User(userId).SendAsync("CreditAlert", alertData);
                }
                else
                {
                    // 根據警報嚴重程度發送給不同角色
                    var targetRoles = GetTargetRoles(alert.Severity);
                    foreach (var role in targetRoles)
                    {
                        await _hubContext.Clients.Group($"Role_{role}").SendAsync("CreditAlert", alertData);
                    }
                }

                _logger.LogInformation("Credit alert {AlertId} sent successfully", alert.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send credit alert {AlertId}", alert.Id);
                throw;
            }
        }

        /// <summary>
        /// 發送帳戶狀態更新通知
        /// </summary>
        public async Task SendAccountStatusUpdateAsync(int accountId, string newStatus, string? userId = null)
        {
            try
            {
                var statusData = new
                {
                    AccountId = accountId,
                    NewStatus = newStatus,
                    Timestamp = DateTime.UtcNow
                };

                if (!string.IsNullOrEmpty(userId))
                {
                    await _hubContext.Clients.User(userId).SendAsync("AccountStatusUpdate", statusData);
                }
                else
                {
                    // 發送給所有有權限的用戶
                    await _hubContext.Clients.Groups("Role_CreditOfficer", "Role_Manager", "Role_Admin")
                        .SendAsync("AccountStatusUpdate", statusData);
                }

                _logger.LogInformation("Account status update sent for account {AccountId}", accountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send account status update for account {AccountId}", accountId);
                throw;
            }
        }

        /// <summary>
        /// 發送系統通知
        /// </summary>
        public async Task SendSystemNotificationAsync(string message, string level = "info", string? userRole = null)
        {
            try
            {
                var notificationData = new
                {
                    Message = message,
                    Level = level,
                    Timestamp = DateTime.UtcNow
                };

                if (!string.IsNullOrEmpty(userRole))
                {
                    await _hubContext.Clients.Group($"Role_{userRole}").SendAsync("SystemNotification", notificationData);
                }
                else
                {
                    await _hubContext.Clients.All.SendAsync("SystemNotification", notificationData);
                }

                _logger.LogInformation("System notification sent: {Message}", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send system notification: {Message}", message);
                throw;
            }
        }

        /// <summary>
        /// 發送報告完成通知
        /// </summary>
        public async Task SendReportReadyNotificationAsync(string reportName, string downloadUrl, string userId)
        {
            try
            {
                var reportData = new
                {
                    ReportName = reportName,
                    DownloadUrl = downloadUrl,
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.User(userId).SendAsync("ReportReady", reportData);

                _logger.LogInformation("Report ready notification sent to user {UserId} for report {ReportName}", 
                    userId, reportName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send report ready notification to user {UserId} for report {ReportName}", 
                    userId, reportName);
                throw;
            }
        }        /// <summary>
        /// 根據警報嚴重程度獲取目標角色
        /// </summary>
        private static string[] GetTargetRoles(AlertSeverity severity)
        {
            return severity switch
            {
                AlertSeverity.Critical => new[] { "Admin", "Manager", "CreditOfficer" },
                AlertSeverity.High => new[] { "Admin", "Manager", "CreditOfficer" },
                AlertSeverity.Medium => new[] { "Admin", "Manager", "CreditOfficer" },
                AlertSeverity.Low => new[] { "CreditOfficer" },
                _ => new[] { "CreditOfficer" }
            };
        }
    }
}
