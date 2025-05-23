using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaofAPI.Data;
using MaofAPI.Models;
using MaofAPI.Models.Enums;
using MaofAPI.Authorization;
using MaofAPI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace MaofAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UsersController> _logger;
        private readonly AuthService _authService;

        public UsersController(ApplicationDbContext context, ILogger<UsersController> logger, AuthService authService)
        {
            _context = context;
            _logger = logger;
            _authService = authService;
        }

        // GET: api/users
        [HttpGet]
        [Authorize(Policy = Permissions.ViewUsers)]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers([FromQuery] UserFilterDto filter)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue && !User.HasClaim("permission", Permissions.ManageAllStores))
                {
                    return Forbid("User is not associated with any store and doesn't have system-wide permissions");
                }

                var query = _context.Users
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                    .AsQueryable();

                // If not a system admin (who can view all stores), filter by store
                if (!User.HasClaim("permission", Permissions.ManageAllStores))
                {
                    query = query.Where(u => u.StoreId == storeId);
                }

                // Apply filters
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.SearchTerm))
                    {
                        query = query.Where(u =>
                            u.UserName.Contains(filter.SearchTerm) ||
                            u.Email.Contains(filter.SearchTerm) ||
                            u.FirstName.Contains(filter.SearchTerm) ||
                            u.LastName.Contains(filter.SearchTerm));
                    }

                    if (filter.IsActive.HasValue)
                    {
                        query = query.Where(u => u.IsActive == filter.IsActive.Value);
                    }

                    if (filter.RoleId.HasValue)
                    {
                        query = query.Where(u => u.UserRoles.Any(ur => ur.RoleId == filter.RoleId.Value));
                    }
                }

                // Apply pagination
                var pageSize = filter?.PageSize ?? 50;
                var pageNumber = filter?.PageNumber ?? 1;

                var users = await query
                    .OrderBy(u => u.UserName)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Remove sensitive data before returning
                foreach (var user in users)
                {
                    user.PasswordHash = null;
                }

                _logger.LogInformation("Retrieved {UserCount} users", users.Count);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return StatusCode(500, "An error occurred while retrieving users");
            }
        }

        [HttpGet("{id}")]
        [Authorize(Policy = Permissions.ViewUsers)]
        public async Task<ActionResult<UserDto>> GetUser(int id)
        {
            try
            {
                int? storeId = GetUserStoreId();
             if (!storeId.HasValue && !User.HasClaim("permission", Permissions.ManageAllStores))
               {
                   return Forbid("User is not associated with any store and doesn't have system-wide permissions");
               }

                var user = await _context.Users
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                    .Where(u => u.Id == id)
                    .Select(u => new UserDto
                    {
                        Id = u.Id,
                        UserName = u.UserName,
                        Email = u.Email,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        IsActive = u.IsActive,
                        StoreId = u.StoreId,
                        CreatedAt = u.CreatedAt,
                        UpdatedAt = u.UpdatedAt,
                        LastLoginDate = u.LastLoginDate,
                        UserRoles = u.UserRoles.Select(ur => new UserRoleDto
                        {
                            RoleId = ur.RoleId,
                            RoleName = ur.Role.Name
                        }).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found", id);
                    return NotFound($"User with ID {id} not found");
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user with ID {UserId}", id);
                return StatusCode(500, "An error occurred while retrieving the user");
            }
        }

        // POST: api/users
        [HttpPost]
        [Authorize(Policy = Permissions.CreateUsers)]
        public async Task<ActionResult<User>> CreateUser(UserCreateDto userDto)
        {
            try
            {
                int? adminStoreId = GetUserStoreId();
                
                // Only system admins (ManageAllStores) can create users for any store
                // Regular admins must be associated with a store and can only create users for their store
                if (!adminStoreId.HasValue && !User.HasClaim("permission", Permissions.ManageAllStores))
                {
                    return Forbid("Admin is not associated with any store and doesn't have system-wide permissions");
                }

                // Check if username already exists
                bool usernameExists = await _context.Users.AnyAsync(u => u.UserName == userDto.UserName);
                if (usernameExists)
                {
                    return BadRequest("Username is already taken");
                }

                // Check if email already exists
                if (!string.IsNullOrEmpty(userDto.Email))
                {
                    bool emailExists = await _context.Users.AnyAsync(u => u.Email == userDto.Email);
                    if (emailExists)
                    {
                        return BadRequest("Email is already registered");
                    }
                }

                // If not system admin, ensure user is created for admin's store
                if (!User.HasClaim("permission", Permissions.ManageAllStores))
                {
                    userDto.StoreId = adminStoreId;
                }
                
                // Validate if the specified StoreId exists
                if (userDto.StoreId.HasValue)
                {
                    bool storeExists = await _context.Stores.AnyAsync(s => s.Id == userDto.StoreId.Value);
                    if (!storeExists)
                    {
                        return BadRequest("Specified store does not exist");
                    }
                }

                // Validate roles
                if (userDto.RoleIds != null && userDto.RoleIds.Any())
                {
                    foreach (var roleId in userDto.RoleIds)
                    {
                        bool roleExists = await _context.Roles.AnyAsync(r => r.Id == roleId);
                        if (!roleExists)
                        {
                            return BadRequest($"Role with ID {roleId} does not exist");
                        }
                    }
                }

                // Hash password
                string hashedPassword = _authService.HashPassword(userDto.Password);

                // Create new user
                var user = new User
                {
                    UserName = userDto.UserName,
                    PasswordHash = hashedPassword,
                    Email = userDto.Email,
                    FirstName = userDto.FirstName,
                    LastName = userDto.LastName,
                    IsActive = true,
                    StoreId = userDto.StoreId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Assign roles
                if (userDto.RoleIds != null && userDto.RoleIds.Any())
                {
                    foreach (var roleId in userDto.RoleIds)
                    {
                        var userRole = new UserRole
                        {
                            UserId = user.Id,
                            RoleId = roleId,
                            CreatedAt = DateTime.UtcNow,
                        };
                        _context.UserRoles.Add(userRole);
                    }
                    await _context.SaveChangesAsync();
                }

                // Log user creation
                var adminId = GetUserId();
                _logger.LogInformation("User {Username} created by admin ID {AdminId}", user.UserName, adminId);

                // Remove sensitive data before returning
                user.PasswordHash = null;

                return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, "An error occurred while creating user");
            }
        }

        // PUT: api/users/5
        [HttpPut("{id}")]
        [Authorize(Policy = Permissions.EditUsers)]
        public async Task<IActionResult> UpdateUser(int id, UserUpdateDto userDto)
        {
            try
            {
                int? adminStoreId = GetUserStoreId();
                
                // Only system admins can update users from any store
                // Regular admins must be associated with a store and can only update users in their store
                if (!adminStoreId.HasValue && !User.HasClaim("permission", Permissions.ManageAllStores))
                {
                    return Forbid("Admin is not associated with any store and doesn't have system-wide permissions");
                }

                // Find the user
                var user = await _context.Users
                    .Include(u => u.UserRoles)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found", id);
                    return NotFound($"User with ID {id} not found");
                }

                // Regular admins can only update users in their store
                if (!User.HasClaim("permission", Permissions.ManageAllStores) && user.StoreId != adminStoreId)
                {
                    return Forbid("You can only update users in your own store");
                }

                // Check username uniqueness if changing username
                if (!string.IsNullOrEmpty(userDto.UserName) && userDto.UserName != user.UserName)
                {
                    bool usernameExists = await _context.Users.AnyAsync(u => u.UserName == userDto.UserName && u.Id != id);
                    if (usernameExists)
                    {
                        return BadRequest("Username is already taken");
                    }
                    user.UserName = userDto.UserName;
                }

                // Check email uniqueness if changing email
                if (!string.IsNullOrEmpty(userDto.Email) && userDto.Email != user.Email)
                {
                    bool emailExists = await _context.Users.AnyAsync(u => u.Email == userDto.Email && u.Id != id);
                    if (emailExists)
                    {
                        return BadRequest("Email is already registered");
                    }
                    user.Email = userDto.Email;
                }

                // Update password if provided
                if (!string.IsNullOrEmpty(userDto.Password))
                {
                    user.PasswordHash = _authService.HashPassword(userDto.Password);
                }

                // Update other fields
                if (!string.IsNullOrEmpty(userDto.FirstName))
                {
                    user.FirstName = userDto.FirstName;
                }

                if (!string.IsNullOrEmpty(userDto.LastName))
                {
                    user.LastName = userDto.LastName;
                }

                if (userDto.IsActive.HasValue)
                {
                    user.IsActive = userDto.IsActive.Value;
                }

                // System admins can change user's store
                if (User.HasClaim("permission", Permissions.ManageAllStores) && userDto.StoreId.HasValue)
                {
                    bool storeExists = await _context.Stores.AnyAsync(s => s.Id == userDto.StoreId.Value);
                    if (!storeExists)
                    {
                        return BadRequest("Specified store does not exist");
                    }
                    user.StoreId = userDto.StoreId.Value;
                }

                user.UpdatedAt = DateTime.UtcNow;

                // Update roles if provided
                if (userDto.RoleIds != null && userDto.RoleIds.Any())
                {
                    // Validate roles
                    foreach (var roleId in userDto.RoleIds)
                    {
                        bool roleExists = await _context.Roles.AnyAsync(r => r.Id == roleId);
                        if (!roleExists)
                        {
                            return BadRequest($"Role with ID {roleId} does not exist");
                        }
                    }

                    // Remove existing roles
                    _context.UserRoles.RemoveRange(user.UserRoles);

                    // Add new roles
                    foreach (var roleId in userDto.RoleIds)
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

                // Log user update
                var adminId = GetUserId();
                _logger.LogInformation("User with ID {UserId} updated by admin ID {AdminId}", id, adminId);

                return Ok(new { message = "User updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user with ID {UserId}", id);
                return StatusCode(500, $"An error occurred while updating user with ID {id}");
            }
        }

        // DELETE: api/users/5
        [HttpDelete("{id}")]
        [Authorize(Policy = Permissions.DeleteUsers)]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                int? adminStoreId = GetUserStoreId();
                
                // Only system admins can delete users from any store
                // Regular admins must be associated with a store and can only delete users in their store
                if (!adminStoreId.HasValue && !User.HasClaim("permission", Permissions.ManageAllStores))
                {
                    return Forbid("Admin is not associated with any store and doesn't have system-wide permissions");
                }

                // Find the user
                var user = await _context.Users
                    .Include(u => u.UserRoles)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found", id);
                    return NotFound($"User with ID {id} not found");
                }

                // Regular admins can only delete users in their store
                if (!User.HasClaim("permission", Permissions.ManageAllStores) && user.StoreId != adminStoreId)
                {
                    return Forbid("You can only delete users in your own store");
                }

                // Prevent deletion of the currently logged-in user
                var currentUserId = GetUserId();
                if (user.Id == currentUserId)
                {
                    return BadRequest("You cannot delete your own account");
                }

                // Instead of hard delete, soft delete by setting IsActive to false
                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Log user deactivation
                _logger.LogInformation("User with ID {UserId} deactivated by admin ID {AdminId}", id, currentUserId);

                return Ok(new { message = "User deactivated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user with ID {UserId}", id);
                return StatusCode(500, $"An error occurred while deleting user with ID {id}");
            }
        }

        // POST: api/users/sync
        [HttpPost("sync")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<IEnumerable<User>>> SyncUsers()
        {
            // Sync functionality has been removed as it's not supported in this version
            return BadRequest("User synchronization is not supported in this version");
        }

        // GET: api/users/pending-sync
        [HttpGet("pending-sync")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<IEnumerable<User>>> GetPendingSyncUsers()
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue && !User.HasClaim("permission", Permissions.ManageAllStores))
                {
                    return Forbid("User is not associated with any store and doesn't have system-wide permissions");
                }

                // Return empty list as sync is not supported
                _logger.LogInformation("User synchronization is not supported in this version");
                return Ok(new List<User>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPendingSyncUsers");
                return StatusCode(500, "An error occurred while processing the request");
            }
        }

        // POST: api/users/change-password
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto model)
        {
            try
            {
                var userId = GetUserId();
                var result = await _authService.ChangePasswordAsync(userId, model.CurrentPassword, model.NewPassword);

                if (!result.Success)
                {
                    return BadRequest(new { message = result.Message });
                }

                _logger.LogInformation("Password changed for user ID {UserId}", userId);
                return Ok(new { message = "Password changed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user ID {UserId}", GetUserId());
                return StatusCode(500, "An error occurred while changing password");
            }
        }

        // Helper methods
        private int? GetUserStoreId()
        {
            var storeIdClaim = User.FindFirst("storeId");
            if (storeIdClaim != null && int.TryParse(storeIdClaim.Value, out int storeId))
            {
                return storeId;
            }
            return null;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return 0; // This should never happen if authorization is working correctly
        }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsActive { get; set; }
        public int? StoreId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public ICollection<UserRoleDto> UserRoles { get; set; }
    }

    
    public class UserRoleDto
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }
    }

    // DTOs for Users
    public class UserFilterDto
    {
        public string SearchTerm { get; set; }
        public bool? IsActive { get; set; }
        public int? RoleId { get; set; }
        public int PageSize { get; set; } = 50;
        public int PageNumber { get; set; } = 1;
    }

    public class UserCreateDto
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string UserName { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        public int? StoreId { get; set; }

        [Required]
        public List<int> RoleIds { get; set; }

        public Guid? SyncId { get; set; }
    }

    public class UserUpdateDto
    {
        [StringLength(50, MinimumLength = 3)]
        public string UserName { get; set; }

        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public bool? IsActive { get; set; }

        public int? StoreId { get; set; }

        public List<int> RoleIds { get; set; }
    }

    public class ChangePasswordDto
    {
        [Required]
        public string CurrentPassword { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; }
    }
}
