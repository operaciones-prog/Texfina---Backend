using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TexfinaApi.Data;
using TexfinaApi.DTOs;
using TexfinaApi.Models;

namespace TexfinaApi.Services
{
    public class AuthService : IAuthService
    {
        private readonly TexfinaDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(TexfinaDbContext context, IConfiguration configuration, ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto loginDto)
        {
            try
            {
                var usuario = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.Username == loginDto.Username && u.Activo == true);

                if (usuario == null || string.IsNullOrEmpty(usuario.PasswordHash) || !VerifyPassword(loginDto.Password, usuario.PasswordHash))
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Credenciales inválidas"
                    };
                }

                // Actualizar último login
                usuario.LastLogin = DateTime.Now;
                await _context.SaveChangesAsync();

                // Temporal: simplificar para testing
                var userInfo = new UserInfoDto
                {
                    IdUsuario = usuario.IdUsuario,
                    Username = usuario.Username,
                    Email = usuario.Email,
                    Rol = usuario.IdRol ?? "USER"
                };

                // Generar token
                var token = GenerateJwtToken(userInfo);
                var expires = DateTime.UtcNow.AddHours(8); // Token válido por 8 horas

                return new AuthResponseDto
                {
                    Success = true,
                    Token = token,
                    Expires = expires,
                    User = userInfo,
                    Message = "Login exitoso"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el login");
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Error interno del servidor"
                };
            }
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto)
        {
            try
            {
                // Verificar si el usuario ya existe
                var existingUser = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.Username == registerDto.Username);

                if (existingUser != null)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "El nombre de usuario ya está en uso"
                    };
                }

                // Verificar email si se proporciona
                if (!string.IsNullOrEmpty(registerDto.Email))
                {
                    var existingEmail = await _context.Usuarios
                        .FirstOrDefaultAsync(u => u.Email == registerDto.Email);

                    if (existingEmail != null)
                    {
                        return new AuthResponseDto
                        {
                            Success = false,
                            Message = "El email ya está en uso"
                        };
                    }
                }

                // Crear nuevo usuario
                var usuario = new Usuario
                {
                    Username = registerDto.Username,
                    Email = registerDto.Email,
                    PasswordHash = HashPassword(registerDto.Password),
                    IdRol = registerDto.IdRol ?? "OPERARIO", // Rol por defecto
                    IdTipoUsuario = registerDto.IdTipoUsuario,
                    Activo = true,
                    CreatedAt = DateTime.Now
                };

                _context.Usuarios.Add(usuario);
                await _context.SaveChangesAsync();

                // Log del evento de registro
                var logEvento = new LogEvento
                {
                    IdUsuario = usuario.IdUsuario,
                    Accion = "REGISTER",
                    Descripcion = $"Nuevo usuario registrado: {usuario.Username}",
                    Modulo = "Autenticación",
                    Timestamp = DateTime.Now
                };
                _context.LogEventos.Add(logEvento);
                await _context.SaveChangesAsync();

                // Obtener información del usuario creado
                var userInfo = await GetUserInfoAsync(usuario.IdUsuario);
                if (userInfo == null)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Error al crear el usuario"
                    };
                }

                return new AuthResponseDto
                {
                    Success = true,
                    User = userInfo,
                    Message = "Usuario registrado exitosamente"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el registro");
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Error interno del servidor"
                };
            }
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
        {
            // TODO: Implementar refresh token completo
            // Esto sería útil para renovar tokens sin hacer login otra vez
            await Task.CompletedTask;
            return new AuthResponseDto
            {
                Success = false,
                Message = "Refresh token no implementado aún"
            };
        }

        public string GenerateJwtToken(UserInfoDto user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "default-secret-key-for-development-only");

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.IdUsuario.ToString()),
                new(ClaimTypes.Name, user.Username),
                new("username", user.Username),
                new("userId", user.IdUsuario.ToString())
            };

            if (!string.IsNullOrEmpty(user.Email))
                claims.Add(new Claim(ClaimTypes.Email, user.Email));

            if (!string.IsNullOrEmpty(user.Rol))
                claims.Add(new Claim(ClaimTypes.Role, user.Rol));

            // Agregar permisos como claims
            foreach (var permiso in user.Permisos)
            {
                claims.Add(new Claim("permission", permiso));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(8),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// Obtiene información completa del usuario incluyendo permisos
        /// Útil para el endpoint /auth/me y verificar datos actualizados
        /// </summary>
        public async Task<UserInfoDto?> GetUserInfoAsync(int userId)
        {
            try
            {
                var usuario = await _context.Usuarios
                    .Include(u => u.Rol)
                        .ThenInclude(r => r!.RolPermisos)
                        .ThenInclude(rp => rp.Permiso)
                    .Include(u => u.TipoUsuario)
                    .FirstOrDefaultAsync(u => u.IdUsuario == userId);

                if (usuario == null)
                    return null;

                var permisos = usuario.Rol?.RolPermisos?.Select(rp => rp.Permiso?.Nombre).Where(p => !string.IsNullOrEmpty(p)).Cast<string>().ToList() ?? new List<string>();

                return new UserInfoDto
                {
                    IdUsuario = usuario.IdUsuario,
                    Username = usuario.Username,
                    Email = usuario.Email,
                    Rol = usuario.Rol?.Nombre,
                    TipoUsuario = usuario.TipoUsuario?.Descripcion,
                    Activo = usuario.Activo ?? false,
                    LastLogin = usuario.LastLogin,
                    Permisos = permisos!
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener información del usuario {UserId}", userId);
                return null;
            }
        }

        /// <summary>
        /// Cierra sesiones activas del usuario en la base de datos
        /// Aunque el token JWT sigue siendo válido hasta que expire,
        /// esto nos ayuda con auditoría y control de sesiones
        /// </summary>
        public async Task LogoutAsync(int userId)
        {
            try
            {
                // Cerrar sesiones activas del usuario
                var sesionesActivas = await _context.Sesiones
                    .Where(s => s.IdUsuario == userId && s.Fin == null)
                    .ToListAsync();

                foreach (var sesion in sesionesActivas)
                {
                    sesion.Fin = DateTime.Now;
                    sesion.CerradaAutomaticamente = false;
                }

                // Log del evento de logout
                var logEvento = new LogEvento
                {
                    IdUsuario = userId,
                    Accion = "LOGOUT",
                    Descripcion = "Usuario cerró sesión",
                    Modulo = "Autenticación",
                    Timestamp = DateTime.Now
                };
                _context.LogEventos.Add(logEvento);

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante logout del usuario {UserId}", userId);
            }
        }

        private string HashPassword(string password)
        {
            // Usar BCrypt para hash seguro de contraseñas
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        private bool VerifyPassword(string password, string hash)
        {
            try 
            {
                // Verificar con BCrypt
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying password hash for user authentication");
                return false; // Fallar de forma segura
            }
        }
    }
} 