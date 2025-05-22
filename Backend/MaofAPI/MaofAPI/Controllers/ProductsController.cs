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
using System.Security.Claims;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace MaofAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(ApplicationDbContext context, ILogger<ProductsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/products
        [HttpGet]
        [Authorize(Policy = Permissions.ViewProducts)]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts([FromQuery] ProductFilterDto filter)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var query = _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.StoreId == storeId);

                // Apply filters
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.SearchTerm))
                    {
                        query = query.Where(p => 
                            p.Name.Contains(filter.SearchTerm) || 
                            p.Barcode.Contains(filter.SearchTerm) || 
                            p.Description.Contains(filter.SearchTerm));
                    }

                    if (filter.CategoryId.HasValue)
                    {
                        query = query.Where(p => p.CategoryId == filter.CategoryId);
                    }

                    if (filter.OnlyActive)
                    {
                        query = query.Where(p => p.IsActive);
                    }
                }

                // Apply pagination
                var pageSize = filter?.PageSize ?? 50;
                var pageNumber = filter?.PageNumber ?? 1;

                var products = await query
                    .OrderBy(p => p.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {ProductCount} products for store {StoreId}", products.Count, storeId);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products for store {StoreId}", GetUserStoreId());
                return StatusCode(500, "An error occurred while retrieving products");
            }
        }

        // GET: api/products/5
        [HttpGet("{id}")]
        [Authorize(Policy = Permissions.ViewProducts)]
        public async Task<ActionResult<Product>> GetProduct(int id)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var product = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Store)
                    .Include(p => p.ProductTransactions)
                    .Include(p => p.Promotions)
                    .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId);

                if (product == null)
                {
                    _logger.LogWarning("Product with ID {ProductId} not found in store {StoreId}", id, storeId);
                    return NotFound($"Product with ID {id} not found");
                }

                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product {ProductId} for store {StoreId}", id, GetUserStoreId());
                return StatusCode(500, $"An error occurred while retrieving product with ID {id}");
            }
        }

        // GET: api/products/barcode/{barcode}
        [HttpGet("barcode/{barcode}")]
        [Authorize(Policy = Permissions.ViewProducts)]
        public async Task<ActionResult<Product>> GetProductByBarcode(string barcode)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var product = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Store)
                    .Include(p => p.ProductTransactions)
                    .Include(p => p.Promotions)
                    .FirstOrDefaultAsync(p => p.Barcode == barcode && p.StoreId == storeId);

                if (product == null)
                {
                    _logger.LogWarning("Product with barcode {Barcode} not found in store {StoreId}", barcode, storeId);
                    return NotFound($"Product with barcode {barcode} not found");
                }

                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product with barcode {Barcode} for store {StoreId}", barcode, GetUserStoreId());
                return StatusCode(500, $"An error occurred while retrieving product with barcode {barcode}");
            }
        }

        // POST: api/products
        [HttpPost]
        [Authorize(Policy = Permissions.CreateProducts)]
        public async Task<ActionResult<Product>> CreateProduct(ProductCreateDto productDto)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(productDto.Name))
                {
                    return BadRequest("Product name is required");
                }

                if (productDto.CategoryId <= 0)
                {
                    return BadRequest("Category ID must be greater than zero");
                }

                // Check if category exists and is active
                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Id == productDto.CategoryId && c.IsActive);
                
                if (category == null)
                {
                    return BadRequest($"Active category with ID {productDto.CategoryId} not found");
                }

                // Check if store exists and is active
                var store = await _context.Stores
                    .FirstOrDefaultAsync(s => s.Id == storeId && s.IsActive);
                
                if (store == null)
                {
                    return BadRequest("Invalid or inactive store");
                }

                // Check if barcode is unique within the store
                if (!string.IsNullOrWhiteSpace(productDto.Barcode))
                {
                    bool duplicateBarcode = await _context.Products
                        .AnyAsync(p => p.Barcode == productDto.Barcode.Trim() && p.StoreId == storeId);

                    if (duplicateBarcode)
                    {
                        return BadRequest("A product with this barcode already exists in your store");
                    }
                }


                // Validate prices and quantities
                if (productDto.PurchasePrice < 0)
                {
                    return BadRequest("Purchase price cannot be negative");
                }

                if (productDto.SalesPrice < 0)
                {
                    return BadRequest("Sales price cannot be negative");
                }

                if (productDto.TaxRate < 0 || productDto.TaxRate > 1)
                {
                    return BadRequest("Tax rate must be between 0 and 1");
                }

                if (productDto.StockQuantity < 0)
                {
                    return BadRequest("Stock quantity cannot be negative");
                }
                if (productDto.MinimumStockLevel < 0)
                {
                    return BadRequest("Minimum stock level cannot be negative");
                }

                var product = new Product
                {
                    Name = productDto.Name,
                    Barcode = productDto.Barcode,
                    Description = productDto.Description,
                    CategoryId = productDto.CategoryId,
                    StoreId = storeId.Value,
                    PurchasePrice = productDto.PurchasePrice,
                    SalesPrice = productDto.SalesPrice,
                    TaxRate = productDto.TaxRate,
                    StockQuantity = productDto.StockQuantity,
                    IsActive = productDto.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    SyncStatus = SyncStatus.NotSynced,
                    SyncId = productDto.SyncId ?? Guid.NewGuid()
                };

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created product {ProductId} in store {StoreId}", product.Id, storeId);
                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product in store {StoreId}", GetUserStoreId());
                return StatusCode(500, "An error occurred while creating the product");
            }
        }

        // PUT: api/products/5
        [HttpPut("{id}")]
        [Authorize(Policy = Permissions.EditProducts)]
        public async Task<IActionResult> UpdateProduct(int id, ProductUpdateDto productDto)
        {
            int? storeId = GetUserStoreId();
            if (!storeId.HasValue)
            {
                return Forbid("User is not associated with any store");
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId);

            if (product == null)
            {
                _logger.LogWarning("Product with ID {ProductId} not found in store {StoreId} during update", id, storeId);
                return NotFound($"Product with ID {id} not found in your store");
            }

            // Check if category exists
            if (productDto.CategoryId != product.CategoryId)
            {
                var category = await _context.Categories.FindAsync(productDto.CategoryId);
                if (category == null)
                {
                    return BadRequest($"Category with ID {productDto.CategoryId} not found");
                }
            }

            // Check for duplicate barcode if changed
            if (!string.IsNullOrEmpty(productDto.Barcode) && productDto.Barcode != product.Barcode)
            {
                bool duplicateBarcode = await _context.Products
                    .AnyAsync(p => p.Barcode == productDto.Barcode && p.StoreId == storeId && p.Id != id);

                if (duplicateBarcode)
                {
                    return BadRequest("A product with this barcode already exists in your store");
                }
            }

            // Update product properties with null checks and trimming
            product.Name = productDto.Name?.Trim();
            product.Barcode = !string.IsNullOrWhiteSpace(productDto.Barcode) ? productDto.Barcode.Trim() : null;
            product.Description = !string.IsNullOrWhiteSpace(productDto.Description) ? productDto.Description.Trim() : null;
            product.CategoryId = productDto.CategoryId;
            product.PurchasePrice = productDto.PurchasePrice;
            product.SalesPrice = productDto.SalesPrice;
            product.TaxRate = productDto.TaxRate;
            product.StockQuantity = productDto.StockQuantity;
            product.IsActive = productDto.IsActive;
            product.UpdatedAt = DateTime.UtcNow;
            product.SyncStatus = SyncStatus.NotSynced;

            // Ensure stock level consistency
            if (product.StockQuantity < 0)
            {
                product.StockQuantity = 0;
            }

            // Create product transaction for tracking changes
            var productTransaction = new ProductTransaction
            {
                ProductId = product.Id,
                StoreId = storeId.Value,
                TransactionType = ProductTransactionType.ProductUpdated,
                // Track price changes if any
                PriceBefore = productDto.SalesPrice != product.SalesPrice ? product.SalesPrice : null,
                PriceAfter = productDto.SalesPrice != product.SalesPrice ? productDto.SalesPrice : null,
                // Track cost changes if any
                CostBefore = productDto.PurchasePrice != product.PurchasePrice ? product.PurchasePrice : null,
                CostAfter = productDto.PurchasePrice != product.PurchasePrice ? productDto.PurchasePrice : null,
                // Track tax rate changes if any
                TaxRateBefore = productDto.TaxRate != product.TaxRate ? product.TaxRate : null,
                TaxRateAfter = productDto.TaxRate != product.TaxRate ? productDto.TaxRate : null,
                // Track stock changes if any
                QuantityBefore = productDto.StockQuantity != product.StockQuantity ? product.StockQuantity : null,
                QuantityAfter = productDto.StockQuantity != product.StockQuantity ? productDto.StockQuantity : null,
                QuantityChange = productDto.StockQuantity != product.StockQuantity ? productDto.StockQuantity - product.StockQuantity : null,
                // Other details
                Notes = "Product updated via API",
                ReferenceType = "ProductUpdate",
                UserId = GetUserId(),
                TransactionDate = DateTime.UtcNow
            };

            _context.ProductTransactions.Add(productTransaction);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated product {ProductId} in store {StoreId}", id, storeId);
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(id, storeId.Value))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        // DELETE: api/products/5
        [HttpDelete("{id}")]
        [Authorize(Policy = Permissions.DeleteProducts)]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            int? storeId = GetUserStoreId();
            if (!storeId.HasValue)
            {
                return Forbid("User is not associated with any store");
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId);

            if (product == null)
            {
                _logger.LogWarning("Product with ID {ProductId} not found in store {StoreId} during delete", id, storeId);
                return NotFound($"Product with ID {id} not found in your store");
            }

            // Check if product is in use in sales
            bool productInUse = await _context.SaleItems.AnyAsync(si => si.ProductId == id);
            if (productInUse)
            {
                // Soft delete by setting IsActive to false
                product.IsActive = false;
                product.SyncStatus = SyncStatus.NotSynced;
                product.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Soft deleted product {ProductId} in store {StoreId} (in use in sales)", id, storeId);
                return Ok(new { message = "Product was soft deleted as it is used in sales records" });
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Permanently deleted product {ProductId} from store {StoreId}", id, storeId);
            return NoContent();
        }

        // POST: api/products/sync
        [HttpPost("sync")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<List<Product>>> SyncProducts(List<ProductSyncDto> products)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var result = new List<Product>();
                foreach (var productDto in products)
                {
                    try
                    {
                        Product product;
                        
                        if (productDto.Id > 0)
                        {
                            // Update existing product
                            product = await _context.Products.FirstOrDefaultAsync(p => 
                                p.Id == productDto.Id && p.StoreId == storeId);
                                
                            if (product == null)
                            {
                                // Product not found or belongs to another store
                                continue;
                            }
                            
                            // Update properties
                            product.Name = productDto.Name;
                            product.Barcode = productDto.Barcode;
                            product.Description = productDto.Description;
                            product.CategoryId = productDto.CategoryId;
                            product.PurchasePrice = productDto.PurchasePrice;
                            product.SalesPrice = productDto.SalesPrice;
                            product.TaxRate = productDto.TaxRate;
                            product.StockQuantity = productDto.StockQuantity;
                            product.IsActive = productDto.IsActive;
                            product.UpdatedAt = DateTime.UtcNow;
                            product.SyncStatus = SyncStatus.Synced;
                        }
                        else if (productDto.SyncId != Guid.Empty)
                        {
                            // Look for existing product by SyncId
                            product = await _context.Products.FirstOrDefaultAsync(p => 
                                p.SyncId == productDto.SyncId && p.StoreId == storeId);
                                
                            if (product == null)
                            {
                                // Create new product with the provided SyncId
                                product = new Product
                                {
                                    Name = productDto.Name,
                                    Barcode = productDto.Barcode,
                                    Description = productDto.Description,
                                    CategoryId = productDto.CategoryId,
                                    StoreId = storeId.Value,
                                    PurchasePrice = productDto.PurchasePrice,
                                    SalesPrice = productDto.SalesPrice,
                                    TaxRate = productDto.TaxRate,
                                    StockQuantity = productDto.StockQuantity,
                                    IsActive = productDto.IsActive,
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow,
                                    SyncStatus = SyncStatus.Synced,
                                    SyncId = productDto.SyncId
                                };
                                
                                _context.Products.Add(product);
                            }
                            else
                            {
                                // Update existing product found by SyncId
                                product.Name = productDto.Name;
                                product.Barcode = productDto.Barcode;
                                product.Description = productDto.Description;
                                product.CategoryId = productDto.CategoryId;
                                product.PurchasePrice = productDto.PurchasePrice;
                                product.SalesPrice = productDto.SalesPrice;
                                product.TaxRate = productDto.TaxRate;
                                product.StockQuantity = productDto.StockQuantity;
                                product.IsActive = productDto.IsActive;
                                product.UpdatedAt = DateTime.UtcNow;
                                product.SyncStatus = SyncStatus.Synced;
                            }
                        }
                        else
                        {
                            // Skip products without Id or SyncId
                            continue;
                        }
                        
                        result.Add(product);
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other products
                        _logger.LogError(ex, "Error syncing product with ID {ProductId} and SyncId {SyncId}", 
                            productDto.Id, productDto.SyncId);
                    }
                }
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Synced {ProductCount} products for store {StoreId}", result.Count, storeId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing products for store {StoreId}", GetUserStoreId());
                return StatusCode(500, "An error occurred while syncing products");
            }
        }

        // GET: api/products/pending-sync
        [HttpGet("pending-sync")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<IEnumerable<Product>>> GetPendingSyncProducts()
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var products = await _context.Products
                    .Where(p => p.StoreId == storeId && p.SyncStatus == SyncStatus.NotSynced)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {ProductCount} pending sync products for store {StoreId}", products.Count, storeId);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending sync products for store {StoreId}", GetUserStoreId());
                return StatusCode(500, "An error occurred while retrieving pending sync products");
            }
        }

        // GET: api/products/{id}/price-history
        [HttpGet("{id}/price-history")]
        [Authorize(Policy = Permissions.ViewProductHistory)]
        public async Task<ActionResult<IEnumerable<ProductTransaction>>> GetProductPriceHistory(int id)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                // Verify product exists and belongs to the store
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId);

                if (product == null)
                {
                    _logger.LogWarning("Product with ID {ProductId} not found in store {StoreId} while retrieving price history", id, storeId);
                    return NotFound($"Product with ID {id} not found in your store");
                }

                // Get price history transactions
                var priceHistory = await _context.ProductTransactions
                    .Where(pt => pt.ProductId == id && pt.StoreId == storeId && 
                           (pt.PriceBefore.HasValue || pt.PriceAfter.HasValue))
                    .OrderByDescending(pt => pt.TransactionDate)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {TransactionCount} price history records for product {ProductId} in store {StoreId}", 
                    priceHistory.Count, id, storeId);
                return Ok(priceHistory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving price history for product {ProductId} in store {StoreId}", id, GetUserStoreId());
                return StatusCode(500, $"An error occurred while retrieving price history for product with ID {id}");
            }
        }

        // GET: api/products/{id}/stock-history
        [HttpGet("{id}/stock-history")]
        [Authorize(Policy = Permissions.ViewProductHistory)]
        public async Task<ActionResult<IEnumerable<ProductTransaction>>> GetProductStockHistory(int id)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                // Verify product exists and belongs to the store
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId);

                if (product == null)
                {
                    _logger.LogWarning("Product with ID {ProductId} not found in store {StoreId} while retrieving stock history", id, storeId);
                    return NotFound($"Product with ID {id} not found in your store");
                }

                // Get stock history transactions
                var stockHistory = await _context.ProductTransactions
                    .Where(pt => pt.ProductId == id && pt.StoreId == storeId && 
                           (pt.QuantityBefore.HasValue || pt.QuantityAfter.HasValue))
                    .OrderByDescending(pt => pt.TransactionDate)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {TransactionCount} stock history records for product {ProductId} in store {StoreId}", 
                    stockHistory.Count, id, storeId);
                return Ok(stockHistory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving stock history for product {ProductId} in store {StoreId}", id, GetUserStoreId());
                return StatusCode(500, $"An error occurred while retrieving stock history for product with ID {id}");
            }
        }

        // POST: api/products/update-stock
        [HttpPost("update-stock")]
        [Authorize(Policy = Permissions.ManageStock)]
        public async Task<IActionResult> UpdateStock(StockUpdateDto stockUpdate)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == stockUpdate.ProductId && p.StoreId == storeId);

                if (product == null)
                {
                    _logger.LogWarning("Product with ID {ProductId} not found in store {StoreId} during stock update", stockUpdate.ProductId, storeId);
                    return NotFound($"Product with ID {stockUpdate.ProductId} not found in your store");
                }

                // Update stock quantity
                product.StockQuantity = stockUpdate.NewQuantity;
                product.UpdatedAt = DateTime.UtcNow;
                product.SyncStatus = SyncStatus.NotSynced;

                // Create product transaction
                var productTransaction = new ProductTransaction
                {
                    ProductId = product.Id,
                    StoreId = storeId.Value,
                    TransactionType = stockUpdate.NewQuantity > stockUpdate.OldQuantity 
                        ? ProductTransactionType.StockIn 
                        : ProductTransactionType.StockOut,
                    QuantityBefore = stockUpdate.OldQuantity,
                    QuantityAfter = stockUpdate.NewQuantity,
                    QuantityChange = stockUpdate.NewQuantity - stockUpdate.OldQuantity,
                    Notes = stockUpdate.Notes,
                    ReferenceType = "Manual",
                    UserId = GetUserId(),
                    TransactionDate = DateTime.UtcNow
                };

                _context.ProductTransactions.Add(productTransaction);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated stock for product {ProductId} in store {StoreId} from {OldQuantity} to {NewQuantity}", 
                    stockUpdate.ProductId, storeId, stockUpdate.OldQuantity, stockUpdate.NewQuantity);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock for product {ProductId} in store {StoreId}", stockUpdate.ProductId, GetUserStoreId());
                return StatusCode(500, $"An error occurred while updating stock for product with ID {stockUpdate.ProductId}");
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
            return 0;
        }

        private bool ProductExists(int id, int storeId)
        {
            return _context.Products.Any(p => p.Id == id && p.StoreId == storeId);
        }
    }

    // DTOs
    public class ProductFilterDto
    {
        [StringLength(100)]
        public string SearchTerm { get; set; }

        public int? CategoryId { get; set; }

        public bool OnlyActive { get; set; } = true;

        public bool OnlyLowStock { get; set; } = false;

        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;

        [Range(5, 200)]
        public int PageSize { get; set; } = 50;
    }

    public class ProductCreateDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; }

        [StringLength(30)]
        public string Barcode { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int CategoryId { get; set; }

        [Required]
        [Range(0, 99999.99)]
        public decimal PurchasePrice { get; set; }

        [Required]
        [Range(0, 99999.99)]
        public decimal SalesPrice { get; set; }

        [Required]
        [Range(0, 1)]
        public decimal TaxRate { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int MinimumStockLevel { get; set; }

        [StringLength(500)]
        [Url]
        public string ImageUrl { get; set; }

        public bool IsActive { get; set; } = true;

        public Guid? SyncId { get; set; } // Optional for offline-first functionality
    }

    public class ProductUpdateDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; }

        [StringLength(30)]
        public string Barcode { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int CategoryId { get; set; }

        [Required]
        [Range(0, 99999.99)]
        public decimal PurchasePrice { get; set; }

        [Required]
        [Range(0, 99999.99)]
        public decimal SalesPrice { get; set; }

        [Required]
        [Range(0, 1)]
        public decimal TaxRate { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int MinimumStockLevel { get; set; }

        [StringLength(500)]
        [Url]
        public string ImageUrl { get; set; }

        public bool IsActive { get; set; }
    }

    public class ProductSyncDto
    {
        public int Id { get; set; }

        [Required]
        public Guid SyncId { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; }

        [StringLength(30)]
        public string Barcode { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int CategoryId { get; set; }

        [Required]
        [Range(0, 99999.99)]
        public decimal PurchasePrice { get; set; }

        [Required]
        [Range(0, 99999.99)]
        public decimal SalesPrice { get; set; }

        [Required]
        [Range(0, 1)]
        public decimal TaxRate { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int MinimumStockLevel { get; set; }

        [StringLength(500)]
        [Url]
        public string ImageUrl { get; set; }

        public bool IsActive { get; set; }
    }

    public class StockUpdateDto
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int ProductId { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int OldQuantity { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int NewQuantity { get; set; }

        [StringLength(200)]
        public string Notes { get; set; }
    }
}
