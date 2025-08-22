using LocalIdentityServer.Models.Requests;
using LocalIdentityServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocalIdentityServer.Endpoints;

/// <summary>
/// OAuth2 Token Introspection Endpoint (RFC 7662)
/// </summary>
public static class IntrospectionEndpoints
{
    public static void MapIntrospectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/connect").WithTags("OAuth2 Introspection");

        // Token introspection endpoint
        group.MapPost("/introspect", HandleIntrospectionRequest)
            .WithName("TokenIntrospection")
            .WithSummary("OAuth2 Token Introspection")
            .WithDescription("Introspects a token to determine its current state and metadata (RFC 7662)")
            .Produces<object>(200, "application/json")
            .Produces(400)
            .Produces(401);
    }

    private static async Task<IResult> HandleIntrospectionRequest(
        HttpContext context,
        ITokenIntrospectionService introspectionService,
        IErrorService errorService)
    {
        try
        {
            // Ensure correct content type
            if (!context.Request.HasFormContentType)
            {
                await errorService.WriteTokenErrorAsync(context, "invalid_request",
                    "Content-Type must be application/x-www-form-urlencoded");
                return Results.BadRequest();
            }

            var form = await context.Request.ReadFormAsync();

            // Extract request parameters
            var request = new IntrospectionRequest
            {
                Token = form["token"].ToString(),
                TokenTypeHint = form["token_type_hint"].ToString(),
                ClientId = form["client_id"].ToString(),
                ClientSecret = form["client_secret"].ToString()
            };

            // Validate required parameters
            if (string.IsNullOrEmpty(request.Token))
            {
                await errorService.WriteTokenErrorAsync(context, "invalid_request",
                    "Missing required parameter: token");
                return Results.BadRequest();
            }

            if (string.IsNullOrEmpty(request.ClientId))
            {
                await errorService.WriteTokenErrorAsync(context, "invalid_request",
                    "Missing required parameter: client_id");
                return Results.BadRequest();
            }

            // Validate client credentials
            var isClientValid = await introspectionService.ValidateClientAsync(
                request.ClientId, request.ClientSecret);
            
            if (!isClientValid)
            {
                await errorService.WriteTokenErrorAsync(context, "invalid_client",
                    "Client authentication failed");
                return Results.Unauthorized();
            }

            // Perform token introspection
            var introspectionResult = await introspectionService.IntrospectTokenAsync(
                request.Token, request.TokenTypeHint, request.ClientId);

            // Return introspection response
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
            
            return Results.Json(introspectionResult, 
                options: new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
        }
        catch (Exception ex)
        {
            // Log error but don't expose internal details
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Unexpected error during token introspection");
            
            await errorService.WriteTokenErrorAsync(context, "server_error",
                "An unexpected error occurred during token introspection");
            return Results.StatusCode(500);
        }
    }
}