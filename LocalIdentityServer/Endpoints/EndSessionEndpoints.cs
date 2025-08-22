using LocalIdentityServer.Models.Requests;
using LocalIdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace LocalIdentityServer.Endpoints;

/// <summary>
/// OIDC End Session Endpoint (RP-Initiated Logout)
/// </summary>
public static class EndSessionEndpoints
{
    public static void MapEndSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/connect").WithTags("OIDC End Session");

        // End session endpoint - supports both GET and POST
        group.MapGet("/logout", HandleEndSessionRequest)
            .WithName("EndSessionGet")
            .WithSummary("OIDC End Session (GET)")
            .WithDescription("Initiates logout and session termination (OIDC Core 1.0)")
            .Produces(302) // Redirect
            .Produces(200) // Logout confirmation page
            .Produces(400);

        group.MapPost("/logout", HandleEndSessionRequest)
            .WithName("EndSessionPost")
            .WithSummary("OIDC End Session (POST)")
            .WithDescription("Processes logout confirmation (OIDC Core 1.0)")
            .Produces(302) // Redirect
            .Produces(200) // Logout confirmation page
            .Produces(400);
    }

    private static async Task<IResult> HandleEndSessionRequest(
        HttpContext context,
        IEndSessionService endSessionService,
        IErrorService errorService)
    {
        try
        {
            // Extract request parameters
            var request = await ExtractEndSessionRequestAsync(context);

            // Get current user if authenticated
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                        context.User?.FindFirst("sub")?.Value;

            // For POST requests with confirmation, process logout immediately
            if (context.Request.Method == "POST" && 
                context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync();
                var confirmed = form["logout_confirmed"].ToString();
                
                if (confirmed == "true")
                {
                    return await ProcessLogoutAsync(context, request, endSessionService, userId);
                }
            }

            // Validate the end session request
            var validation = await endSessionService.ValidateEndSessionRequestAsync(request);
            if (!validation.IsValid)
            {
                await errorService.WriteTokenErrorAsync(context, "invalid_request", 
                    validation.ErrorMessage ?? "Invalid end session request");
                return Results.BadRequest();
            }

            // If user is authenticated, show logout confirmation page or process directly
            if (!string.IsNullOrEmpty(userId))
            {
                // For applications that prefer direct logout without confirmation
                var skipConfirmation = context.Request.Query["prompt"].ToString() == "none";
                
                if (skipConfirmation)
                {
                    return await ProcessLogoutAsync(context, request, endSessionService, userId);
                }
                else
                {
                    // Show logout confirmation page
                    return await ShowLogoutConfirmationPageAsync(context, request, validation);
                }
            }
            else
            {
                // User not authenticated, redirect to post-logout URI if available
                var postLogoutUri = await endSessionService.GetPostLogoutRedirectUriAsync(request, validation.ClientId);
                if (!string.IsNullOrEmpty(postLogoutUri))
                {
                    var redirectUri = BuildRedirectUri(postLogoutUri, request.State);
                    return Results.Redirect(redirectUri);
                }
                else
                {
                    // Show a simple logged out page
                    return await ShowLoggedOutPageAsync(context);
                }
            }
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Unexpected error during end session");
            
            await errorService.WriteTokenErrorAsync(context, "server_error",
                "An unexpected error occurred during logout");
            return Results.StatusCode(500);
        }
    }

    private static async Task<EndSessionRequest> ExtractEndSessionRequestAsync(HttpContext context)
    {
        var request = new EndSessionRequest();

        if (context.Request.Method == "GET")
        {
            request.IdTokenHint = context.Request.Query["id_token_hint"];
            request.PostLogoutRedirectUri = context.Request.Query["post_logout_redirect_uri"];
            request.State = context.Request.Query["state"];
            request.UiLocales = context.Request.Query["ui_locales"];
            request.ClientId = context.Request.Query["client_id"];
        }
        else if (context.Request.Method == "POST" && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync();
            request.IdTokenHint = form["id_token_hint"];
            request.PostLogoutRedirectUri = form["post_logout_redirect_uri"];
            request.State = form["state"];
            request.UiLocales = form["ui_locales"];
            request.ClientId = form["client_id"];
        }

        return request;
    }

    private static async Task<IResult> ProcessLogoutAsync(
        HttpContext context,
        EndSessionRequest request,
        IEndSessionService endSessionService,
        string? userId)
    {
        // Process the logout
        var result = await endSessionService.ProcessLogoutAsync(request, userId);
        
        if (!result.Success)
        {
            var errorService = context.RequestServices.GetRequiredService<IErrorService>();
            await errorService.WriteTokenErrorAsync(context, "server_error", 
                result.ErrorMessage ?? "Logout processing failed");
            return Results.StatusCode(500);
        }

        // Sign out the user from the local authentication scheme
        await context.SignOutAsync("auth"); // Using the cookie scheme from Program.cs

        // Clear any additional authentication cookies
        context.Response.Cookies.Delete("idsrv.session");

        // Redirect to post-logout URI or show logged out page
        if (!string.IsNullOrEmpty(result.PostLogoutRedirectUri))
        {
            var redirectUri = BuildRedirectUri(result.PostLogoutRedirectUri, result.State);
            return Results.Redirect(redirectUri);
        }
        else
        {
            return await ShowLoggedOutPageAsync(context);
        }
    }

    private static async Task<IResult> ShowLogoutConfirmationPageAsync(
        HttpContext context, 
        EndSessionRequest request, 
        EndSessionValidationResult validation)
    {
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Logout Confirmation</title>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <style>
        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5; }}
        .container {{ max-width: 500px; margin: 50px auto; background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .header h1 {{ color: #333; margin: 0; }}
        .content {{ text-align: center; }}
        .buttons {{ margin-top: 30px; }}
        .btn {{ display: inline-block; padding: 12px 24px; margin: 0 10px; text-decoration: none; border-radius: 4px; font-weight: bold; border: none; cursor: pointer; }}
        .btn-primary {{ background-color: #007bff; color: white; }}
        .btn-secondary {{ background-color: #6c757d; color: white; }}
        .btn:hover {{ opacity: 0.8; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Logout Confirmation</h1>
        </div>
        <div class=""content"">
            <p>Are you sure you want to logout?</p>
            {(string.IsNullOrEmpty(validation.ClientId) ? "" : $"<p><small>Client: {validation.ClientId}</small></p>")}
        </div>
        <div class=""buttons"">
            <form method=""post"" style=""display: inline;"">
                <input type=""hidden"" name=""id_token_hint"" value=""{request.IdTokenHint ?? ""}"" />
                <input type=""hidden"" name=""post_logout_redirect_uri"" value=""{request.PostLogoutRedirectUri ?? ""}"" />
                <input type=""hidden"" name=""state"" value=""{request.State ?? ""}"" />
                <input type=""hidden"" name=""client_id"" value=""{request.ClientId ?? ""}"" />
                <input type=""hidden"" name=""logout_confirmed"" value=""true"" />
                <button type=""submit"" class=""btn btn-primary"">Yes, Logout</button>
            </form>
            <a href=""javascript:history.back()"" class=""btn btn-secondary"">Cancel</a>
        </div>
    </div>
</body>
</html>";

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(html);
        return Results.Empty;
    }

    private static async Task<IResult> ShowLoggedOutPageAsync(HttpContext context)
    {
        var html = @"
<!DOCTYPE html>
<html>
<head>
    <title>Logged Out</title>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <style>
        body { font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5; }
        .container { max-width: 500px; margin: 50px auto; background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        .header { text-align: center; margin-bottom: 30px; }
        .header h1 { color: #28a745; margin: 0; }
        .content { text-align: center; }
        .success-icon { font-size: 48px; color: #28a745; margin-bottom: 20px; }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <div class=""success-icon"">✓</div>
            <h1>Logged Out Successfully</h1>
        </div>
        <div class=""content"">
            <p>You have been successfully logged out.</p>
            <p><small>You can safely close this window.</small></p>
        </div>
    </div>
</body>
</html>";

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(html);
        return Results.Empty;
    }

    private static string BuildRedirectUri(string baseUri, string? state)
    {
        if (string.IsNullOrEmpty(state))
            return baseUri;

        var separator = baseUri.Contains('?') ? "&" : "?";
        return $"{baseUri}{separator}state={Uri.EscapeDataString(state)}";
    }
}