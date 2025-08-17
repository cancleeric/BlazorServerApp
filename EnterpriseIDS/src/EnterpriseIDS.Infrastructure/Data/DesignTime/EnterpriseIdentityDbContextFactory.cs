using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace EnterpriseIDS.Infrastructure.Data.DesignTime;

/// <summary>
/// Design-time DbContext Factory for EF Core Migrations
/// </summary>
public class EnterpriseIdentityDbContextFactory : IDesignTimeDbContextFactory<EnterpriseIdentityDbContext>
{
    public EnterpriseIdentityDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<EnterpriseIdentityDbContext>();

        // 使用 SQLite 作為預設的 Migration 資料庫
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Data Source=enterprise_identity.db";

        optionsBuilder.UseSqlite(connectionString, options =>
        {
            options.MigrationsAssembly(typeof(EnterpriseIdentityDbContext).Assembly.FullName);
        });

        // 也支援 SQL Server 和 PostgreSQL
        var provider = configuration.GetValue<string>("DatabaseProvider")?.ToLower();
        switch (provider)
        {
            case "sqlserver":
                var sqlServerConnectionString = configuration.GetConnectionString("SqlServerConnection");
                if (!string.IsNullOrEmpty(sqlServerConnectionString))
                {
                    optionsBuilder.UseSqlServer(sqlServerConnectionString, options =>
                    {
                        options.MigrationsAssembly(typeof(EnterpriseIdentityDbContext).Assembly.FullName);
                    });
                }
                break;

            case "postgresql":
                var postgresConnectionString = configuration.GetConnectionString("PostgreSqlConnection");
                if (!string.IsNullOrEmpty(postgresConnectionString))
                {
                    optionsBuilder.UseNpgsql(postgresConnectionString, options =>
                    {
                        options.MigrationsAssembly(typeof(EnterpriseIdentityDbContext).Assembly.FullName);
                    });
                }
                break;

            default:
                // 預設使用 SQLite
                break;
        }

        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.EnableDetailedErrors();

        return new EnterpriseIdentityDbContext(optionsBuilder.Options);
    }
}