using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using CreditMonitoring.Common.Models;
using CreditMonitoring.Common.Services.Azure;

namespace CreditMonitoring.Web.Hubs
{
    /// <summary>
    /// 信貸監控即時通訊 Hub
    /// 提供即時信貸警報推送和監控狀態更新
    /// </summary>
    [Authorize]
    public class CreditMonitoringHub : Hub
    {
        private readonly IAzureMonitoringService _monitoringService;
        private readonly ILogger<CreditMonitoringHub> _logger;

        public CreditMonitoringHub(
            IAzureMonitoringService monitoringService,
            ILogger<CreditMonitoringHub> logger)
        {
            _monitoringService = monitoringService;
            _logger = logger;
        }

        /// <summary>
        /// 客戶端連接時
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.Identity?.Name ?? "Unknown";
            var connectionId = Context.ConnectionId;

            _logger.LogInformation("用戶已連接到信貸監控 Hub: UserId={UserId}, ConnectionId={ConnectionId}", 
                userId, connectionId);

            // 將用戶加入到角色群組
            var userRoles = Context.User?.Claims
                .Where(c => c.Type == "role")
                .Select(c => c.Value)
                .ToList() ?? new List<string>();

            foreach (var role in userRoles)
            {
                await Groups.AddToGroupAsync(connectionId, $"Role_{role}");
            }

            // 追蹤連接事件
            _monitoringService.TrackUserAction(userId, "SignalR_Connected", new Dictionary<string, string>
            {
                ["ConnectionId"] = connectionId,
                ["UserAgent"] = Context.GetHttpContext()?.Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown"
            });

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// 客戶端斷線時
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.Identity?.Name ?? "Unknown";
            var connectionId = Context.ConnectionId;

            _logger.LogInformation("用戶已斷開信貸監控 Hub 連接: UserId={UserId}, ConnectionId={ConnectionId}", 
                userId, connectionId);

            // 追蹤斷線事件
            _monitoringService.TrackUserAction(userId, "SignalR_Disconnected", new Dictionary<string, string>
            {
                ["ConnectionId"] = connectionId,
                ["Exception"] = exception?.Message ?? "Normal"
            });

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// 加入貸款帳戶監控群組
        /// </summary>
        public async Task JoinAccountGroup(int accountId)
        {
            var userId = Context.User?.Identity?.Name ?? "Unknown";
            var groupName = $"Account_{accountId}";

            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            _logger.LogInformation("用戶已加入帳戶監控群組: UserId={UserId}, AccountId={AccountId}", 
                userId, accountId);

            _monitoringService.TrackUserAction(userId, "JoinAccountGroup", new Dictionary<string, string>
            {
                ["AccountId"] = accountId.ToString(),
                ["GroupName"] = groupName
            });
        }

        /// <summary>
        /// 離開貸款帳戶監控群組
        /// </summary>
        public async Task LeaveAccountGroup(int accountId)
        {
            var userId = Context.User?.Identity?.Name ?? "Unknown";
            var groupName = $"Account_{accountId}";

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

            _logger.LogInformation("用戶已離開帳戶監控群組: UserId={UserId}, AccountId={AccountId}", 
                userId, accountId);

            _monitoringService.TrackUserAction(userId, "LeaveAccountGroup", new Dictionary<string, string>
            {
                ["AccountId"] = accountId.ToString(),
                ["GroupName"] = groupName
            });
        }

        /// <summary>
        /// 請求帳戶即時狀態
        /// </summary>
        public async Task RequestAccountStatus(int accountId)
        {
            var userId = Context.User?.Identity?.Name ?? "Unknown";

            _logger.LogDebug("用戶請求帳戶狀態: UserId={UserId}, AccountId={AccountId}", 
                userId, accountId);

            // 這裡可以實作即時狀態查詢邏輯
            // 例如從快取或資料庫取得最新狀態並推送給客戶端

            await Clients.Caller.SendAsync("AccountStatusReceived", new
            {
                AccountId = accountId,
                Status = "Active", // 實際狀態
                LastUpdate = DateTime.UtcNow,
                AlertCount = 0 // 實際警報數量
            });

            _monitoringService.TrackUserAction(userId, "RequestAccountStatus", new Dictionary<string, string>
            {
                ["AccountId"] = accountId.ToString()
            });        }
    }
}
