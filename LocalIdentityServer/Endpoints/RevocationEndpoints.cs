using LocalIdentityServer.Models.Requests;
using LocalIdentityServer.Services;

namespace LocalIdentityServer.Endpoints;

/// <summary>
/// OAuth2 Token Revocation Endpoint (RFC 7009)
/// </summary>
public static class RevocationEndpoints
{
    public static void MapRevocationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/connect").WithTags("OAuth2 Revocation");

        // Token revocation endpoint
        group.MapPost("/revocation", HandleRevocationRequest)
            .WithName("TokenRevocation")
            .WithSummary("OAuth2 Token Revocation")
            .WithDescription("Revokes an access token or refresh token (RFC 7009)")
            .Produces(200)
            .Produces(400)
            .Produces(401);
    }

    private static async Task<IResult> HandleRevocationRequest(
        HttpContext context,
        ITokenRevocationService revocationService,
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
            var request = new RevocationRequest
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
            var isClientValid = await revocationService.ValidateClientAsync(
                request.ClientId, request.ClientSecret);
            
            if (!isClientValid)
            {
                await errorService.WriteTokenErrorAsync(context, "invalid_client",
                    "Client authentication failed");
                return Results.Unauthorized();
            }

            // Validate token_type_hint if provided
            if (!string.IsNullOrEmpty(request.TokenTypeHint) && 
                request.TokenTypeHint != "access_token" && 
                request.TokenTypeHint != "refresh_token")
            {
                await errorService.WriteTokenErrorAsync(context, "unsupported_token_type",
                    "Unsupported token type hint");
                return Results.BadRequest();
            }

            // Perform token revocation
            var revocationResult = await revocationService.RevokeTokenAsync(
                request.Token, 
                request.TokenTypeHint, 
                request.ClientId,
                "Client requested revocation");

            if (!revocationResult)
            {
                // Per RFC 7009, we should return success even if the token was invalid
                // but log the issue for monitoring
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("Token revocation failed for client {ClientId}, but returning success per RFC 7009", 
                    request.ClientId);
            }

            // Per RFC 7009: "The authorization server responds with HTTP status code 200 
            // if the revocation is successful or if the client submitted an invalid token"
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
            
            return Results.Ok();
        }
        catch (Exception ex)
        {
            // Log error but don't expose internal details
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Unexpected error during token revocation");
            
            await errorService.WriteTokenErrorAsync(context, "server_error",
                "An unexpected error occurred during token revocation");
            return Results.StatusCode(500);
        }
    }
}