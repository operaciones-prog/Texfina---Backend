using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TexfinaApi.DTOs;
using TexfinaApi.Services;

namespace TexfinaApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.LoginAsync(loginDto);
            
            if (!result.Success)
            {
                return Unauthorized(result);
            }

            return Ok(result);
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.RegisterAsync(registerDto);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<ActionResult> Logout()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return BadRequest("ID de usuario inválido");
            }

            await _authService.LogoutAsync(userId);
            
            return Ok(new { Success = true, Message = "Logout exitoso" });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserInfoDto>> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return BadRequest("ID de usuario inválido");
            }

            var userInfo = await _authService.GetUserInfoAsync(userId);
            
            if (userInfo == null)
            {
                return NotFound("Usuario no encontrado");
            }

            return Ok(userInfo);
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] string refreshToken)
        {
            var result = await _authService.RefreshTokenAsync(refreshToken);
            
            if (!result.Success)
            {
                return Unauthorized(result);
            }

            return Ok(result);
        }
    }
} 