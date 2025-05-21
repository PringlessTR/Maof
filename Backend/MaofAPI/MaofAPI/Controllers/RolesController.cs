using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaofAPI.Data;
using MaofAPI.Models;
using MaofAPI.Models.Enums;
using MaofAPI.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace MaofAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RolesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RolesController> _logger;

        public RolesController(ApplicationDbContext context, ILogger<RolesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/roles
        [HttpGet]
        [Authorize(Policy = Permissions.ManageRoles)]
        public async Task<ActionResult<IEnumerable<object>>> GetRoles([FromQuery] RoleFilterDto filter)
        {
            try
            {
                var query = _context.Roles
                    .Include(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                    .AsQueryable();

                // Apply filters
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.SearchTerm))
                    {
                        query = query.Where(r =>
                            r.Name.Contains(filter.SearchTerm) ||
                            r.Description.Contains(filter.SearchTerm));
                    }

                    if (filter.PermissionId.HasValue)
                    {
                        query = query.Where(r => r.RolePermissions.Any(rp => rp.PermissionId == filter.PermissionId.Value));
                    }
                }

                // Apply pagination
                var pageSize = filter?.PageSize ?? 50;
                var pageNumber = filter?.PageNumber ?? 1;

                var roles = await query
                    .OrderBy(r => r.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Transform to remove sensitive info and shape response
                var result = roles.Select(r => new {
                    r.Id,
                    r.Name,
                    r.Description,
                    r.CreatedAt,
                    r.UpdatedAt,
                    r.SyncId,
                    r.SyncStatus,
                    Permissions = r.RolePermissions.Select(rp => new {
                        rp.Permission.Id,
                        rp.Permission.Name,
                        rp.Permission.Description
                    })
                });
                
                _logger.LogInformation("Retrieved {RoleCount} roles", roles.Count);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving roles");
                return StatusCode(500, "An error occurred while retrieving roles");
            }
        }

        // GET: api/roles/5
        [HttpGet("{id}")]
        [Authorize(Policy = Permissions.ManageRoles)]
        public async Task<ActionResult<Role>> GetRole(int id)
        {
            try
            {
                var role = await _context.Roles
                    .Include(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (role == null)
                {
                    _logger.LogWarning("Role with ID {RoleId} not found", id);
                    return NotFound($"Role with ID {id} not found");
                }

                return Ok(role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving role with ID {RoleId}", id);
                return StatusCode(500, $"An error occurred while retrieving role with ID {id}");
            }
        }

        // POST: api/roles
        [HttpPost]
        [Authorize(Policy = Permissions.ManageRoles)]
        public async Task<ActionResult<Role>> CreateRole(RoleCreateDto roleDto)
        {
            try
            {
                // Check if role name already exists
                bool nameExists = await _context.Roles.AnyAsync(r => r.Name == roleDto.Name);
                if (nameExists)
                {
                    return BadRequest("Role name already exists");
                }

                // Validate permissions
                if (roleDto.PermissionIds != null && roleDto.PermissionIds.Any())
                {
                    foreach (var permissionId in roleDto.PermissionIds)
                    {
                        bool permissionExists = await _context.Permissions.AnyAsync(p => p.Id == permissionId);
                        if (!permissionExists)
                        {
                            return BadRequest($"Permission with ID {permissionId} does not exist");
                        }
                    }
                }

                // Create new role
                var role = new Role
                {
                    Name = roleDto.Name,
                    Description = roleDto.Description,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    SyncStatus = SyncStatus.NotSynced,
                    SyncId = roleDto.SyncId ?? Guid.NewGuid()
                };

                _context.Roles.Add(role);
                await _context.SaveChangesAsync();

                // Assign permissions
                if (roleDto.PermissionIds != null && roleDto.PermissionIds.Any())
                {
                    foreach (var permissionId in roleDto.PermissionIds)
                    {
                        var rolePermission = new RolePermission
                        {
                            RoleId = role.Id,
                            PermissionId = permissionId,
                            CreatedAt = DateTime.UtcNow,
                            SyncStatus = SyncStatus.NotSynced,
                            SyncId = Guid.NewGuid()
                        };
                        _context.RolePermissions.Add(rolePermission);
                    }
                    await _context.SaveChangesAsync();
                }

                // Reload role with permissions
                role = await _context.Roles
                    .Include(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                    .FirstOrDefaultAsync(r => r.Id == role.Id);

                _logger.LogInformation("Role {RoleName} created with ID {RoleId}", role.Name, role.Id);
                return CreatedAtAction(nameof(GetRole), new { id = role.Id }, role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role");
                return StatusCode(500, "An error occurred while creating role");
            }
        }

        // PUT: api/roles/5
        [HttpPut("{id}")]
        [Authorize(Policy = Permissions.ManageRoles)]
        public async Task<IActionResult> UpdateRole(int id, RoleUpdateDto roleDto)
        {
            try
            {
                var role = await _context.Roles
                    .Include(r => r.RolePermissions)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (role == null)
                {
                    _logger.LogWarning("Role with ID {RoleId} not found", id);
                    return NotFound($"Role with ID {id} not found");
                }

                // Check if role name already exists (if changing)
                if (!string.IsNullOrEmpty(roleDto.Name) && roleDto.Name != role.Name)
                {
                    bool nameExists = await _context.Roles.AnyAsync(r => r.Name == roleDto.Name && r.Id != id);
                    if (nameExists)
                    {
                        return BadRequest("Role name already exists");
                    }
                    role.Name = roleDto.Name;
                }

                // Update description if provided
                if (!string.IsNullOrEmpty(roleDto.Description))
                {
                    role.Description = roleDto.Description;
                }

                role.UpdatedAt = DateTime.UtcNow;
                role.SyncStatus = SyncStatus.NotSynced;

                // Update permissions if provided
                if (roleDto.PermissionIds != null)
                {
                    // Validate permissions
                    foreach (var permissionId in roleDto.PermissionIds)
                    {
                        bool permissionExists = await _context.Permissions.AnyAsync(p => p.Id == permissionId);
                        if (!permissionExists)
                        {
                            return BadRequest($"Permission with ID {permissionId} does not exist");
                        }
                    }

                    // Remove existing permissions
                    _context.RolePermissions.RemoveRange(role.RolePermissions);

                    // Add new permissions
                    foreach (var permissionId in roleDto.PermissionIds)
                    {
                        var rolePermission = new RolePermission
                        {
                            RoleId = role.Id,
                            PermissionId = permissionId,
                            CreatedAt = DateTime.UtcNow,
                            SyncStatus = SyncStatus.NotSynced,
                            SyncId = Guid.NewGuid()
                        };
                        _context.RolePermissions.Add(rolePermission);
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Role with ID {RoleId} updated", id);
                return Ok(new { message = "Role updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role with ID {RoleId}", id);
                return StatusCode(500, $"An error occurred while updating role with ID {id}");
            }
        }

        // DELETE: api/roles/5
        [HttpDelete("{id}")]
        [Authorize(Policy = Permissions.ManageRoles)]
        public async Task<IActionResult> DeleteRole(int id)
        {
            try
            {
                var role = await _context.Roles
                    .Include(r => r.RolePermissions)
                    .Include(r => r.UserRoles)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (role == null)
                {
                    _logger.LogWarning("Role with ID {RoleId} not found", id);
                    return NotFound($"Role with ID {id} not found");
                }

                // Check if role is assigned to any users
                if (role.UserRoles != null && role.UserRoles.Any())
                {
                    return BadRequest("Cannot delete role because it is assigned to users. Remove role from users first.");
                }

                // Remove role permissions
                if (role.RolePermissions != null && role.RolePermissions.Any())
                {
                    _context.RolePermissions.RemoveRange(role.RolePermissions);
                }

                // Remove the role
                _context.Roles.Remove(role);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Role with ID {RoleId} deleted", id);
                return Ok(new { message = "Role deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role with ID {RoleId}", id);
                return StatusCode(500, $"An error occurred while deleting role with ID {id}");
            }
        }

        // POST: api/roles/sync
        [HttpPost("sync")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<IEnumerable<Role>>> SyncRoles(List<RoleSyncDto> roleDtos)
        {
            try
            {
                var result = new List<Role>();

                foreach (var roleDto in roleDtos)
                {
                    try
                    {
                        // Skip invalid roles
                        if (roleDto.SyncId == Guid.Empty)
                        {
                            continue;
                        }

                        // Try to find existing role by SyncId
                        var existingRole = await _context.Roles
                            .Include(r => r.RolePermissions)
                            .FirstOrDefaultAsync(r => r.SyncId == roleDto.SyncId);

                        if (existingRole == null && roleDto.Id > 0)
                        {
                            // Try to find by ID
                            existingRole = await _context.Roles
                                .Include(r => r.RolePermissions)
                                .FirstOrDefaultAsync(r => r.Id == roleDto.Id);
                        }

                        if (existingRole == null)
                        {
                            // Create new role
                            var newRole = new Role
                            {
                                Name = roleDto.Name,
                                Description = roleDto.Description,
                                CreatedAt = roleDto.CreatedAt,
                                UpdatedAt = DateTime.UtcNow,
                                SyncStatus = SyncStatus.Synced,
                                SyncId = roleDto.SyncId
                            };

                            _context.Roles.Add(newRole);
                            await _context.SaveChangesAsync();

                            // Add role permissions
                            if (roleDto.PermissionIds != null && roleDto.PermissionIds.Any())
                            {
                                foreach (var permissionId in roleDto.PermissionIds)
                                {
                                    // Verify permission exists
                                    bool permissionExists = await _context.Permissions.AnyAsync(p => p.Id == permissionId);
                                    if (permissionExists)
                                    {
                                        var rolePermission = new RolePermission
                                        {
                                            RoleId = newRole.Id,
                                            PermissionId = permissionId,
                                            CreatedAt = DateTime.UtcNow,
                                            SyncStatus = SyncStatus.Synced,
                                            SyncId = Guid.NewGuid()
                                        };
                                        _context.RolePermissions.Add(rolePermission);
                                    }
                                }
                                await _context.SaveChangesAsync();
                            }

                            result.Add(newRole);
                        }
                        else
                        {
                            // Update existing role
                            existingRole.Name = roleDto.Name;
                            existingRole.Description = roleDto.Description;
                            existingRole.UpdatedAt = DateTime.UtcNow;
                            existingRole.SyncStatus = SyncStatus.Synced;

                            // Update role permissions
                            if (roleDto.PermissionIds != null)
                            {
                                // Remove existing permissions
                                _context.RolePermissions.RemoveRange(existingRole.RolePermissions);

                                // Add new permissions
                                foreach (var permissionId in roleDto.PermissionIds)
                                {
                                    // Verify permission exists
                                    bool permissionExists = await _context.Permissions.AnyAsync(p => p.Id == permissionId);
                                    if (permissionExists)
                                    {
                                        var rolePermission = new RolePermission
                                        {
                                            RoleId = existingRole.Id,
                                            PermissionId = permissionId,
                                            CreatedAt = DateTime.UtcNow,
                                            SyncStatus = SyncStatus.Synced,
                                            SyncId = Guid.NewGuid()
                                        };
                                        _context.RolePermissions.Add(rolePermission);
                                    }
                                }
                            }

                            await _context.SaveChangesAsync();
                            result.Add(existingRole);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other roles
                        _logger.LogError(ex, "Error syncing role with ID {RoleId} and SyncId {SyncId}",
                            roleDto.Id, roleDto.SyncId);
                    }
                }

                _logger.LogInformation("Synced {RoleCount} roles", result.Count);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing roles");
                return StatusCode(500, "An error occurred while syncing roles");
            }
        }

        // GET: api/roles/pending-sync
        [HttpGet("pending-sync")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<IEnumerable<Role>>> GetPendingSyncRoles()
        {
            try
            {
                var roles = await _context.Roles
                    .Include(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                    .Where(r => r.SyncStatus == SyncStatus.NotSynced)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {RoleCount} pending sync roles", roles.Count);
                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending sync roles");
                return StatusCode(500, "An error occurred while retrieving pending sync roles");
            }
        }

        // GET: api/roles/with-permissions
        [HttpGet("with-permissions")]
        [Authorize(Policy = Permissions.ManageRoles)]
        public async Task<ActionResult<IEnumerable<object>>> GetRolesWithPermissions()
        {
            try
            {
                var permissions = await _context.Permissions.ToListAsync();
                var roles = await _context.Roles
                    .Include(r => r.RolePermissions)
                    .ToListAsync();

                var result = roles.Select(r => new
                {
                    Role = new
                    {
                        r.Id,
                        r.Name,
                        r.Description,
                        r.CreatedAt,
                        r.UpdatedAt
                    },
                    Permissions = r.RolePermissions
                        .Select(rp => permissions.FirstOrDefault(p => p.Id == rp.PermissionId))
                        .Where(p => p != null)
                        .Select(p => new
                        {
                            p.Id,
                            p.Name,
                            p.Description
                        })
                        .ToList()
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving roles with permissions");
                return StatusCode(500, "An error occurred while retrieving roles with permissions");
            }
        }
    }

    // DTOs for Roles
    public class RoleFilterDto
    {
        public string SearchTerm { get; set; }
        public int? PermissionId { get; set; }
        public int PageSize { get; set; } = 50;
        public int PageNumber { get; set; } = 1;
    }

    public class RoleCreateDto
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Name { get; set; }

        public string Description { get; set; }

        [Required]
        public List<int> PermissionIds { get; set; }

        public Guid? SyncId { get; set; }
    }

    public class RoleUpdateDto
    {
        [StringLength(50, MinimumLength = 3)]
        public string Name { get; set; }

        public string Description { get; set; }

        public List<int> PermissionIds { get; set; }
    }

    public class RoleSyncDto
    {
        public int Id { get; set; }
        
        [Required]
        public Guid SyncId { get; set; }
        
        [Required]
        public string Name { get; set; }
        
        public string Description { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public List<int> PermissionIds { get; set; }
    }
}
