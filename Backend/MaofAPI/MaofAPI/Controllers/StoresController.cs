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
using System.Security.Claims;

namespace MaofAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StoresController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StoresController> _logger;

        public StoresController(ApplicationDbContext context, ILogger<StoresController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/stores
        [HttpGet]
        [Authorize(Policy = Permissions.ManageAllStores)]
        public async Task<ActionResult<IEnumerable<Store>>> GetStores([FromQuery] StoreFilterDto filter)
        {
            try
            {
                var query = _context.Stores.AsQueryable();

                // Apply filters
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.SearchTerm))
                    {
                        query = query.Where(s =>
                            s.Name.Contains(filter.SearchTerm) ||
                            s.Address.Contains(filter.SearchTerm) ||
                            s.Phone.Contains(filter.SearchTerm) ||
                            s.Email.Contains(filter.SearchTerm) ||
                            s.TaxNumber.Contains(filter.SearchTerm));
                    }

                    if (filter.IsActive.HasValue)
                    {
                        query = query.Where(s => s.IsActive == filter.IsActive.Value);
                    }
                }

                // Apply pagination
                var pageSize = filter?.PageSize ?? 50;
                var pageNumber = filter?.PageNumber ?? 1;

                var stores = await query
                    .OrderBy(s => s.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {StoreCount} stores", stores.Count);
                return Ok(stores);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving stores");
                return StatusCode(500, "An error occurred while retrieving stores");
            }
        }

        // GET: api/stores/my-store
        [HttpGet("my-store")]
        [Authorize]
        public async Task<ActionResult<Store>> GetMyStore()
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return NotFound("User is not associated with any store");
                }

                var store = await _context.Stores
                    .FirstOrDefaultAsync(s => s.Id == storeId.Value);

                if (store == null)
                {
                    _logger.LogWarning("Store with ID {StoreId} not found", storeId.Value);
                    return NotFound($"Store with ID {storeId.Value} not found");
                }

                return Ok(store);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user's store");
                return StatusCode(500, "An error occurred while retrieving user's store");
            }
        }

        // GET: api/stores/5
        [HttpGet("{id}")]
        [Authorize(Policy = Permissions.ManageAllStores)]
        public async Task<ActionResult<Store>> GetStore(int id)
        {
            try
            {
                var store = await _context.Stores
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (store == null)
                {
                    _logger.LogWarning("Store with ID {StoreId} not found", id);
                    return NotFound($"Store with ID {id} not found");
                }

                return Ok(store);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving store with ID {StoreId}", id);
                return StatusCode(500, $"An error occurred while retrieving store with ID {id}");
            }
        }

        // GET: api/stores/5/users
        [HttpGet("{id}/users")]
        [Authorize(Policy = Permissions.ManageAllStores)]
        public async Task<ActionResult<IEnumerable<User>>> GetStoreUsers(int id)
        {
            try
            {
                var store = await _context.Stores
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (store == null)
                {
                    _logger.LogWarning("Store with ID {StoreId} not found", id);
                    return NotFound($"Store with ID {id} not found");
                }

                var users = await _context.Users
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                    .Where(u => u.StoreId == id)
                    .ToListAsync();

                // Remove sensitive data
                foreach (var user in users)
                {
                    user.PasswordHash = null;
                }

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users for store ID {StoreId}", id);
                return StatusCode(500, $"An error occurred while retrieving users for store ID {id}");
            }
        }

        // GET: api/stores/5/stats
        [HttpGet("{id}/stats")]
        [Authorize(Policy = Permissions.ManageAllStores)]
        public async Task<ActionResult<object>> GetStoreStats(int id)
        {
            try
            {
                var store = await _context.Stores
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (store == null)
                {
                    _logger.LogWarning("Store with ID {StoreId} not found", id);
                    return NotFound($"Store with ID {id} not found");
                }

                // Get various counts and statistics for the store
                var userCount = await _context.Users.CountAsync(u => u.StoreId == id);
                var productCount = await _context.Products.CountAsync(p => p.StoreId == id);
                var saleCount = await _context.Sales.CountAsync(s => s.StoreId == id);
                
                // Get total sales amount for the store
                var totalSales = await _context.Sales
                    .Where(s => s.StoreId == id)
                    .SumAsync(s => s.GrandTotal);

                // Get recent sales
                var recentSales = await _context.Sales
                    .Where(s => s.StoreId == id)
                    .OrderByDescending(s => s.SaleDate)
                    .Take(5)
                    .ToListAsync();

                return Ok(new
                {
                    StoreName = store.Name,
                    UserCount = userCount,
                    ProductCount = productCount,
                    SaleCount = saleCount,
                    TotalSales = totalSales,
                    RecentSales = recentSales
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving stats for store ID {StoreId}", id);
                return StatusCode(500, $"An error occurred while retrieving stats for store ID {id}");
            }
        }

        // POST: api/stores
        [HttpPost]
        [Authorize(Policy = Permissions.ManageAllStores)]
        public async Task<ActionResult<Store>> CreateStore(StoreCreateDto storeDto)
        {
            try
            {
                // Check if store name already exists
                bool nameExists = await _context.Stores.AnyAsync(s => s.Name == storeDto.Name);
                if (nameExists)
                {
                    return BadRequest("Store name already exists");
                }

                // Create new store
                var store = new Store
                {
                    Name = storeDto.Name,
                    Address = storeDto.Address,
                    Phone = storeDto.Phone,
                    Email = storeDto.Email,
                    TaxNumber = storeDto.TaxNumber,
                    ReceiptHeader = storeDto.ReceiptHeader,
                    ReceiptFooter = storeDto.ReceiptFooter,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    SyncStatus = SyncStatus.NotSynced,
                    SyncId = storeDto.SyncId ?? Guid.NewGuid()
                };

                _context.Stores.Add(store);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Store {StoreName} created with ID {StoreId}", store.Name, store.Id);
                return CreatedAtAction(nameof(GetStore), new { id = store.Id }, store);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating store");
                return StatusCode(500, "An error occurred while creating store");
            }
        }

        // PUT: api/stores/5
        [HttpPut("{id}")]
        [Authorize(Policy = Permissions.ManageAllStores)]
        public async Task<IActionResult> UpdateStore(int id, StoreUpdateDto storeDto)
        {
            try
            {
                var store = await _context.Stores.FindAsync(id);
                if (store == null)
                {
                    _logger.LogWarning("Store with ID {StoreId} not found", id);
                    return NotFound($"Store with ID {id} not found");
                }

                // Check if name already exists (if changing)
                if (!string.IsNullOrEmpty(storeDto.Name) && storeDto.Name != store.Name)
                {
                    bool nameExists = await _context.Stores.AnyAsync(s => s.Name == storeDto.Name && s.Id != id);
                    if (nameExists)
                    {
                        return BadRequest("Store name already exists");
                    }
                    store.Name = storeDto.Name;
                }

                // Update fields if provided
                if (!string.IsNullOrEmpty(storeDto.Address))
                {
                    store.Address = storeDto.Address;
                }

                if (!string.IsNullOrEmpty(storeDto.Phone))
                {
                    store.Phone = storeDto.Phone;
                }

                if (!string.IsNullOrEmpty(storeDto.Email))
                {
                    store.Email = storeDto.Email;
                }

                if (!string.IsNullOrEmpty(storeDto.TaxNumber))
                {
                    store.TaxNumber = storeDto.TaxNumber;
                }

                if (!string.IsNullOrEmpty(storeDto.ReceiptHeader))
                {
                    store.ReceiptHeader = storeDto.ReceiptHeader;
                }

                if (!string.IsNullOrEmpty(storeDto.ReceiptFooter))
                {
                    store.ReceiptFooter = storeDto.ReceiptFooter;
                }

                if (storeDto.IsActive.HasValue)
                {
                    store.IsActive = storeDto.IsActive.Value;
                }

                store.UpdatedAt = DateTime.UtcNow;
                store.SyncStatus = SyncStatus.NotSynced;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Store with ID {StoreId} updated", id);
                return Ok(new { message = "Store updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating store with ID {StoreId}", id);
                return StatusCode(500, $"An error occurred while updating store with ID {id}");
            }
        }

        // PUT: api/stores/my-store
        [HttpPut("my-store")]
        [Authorize(Policy = Permissions.SystemSettings)]
        public async Task<IActionResult> UpdateMyStore(StoreUpdateDto storeDto)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return NotFound("User is not associated with any store");
                }

                var store = await _context.Stores.FindAsync(storeId.Value);
                if (store == null)
                {
                    _logger.LogWarning("Store with ID {StoreId} not found", storeId.Value);
                    return NotFound($"Store with ID {storeId.Value} not found");
                }

                // Update fields if provided
                if (!string.IsNullOrEmpty(storeDto.Name))
                {
                    store.Name = storeDto.Name;
                }

                if (!string.IsNullOrEmpty(storeDto.Address))
                {
                    store.Address = storeDto.Address;
                }

                if (!string.IsNullOrEmpty(storeDto.Phone))
                {
                    store.Phone = storeDto.Phone;
                }

                if (!string.IsNullOrEmpty(storeDto.Email))
                {
                    store.Email = storeDto.Email;
                }

                if (!string.IsNullOrEmpty(storeDto.TaxNumber))
                {
                    store.TaxNumber = storeDto.TaxNumber;
                }

                if (!string.IsNullOrEmpty(storeDto.ReceiptHeader))
                {
                    store.ReceiptHeader = storeDto.ReceiptHeader;
                }

                if (!string.IsNullOrEmpty(storeDto.ReceiptFooter))
                {
                    store.ReceiptFooter = storeDto.ReceiptFooter;
                }

                // Store managers can't activate/deactivate their own store
                
                store.UpdatedAt = DateTime.UtcNow;
                store.SyncStatus = SyncStatus.NotSynced;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Store with ID {StoreId} updated by store manager", storeId.Value);
                return Ok(new { message = "Store updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating store by store manager");
                return StatusCode(500, "An error occurred while updating store");
            }
        }

        // DELETE: api/stores/5
        [HttpDelete("{id}")]
        [Authorize(Policy = Permissions.ManageAllStores)]
        public async Task<IActionResult> DeleteStore(int id)
        {
            try
            {
                var store = await _context.Stores
                    .Include(s => s.Users)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (store == null)
                {
                    _logger.LogWarning("Store with ID {StoreId} not found", id);
                    return NotFound($"Store with ID {id} not found");
                }

                // Check if store has users
                if (store.Users != null && store.Users.Any())
                {
                    return BadRequest("Cannot delete store because it has associated users. Remove users first or deactivate the store instead.");
                }

                // Check if store has products (just check count, don't load all products)
                bool hasProducts = await _context.Products.AnyAsync(p => p.StoreId == id);
                if (hasProducts)
                {
                    return BadRequest("Cannot delete store because it has associated products. Remove products first or deactivate the store instead.");
                }

                // Check if store has sales (just check count, don't load all sales)
                bool hasSales = await _context.Sales.AnyAsync(s => s.StoreId == id);
                if (hasSales)
                {
                    return BadRequest("Cannot delete store because it has associated sales. Deactivate the store instead.");
                }

                // Instead of hard deleting, soft delete by setting IsActive to false
                store.IsActive = false;
                store.UpdatedAt = DateTime.UtcNow;
                store.SyncStatus = SyncStatus.NotSynced;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Store with ID {StoreId} deactivated", id);
                return Ok(new { message = "Store deactivated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating store with ID {StoreId}", id);
                return StatusCode(500, $"An error occurred while deactivating store with ID {id}");
            }
        }

        // POST: api/stores/sync
        [HttpPost("sync")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<IEnumerable<Store>>> SyncStores(List<StoreSyncDto> storeDtos)
        {
            try
            {
                // Only system admins can sync stores
                if (!User.HasClaim("permission", Permissions.ManageAllStores))
                {
                    return Forbid("Only system administrators can sync stores");
                }

                var result = new List<Store>();

                foreach (var storeDto in storeDtos)
                {
                    try
                    {
                        // Skip invalid stores
                        if (storeDto.SyncId == Guid.Empty)
                        {
                            continue;
                        }

                        // Try to find existing store by SyncId
                        var existingStore = await _context.Stores
                            .FirstOrDefaultAsync(s => s.SyncId == storeDto.SyncId);

                        if (existingStore == null && storeDto.Id > 0)
                        {
                            // Try to find by ID
                            existingStore = await _context.Stores
                                .FirstOrDefaultAsync(s => s.Id == storeDto.Id);
                        }

                        if (existingStore == null)
                        {
                            // Create new store
                            var newStore = new Store
                            {
                                Name = storeDto.Name,
                                Address = storeDto.Address,
                                Phone = storeDto.Phone,
                                Email = storeDto.Email,
                                TaxNumber = storeDto.TaxNumber,
                                ReceiptHeader = storeDto.ReceiptHeader,
                                ReceiptFooter = storeDto.ReceiptFooter,
                                IsActive = storeDto.IsActive,
                                CreatedAt = storeDto.CreatedAt,
                                UpdatedAt = DateTime.UtcNow,
                                SyncStatus = SyncStatus.Synced,
                                SyncId = storeDto.SyncId
                            };

                            _context.Stores.Add(newStore);
                            await _context.SaveChangesAsync();
                            result.Add(newStore);
                        }
                        else
                        {
                            // Update existing store
                            existingStore.Name = storeDto.Name;
                            existingStore.Address = storeDto.Address;
                            existingStore.Phone = storeDto.Phone;
                            existingStore.Email = storeDto.Email;
                            existingStore.TaxNumber = storeDto.TaxNumber;
                            existingStore.ReceiptHeader = storeDto.ReceiptHeader;
                            existingStore.ReceiptFooter = storeDto.ReceiptFooter;
                            existingStore.IsActive = storeDto.IsActive;
                            existingStore.UpdatedAt = DateTime.UtcNow;
                            existingStore.SyncStatus = SyncStatus.Synced;

                            await _context.SaveChangesAsync();
                            result.Add(existingStore);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other stores
                        _logger.LogError(ex, "Error syncing store with ID {StoreId} and SyncId {SyncId}",
                            storeDto.Id, storeDto.SyncId);
                    }
                }

                _logger.LogInformation("Synced {StoreCount} stores", result.Count);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing stores");
                return StatusCode(500, "An error occurred while syncing stores");
            }
        }

        // GET: api/stores/pending-sync
        [HttpGet("pending-sync")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<IEnumerable<Store>>> GetPendingSyncStores()
        {
            try
            {
                // Only system admins can get pending sync stores
                if (!User.HasClaim("permission", Permissions.ManageAllStores))
                {
                    return Forbid("Only system administrators can view pending sync stores");
                }

                var stores = await _context.Stores
                    .Where(s => s.SyncStatus == SyncStatus.NotSynced)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {StoreCount} pending sync stores", stores.Count);
                return Ok(stores);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending sync stores");
                return StatusCode(500, "An error occurred while retrieving pending sync stores");
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
    }

    // DTOs for Stores
    public class StoreFilterDto
    {
        public string SearchTerm { get; set; }
        public bool? IsActive { get; set; }
        public int PageSize { get; set; } = 50;
        public int PageNumber { get; set; } = 1;
    }

    public class StoreCreateDto
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; }

        public string Address { get; set; }
        public string Phone { get; set; }
        
        [EmailAddress]
        public string Email { get; set; }
        
        public string TaxNumber { get; set; }
        public string ReceiptHeader { get; set; }
        public string ReceiptFooter { get; set; }
        public Guid? SyncId { get; set; }
    }

    public class StoreUpdateDto
    {
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; }

        public string Address { get; set; }
        public string Phone { get; set; }
        
        [EmailAddress]
        public string Email { get; set; }
        
        public string TaxNumber { get; set; }
        public string ReceiptHeader { get; set; }
        public string ReceiptFooter { get; set; }
        public bool? IsActive { get; set; }
    }

    public class StoreSyncDto
    {
        public int Id { get; set; }
        
        [Required]
        public Guid SyncId { get; set; }
        
        [Required]
        public string Name { get; set; }
        
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string TaxNumber { get; set; }
        public string ReceiptHeader { get; set; }
        public string ReceiptFooter { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
