using Microsoft.AspNetCore.Mvc;
using AuthCoreSdk.Models;
using AuthCoreSdk.Services;

namespace AuthCoreSdk.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly JwtTokenService _jwtTokenService;

        public AuthController(UserService userService, JwtTokenService jwtTokenService)
        {
            _userService = userService;
            _jwtTokenService = jwtTokenService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
        {
            var user = await _userService.ValidateUserCredentials(loginRequest.Username, loginRequest.Password);

            if (user == null)
            {
                return Unauthorized();
            }

            var token = _jwtTokenService.GenerateToken(user);
            return Ok(new LoginResponse { Token = token });
        }
    }
}
