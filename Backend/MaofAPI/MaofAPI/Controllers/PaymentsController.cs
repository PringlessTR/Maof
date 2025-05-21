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
    public class PaymentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(ApplicationDbContext context, ILogger<PaymentsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/payments
        [HttpGet]
        [Authorize(Policy = Permissions.ViewSales)]
        public async Task<ActionResult<IEnumerable<Payment>>> GetPayments([FromQuery] PaymentFilterDto filter)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var query = _context.Payments
                    .Include(p => p.Sale)
                    .Where(p => p.StoreId == storeId);

                // Apply basic filters (to be expanded as needed)
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.PaymentMethod))
                    {
                        query = query.Where(p => p.PaymentMethod == filter.PaymentMethod);
                    }

                    if (filter.StartDate.HasValue)
                    {
                        query = query.Where(p => p.PaymentDate >= filter.StartDate.Value);
                    }

                    if (filter.EndDate.HasValue)
                    {
                        query = query.Where(p => p.PaymentDate <= filter.EndDate.Value.AddDays(1).AddTicks(-1));
                    }

                    if (filter.SaleId.HasValue)
                    {
                        query = query.Where(p => p.SaleId == filter.SaleId.Value);
                    }
                }

                // Apply pagination
                var pageSize = filter?.PageSize ?? 50;
                var pageNumber = filter?.PageNumber ?? 1;

                var payments = await query
                    .OrderByDescending(p => p.PaymentDate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {PaymentCount} payments for store {StoreId}", payments.Count, storeId);
                return Ok(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payments for store {StoreId}", GetUserStoreId());
                return StatusCode(500, "An error occurred while retrieving payments");
            }
        }

        // GET: api/payments/5
        [HttpGet("{id}")]
        [Authorize(Policy = Permissions.ViewSales)]
        public async Task<ActionResult<Payment>> GetPayment(int id)
        {
            try
            {
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var payment = await _context.Payments
                    .Include(p => p.Sale)
                    .FirstOrDefaultAsync(p => p.Id == id && p.StoreId == storeId);

                if (payment == null)
                {
                    _logger.LogWarning("Payment with ID {PaymentId} not found for store {StoreId}", id, storeId);
                    return NotFound($"Payment with ID {id} not found");
                }

                return Ok(payment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payment with ID {PaymentId} for store {StoreId}", id, GetUserStoreId());
                return StatusCode(500, $"An error occurred while retrieving payment with ID {id}");
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

    // Basic DTO for Payment filtering
    public class PaymentFilterDto
    {
        public string PaymentMethod { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? SaleId { get; set; }
        public int PageSize { get; set; } = 50;
        public int PageNumber { get; set; } = 1;
    }
}
