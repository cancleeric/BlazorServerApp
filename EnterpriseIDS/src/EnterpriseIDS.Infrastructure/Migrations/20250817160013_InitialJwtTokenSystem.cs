using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseIDS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialJwtTokenSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "NEWID()"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    TenantType = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    SubscriptionPlan = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    ParentTenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PrimaryDomain = table.Column<string>(type: "TEXT", maxLength: 253, nullable: true),
                    AllowedDomains = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactEmail = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    ContactPhone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TimeZone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "UTC"),
                    Language = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false, defaultValue: "en-US"),
                    Branding = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quotas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecuritySettings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnabledFeatures = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubscriptionStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubscriptionEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrialEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedById = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedById = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                    table.CheckConstraint("CK_Tenants_Email_Format", "[ContactEmail] IS NULL OR [ContactEmail] LIKE '%@%.%'");
                    table.CheckConstraint("CK_Tenants_Slug_Format", "[Slug] NOT LIKE '%[^a-z0-9-]%' AND [Slug] NOT LIKE '-%' AND [Slug] NOT LIKE '%-'");
                    table.ForeignKey(
                        name: "FK_Tenants_Tenants_ParentTenantId",
                        column: x => x.ParentTenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    GroupType = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSystemGroup = table.Column<bool>(type: "INTEGER", nullable: false),
                    ParentGroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LdapDistinguishedName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LdapObjectGuid = table.Column<string>(type: "TEXT", nullable: true),
                    LdapSecurityIdentifier = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsFromLdap = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastLdapSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LdapSyncHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedById = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedById = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedById = table.Column<string>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Groups_Groups_ParentGroupId",
                        column: x => x.ParentGroupId,
                        principalTable: "Groups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Groups_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LdapConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ServerType = table.Column<int>(type: "INTEGER", nullable: false),
                    ServerUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    UseSsl = table.Column<bool>(type: "INTEGER", nullable: false),
                    UseStartTls = table.Column<bool>(type: "INTEGER", nullable: false),
                    IgnoreSslErrors = table.Column<bool>(type: "INTEGER", nullable: false),
                    BaseDn = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    UserBaseDn = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    GroupBaseDn = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UserSearchFilter = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    GroupSearchFilter = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    UsernameAttribute = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EmailAttribute = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FirstNameAttribute = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastNameAttribute = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayNameAttribute = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GroupNameAttribute = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GroupMemberAttribute = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AuthenticationType = table.Column<int>(type: "INTEGER", nullable: false),
                    ServiceAccountDn = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ServiceAccountPassword = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ConnectionTimeout = table.Column<int>(type: "INTEGER", nullable: false),
                    SearchTimeout = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxConnections = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncFrequencyMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    EnableIncrementalSync = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConflictResolution = table.Column<int>(type: "INTEGER", nullable: false),
                    SyncDisabledUsers = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncGroups = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncNestedGroups = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxNestingLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    AdditionalAttributeMapping = table.Column<string>(type: "TEXT", nullable: true),
                    LastSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSyncError = table.Column<string>(type: "TEXT", nullable: true),
                    SyncStatistics = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedById = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedById = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedById = table.Column<string>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LdapConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LdapConfigurations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "INTEGER", nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDefaultRole = table.Column<bool>(type: "INTEGER", nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedById = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedById = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedById = table.Column<string>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Roles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "NEWID()"),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConfigKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ConfigValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfigType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "string"),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsSensitive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsInheritable = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsReadOnly = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ValidationRules = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AllowedValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Version = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags1 = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedById = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantConfigurations", x => x.Id);
                    table.CheckConstraint("CK_TenantConfigurations_ConfigKey_Format", "[ConfigKey] NOT LIKE '% %' AND [ConfigKey] NOT LIKE '%[^a-zA-Z0-9._-]%'");
                    table.CheckConstraint("CK_TenantConfigurations_ConfigType_Valid", "[ConfigType] IN ('string', 'int', 'long', 'double', 'decimal', 'bool', 'datetime', 'json')");
                    table.CheckConstraint("CK_TenantConfigurations_EffectiveDate_BeforeExpiry", "[EffectiveDate] IS NULL OR [ExpiryDate] IS NULL OR [EffectiveDate] < [ExpiryDate]");
                    table.CheckConstraint("CK_TenantConfigurations_SortOrder_NonNegative", "[SortOrder] >= 0");
                    table.CheckConstraint("CK_TenantConfigurations_Version_Positive", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_TenantConfigurations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Department = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    JobTitle = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    MobileNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Office = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ManagerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PasswordLastChangedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PasswordExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LockedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LockoutEndAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FailedLoginAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    IsMfaEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreferredLanguage = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    TimeZone = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LdapDistinguishedName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LdapObjectGuid = table.Column<string>(type: "TEXT", nullable: true),
                    LdapSecurityIdentifier = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsFromLdap = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastLdapSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LdapSyncHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedById = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedById = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedById = table.Column<string>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Users_Users_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GroupRoleMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    InheritToChildGroups = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MappingConditions = table.Column<string>(type: "TEXT", nullable: true),
                    MappingSource = table.Column<string>(type: "TEXT", nullable: true),
                    ExternalIdentifier = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedById = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedById = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedById = table.Column<string>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupRoleMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupRoleMappings_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupRoleMappings_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupRoleMappings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Permission = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    PermissionSource = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PermissionScope = table.Column<string>(type: "TEXT", nullable: true),
                    PermissionConditions = table.Column<string>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExternalIdentifier = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedById = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedById = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedById = table.Column<string>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JwtTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JwtId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, comment: "JWT ID (jti claim)"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false, comment: "使用者 ID"),
                    TokenType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, comment: "Token 類型 (access_token, refresh_token, id_token)"),
                    TokenValue = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false, comment: "Token 值 (已加密或雜湊)"),
                    IssuedAt = table.Column<DateTime>(type: "TEXT", nullable: false, comment: "Token 發行時間"),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false, comment: "Token 過期時間"),
                    Issuer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, comment: "發行者 (iss claim)"),
                    Audience = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, comment: "接收者 (aud claim)"),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, comment: "主體 (sub claim)"),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, comment: "客戶端 ID"),
                    Scopes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true, comment: "授權範圍 (space-separated)"),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, comment: "Token 狀態"),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true, comment: "撤銷時間"),
                    RevokedReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true, comment: "撤銷原因"),
                    RevokedByUserId = table.Column<Guid>(type: "TEXT", nullable: true, comment: "撤銷者使用者 ID"),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true, comment: "最後使用時間"),
                    UseCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0, comment: "使用次數"),
                    SourceIpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true, comment: "來源 IP 地址"),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true, comment: "User Agent"),
                    RefreshTokenId = table.Column<Guid>(type: "TEXT", nullable: true, comment: "關聯的 Refresh Token ID"),
                    ParentTokenId = table.Column<Guid>(type: "TEXT", nullable: true, comment: "父級 Token ID"),
                    Metadata = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true, comment: "額外的 Metadata (JSON 格式)"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()", comment: "建立時間"),
                    CreatedById = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true, comment: "更新時間"),
                    UpdatedById = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false, comment: "是否已刪除"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true, comment: "刪除時間"),
                    DeletedById = table.Column<string>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false, comment: "租戶 ID")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JwtTokens", x => x.Id);
                    table.CheckConstraint("CK_JwtTokens_ExpiresAt", "[ExpiresAt] > [IssuedAt]");
                    table.CheckConstraint("CK_JwtTokens_Status", "[Status] IN (1, 2, 3, 4, 5)");
                    table.CheckConstraint("CK_JwtTokens_TokenType", "[TokenType] IN ('access_token', 'refresh_token', 'id_token')");
                    table.CheckConstraint("CK_JwtTokens_UseCount", "[UseCount] >= 0");
                    table.ForeignKey(
                        name: "FK_JwtTokens_JwtTokens_ParentTokenId",
                        column: x => x.ParentTokenId,
                        principalTable: "JwtTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JwtTokens_JwtTokens_RefreshTokenId",
                        column: x => x.RefreshTokenId,
                        principalTable: "JwtTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JwtTokens_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JwtTokens_Users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JwtTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TokenBlacklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JwtId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, comment: "JWT ID (jti claim)"),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, comment: "Token 雜湊值 (用於快速比對)"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false, comment: "使用者 ID"),
                    TokenType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, comment: "Token 類型"),
                    BlacklistedAt = table.Column<DateTime>(type: "TEXT", nullable: false, comment: "加入黑名單的時間"),
                    OriginalExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false, comment: "Token 原本的過期時間"),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, comment: "加入黑名單的原因"),
                    BlacklistedByUserId = table.Column<Guid>(type: "TEXT", nullable: true, comment: "加入黑名單的使用者 ID"),
                    Type = table.Column<int>(type: "INTEGER", nullable: false, comment: "黑名單類型"),
                    IsPermanent = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false, comment: "是否為永久黑名單"),
                    BlacklistExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true, comment: "黑名單到期時間 (null 表示永久)"),
                    Metadata = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true, comment: "額外的 Metadata (JSON 格式)"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()", comment: "建立時間"),
                    CreatedById = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true, comment: "更新時間"),
                    UpdatedById = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false, comment: "是否已刪除"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true, comment: "刪除時間"),
                    DeletedById = table.Column<string>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false, comment: "租戶 ID")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenBlacklists", x => x.Id);
                    table.CheckConstraint("CK_TokenBlacklists_BlacklistedAt", "[BlacklistedAt] <= [OriginalExpiresAt]");
                    table.CheckConstraint("CK_TokenBlacklists_BlacklistExpiry", "([IsPermanent] = 1 AND [BlacklistExpiresAt] IS NULL) OR ([IsPermanent] = 0)");
                    table.CheckConstraint("CK_TokenBlacklists_TokenType", "[TokenType] IN ('access_token', 'refresh_token', 'id_token')");
                    table.CheckConstraint("CK_TokenBlacklists_Type", "[Type] IN (1, 2, 3, 4, 5, 6, 7, 8)");
                    table.ForeignKey(
                        name: "FK_TokenBlacklists_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TokenBlacklists_Users_BlacklistedByUserId",
                        column: x => x.BlacklistedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TokenBlacklists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserGroupMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsGroupAdmin = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", nullable: true),
                    ExternalIdentifier = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedById = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedById = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedById = table.Column<string>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroupMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserGroupMemberships_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserGroupMemberships_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserGroupMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AssignmentSource = table.Column<string>(type: "TEXT", nullable: true),
                    SourceGroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExternalIdentifier = table.Column<string>(type: "TEXT", nullable: true),
                    AssignmentReason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedById = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedById = table.Column<string>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedById = table.Column<string>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupRoleMappings_GroupId",
                table: "GroupRoleMappings",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupRoleMappings_RoleId",
                table: "GroupRoleMappings",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupRoleMappings_TenantId",
                table: "GroupRoleMappings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_ParentGroupId",
                table: "Groups",
                column: "ParentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_TenantId",
                table: "Groups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_JwtTokens_ExpiresAt",
                table: "JwtTokens",
                column: "ExpiresAt",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JwtTokens_IssuedAt",
                table: "JwtTokens",
                column: "IssuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_JwtTokens_JwtId",
                table: "JwtTokens",
                column: "JwtId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JwtTokens_ParentTokenId",
                table: "JwtTokens",
                column: "ParentTokenId",
                filter: "[ParentTokenId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JwtTokens_RefreshTokenId",
                table: "JwtTokens",
                column: "RefreshTokenId",
                filter: "[RefreshTokenId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JwtTokens_RevokedByUserId",
                table: "JwtTokens",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JwtTokens_TenantId",
                table: "JwtTokens",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_JwtTokens_TenantId_TokenType",
                table: "JwtTokens",
                columns: new[] { "TenantId", "TokenType" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JwtTokens_TenantId_TokenType_ExpiresAt",
                table: "JwtTokens",
                columns: new[] { "TenantId", "TokenType", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JwtTokens_TenantId_UserId_Status",
                table: "JwtTokens",
                columns: new[] { "TenantId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_JwtTokens_TokenType_Status",
                table: "JwtTokens",
                columns: new[] { "TokenType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_JwtTokens_UserId",
                table: "JwtTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_JwtTokens_UserId_TokenType_Status",
                table: "JwtTokens",
                columns: new[] { "UserId", "TokenType", "Status" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LdapConfigurations_TenantId",
                table: "LdapConfigurations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_TenantId",
                table: "RolePermissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId",
                table: "Roles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfiguration_CreatedAt",
                table: "TenantConfigurations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfigurations_Category",
                table: "TenantConfigurations",
                column: "Category",
                filter: "[Category] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfigurations_ConfigKey",
                table: "TenantConfigurations",
                column: "ConfigKey");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfigurations_ConfigType",
                table: "TenantConfigurations",
                column: "ConfigType");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfigurations_EffectiveDate_ExpiryDate",
                table: "TenantConfigurations",
                columns: new[] { "EffectiveDate", "ExpiryDate" },
                filter: "[EffectiveDate] IS NOT NULL OR [ExpiryDate] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfigurations_IsRequired",
                table: "TenantConfigurations",
                column: "IsRequired",
                filter: "[IsRequired] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfigurations_TenantId_Category",
                table: "TenantConfigurations",
                columns: new[] { "TenantId", "Category" },
                filter: "[Category] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfigurations_TenantId_ConfigKey",
                table: "TenantConfigurations",
                columns: new[] { "TenantId", "ConfigKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_CreatedAt",
                table: "Tenants",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_IsDeleted",
                table: "Tenants",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ParentTenantId",
                table: "Tenants",
                column: "ParentTenantId",
                filter: "[ParentTenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_PrimaryDomain",
                table: "Tenants",
                column: "PrimaryDomain",
                filter: "[PrimaryDomain] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Status",
                table: "Tenants",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Status_IsDeleted",
                table: "Tenants",
                columns: new[] { "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantType",
                table: "Tenants",
                column: "TenantType");

            migrationBuilder.CreateIndex(
                name: "IX_TokenBlacklists_BlacklistedAt",
                table: "TokenBlacklists",
                column: "BlacklistedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TokenBlacklists_BlacklistedByUserId",
                table: "TokenBlacklists",
                column: "BlacklistedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TokenBlacklists_BlacklistExpiresAt",
                table: "TokenBlacklists",
                column: "BlacklistExpiresAt",
                filter: "[BlacklistExpiresAt] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TokenBlacklists_Cleanup",
                table: "TokenBlacklists",
                columns: new[] { "OriginalExpiresAt", "BlacklistExpiresAt", "IsPermanent" });

            migrationBuilder.CreateIndex(
                name: "IX_TokenBlacklists_IsPermanent_BlacklistExpiresAt",
                table: "TokenBlacklists",
                columns: new[] { "IsPermanent", "BlacklistExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TokenBlacklists_JwtId",
                table: "TokenBlacklists",
                column: "JwtId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TokenBlacklists_OriginalExpiresAt",
                table: "TokenBlacklists",
                column: "OriginalExpiresAt",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TokenBlacklists_TenantId",
                table: "TokenBlacklists",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TokenBlacklists_TenantId_JwtId",
                table: "TokenBlacklists",
                columns: new[] { "TenantId", "JwtId" });

            migrationBuilder.CreateIndex(
                name: "IX_TokenBlacklists_TenantId_TokenHash",
                table: "TokenBlacklists",
                columns: new[] { "TenantId", "TokenHash" });

            migrationBuilder.CreateIndex(
                name: "IX_TokenBlacklists_TenantId_UserId_Type",
                table: "TokenBlacklists",
                columns: new[] { "TenantId", "UserId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_TokenBlacklists_TokenHash",
                table: "TokenBlacklists",
                column: "TokenHash",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TokenBlacklists_Type",
                table: "TokenBlacklists",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_TokenBlacklists_UserId",
                table: "TokenBlacklists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TokenBlacklists_UserId_Type",
                table: "TokenBlacklists",
                columns: new[] { "UserId", "Type" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupMemberships_GroupId",
                table: "UserGroupMemberships",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupMemberships_TenantId",
                table: "UserGroupMemberships",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupMemberships_UserId",
                table: "UserGroupMemberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_TenantId",
                table: "UserRoles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ManagerId",
                table: "Users",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupRoleMappings");

            migrationBuilder.DropTable(
                name: "JwtTokens");

            migrationBuilder.DropTable(
                name: "LdapConfigurations");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "TenantConfigurations");

            migrationBuilder.DropTable(
                name: "TokenBlacklists");

            migrationBuilder.DropTable(
                name: "UserGroupMemberships");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
