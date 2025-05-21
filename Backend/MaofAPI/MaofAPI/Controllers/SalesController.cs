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
using Microsoft.Extensions.Logging;

namespace MaofAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SalesController> _logger;

        public SalesController(ApplicationDbContext context, ILogger<SalesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/sales
        [HttpGet]
        [Authorize(Policy = Permissions.ViewSales)]
        public async Task<ActionResult<IEnumerable<Sale>>> GetSales([FromQuery] SaleFilterDto filter)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var query = _context.Sales
                    .Include(s => s.User)
                    .Include(s => s.Currency)
                    .Where(s => s.StoreId == storeId);

                // Apply filters
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.SearchTerm))
                    {
                        query = query.Where(s =>
                            s.SaleNumber.Contains(filter.SearchTerm) ||
                            s.Notes.Contains(filter.SearchTerm));
                    }

                    if (filter.StartDate.HasValue)
                    {
                        query = query.Where(s => s.SaleDate >= filter.StartDate.Value);
                    }

                    if (filter.EndDate.HasValue)
                    {
                        // Include the entire end date (until 23:59:59)
                        query = query.Where(s => s.SaleDate <= filter.EndDate.Value.AddDays(1).AddTicks(-1));
                    }

                    if (!string.IsNullOrEmpty(filter.Status))
                    {
                        query = query.Where(s => s.Status == filter.Status);
                    }

                    if (filter.UserId.HasValue)
                    {
                        query = query.Where(s => s.UserId == filter.UserId);
                    }

                    if (filter.MinAmount.HasValue)
                    {
                        query = query.Where(s => s.GrandTotal >= filter.MinAmount.Value);
                    }

                    if (filter.MaxAmount.HasValue)
                    {
                        query = query.Where(s => s.GrandTotal <= filter.MaxAmount.Value);
                    }
                }

                // Apply sorting
                query = filter?.SortBy?.ToLower() switch
                {
                    "date_asc" => query.OrderBy(s => s.SaleDate),
                    "date_desc" => query.OrderByDescending(s => s.SaleDate),
                    "amount_asc" => query.OrderBy(s => s.GrandTotal),
                    "amount_desc" => query.OrderByDescending(s => s.GrandTotal),
                    _ => query.OrderByDescending(s => s.SaleDate) // Default sort by date descending
                };

                // Apply pagination
                var pageSize = filter?.PageSize ?? 50;
                var pageNumber = filter?.PageNumber ?? 1;

                var sales = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // For each sale, get the total count of items
                foreach (var sale in sales)
                {
                    sale.SaleItems = await _context.SaleItems
                        .Where(si => si.SaleId == sale.Id)
                        .ToListAsync();
                }

                _logger.LogInformation("Retrieved {SaleCount} sales for store {StoreId}", sales.Count, storeId);
                return Ok(sales);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sales for store {StoreId}", GetUserStoreId());
                return StatusCode(500, "An error occurred while retrieving sales");
            }
        }

        // GET: api/sales/5
        [HttpGet("{id}")]
        [Authorize(Policy = Permissions.ViewSales)]
        public async Task<ActionResult<Sale>> GetSale(int id)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var sale = await _context.Sales
                    .Include(s => s.User)
                    .Include(s => s.Currency)
                    .Include(s => s.SaleItems)
                        .ThenInclude(si => si.Product)
                    .Include(s => s.Payments)
                    .FirstOrDefaultAsync(s => s.Id == id && s.StoreId == storeId);

                if (sale == null)
                {
                    _logger.LogWarning("Sale with ID {SaleId} not found for store {StoreId}", id, storeId);
                    return NotFound($"Sale with ID {id} not found");
                }

                return Ok(sale);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sale with ID {SaleId} for store {StoreId}", id, GetUserStoreId());
                return StatusCode(500, $"An error occurred while retrieving sale with ID {id}");
            }
        }

        // POST: api/sales
        [HttpPost]
        [Authorize(Policy = Permissions.CreateSales)]
        public async Task<ActionResult<Sale>> CreateSale(SaleCreateDto saleDto)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                // Validate if products exist and have enough stock
                foreach (var item in saleDto.Items)
                {
                    var product = await _context.Products
                        .FirstOrDefaultAsync(p => p.Id == item.ProductId && p.StoreId == storeId);

                    if (product == null)
                    {
                        return BadRequest($"Product with ID {item.ProductId} not found");
                    }

                    if (product.StockQuantity < item.Quantity)
                    {
                        return BadRequest($"Insufficient stock for product '{product.Name}'. Available: {product.StockQuantity}, Requested: {item.Quantity}");
                    }
                }

                // Generate a unique sale number (e.g., S-YYYYMMDD-XXXX)
                string saleNumber = $"S-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";

                // Create new Sale
                var sale = new Sale
                {
                    SaleNumber = saleNumber,
                    UserId = GetUserId(),
                    StoreId = storeId.Value,
                    SaleDate = DateTime.UtcNow,
                    SubTotal = saleDto.Items.Sum(i => i.Quantity * i.UnitPrice),
                    DiscountAmount = saleDto.DiscountAmount,
                    TaxAmount = saleDto.Items.Sum(i => (i.Quantity * i.UnitPrice * i.TaxRate)),
                    GrandTotal = saleDto.Items.Sum(i => i.Quantity * i.UnitPrice * (1 + i.TaxRate)) - saleDto.DiscountAmount,
                    Status = saleDto.Status ?? "Completed",
                    Notes = saleDto.Notes,
                    DeviceId = saleDto.DeviceId,
                    CurrencyId = saleDto.CurrencyId,
                    ExchangeRate = saleDto.ExchangeRate ?? 1.0m,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    SyncStatus = SyncStatus.NotSynced,
                    SyncId = saleDto.SyncId ?? Guid.NewGuid()
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // Add sale items
                foreach (var itemDto in saleDto.Items)
                {
                    var saleItem = new SaleItem
                    {
                        SaleId = sale.Id,
                        ProductId = itemDto.ProductId,
                        StoreId = storeId.Value,
                        Quantity = itemDto.Quantity,
                        UnitPrice = itemDto.UnitPrice,
                        DiscountRate = itemDto.DiscountRate,
                        DiscountAmount = itemDto.Quantity * itemDto.UnitPrice * itemDto.DiscountRate,
                        TaxRate = itemDto.TaxRate,
                        TaxAmount = itemDto.Quantity * itemDto.UnitPrice * itemDto.TaxRate,
                        LineTotal = itemDto.Quantity * itemDto.UnitPrice * (1 + itemDto.TaxRate) * (1 - itemDto.DiscountRate),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        SyncStatus = SyncStatus.NotSynced,
                        SyncId = Guid.NewGuid()
                    };

                    _context.SaleItems.Add(saleItem);

                    // Update product stock
                    var product = await _context.Products.FindAsync(itemDto.ProductId);
                    product.StockQuantity -= itemDto.Quantity;
                    product.UpdatedAt = DateTime.UtcNow;

                    // Record stock transaction
                    var stockTransaction = new ProductTransaction
                    {
                        ProductId = itemDto.ProductId,
                        StoreId = storeId.Value,
                        UserId = GetUserId(),
                        TransactionType = ProductTransactionType.SaleDeduction,
                        ReferenceType = "Sale",
                        ReferenceId = sale.Id,
                        QuantityBefore = product.StockQuantity + itemDto.Quantity,
                        QuantityChange = -itemDto.Quantity, // Negative for outgoing stock
                        QuantityAfter = product.StockQuantity,
                        Notes = $"Sale {sale.SaleNumber} item",
                        TransactionDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        SyncStatus = SyncStatus.NotSynced,
                        SyncId = Guid.NewGuid()
                    };

                    _context.ProductTransactions.Add(stockTransaction);
                }

                // Add payments if provided
                if (saleDto.Payments != null && saleDto.Payments.Any())
                {
                    foreach (var paymentDto in saleDto.Payments)
                    {
                        var payment = new Payment
                        {
                            SaleId = sale.Id,
                            StoreId = storeId.Value,
                            Amount = paymentDto.Amount,
                            PaymentMethod = paymentDto.PaymentMethod,
                            ReferenceNumber = paymentDto.ReferenceNumber,
                            PaymentDate = DateTime.UtcNow,
                            Status = paymentDto.Status ?? "Approved",
                            CurrencyId = paymentDto.CurrencyId,
                            ExchangeRate = paymentDto.ExchangeRate ?? 1.0m,
                            Notes = paymentDto.Notes,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            SyncStatus = SyncStatus.NotSynced,
                            SyncId = Guid.NewGuid()
                        };

                        _context.Payments.Add(payment);
                    }

                    // Update sale status based on payments
                    decimal totalPaid = saleDto.Payments.Sum(p => p.Amount);
                    if (totalPaid >= sale.GrandTotal)
                    {
                        sale.Status = "Fully Paid";
                    }
                    else if (totalPaid > 0)
                    {
                        sale.Status = "Partially Paid";
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Created new sale with ID {SaleId} and number {SaleNumber} for store {StoreId}",
                    sale.Id, sale.SaleNumber, storeId);

                return CreatedAtAction(nameof(GetSale), new { id = sale.Id }, sale);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sale for store {StoreId}", GetUserStoreId());
                return StatusCode(500, "An error occurred while creating sale");
            }
        }

        // PUT: api/sales/5
        [HttpPut("{id}")]
        [Authorize(Policy = Permissions.EditSales)]
        public async Task<IActionResult> UpdateSale(int id, SaleUpdateDto saleDto)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                // Get existing sale
                var sale = await _context.Sales
                    .Include(s => s.SaleItems)
                    .Include(s => s.Payments)
                    .FirstOrDefaultAsync(s => s.Id == id && s.StoreId == storeId);

                if (sale == null)
                {
                    _logger.LogWarning("Sale with ID {SaleId} not found for store {StoreId}", id, storeId);
                    return NotFound($"Sale with ID {id} not found");
                }

                // Only allow updating certain fields for completed sales
                if (sale.Status == "Completed" || sale.Status == "Fully Paid")
                {
                    // For completed sales, only allow updating notes and status in certain cases
                    sale.Notes = saleDto.Notes ?? sale.Notes;

                    // Only allow status change if it's a valid transition
                    if (!string.IsNullOrEmpty(saleDto.Status) &&
                        (saleDto.Status == "Canceled" || saleDto.Status == "Refunded"))
                    {
                        sale.Status = saleDto.Status;
                    }
                }
                else
                {
                    // For draft or partially paid sales, allow more updates
                    sale.DiscountAmount = saleDto.DiscountAmount ?? sale.DiscountAmount;
                    sale.Notes = saleDto.Notes ?? sale.Notes;
                    sale.Status = saleDto.Status ?? sale.Status;

                    if (saleDto.CurrencyId.HasValue)
                    {
                        sale.CurrencyId = saleDto.CurrencyId.Value;
                    }

                    if (saleDto.ExchangeRate.HasValue)
                    {
                        sale.ExchangeRate = saleDto.ExchangeRate.Value;
                    }
                }

                sale.UpdatedAt = DateTime.UtcNow;
                sale.SyncStatus = SyncStatus.NotSynced;

                // Update SaleItems if provided (only for draft sales)
                if (saleDto.Items != null && saleDto.Items.Any() && sale.Status == "Draft")
                {
                    // Remove existing items
                    foreach (var existingItem in sale.SaleItems.ToList())
                    {
                        // Return stock to inventory
                        var product = await _context.Products.FindAsync(existingItem.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity += existingItem.Quantity;
                            product.UpdatedAt = DateTime.UtcNow;

                            // Record stock transaction
                            var stockTransaction = new ProductTransaction
                            {
                                ProductId = existingItem.ProductId,
                                StoreId = storeId.Value,
                                UserId = GetUserId(),
                                TransactionType = ProductTransactionType.StockAdjustment,
                                ReferenceType = "Sale",
                                ReferenceId = sale.Id,
                                QuantityBefore = product.StockQuantity - existingItem.Quantity,
                                QuantityChange = existingItem.Quantity, // Positive for returning stock
                                QuantityAfter = product.StockQuantity,
                                Notes = $"Sale {sale.SaleNumber} item removed during edit",
                                TransactionDate = DateTime.UtcNow,
                                CreatedAt = DateTime.UtcNow,
                                SyncStatus = SyncStatus.NotSynced,
                                SyncId = Guid.NewGuid()
                            };

                            _context.ProductTransactions.Add(stockTransaction);
                        }

                        _context.SaleItems.Remove(existingItem);
                    }

                    // Add new items
                    decimal subTotal = 0;
                    decimal taxAmount = 0;

                    foreach (var itemDto in saleDto.Items)
                    {
                        // Validate product existence and stock
                        var product = await _context.Products
                            .FirstOrDefaultAsync(p => p.Id == itemDto.ProductId && p.StoreId == storeId);

                        if (product == null)
                        {
                            return BadRequest($"Product with ID {itemDto.ProductId} not found");
                        }

                        if (product.StockQuantity < itemDto.Quantity)
                        {
                            return BadRequest($"Insufficient stock for product '{product.Name}'. Available: {product.StockQuantity}, Requested: {itemDto.Quantity}");
                        }

                        var saleItem = new SaleItem
                        {
                            SaleId = sale.Id,
                            ProductId = itemDto.ProductId,
                            StoreId = storeId.Value,
                            Quantity = itemDto.Quantity,
                            UnitPrice = itemDto.UnitPrice,
                            DiscountRate = itemDto.DiscountRate,
                            DiscountAmount = itemDto.Quantity * itemDto.UnitPrice * itemDto.DiscountRate,
                            TaxRate = itemDto.TaxRate,
                            TaxAmount = itemDto.Quantity * itemDto.UnitPrice * itemDto.TaxRate,
                            LineTotal = itemDto.Quantity * itemDto.UnitPrice * (1 + itemDto.TaxRate) * (1 - itemDto.DiscountRate),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            SyncStatus = SyncStatus.NotSynced,
                            SyncId = Guid.NewGuid()
                        };

                        _context.SaleItems.Add(saleItem);

                        // Update product stock
                        product.StockQuantity -= itemDto.Quantity;
                        product.UpdatedAt = DateTime.UtcNow;

                        // Record stock transaction
                        var stockTransaction = new ProductTransaction
                        {
                            ProductId = itemDto.ProductId,
                            StoreId = storeId.Value,
                            UserId = GetUserId(),
                            TransactionType = ProductTransactionType.StockAdjustment,
                            ReferenceType = "Sale",
                            ReferenceId = sale.Id,
                            QuantityBefore = product.StockQuantity + itemDto.Quantity,
                            QuantityChange = -itemDto.Quantity, // Negative for outgoing stock
                            QuantityAfter = product.StockQuantity,
                            Notes = $"Sale {sale.SaleNumber} item added during edit",
                            TransactionDate = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow,
                            SyncStatus = SyncStatus.NotSynced,
                            SyncId = Guid.NewGuid()
                        };

                        _context.ProductTransactions.Add(stockTransaction);

                        // Calculate totals
                        subTotal += itemDto.Quantity * itemDto.UnitPrice;
                        taxAmount += itemDto.Quantity * itemDto.UnitPrice * itemDto.TaxRate;
                    }

                    // Update sale totals
                    sale.SubTotal = subTotal;
                    sale.TaxAmount = taxAmount;
                    sale.GrandTotal = subTotal + taxAmount - sale.DiscountAmount;
                }

                // Update Payments if provided (only allow adding payments, not removing)
                if (saleDto.Payments != null && saleDto.Payments.Any())
                {
                    foreach (var paymentDto in saleDto.Payments)
                    {
                        if (paymentDto.Id == 0) // New payment
                        {
                            var payment = new Payment
                            {
                                SaleId = sale.Id,
                                StoreId = storeId.Value,
                                Amount = paymentDto.Amount,
                                PaymentMethod = paymentDto.PaymentMethod,
                                ReferenceNumber = paymentDto.ReferenceNumber,
                                PaymentDate = DateTime.UtcNow,
                                Status = paymentDto.Status ?? "Approved",
                                CurrencyId = paymentDto.CurrencyId,
                                ExchangeRate = paymentDto.ExchangeRate ?? 1.0m,
                                Notes = paymentDto.Notes,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                SyncStatus = SyncStatus.NotSynced,
                                SyncId = Guid.NewGuid()
                            };

                            _context.Payments.Add(payment);
                        }
                    }

                    // Update sale status based on payments after save
                    await _context.SaveChangesAsync();

                    var allPayments = await _context.Payments
                        .Where(p => p.SaleId == sale.Id)
                        .ToListAsync();

                    decimal totalPaid = allPayments.Sum(p => p.Amount);

                    if (totalPaid >= sale.GrandTotal)
                    {
                        sale.Status = "Fully Paid";
                    }
                    else if (totalPaid > 0)
                    {
                        sale.Status = "Partially Paid";
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated sale with ID {SaleId} for store {StoreId}", id, storeId);
                return Ok(new { message = "Sale updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sale with ID {SaleId} for store {StoreId}", id, GetUserStoreId());
                return StatusCode(500, $"An error occurred while updating sale with ID {id}");
            }
        }

        // DELETE: api/sales/5
        [HttpDelete("{id}")]
        [Authorize(Policy = Permissions.DeleteSales)]
        public async Task<IActionResult> DeleteSale(int id)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var sale = await _context.Sales
                    .Include(s => s.SaleItems)
                    .Include(s => s.Payments)
                    .FirstOrDefaultAsync(s => s.Id == id && s.StoreId == storeId);

                if (sale == null)
                {
                    _logger.LogWarning("Sale with ID {SaleId} not found for store {StoreId}", id, storeId);
                    return NotFound($"Sale with ID {id} not found");
                }

                // Only allow deleting draft sales or applying a soft delete
                if (sale.Status != "Draft")
                {
                    // For completed sales, just mark as canceled instead of deleting
                    sale.Status = "Canceled";
                    sale.UpdatedAt = DateTime.UtcNow;
                    sale.SyncStatus = SyncStatus.NotSynced;

                    // Return stock to inventory for all items
                    foreach (var item in sale.SaleItems)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity += item.Quantity;
                            product.UpdatedAt = DateTime.UtcNow;

                            // Record stock transaction
                            var stockTransaction = new ProductTransaction
                            {
                                ProductId = item.ProductId,
                                StoreId = storeId.Value,
                                UserId = GetUserId(),
                                TransactionType = ProductTransactionType.StockReturn,
                                ReferenceType = "Sale",
                                ReferenceId = sale.Id,
                                QuantityBefore = product.StockQuantity - item.Quantity,
                                QuantityChange = item.Quantity, // Positive for returning stock
                                QuantityAfter = product.StockQuantity,
                                Notes = $"Sale {sale.SaleNumber} canceled",
                                TransactionDate = DateTime.UtcNow,
                                CreatedAt = DateTime.UtcNow,
                                SyncStatus = SyncStatus.NotSynced,
                                SyncId = Guid.NewGuid()
                            };

                            _context.ProductTransactions.Add(stockTransaction);
                        }
                    }

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Sale with ID {SaleId} for store {StoreId} marked as canceled", id, storeId);
                    return Ok(new { message = "Sale has been canceled" });
                }
                else
                {
                    // For draft sales, we can delete completely
                    // First return any allocated stock
                    foreach (var item in sale.SaleItems)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity += item.Quantity;
                            product.UpdatedAt = DateTime.UtcNow;
                        }
                    }

                    // Delete related payments
                    _context.Payments.RemoveRange(sale.Payments);

                    // Delete related items
                    _context.SaleItems.RemoveRange(sale.SaleItems);

                    // Delete the sale
                    _context.Sales.Remove(sale);

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Draft sale with ID {SaleId} for store {StoreId} deleted", id, storeId);
                    return Ok(new { message = "Sale deleted successfully" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting sale with ID {SaleId} for store {StoreId}", id, GetUserStoreId());
                return StatusCode(500, $"An error occurred while deleting sale with ID {id}");
            }
        }

        // POST: api/sales/sync
        [HttpPost("sync")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<IEnumerable<Sale>>> SyncSales(List<SaleSyncDto> saleDtos)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var result = new List<Sale>();

                foreach (var saleDto in saleDtos)
                {
                    try
                    {
                        // Skip invalid sales
                        if (saleDto.SyncId == Guid.Empty)
                        {
                            continue;
                        }

                        // Try to find existing sale by SyncId
                        var existingSale = await _context.Sales
                            .FirstOrDefaultAsync(s => s.SyncId == saleDto.SyncId && s.StoreId == storeId);

                        if (existingSale == null && saleDto.Id > 0)
                        {
                            // Try to find by ID
                            existingSale = await _context.Sales
                                .FirstOrDefaultAsync(s => s.Id == saleDto.Id && s.StoreId == storeId);
                        }

                        if (existingSale == null)
                        {
                            // Create new sale
                            var newSale = new Sale
                            {
                                SaleNumber = saleDto.SaleNumber,
                                UserId = saleDto.UserId,
                                StoreId = storeId.Value,
                                SaleDate = saleDto.SaleDate,
                                SubTotal = saleDto.SubTotal,
                                DiscountAmount = saleDto.DiscountAmount,
                                TaxAmount = saleDto.TaxAmount,
                                GrandTotal = saleDto.GrandTotal,
                                Status = saleDto.Status,
                                Notes = saleDto.Notes,
                                DeviceId = saleDto.DeviceId,
                                CurrencyId = saleDto.CurrencyId,
                                ExchangeRate = saleDto.ExchangeRate,
                                CreatedAt = saleDto.CreatedAt,
                                UpdatedAt = DateTime.UtcNow,
                                SyncStatus = SyncStatus.Synced,
                                SyncId = saleDto.SyncId
                            };

                            _context.Sales.Add(newSale);
                            await _context.SaveChangesAsync();

                            // Process sale items
                            if (saleDto.Items != null)
                            {
                                foreach (var itemDto in saleDto.Items)
                                {
                                    var saleItem = new SaleItem
                                    {
                                        SaleId = newSale.Id,
                                        ProductId = itemDto.ProductId,
                                        StoreId = storeId.Value,
                                        Quantity = itemDto.Quantity,
                                        UnitPrice = itemDto.UnitPrice,
                                        DiscountRate = itemDto.DiscountRate,
                                        DiscountAmount = itemDto.DiscountAmount,
                                        TaxRate = itemDto.TaxRate,
                                        TaxAmount = itemDto.TaxAmount,
                                        LineTotal = itemDto.LineTotal,
                                        CreatedAt = itemDto.CreatedAt,
                                        UpdatedAt = DateTime.UtcNow,
                                        SyncStatus = SyncStatus.Synced,
                                        SyncId = itemDto.SyncId
                                    };

                                    _context.SaleItems.Add(saleItem);
                                }
                            }

                            // Process payments
                            if (saleDto.Payments != null)
                            {
                                foreach (var paymentDto in saleDto.Payments)
                                {
                                    var payment = new Payment
                                    {
                                        SaleId = newSale.Id,
                                        StoreId = storeId.Value,
                                        Amount = paymentDto.Amount,
                                        PaymentMethod = paymentDto.PaymentMethod,
                                        ReferenceNumber = paymentDto.ReferenceNumber,
                                        PaymentDate = paymentDto.PaymentDate,
                                        Status = paymentDto.Status,
                                        CurrencyId = paymentDto.CurrencyId,
                                        ExchangeRate = paymentDto.ExchangeRate,
                                        Notes = paymentDto.Notes,
                                        CreatedAt = paymentDto.CreatedAt,
                                        UpdatedAt = DateTime.UtcNow,
                                        SyncStatus = SyncStatus.Synced,
                                        SyncId = paymentDto.SyncId
                                    };

                                    _context.Payments.Add(payment);
                                }
                            }

                            await _context.SaveChangesAsync();
                            result.Add(newSale);
                        }
                        else
                        {
                            // Update existing sale
                            existingSale.SaleNumber = saleDto.SaleNumber;
                            existingSale.SaleDate = saleDto.SaleDate;
                            existingSale.SubTotal = saleDto.SubTotal;
                            existingSale.DiscountAmount = saleDto.DiscountAmount;
                            existingSale.TaxAmount = saleDto.TaxAmount;
                            existingSale.GrandTotal = saleDto.GrandTotal;
                            existingSale.Status = saleDto.Status;
                            existingSale.Notes = saleDto.Notes;
                            existingSale.DeviceId = saleDto.DeviceId;
                            existingSale.CurrencyId = saleDto.CurrencyId;
                            existingSale.ExchangeRate = saleDto.ExchangeRate;
                            existingSale.UpdatedAt = DateTime.UtcNow;
                            existingSale.SyncStatus = SyncStatus.Synced;

                            // Process sale items by removing and re-adding
                            var existingItems = await _context.SaleItems
                                .Where(si => si.SaleId == existingSale.Id)
                                .ToListAsync();

                            _context.SaleItems.RemoveRange(existingItems);

                            if (saleDto.Items != null)
                            {
                                foreach (var itemDto in saleDto.Items)
                                {
                                    var saleItem = new SaleItem
                                    {
                                        SaleId = existingSale.Id,
                                        ProductId = itemDto.ProductId,
                                        StoreId = storeId.Value,
                                        Quantity = itemDto.Quantity,
                                        UnitPrice = itemDto.UnitPrice,
                                        DiscountRate = itemDto.DiscountRate,
                                        DiscountAmount = itemDto.DiscountAmount,
                                        TaxRate = itemDto.TaxRate,
                                        TaxAmount = itemDto.TaxAmount,
                                        LineTotal = itemDto.LineTotal,
                                        CreatedAt = itemDto.CreatedAt,
                                        UpdatedAt = DateTime.UtcNow,
                                        SyncStatus = SyncStatus.Synced,
                                        SyncId = itemDto.SyncId
                                    };

                                    _context.SaleItems.Add(saleItem);
                                }
                            }

                            // Process payments by comparing and adding new ones
                            if (saleDto.Payments != null)
                            {
                                var existingPayments = await _context.Payments
                                    .Where(p => p.SaleId == existingSale.Id)
                                    .ToListAsync();

                                var existingPaymentSyncIds = existingPayments.Select(p => p.SyncId).ToList();

                                foreach (var paymentDto in saleDto.Payments)
                                {
                                    // Only add payments that don't exist yet
                                    if (!existingPaymentSyncIds.Contains(paymentDto.SyncId))
                                    {
                                        var payment = new Payment
                                        {
                                            SaleId = existingSale.Id,
                                            StoreId = storeId.Value,
                                            Amount = paymentDto.Amount,
                                            PaymentMethod = paymentDto.PaymentMethod,
                                            ReferenceNumber = paymentDto.ReferenceNumber,
                                            PaymentDate = paymentDto.PaymentDate,
                                            Status = paymentDto.Status,
                                            CurrencyId = paymentDto.CurrencyId,
                                            ExchangeRate = paymentDto.ExchangeRate,
                                            Notes = paymentDto.Notes,
                                            CreatedAt = paymentDto.CreatedAt,
                                            UpdatedAt = DateTime.UtcNow,
                                            SyncStatus = SyncStatus.Synced,
                                            SyncId = paymentDto.SyncId
                                        };

                                        _context.Payments.Add(payment);
                                    }
                                }
                            }

                            await _context.SaveChangesAsync();
                            result.Add(existingSale);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other sales
                        _logger.LogError(ex, "Error syncing sale with ID {SaleId} and SyncId {SyncId}",
                            saleDto.Id, saleDto.SyncId);
                    }
                }

                _logger.LogInformation("Synced {SaleCount} sales for store {StoreId}", result.Count, storeId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing sales for store {StoreId}", GetUserStoreId());
                return StatusCode(500, "An error occurred while syncing sales");
            }
        }

        // GET: api/sales/pending-sync
        [HttpGet("pending-sync")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<IEnumerable<Sale>>> GetPendingSyncSales()
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var sales = await _context.Sales
                    .Include(s => s.SaleItems)
                    .Include(s => s.Payments)
                    .Where(s => s.SyncStatus == SyncStatus.NotSynced && s.StoreId == storeId)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {SaleCount} pending sync sales for store {StoreId}", sales.Count, storeId);
                return Ok(sales);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetPendingSyncSales");
                return StatusCode(500, "An error occurred while retrieving pending sync sales");
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

    // DTOs for Sales
    public class SaleFilterDto
    {
        public string SearchTerm { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; }
        public int? UserId { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public string SortBy { get; set; } = "date_desc"; // date_asc, date_desc, amount_asc, amount_desc
        public int PageSize { get; set; } = 50;
        public int PageNumber { get; set; } = 1;
    }

    public class SaleCreateDto
    {
        [Required]
        public List<SaleItemCreateDto> Items { get; set; }

        public decimal DiscountAmount { get; set; } = 0;
        public string Status { get; set; } // Draft, Completed, etc.
        public string Notes { get; set; }
        public string DeviceId { get; set; }

        [Required]
        public int CurrencyId { get; set; } = 1; // Default currency

        public decimal? ExchangeRate { get; set; } = 1.0m;
        public Guid? SyncId { get; set; } // Optional for offline-first functionality
        public List<PaymentCreateDto> Payments { get; set; } // Optional payments
    }

    public class SaleItemCreateDto
    {
        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        [Range(0, 99999.99)]
        public decimal UnitPrice { get; set; }

        [Range(0, 1)]
        public decimal DiscountRate { get; set; } = 0;

        [Range(0, 1)]
        public decimal TaxRate { get; set; } = 0;
    }

    public class PaymentCreateDto
    {
        public int Id { get; set; } = 0; // Default 0 for new payments

        [Required]
        [Range(0.01, 99999.99)]
        public decimal Amount { get; set; }

        [Required]
        public string PaymentMethod { get; set; } // Cash, Credit Card, etc.

        public string ReferenceNumber { get; set; }
        public string Status { get; set; }
        public int CurrencyId { get; set; } = 1; // Default currency
        public decimal? ExchangeRate { get; set; } = 1.0m;
        public string Notes { get; set; }
    }

    public class SaleUpdateDto
    {
        public List<SaleItemCreateDto> Items { get; set; }
        public decimal? DiscountAmount { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
        public int? CurrencyId { get; set; }
        public decimal? ExchangeRate { get; set; }
        public List<PaymentCreateDto> Payments { get; set; }
    }

    public class PaymentUpdateDto : PaymentCreateDto
    {
        public int Id { get; set; } // Existing payment ID or 0 for new payments
    }

    public class SaleSyncDto
    {
        public int Id { get; set; }
        [Required]
        public Guid SyncId { get; set; }
        [Required]
        public string SaleNumber { get; set; }
        public int UserId { get; set; }
        [Required]
        public DateTime SaleDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
        public string DeviceId { get; set; }
        public int CurrencyId { get; set; }
        public decimal ExchangeRate { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<SaleItemSyncDto> Items { get; set; }
        public List<PaymentSyncDto> Payments { get; set; }
    }

    public class SaleItemSyncDto
    {
        public int Id { get; set; }
        public Guid SyncId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountRate { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal LineTotal { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PaymentSyncDto
    {
        public int Id { get; set; }
        public Guid SyncId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string ReferenceNumber { get; set; }
        public DateTime PaymentDate { get; set; }
        public string Status { get; set; }
        public int CurrencyId { get; set; }
        public decimal ExchangeRate { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

