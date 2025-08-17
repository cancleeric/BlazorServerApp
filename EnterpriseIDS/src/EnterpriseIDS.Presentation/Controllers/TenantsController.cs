using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EnterpriseIDS.Core.Entities;
using EnterpriseIDS.Core.Interfaces;

namespace EnterpriseIDS.Presentation.Controllers;

/// <summary>
/// 租戶管理 API 控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ITenantContextService _tenantContextService;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        ITenantService tenantService,
        ITenantContextService tenantContextService,
        ILogger<TenantsController> logger)
    {
        _tenantService = tenantService;
        _tenantContextService = tenantContextService;
        _logger = logger;
    }

    /// <summary>
    /// 取得所有租戶
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult<IEnumerable<TenantDto>>> GetTenants(CancellationToken cancellationToken)
    {
        try
        {
            var tenants = await _tenantService.GetAllAsync(cancellationToken);
            return Ok(tenants.Select(TenantDto.FromEntity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得租戶列表時發生錯誤");
            return StatusCode(500, "內部伺服器錯誤");
        }
    }

    /// <summary>
    /// 取得分頁租戶列表
    /// </summary>
    [HttpGet("paged")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult<PagedResult<TenantDto>>> GetPagedTenants(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] TenantStatus? status = null,
        [FromQuery] TenantType? tenantType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (items, totalCount) = await _tenantService.GetPagedAsync(
                pageNumber, pageSize, searchTerm, status, tenantType, cancellationToken);

            var result = new PagedResult<TenantDto>
            {
                Items = items.Select(TenantDto.FromEntity).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得分頁租戶列表時發生錯誤");
            return StatusCode(500, "內部伺服器錯誤");
        }
    }

    /// <summary>
    /// 根據 ID 取得租戶
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TenantDto>> GetTenant(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            // 檢查存取權限
            if (!_tenantContextService.IsSuperAdminContext() && !_tenantContextService.HasTenantAccess(id))
            {
                return Forbid("沒有權限存取此租戶");
            }

            var tenant = await _tenantService.GetByIdAsync(id, cancellationToken);
            if (tenant == null)
            {
                return NotFound("租戶不存在");
            }

            return Ok(TenantDto.FromEntity(tenant));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得租戶時發生錯誤: {TenantId}", id);
            return StatusCode(500, "內部伺服器錯誤");
        }
    }

    /// <summary>
    /// 根據 Slug 取得租戶
    /// </summary>
    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<TenantDto>> GetTenantBySlug(string slug, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await _tenantService.GetBySlugAsync(slug, cancellationToken);
            if (tenant == null)
            {
                return NotFound("租戶不存在");
            }

            // 檢查存取權限
            if (!_tenantContextService.IsSuperAdminContext() && !_tenantContextService.HasTenantAccess(tenant.Id))
            {
                return Forbid("沒有權限存取此租戶");
            }

            return Ok(TenantDto.FromEntity(tenant));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根據 Slug 取得租戶時發生錯誤: {Slug}", slug);
            return StatusCode(500, "內部伺服器錯誤");
        }
    }

    /// <summary>
    /// 建立租戶
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<TenantDto>> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenant = new Tenant
            {
                Name = request.Name,
                Slug = request.Slug.ToLowerInvariant(),
                Description = request.Description,
                TenantType = request.TenantType,
                ContactEmail = request.ContactEmail,
                ContactPhone = request.ContactPhone,
                PrimaryDomain = request.PrimaryDomain,
                TimeZone = request.TimeZone ?? "UTC",
                Language = request.Language ?? "en-US"
            };

            if (request.AllowedDomains?.Any() == true)
            {
                tenant.AllowedDomains = request.AllowedDomains.ToList();
            }

            var createdTenant = await _tenantService.CreateAsync(tenant, cancellationToken);

            return CreatedAtAction(
                nameof(GetTenant),
                new { id = createdTenant.Id },
                TenantDto.FromEntity(createdTenant));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立租戶時發生錯誤");
            return StatusCode(500, "內部伺服器錯誤");
        }
    }

    /// <summary>
    /// 更新租戶
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TenantDto>> UpdateTenant(Guid id, [FromBody] UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // 檢查存取權限
            if (!_tenantContextService.IsSuperAdminContext() && !_tenantContextService.HasTenantAccess(id))
            {
                return Forbid("沒有權限修改此租戶");
            }

            var existingTenant = await _tenantService.GetByIdAsync(id, cancellationToken);
            if (existingTenant == null)
            {
                return NotFound("租戶不存在");
            }

            // 更新租戶資訊
            existingTenant.Name = request.Name;
            existingTenant.Description = request.Description;
            existingTenant.ContactEmail = request.ContactEmail;
            existingTenant.ContactPhone = request.ContactPhone;
            existingTenant.PrimaryDomain = request.PrimaryDomain;
            existingTenant.TimeZone = request.TimeZone ?? existingTenant.TimeZone;
            existingTenant.Language = request.Language ?? existingTenant.Language;

            if (request.AllowedDomains != null)
            {
                existingTenant.AllowedDomains = request.AllowedDomains.ToList();
            }

            var updatedTenant = await _tenantService.UpdateAsync(existingTenant, cancellationToken);

            return Ok(TenantDto.FromEntity(updatedTenant));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新租戶時發生錯誤: {TenantId}", id);
            return StatusCode(500, "內部伺服器錯誤");
        }
    }

    /// <summary>
    /// 刪除租戶
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult> DeleteTenant(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _tenantService.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刪除租戶時發生錯誤: {TenantId}", id);
            return StatusCode(500, "內部伺服器錯誤");
        }
    }

    /// <summary>
    /// 啟用租戶
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult> ActivateTenant(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _tenantService.ActivateAsync(id, cancellationToken);
            return Ok("租戶已啟用");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "啟用租戶時發生錯誤: {TenantId}", id);
            return StatusCode(500, "內部伺服器錯誤");
        }
    }

    /// <summary>
    /// 暫停租戶
    /// </summary>
    [HttpPost("{id:guid}/suspend")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult> SuspendTenant(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _tenantService.SuspendAsync(id, cancellationToken);
            return Ok("租戶已暫停");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "暫停租戶時發生錯誤: {TenantId}", id);
            return StatusCode(500, "內部伺服器錯誤");
        }
    }

    /// <summary>
    /// 取得子租戶
    /// </summary>
    [HttpGet("{id:guid}/children")]
    public async Task<ActionResult<IEnumerable<TenantDto>>> GetChildTenants(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            // 檢查存取權限
            if (!_tenantContextService.IsSuperAdminContext() && !_tenantContextService.HasTenantAccess(id))
            {
                return Forbid("沒有權限存取此租戶");
            }

            var childTenants = await _tenantService.GetChildTenantsAsync(id, cancellationToken);
            return Ok(childTenants.Select(TenantDto.FromEntity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取得子租戶時發生錯誤: {ParentTenantId}", id);
            return StatusCode(500, "內部伺服器錯誤");
        }
    }

    /// <summary>
    /// 檢查 Slug 是否可用
    /// </summary>
    [HttpGet("check-slug/{slug}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public async Task<ActionResult<bool>> CheckSlugAvailability(string slug, [FromQuery] Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await _tenantService.IsSlugExistsAsync(slug, excludeId, cancellationToken);
            return Ok(!exists); // 回傳是否可用（不存在 = 可用）
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢查 Slug 可用性時發生錯誤: {Slug}", slug);
            return StatusCode(500, "內部伺服器錯誤");
        }
    }
}

