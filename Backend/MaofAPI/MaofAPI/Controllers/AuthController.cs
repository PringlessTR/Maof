using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaofAPI.Services;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using MaofAPI.Authorization;
using MaofAPI.Models;
using System;

namespace MaofAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.AuthenticateAsync(request.Username, request.Password);

            if (!result.Success)
            {
                _logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
                return Unauthorized(new { message = result.Message });
            }

            _logger.LogInformation("Successful login for user: {Username}", request.Username);
            return Ok(result);
        }

        [HttpGet("validate")]
        [Authorize]
        public IActionResult ValidateToken()
        {
            // If we get here, the token is valid (because of the [Authorize] attribute)
            return Ok(new { valid = true });
        }

        [HttpGet("user-info")]
        [Authorize]
        public IActionResult GetUserInfo()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            var storeId = User.FindFirst("storeId")?.Value;
            var firstName = User.FindFirst("firstName")?.Value;
            var lastName = User.FindFirst("lastName")?.Value;
            var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
            var permissions = User.FindAll("permission").Select(c => c.Value).ToList();

            return Ok(new
            {
                UserId = userId,
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                StoreId = storeId,
                Roles = roles,
                Permissions = permissions
            });
        }

        // Admin only endpoint to create a new user
        [HttpPost("create-user")]
        [Authorize(Policy = Permissions.CreateUsers)]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            int? adminStoreId = GetUserStoreId();
            // Allow system admin to create users for any store
            if (!adminStoreId.HasValue && !User.HasClaim("permission", Permissions.ManageAllStores))
            {
                return Forbid("Admin is not associated with any store");
            }

            // If request doesn't specify a storeId, use the admin's storeId
            if (!request.StoreId.HasValue && adminStoreId.HasValue)
            {
                request.StoreId = adminStoreId.Value;
            }

            // Only system admins can create users for different stores
            if (request.StoreId != adminStoreId && !User.HasClaim("permission", Permissions.ManageAllStores))
            {
                return Forbid("You can only create users for your own store");
            }

            var result = await _authService.CreateUserAsync(
                request.Username, 
                request.Password, 
                request.Email, 
                request.FirstName, 
                request.LastName, 
                request.StoreId, 
                request.RoleIds);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            _logger.LogInformation("New user created: {Username} for store {StoreId} by admin ID: {AdminId}", 
                request.Username, request.StoreId, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            
            return CreatedAtAction(nameof(GetUserInfo), new { id = result.UserId }, new { message = "User created successfully", userId = result.UserId });
        }

        // Admin only endpoint to update a user
        [HttpPut("update-user/{id}")]
        [Authorize(Policy = Permissions.EditUsers)]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            int? adminStoreId = GetUserStoreId();
            // Regular admins can only update users in their store
            if (!adminStoreId.HasValue && !User.HasClaim("permission", Permissions.ManageAllStores))
            {
                return Forbid("Admin is not associated with any store");
            }

            // Check if user belongs to admin's store (unless system admin)
            if (!User.HasClaim("permission", Permissions.ManageAllStores))
            {
                bool userInStore = await _authService.IsUserInStore(id, adminStoreId.Value);
                if (!userInStore)
                {
                    return Forbid("You can only update users in your own store");
                }
            }

            var result = await _authService.UpdateUserAsync(
                id,
                request.Username,
                request.Password, // Allow password change by admin
                request.Email,
                request.FirstName,
                request.LastName,
                request.IsActive,
                request.RoleIds);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            _logger.LogInformation("User updated: {UserId} by admin ID: {AdminId}", 
                id, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            
            return Ok(new { message = "User updated successfully" });
        }

        // Helper method
        private int? GetUserStoreId()
        {
            var storeIdClaim = User.FindFirst("storeId");
            if (storeIdClaim != null && int.TryParse(storeIdClaim.Value, out int storeId))
            {
                return storeId;
            }
            return null;
        }
    }

    public class LoginRequest
    {
        [Required]
        public string Username { get; set; }

        [Required]
        public string Password { get; set; }
    }

    public class CreateUserRequest
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        public int? StoreId { get; set; }

        [Required]
        public List<int> RoleIds { get; set; }
    }

    public class UpdateUserRequest
    {
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; }

        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } // Optional - only if admin wants to change it

        [EmailAddress]
        public string Email { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public bool? IsActive { get; set; }

        public List<int> RoleIds { get; set; }
    }
}
