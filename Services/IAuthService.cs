using TexfinaApi.DTOs;

namespace TexfinaApi.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto> LoginAsync(LoginDto loginDto);
        Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto);
        Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);
        string GenerateJwtToken(UserInfoDto user);
        Task<UserInfoDto?> GetUserInfoAsync(int userId);
        Task LogoutAsync(int userId);
    }
} 