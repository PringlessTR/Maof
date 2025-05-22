using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MaofAPI.Data;
using MaofAPI.Models;
using MaofAPI.Models.Enums;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
// Explicit reference to the BCrypt package with alias to avoid conflicts
using BCryptNet = BCrypt.Net.BCrypt;

namespace MaofAPI.Services
{
    public class AuthService
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public AuthService(IConfiguration configuration, ApplicationDbContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        public string HashPassword(string password)
        {
            // Using the alias defined in the using statement
            return BCryptNet.HashPassword(password, workFactor: 12);
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            // Using the alias defined in the using statement
            return BCryptNet.Verify(password, hashedPassword);
        }

        public async Task<AuthResult> AuthenticateAsync(string username, string password)
        {
            // Find user by username
            var user = await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.UserName == username);

            if (user == null)
            {
                return new AuthResult { Success = false, Message = "User not found" };
            }

            // Verify password
            if (!VerifyPassword(password, user.PasswordHash))
            {
                return new AuthResult { Success = false, Message = "Invalid password" };
            }

            // Get roles and permissions
            var roles = user.UserRoles.Select(ur => ur.Role).ToList();
            var roleName = roles.FirstOrDefault()?.Name ?? "User";
            var permissions = roles
                .SelectMany(r => r.RolePermissions)
                .Select(rp => rp.Permission.Name)
                .Distinct()
                .ToList();

            // Generate JWT token
            var token = GenerateJwtToken(user, new List<string> { roleName }, permissions);

            // Update last login date
            user.LastLoginDate = DateTime.Now; // Local time instead of UTC
            await _context.SaveChangesAsync();

            return new AuthResult
            {
                Success = true,
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.UserName,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    StoreId = user.StoreId,
                    Roles = new List<string> { roleName },
                    Permissions = permissions
                }
            };
        }

        public async Task<AuthResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return new AuthResult { Success = false, Message = "User not found" };
            }

            // Verify current password
            if (!VerifyPassword(currentPassword, user.PasswordHash))
            {
                return new AuthResult { Success = false, Message = "Current password is incorrect" };
            }

            // Check if new password meets complexity requirements
            if (newPassword.Length < 8)
            {
                return new AuthResult { Success = false, Message = "New password must be at least 8 characters long" };
            }

            // Update to new password
            user.PasswordHash = HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            
            return new AuthResult { Success = true, Message = "Password changed successfully" };
        }

        public async Task<AuthResult> CreateUserAsync(string username, string password, string email, string firstName, string lastName, int? storeId, List<int> roleIds)
        {
            // Check if username already exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
            if (existingUser != null)
            {
                return new AuthResult { Success = false, Message = "Username already exists" };
            }

            // Check if email already exists
            if (!string.IsNullOrEmpty(email))
            {
                var userWithEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (userWithEmail != null)
                {
                    return new AuthResult { Success = false, Message = "Email already in use" };
                }
            }

            // Validate password complexity
            if (password.Length < 8)
            {
                return new AuthResult { Success = false, Message = "Password must be at least 8 characters long" };
            }

            // Create user
            var user = new User
            {
                UserName = username,
                PasswordHash = HashPassword(password),
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                StoreId = storeId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Assign roles
            if (roleIds != null && roleIds.Any())
            {
                foreach (var roleId in roleIds)
                {
                    var role = await _context.Roles.FindAsync(roleId);
                    if (role != null)
                    {
                        var userRole = new UserRole
                        {
                            UserId = user.Id,
                            RoleId = roleId,
                            CreatedAt = DateTime.UtcNow,
                        };
                        _context.UserRoles.Add(userRole);
                    }
                }
                await _context.SaveChangesAsync();
            }

            return new AuthResult { Success = true, Message = "User created successfully", UserId = user.Id };
        }

        public async Task<AuthResult> UpdateUserAsync(int userId, string username, string password, string email, string firstName, string lastName, bool? isActive, List<int> roleIds)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return new AuthResult { Success = false, Message = "User not found" };
            }

            // Check if username is already taken by another user
            if (username != user.UserName)
            {
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username && u.Id != userId);
                if (existingUser != null)
                {
                    return new AuthResult { Success = false, Message = "Username already exists" };
                }
                user.UserName = username;
            }

            // Check if email is already taken by another user
            if (!string.IsNullOrEmpty(email) && email != user.Email)
            {
                var userWithEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.Id != userId);
                if (userWithEmail != null)
                {
                    return new AuthResult { Success = false, Message = "Email already in use" };
                }
                user.Email = email;
            }

            // Update password if provided
            if (!string.IsNullOrEmpty(password))
            {
                if (password.Length < 8)
                {
                    return new AuthResult { Success = false, Message = "Password must be at least 8 characters long" };
                }
                user.PasswordHash = HashPassword(password);
            }

            // Update other fields
            user.FirstName = firstName;
            user.LastName = lastName;
            if (isActive.HasValue)
            {
                user.IsActive = isActive.Value;
            }
            user.UpdatedAt = DateTime.UtcNow;

            // Update roles if provided
            if (roleIds != null)
            {
                // Remove existing roles
                _context.UserRoles.RemoveRange(user.UserRoles);

                // Add new roles
                foreach (var roleId in roleIds)
                {
                    var role = await _context.Roles.FindAsync(roleId);
                    if (role != null)
                    {
                        var userRole = new UserRole
                        {
                            UserId = user.Id,
                            RoleId = roleId,
                            CreatedAt = DateTime.UtcNow,
                        };
                        _context.UserRoles.Add(userRole);
                    }
                }
            }

            await _context.SaveChangesAsync();
            return new AuthResult { Success = true, Message = "User updated successfully", UserId = userId };
        }

        public async Task<bool> IsUserInStore(int userId, int storeId)
        {
            var user = await _context.Users.FindAsync(userId);
            return user != null && user.StoreId == storeId;
        }

        private string GenerateJwtToken(User user, List<string> roles, List<string> permissions)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim("email", user.Email ?? string.Empty)
            };

            // Add roles as claims
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Add permissions as claims
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            // Add store ID claim (if applicable)
            if (user.StoreId.HasValue)
            {
                claims.Add(new Claim("storeId", user.StoreId.Value.ToString()));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            
            // Get token duration from config
            int tokenDurationInHours = int.Parse(_configuration["JWT:DurationInHours"] ?? "12");
            var expires = DateTime.UtcNow.AddHours(tokenDurationInHours);

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:Issuer"],
                audience: _configuration["JWT:Audience"],
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Token { get; set; }
        public int UserId { get; set; }
        public UserDto User { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int? StoreId { get; set; }
        public List<string> Roles { get; set; }
        public List<string> Permissions { get; set; }
    }
}
