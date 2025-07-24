using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AuthCoreSdk.Data;
using AuthCoreSdk.Models;
using AuthCoreSdk.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AuthCoreSdk.Extensions
{
    public static class AuthServerSdkExtensions
    {
        public static IServiceCollection AddAuthCoreSdkServer(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure DbContext with SQLite
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

            // Register services
            services.AddScoped<UserService>();
            services.AddScoped<JwtTokenService>();

            // JWT Configuration
            var jwtKey = configuration["Jwt:Key"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLongForJWT";
            var jwtIssuer = configuration["Jwt:Issuer"] ?? "AuthenticationServer";
            var jwtAudience = configuration["Jwt:Audience"] ?? "SimpleJwtWeb";

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme; // Default to Cookie for UI login
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/login"; // Path to the login page
                options.AccessDeniedPath = "/AccessDenied";
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };
            });

            services.AddAuthorization();
            services.AddControllers(); // Add controllers for Auth and Users
            services.AddRazorPages(); // Add Razor Pages support

            return services;
        }

        public static IApplicationBuilder UseAuthCoreSdkServer(this IApplicationBuilder app)
        {
            // Ensure database is created and seed initial data
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                dbContext.Database.Migrate(); // Apply any pending migrations
                SeedUsers(dbContext);
            }

            app.UseStaticFiles(); // Enable static files for Razor Pages (CSS, JS)
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers(); // Map controllers from AuthCoreSdk
                endpoints.MapRazorPages(); // Map Razor Pages
            });

            return app;
        }

        private static void SeedUsers(AppDbContext context)
        {
            if (!context.Users.Any())
            {
                var userService = new UserService(context); // Use UserService to hash passwords
                var adminPasswordHash = userService.HashPassword("password");
                var userPasswordHash = userService.HashPassword("password");

                context.Users.AddRange(
                    new User { Username = "admin", PasswordHash = adminPasswordHash, Role = "Admin" },
                    new User { Username = "user", PasswordHash = userPasswordHash, Role = "User" }
                );
                context.SaveChanges();
            }
        }
    }
}