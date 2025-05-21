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
    public class PromotionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PromotionsController> _logger;

        public PromotionsController(ApplicationDbContext context, ILogger<PromotionsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/promotions
        [HttpGet]
        [Authorize(Policy = Permissions.ViewPromotions)]
        public async Task<ActionResult<IEnumerable<Promotion>>> GetPromotions([FromQuery] PromotionFilterDto filter)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var query = _context.Promotions
                    .Include(p => p.Product)
                    .Where(p => p.StoreId == storeId.Value)
                    .AsQueryable();

                // Apply filters
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.SearchTerm))
                    {
                        query = query.Where(p =>
                            p.Name.Contains(filter.SearchTerm) ||
                            p.Description.Contains(filter.SearchTerm) ||
                            p.Product.Name.Contains(filter.SearchTerm));
                    }

                    if (filter.ProductId.HasValue)
                    {
                        query = query.Where(p => p.ProductId == filter.ProductId.Value);
                    }

                    if (filter.IsActive.HasValue)
                    {
                        query = query.Where(p => p.IsActive == filter.IsActive.Value);
                    }

                    if (filter.DiscountType.HasValue)
                    {
                        query = query.Where(p => p.DiscountType == filter.DiscountType.Value);
                    }

                    if (filter.StartDateFrom.HasValue)
                    {
                        query = query.Where(p => p.StartDate >= filter.StartDateFrom.Value);
                    }

                    if (filter.StartDateTo.HasValue)
                    {
                        query = query.Where(p => p.StartDate <= filter.StartDateTo.Value);
                    }

                    if (filter.EndDateFrom.HasValue)
                    {
                        query = query.Where(p => p.EndDate >= filter.EndDateFrom.Value);
                    }

                    if (filter.EndDateTo.HasValue)
                    {
                        query = query.Where(p => p.EndDate <= filter.EndDateTo.Value);
                    }

                    if (filter.CurrentlyActive)
                    {
                        var today = DateTime.UtcNow.Date;
                        query = query.Where(p => 
                            p.IsActive && 
                            p.StartDate <= today && 
                            p.EndDate >= today);
                    }
                }

                // Apply pagination
                var pageSize = filter?.PageSize ?? 50;
                var pageNumber = filter?.PageNumber ?? 1;

                var promotions = await query
                    .OrderByDescending(p => p.IsActive)
                    .ThenByDescending(p => p.StartDate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {PromotionCount} promotions for store {StoreId}", 
                    promotions.Count, storeId.Value);
                return Ok(promotions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving promotions");
                return StatusCode(500, "An error occurred while retrieving promotions");
            }
        }

        // GET: api/promotions/active
        [HttpGet("active")]
        [Authorize(Policy = Permissions.ViewPromotions)]
        public async Task<ActionResult<IEnumerable<Promotion>>> GetActivePromotions()
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var today = DateTime.UtcNow.Date;
                var promotions = await _context.Promotions
                    .Include(p => p.Product)
                    .Where(p => 
                        p.StoreId == storeId.Value && 
                        p.IsActive && 
                        p.StartDate <= today && 
                        p.EndDate >= today)
                    .OrderBy(p => p.Product.Name)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {PromotionCount} active promotions for store {StoreId}", 
                    promotions.Count, storeId.Value);
                return Ok(promotions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active promotions");
                return StatusCode(500, "An error occurred while retrieving active promotions");
            }
        }

        // GET: api/promotions/5
        [HttpGet("{id}")]
        [Authorize(Policy = Permissions.ViewPromotions)]
        public async Task<ActionResult<Promotion>> GetPromotion(int id)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var promotion = await _context.Promotions
                    .Include(p => p.Product)
                    .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId.Value);

                if (promotion == null)
                {
                    _logger.LogWarning("Promotion with ID {PromotionId} not found for store {StoreId}", 
                        id, storeId.Value);
                    return NotFound($"Promotion with ID {id} not found");
                }

                return Ok(promotion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving promotion with ID {PromotionId}", id);
                return StatusCode(500, $"An error occurred while retrieving promotion with ID {id}");
            }
        }

        // GET: api/promotions/product/5
        [HttpGet("product/{productId}")]
        [Authorize(Policy = Permissions.ViewPromotions)]
        public async Task<ActionResult<IEnumerable<Promotion>>> GetPromotionsByProduct(int productId)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                // Check if product exists and belongs to the user's store
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == productId && p.StoreId == storeId.Value);
                
                if (product == null)
                {
                    return NotFound($"Product with ID {productId} not found");
                }

                var promotions = await _context.Promotions
                    .Where(p => p.ProductId == productId && p.StoreId == storeId.Value)
                    .OrderByDescending(p => p.IsActive)
                    .ThenByDescending(p => p.StartDate)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {PromotionCount} promotions for product {ProductId} in store {StoreId}", 
                    promotions.Count, productId, storeId.Value);
                return Ok(promotions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving promotions for product {ProductId}", productId);
                return StatusCode(500, $"An error occurred while retrieving promotions for product with ID {productId}");
            }
        }

        // POST: api/promotions
        [HttpPost]
        [Authorize(Policy = Permissions.CreatePromotions)]
        public async Task<ActionResult<Promotion>> CreatePromotion(PromotionCreateDto promotionDto)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                // Validate dates
                if (promotionDto.StartDate > promotionDto.EndDate)
                {
                    return BadRequest("Start date must be earlier than or equal to end date");
                }

                // Check if product exists and belongs to the user's store
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == promotionDto.ProductId && p.StoreId == storeId.Value);
                
                if (product == null)
                {
                    return NotFound($"Product with ID {promotionDto.ProductId} not found or doesn't belong to your store");
                }

                // Validate discount value based on type
                if (promotionDto.DiscountType == DiscountType.Percentage && (promotionDto.DiscountValue <= 0 || promotionDto.DiscountValue > 100))
                {
                    return BadRequest("Percentage discount must be between 0 and 100");
                }
                else if (promotionDto.DiscountType == DiscountType.FixedAmount && promotionDto.DiscountValue <= 0)
                {
                    return BadRequest("Fixed amount discount must be greater than 0");
                }

                // Create new promotion
                var promotion = new Promotion
                {
                    Name = promotionDto.Name,
                    Description = promotionDto.Description,
                    StoreId = storeId.Value,
                    ProductId = promotionDto.ProductId,
                    StartDate = promotionDto.StartDate,
                    EndDate = promotionDto.EndDate,
                    DiscountType = promotionDto.DiscountType,
                    DiscountValue = promotionDto.DiscountValue,
                    MinimumPurchaseAmount = promotionDto.MinimumPurchaseAmount,
                    IsActive = promotionDto.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    SyncStatus = SyncStatus.NotSynced,
                    SyncId = promotionDto.SyncId ?? Guid.NewGuid()
                };

                _context.Promotions.Add(promotion);
                await _context.SaveChangesAsync();

                // Reload promotion with related product
                promotion = await _context.Promotions
                    .Include(p => p.Product)
                    .FirstOrDefaultAsync(p => p.Id == promotion.Id);

                _logger.LogInformation("Promotion {PromotionName} created with ID {PromotionId} for store {StoreId}", 
                    promotion.Name, promotion.Id, storeId.Value);
                return CreatedAtAction(nameof(GetPromotion), new { id = promotion.Id }, promotion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating promotion");
                return StatusCode(500, "An error occurred while creating promotion");
            }
        }

        // PUT: api/promotions/5
        [HttpPut("{id}")]
        [Authorize(Policy = Permissions.EditPromotions)]
        public async Task<IActionResult> UpdatePromotion(int id, PromotionUpdateDto promotionDto)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var promotion = await _context.Promotions
                    .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId.Value);

                if (promotion == null)
                {
                    _logger.LogWarning("Promotion with ID {PromotionId} not found for store {StoreId}", 
                        id, storeId.Value);
                    return NotFound($"Promotion with ID {id} not found");
                }

                // If updating product, check if it exists and belongs to the user's store
                if (promotionDto.ProductId.HasValue)
                {
                    var product = await _context.Products
                        .FirstOrDefaultAsync(p => p.Id == promotionDto.ProductId.Value && p.StoreId == storeId.Value);
                    
                    if (product == null)
                    {
                        return NotFound($"Product with ID {promotionDto.ProductId.Value} not found or doesn't belong to your store");
                    }
                    
                    promotion.ProductId = promotionDto.ProductId.Value;
                }

                // Update dates if provided
                if (promotionDto.StartDate.HasValue)
                {
                    promotion.StartDate = promotionDto.StartDate.Value;
                }

                if (promotionDto.EndDate.HasValue)
                {
                    promotion.EndDate = promotionDto.EndDate.Value;
                }

                // Validate dates
                if (promotion.StartDate > promotion.EndDate)
                {
                    return BadRequest("Start date must be earlier than or equal to end date");
                }

                // Update discount if provided
                if (promotionDto.DiscountType.HasValue)
                {
                    promotion.DiscountType = promotionDto.DiscountType.Value;
                }

                if (promotionDto.DiscountValue.HasValue)
                {
                    promotion.DiscountValue = promotionDto.DiscountValue.Value;
                }

                // Validate discount value based on type
                if (promotion.DiscountType == DiscountType.Percentage && (promotion.DiscountValue <= 0 || promotion.DiscountValue > 100))
                {
                    return BadRequest("Percentage discount must be between 0 and 100");
                }
                else if (promotion.DiscountType == DiscountType.FixedAmount && promotion.DiscountValue <= 0)
                {
                    return BadRequest("Fixed amount discount must be greater than 0");
                }

                // Update other fields if provided
                if (!string.IsNullOrEmpty(promotionDto.Name))
                {
                    promotion.Name = promotionDto.Name;
                }

                if (!string.IsNullOrEmpty(promotionDto.Description))
                {
                    promotion.Description = promotionDto.Description;
                }

                if (promotionDto.MinimumPurchaseAmount.HasValue)
                {
                    promotion.MinimumPurchaseAmount = promotionDto.MinimumPurchaseAmount;
                }

                if (promotionDto.IsActive.HasValue)
                {
                    promotion.IsActive = promotionDto.IsActive.Value;
                }

                promotion.UpdatedAt = DateTime.UtcNow;
                promotion.SyncStatus = SyncStatus.NotSynced;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Promotion with ID {PromotionId} updated for store {StoreId}", 
                    id, storeId.Value);
                return Ok(new { message = "Promotion updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating promotion with ID {PromotionId}", id);
                return StatusCode(500, $"An error occurred while updating promotion with ID {id}");
            }
        }

        // PATCH: api/promotions/5/toggle-active
        [HttpPatch("{id}/toggle-active")]
        [Authorize(Policy = Permissions.EditPromotions)]
        public async Task<IActionResult> TogglePromotionActive(int id)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var promotion = await _context.Promotions
                    .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId.Value);

                if (promotion == null)
                {
                    _logger.LogWarning("Promotion with ID {PromotionId} not found for store {StoreId}", 
                        id, storeId.Value);
                    return NotFound($"Promotion with ID {id} not found");
                }

                // Toggle the active status
                promotion.IsActive = !promotion.IsActive;
                promotion.UpdatedAt = DateTime.UtcNow;
                promotion.SyncStatus = SyncStatus.NotSynced;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Promotion with ID {PromotionId} {Status} for store {StoreId}", 
                    id, promotion.IsActive ? "activated" : "deactivated", storeId.Value);
                return Ok(new { 
                    message = $"Promotion {(promotion.IsActive ? "activated" : "deactivated")} successfully",
                    isActive = promotion.IsActive 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling promotion active state with ID {PromotionId}", id);
                return StatusCode(500, $"An error occurred while toggling promotion active state with ID {id}");
            }
        }

        // DELETE: api/promotions/5
        [HttpDelete("{id}")]
        [Authorize(Policy = Permissions.DeletePromotions)]
        public async Task<IActionResult> DeletePromotion(int id)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var promotion = await _context.Promotions
                    .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId.Value);

                if (promotion == null)
                {
                    _logger.LogWarning("Promotion with ID {PromotionId} not found for store {StoreId}", 
                        id, storeId.Value);
                    return NotFound($"Promotion with ID {id} not found");
                }

                _context.Promotions.Remove(promotion);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Promotion with ID {PromotionId} deleted for store {StoreId}", 
                    id, storeId.Value);
                return Ok(new { message = "Promotion deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting promotion with ID {PromotionId}", id);
                return StatusCode(500, $"An error occurred while deleting promotion with ID {id}");
            }
        }

        // POST: api/promotions/sync
        [HttpPost("sync")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<IEnumerable<Promotion>>> SyncPromotions(List<PromotionSyncDto> promotionDtos)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var result = new List<Promotion>();

                foreach (var promotionDto in promotionDtos)
                {
                    try
                    {
                        // Skip invalid promotions
                        if (promotionDto.SyncId == Guid.Empty)
                        {
                            continue;
                        }

                        // Make sure the promotion is for this store
                        if (promotionDto.StoreId != storeId.Value)
                        {
                            continue;
                        }

                        // Check if product exists and belongs to the user's store
                        var productExists = await _context.Products.AnyAsync(p => 
                            p.Id == promotionDto.ProductId && p.StoreId == storeId.Value);
                        
                        if (!productExists)
                        {
                            _logger.LogWarning("Cannot sync promotion: Product with ID {ProductId} not found or doesn't belong to store {StoreId}", 
                                promotionDto.ProductId, storeId.Value);
                            continue;
                        }

                        // Try to find existing promotion by SyncId
                        var existingPromotion = await _context.Promotions
                            .FirstOrDefaultAsync(p => p.SyncId == promotionDto.SyncId && p.StoreId == storeId.Value);

                        if (existingPromotion == null && promotionDto.Id > 0)
                        {
                            // Try to find by ID
                            existingPromotion = await _context.Promotions
                                .FirstOrDefaultAsync(p => p.Id == promotionDto.Id && p.StoreId == storeId.Value);
                        }

                        if (existingPromotion == null)
                        {
                            // Create new promotion
                            var newPromotion = new Promotion
                            {
                                Name = promotionDto.Name,
                                Description = promotionDto.Description,
                                StoreId = storeId.Value,
                                ProductId = promotionDto.ProductId,
                                StartDate = promotionDto.StartDate,
                                EndDate = promotionDto.EndDate,
                                DiscountType = promotionDto.DiscountType,
                                DiscountValue = promotionDto.DiscountValue,
                                MinimumPurchaseAmount = promotionDto.MinimumPurchaseAmount,
                                IsActive = promotionDto.IsActive,
                                CreatedAt = promotionDto.CreatedAt,
                                UpdatedAt = DateTime.UtcNow,
                                SyncStatus = SyncStatus.Synced,
                                SyncId = promotionDto.SyncId
                            };

                            _context.Promotions.Add(newPromotion);
                            await _context.SaveChangesAsync();
                            result.Add(newPromotion);
                        }
                        else
                        {
                            // Update existing promotion
                            existingPromotion.Name = promotionDto.Name;
                            existingPromotion.Description = promotionDto.Description;
                            existingPromotion.ProductId = promotionDto.ProductId;
                            existingPromotion.StartDate = promotionDto.StartDate;
                            existingPromotion.EndDate = promotionDto.EndDate;
                            existingPromotion.DiscountType = promotionDto.DiscountType;
                            existingPromotion.DiscountValue = promotionDto.DiscountValue;
                            existingPromotion.MinimumPurchaseAmount = promotionDto.MinimumPurchaseAmount;
                            existingPromotion.IsActive = promotionDto.IsActive;
                            existingPromotion.UpdatedAt = DateTime.UtcNow;
                            existingPromotion.SyncStatus = SyncStatus.Synced;

                            await _context.SaveChangesAsync();
                            result.Add(existingPromotion);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other promotions
                        _logger.LogError(ex, "Error syncing promotion with ID {PromotionId} and SyncId {SyncId}",
                            promotionDto.Id, promotionDto.SyncId);
                    }
                }

                _logger.LogInformation("Synced {PromotionCount} promotions for store {StoreId}", 
                    result.Count, storeId.Value);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing promotions");
                return StatusCode(500, "An error occurred while syncing promotions");
            }
        }

        // GET: api/promotions/pending-sync
        [HttpGet("pending-sync")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<IEnumerable<Promotion>>> GetPendingSyncPromotions()
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var promotions = await _context.Promotions
                    .Include(p => p.Product)
                    .Where(p => p.StoreId == storeId.Value && p.SyncStatus == SyncStatus.NotSynced)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {PromotionCount} pending sync promotions for store {StoreId}", 
                    promotions.Count, storeId.Value);
                return Ok(promotions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending sync promotions");
                return StatusCode(500, "An error occurred while retrieving pending sync promotions");
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

    // DTOs for Promotions
    public class PromotionFilterDto
    {
        public string SearchTerm { get; set; }
        public int? ProductId { get; set; }
        public bool? IsActive { get; set; }
        public DiscountType? DiscountType { get; set; }
        public DateTime? StartDateFrom { get; set; }
        public DateTime? StartDateTo { get; set; }
        public DateTime? EndDateFrom { get; set; }
        public DateTime? EndDateTo { get; set; }
        public bool CurrentlyActive { get; set; }
        public int PageSize { get; set; } = 50;
        public int PageNumber { get; set; } = 1;
    }

    public class PromotionCreateDto
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; }

        public string Description { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public DiscountType DiscountType { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal DiscountValue { get; set; }

        public decimal? MinimumPurchaseAmount { get; set; }

        public bool IsActive { get; set; } = true;

        public Guid? SyncId { get; set; }
    }

    public class PromotionUpdateDto
    {
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; }

        public string Description { get; set; }
        public int? ProductId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DiscountType? DiscountType { get; set; }
        
        [Range(0.01, double.MaxValue)]
        public decimal? DiscountValue { get; set; }
        
        public decimal? MinimumPurchaseAmount { get; set; }
        public bool? IsActive { get; set; }
    }

    public class PromotionSyncDto
    {
        public int Id { get; set; }
        
        [Required]
        public Guid SyncId { get; set; }
        
        [Required]
        public string Name { get; set; }
        
        public string Description { get; set; }
        
        [Required]
        public int StoreId { get; set; }
        
        [Required]
        public int ProductId { get; set; }
        
        [Required]
        public DateTime StartDate { get; set; }
        
        [Required]
        public DateTime EndDate { get; set; }
        
        [Required]
        public DiscountType DiscountType { get; set; }
        
        [Required]
        public decimal DiscountValue { get; set; }
        
        public decimal? MinimumPurchaseAmount { get; set; }
        
        public bool IsActive { get; set; }
        
        public DateTime CreatedAt { get; set; }
    }
}
