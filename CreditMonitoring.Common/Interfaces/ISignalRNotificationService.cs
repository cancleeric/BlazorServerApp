using CreditMonitoring.Common.Models;

namespace CreditMonitoring.Common.Interfaces
{
    /// <summary>
    /// SignalR通知服務接口
    /// </summary>
    public interface ISignalRNotificationService
    {
        /// <summary>
        /// 發送信用警報通知
        /// </summary>
        /// <param name="alert">信用警報</param>
        /// <param name="userId">特定用戶ID（可選）</param>
        Task SendCreditAlertAsync(CreditAlert alert, string? userId = null);

        /// <summary>
        /// 發送帳戶狀態更新通知
        /// </summary>
        /// <param name="accountId">帳戶ID</param>
        /// <param name="newStatus">新狀態</param>
        /// <param name="userId">特定用戶ID（可選）</param>
        Task SendAccountStatusUpdateAsync(int accountId, string newStatus, string? userId = null);

        /// <summary>
        /// 發送系統通知
        /// </summary>
        /// <param name="message">通知訊息</param>
        /// <param name="level">通知等級</param>
        /// <param name="userRole">目標用戶角色（可選）</param>
        Task SendSystemNotificationAsync(string message, string level = "info", string? userRole = null);

        /// <summary>
        /// 發送報告完成通知
        /// </summary>
        /// <param name="reportName">報告名稱</param>
        /// <param name="downloadUrl">下載URL</param>
        /// <param name="userId">特定用戶ID</param>
        Task SendReportReadyNotificationAsync(string reportName, string downloadUrl, string userId);
    }
}