/// <summary>
/// 租戶 DTO
/// </summary>
public class TenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TenantType TenantType { get; set; }
    public TenantStatus Status { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? PrimaryDomain { get; set; }
    public List<string> AllowedDomains { get; set; } = new();
    public string TimeZone { get; set; } = "UTC";
    public string Language { get; set; } = "en-US";
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }

    public static TenantDto FromEntity(Tenant tenant)
    {
        return new TenantDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            Description = tenant.Description,
            TenantType = tenant.TenantType,
            Status = tenant.Status,
            ContactEmail = tenant.ContactEmail,
            ContactPhone = tenant.ContactPhone,
            PrimaryDomain = tenant.PrimaryDomain,
            AllowedDomains = tenant.AllowedDomains,
            TimeZone = tenant.TimeZone,
            Language = tenant.Language,
            CreatedAt = tenant.CreatedAt,
            IsActive = tenant.IsActive()
        };
    }
}

/// <summary>
/// 建立租戶請求
/// </summary>
public class CreateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TenantType TenantType { get; set; } = TenantType.Small;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? PrimaryDomain { get; set; }
    public string[]? AllowedDomains { get; set; }
    public string? TimeZone { get; set; }
    public string? Language { get; set; }
}

/// <summary>
/// 更新租戶請求
/// </summary>
public class UpdateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? PrimaryDomain { get; set; }
    public string[]? AllowedDomains { get; set; }
    public string? TimeZone { get; set; }
    public string? Language { get; set; }
}

/// <summary>
/// 分頁結果
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}