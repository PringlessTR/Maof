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
using MaofAPI.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace MaofAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SyncController> _logger;
        private readonly IHubContext<SyncHub> _syncHub;

        public SyncController(
            ApplicationDbContext context, 
            ILogger<SyncController> logger,
            IHubContext<SyncHub> syncHub)
        {
            _context = context;
            _logger = logger;
            _syncHub = syncHub;
        }

        // GET: api/sync/batches
        [HttpGet("batches")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<IEnumerable<SyncBatch>>> GetSyncBatches([FromQuery] SyncBatchFilterDto filter)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var query = _context.SyncBatches
                    .Include(sb => sb.User)
                    .Where(sb => sb.StoreId == storeId.Value)
                    .AsQueryable();

                // Apply filters
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.Status))
                    {
                        query = query.Where(sb => sb.Status == filter.Status);
                    }

                    if (!string.IsNullOrEmpty(filter.DeviceId))
                    {
                        query = query.Where(sb => sb.DeviceId == filter.DeviceId);
                    }

                    if (filter.UserId.HasValue)
                    {
                        query = query.Where(sb => sb.UserId == filter.UserId.Value);
                    }

                    if (filter.StartDateFrom.HasValue)
                    {
                        query = query.Where(sb => sb.StartDate >= filter.StartDateFrom.Value);
                    }

                    if (filter.StartDateTo.HasValue)
                    {
                        query = query.Where(sb => sb.StartDate <= filter.StartDateTo.Value);
                    }
                }

                // Apply pagination
                var pageSize = filter?.PageSize ?? 50;
                var pageNumber = filter?.PageNumber ?? 1;

                var batches = await query
                    .OrderByDescending(sb => sb.StartDate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {BatchCount} sync batches for store {StoreId}", 
                    batches.Count, storeId.Value);
                return Ok(batches);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sync batches");
                return StatusCode(500, "An error occurred while retrieving sync batches");
            }
        }

        // GET: api/sync/batches/5
        [HttpGet("batches/{id}")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<SyncBatch>> GetSyncBatch(int id)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var syncBatch = await _context.SyncBatches
                    .Include(sb => sb.User)
                    .Include(sb => sb.SyncLogs)
                    .FirstOrDefaultAsync(sb => sb.Id == id && sb.StoreId == storeId.Value);

                if (syncBatch == null)
                {
                    _logger.LogWarning("Sync batch with ID {BatchId} not found for store {StoreId}", 
                        id, storeId.Value);
                    return NotFound($"Sync batch with ID {id} not found");
                }

                return Ok(syncBatch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sync batch with ID {BatchId}", id);
                return StatusCode(500, $"An error occurred while retrieving sync batch with ID {id}");
            }
        }

        // POST: api/sync/batches
        [HttpPost("batches")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<SyncBatch>> CreateSyncBatch(SyncBatchCreateDto batchDto)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                // Get user ID from claims
                int? userId = GetUserId();
                if (!userId.HasValue)
                {
                    return BadRequest("User ID could not be determined");
                }

                // Validate store and user
                var user = await _context.Users.FirstOrDefaultAsync(u => 
                    u.Id == userId.Value && u.StoreId == storeId.Value && u.IsActive);
                
                if (user == null)
                {
                    return BadRequest("User not found or not active for the specified store");
                }

                // Create new sync batch
                var syncBatch = new SyncBatch
                {
                    StartDate = DateTime.UtcNow,
                    Status = "Pending",
                    DeviceId = batchDto.DeviceId,
                    UserId = userId.Value,
                    StoreId = storeId.Value,
                    TotalRecords = batchDto.TotalRecords,
                    ProcessedRecords = 0,
                    FailedRecords = 0
                };

                _context.SyncBatches.Add(syncBatch);
                await _context.SaveChangesAsync();

                // Notify clients via SignalR
                await _syncHub.Clients.Group(storeId.Value.ToString())
                    .SendAsync("SyncBatchCreated", new { 
                        syncBatch.Id, 
                        syncBatch.Status,
                        syncBatch.StartDate,
                        syncBatch.UserId,
                        Username = user.UserName,
                        syncBatch.DeviceId
                    });

                _logger.LogInformation("Sync batch created with ID {BatchId} for store {StoreId}", 
                    syncBatch.Id, storeId.Value);
                return CreatedAtAction(nameof(GetSyncBatch), new { id = syncBatch.Id }, syncBatch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sync batch");
                return StatusCode(500, "An error occurred while creating sync batch");
            }
        }

        // PUT: api/sync/batches/5/status
        [HttpPut("batches/{id}/status")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<IActionResult> UpdateSyncBatchStatus(int id, SyncBatchStatusUpdateDto statusDto)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var syncBatch = await _context.SyncBatches
                    .FirstOrDefaultAsync(sb => sb.Id == id && sb.StoreId == storeId.Value);

                if (syncBatch == null)
                {
                    _logger.LogWarning("Sync batch with ID {BatchId} not found for store {StoreId}", 
                        id, storeId.Value);
                    return NotFound($"Sync batch with ID {id} not found");
                }

                // Update status
                syncBatch.Status = statusDto.Status;
                
                // Update processed records if provided
                if (statusDto.ProcessedRecords.HasValue)
                {
                    syncBatch.ProcessedRecords = statusDto.ProcessedRecords.Value;
                }
                
                // Update failed records if provided
                if (statusDto.FailedRecords.HasValue)
                {
                    syncBatch.FailedRecords = statusDto.FailedRecords.Value;
                }
                
                // If status is completed or failed, set end date
                if (statusDto.Status == "Completed" || statusDto.Status == "Failed")
                {
                    syncBatch.EndDate = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                // Notify clients via SignalR
                await _syncHub.Clients.Group(storeId.Value.ToString())
                    .SendAsync("SyncBatchUpdated", new { 
                        syncBatch.Id, 
                        syncBatch.Status,
                        syncBatch.ProcessedRecords,
                        syncBatch.FailedRecords,
                        syncBatch.EndDate
                    });

                _logger.LogInformation("Sync batch with ID {BatchId} updated to status {Status} for store {StoreId}", 
                    id, statusDto.Status, storeId.Value);
                return Ok(new { message = "Sync batch status updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sync batch status with ID {BatchId}", id);
                return StatusCode(500, $"An error occurred while updating sync batch status with ID {id}");
            }
        }

        // GET: api/sync/logs
        [HttpGet("logs")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<IEnumerable<SyncLog>>> GetSyncLogs([FromQuery] SyncLogFilterDto filter)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var query = _context.SyncLogs
                    .Include(sl => sl.User)
                    .Where(sl => sl.StoreId == storeId.Value)
                    .AsQueryable();

                // Apply filters
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.EntityName))
                    {
                        query = query.Where(sl => sl.EntityName == filter.EntityName);
                    }

                    if (!string.IsNullOrEmpty(filter.EntityId))
                    {
                        query = query.Where(sl => sl.EntityId == filter.EntityId);
                    }

                    if (!string.IsNullOrEmpty(filter.Operation))
                    {
                        query = query.Where(sl => sl.Operation == filter.Operation);
                    }

                    if (!string.IsNullOrEmpty(filter.DeviceId))
                    {
                        query = query.Where(sl => sl.DeviceId == filter.DeviceId);
                    }

                    if (filter.UserId.HasValue)
                    {
                        query = query.Where(sl => sl.UserId == filter.UserId.Value);
                    }

                    if (filter.SyncBatchId.HasValue)
                    {
                        query = query.Where(sl => sl.SyncBatchId == filter.SyncBatchId.Value);
                    }

                    if (filter.SyncStatus.HasValue)
                    {
                        query = query.Where(sl => sl.SyncStatus == filter.SyncStatus.Value);
                    }

                    if (filter.OperationDateFrom.HasValue)
                    {
                        query = query.Where(sl => sl.OperationDate >= filter.OperationDateFrom.Value);
                    }

                    if (filter.OperationDateTo.HasValue)
                    {
                        query = query.Where(sl => sl.OperationDate <= filter.OperationDateTo.Value);
                    }
                }

                // Apply pagination
                var pageSize = filter?.PageSize ?? 50;
                var pageNumber = filter?.PageNumber ?? 1;

                var logs = await query
                    .OrderByDescending(sl => sl.OperationDate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {LogCount} sync logs for store {StoreId}", 
                    logs.Count, storeId.Value);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sync logs");
                return StatusCode(500, "An error occurred while retrieving sync logs");
            }
        }

        // POST: api/sync/logs
        [HttpPost("logs")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<SyncLog>> CreateSyncLog(SyncLogCreateDto logDto)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                // Get user ID from claims
                int? userId = GetUserId();
                if (!userId.HasValue)
                {
                    return BadRequest("User ID could not be determined");
                }

                // Check if the batch exists if provided
                if (logDto.SyncBatchId.HasValue)
                {
                    var batchExists = await _context.SyncBatches.AnyAsync(sb => 
                        sb.Id == logDto.SyncBatchId.Value && sb.StoreId == storeId.Value);
                    
                    if (!batchExists)
                    {
                        return BadRequest($"Sync batch with ID {logDto.SyncBatchId.Value} does not exist");
                    }
                }

                // Create new sync log
                var syncLog = new SyncLog
                {
                    EntityName = logDto.EntityName,
                    EntityId = logDto.EntityId,
                    Operation = logDto.Operation,
                    OperationDate = DateTime.UtcNow,
                    DeviceId = logDto.DeviceId,
                    UserId = userId.Value,
                    StoreId = storeId.Value,
                    SyncStatus = SyncStatus.NotSynced,
                    RetryCount = 0,
                    SyncBatchId = logDto.SyncBatchId,
                    Priority = logDto.Priority
                };

                _context.SyncLogs.Add(syncLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Sync log created for entity {EntityName} with ID {EntityId} for store {StoreId}", 
                    logDto.EntityName, logDto.EntityId, storeId.Value);
                return CreatedAtAction(nameof(GetSyncLogById), new { id = syncLog.Id }, syncLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sync log");
                return StatusCode(500, "An error occurred while creating sync log");
            }
        }

        // GET: api/sync/logs/5
        [HttpGet("logs/{id}")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<SyncLog>> GetSyncLogById(int id)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var syncLog = await _context.SyncLogs
                    .Include(sl => sl.User)
                    .Include(sl => sl.SyncBatch)
                    .FirstOrDefaultAsync(sl => sl.Id == id && sl.StoreId == storeId.Value);

                if (syncLog == null)
                {
                    _logger.LogWarning("Sync log with ID {LogId} not found for store {StoreId}", 
                        id, storeId.Value);
                    return NotFound($"Sync log with ID {id} not found");
                }

                return Ok(syncLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sync log with ID {LogId}", id);
                return StatusCode(500, $"An error occurred while retrieving sync log with ID {id}");
            }
        }

        // PUT: api/sync/logs/5/status
        [HttpPut("logs/{id}/status")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<IActionResult> UpdateSyncLogStatus(int id, SyncLogStatusUpdateDto statusDto)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                var syncLog = await _context.SyncLogs
                    .FirstOrDefaultAsync(sl => sl.Id == id && sl.StoreId == storeId.Value);

                if (syncLog == null)
                {
                    _logger.LogWarning("Sync log with ID {LogId} not found for store {StoreId}", 
                        id, storeId.Value);
                    return NotFound($"Sync log with ID {id} not found");
                }

                // Update status
                syncLog.SyncStatus = statusDto.SyncStatus;
                
                // Update sync date if status is Synced
                if (statusDto.SyncStatus == SyncStatus.Synced)
                {
                    syncLog.SyncDate = DateTime.UtcNow;
                }
                
                // Update error message if provided
                if (!string.IsNullOrEmpty(statusDto.ErrorMessage))
                {
                    syncLog.ErrorMessage = statusDto.ErrorMessage;
                }
                
                // Increment retry count if failed
                if (statusDto.SyncStatus == SyncStatus.Failed)
                {
                    syncLog.RetryCount++;
                }

                await _context.SaveChangesAsync();

                // Notify clients via SignalR if there's a batch
                if (syncLog.SyncBatchId.HasValue)
                {
                    await _syncHub.Clients.Group(storeId.Value.ToString())
                        .SendAsync("SyncLogUpdated", new { 
                            syncLog.Id, 
                            syncLog.SyncStatus,
                            syncLog.SyncDate,
                            syncLog.ErrorMessage,
                            syncLog.RetryCount,
                            syncLog.SyncBatchId
                        });
                }

                _logger.LogInformation("Sync log with ID {LogId} updated to status {Status} for store {StoreId}", 
                    id, statusDto.SyncStatus, storeId.Value);
                return Ok(new { message = "Sync log status updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sync log status with ID {LogId}", id);
                return StatusCode(500, $"An error occurred while updating sync log status with ID {id}");
            }
        }

        // GET: api/sync/pending
        [HttpGet("pending")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<ActionResult<SyncPendingCountsDto>> GetPendingSyncCounts()
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                // Get counts of entities with pending sync
                var pendingProducts = await _context.Products
                    .Where(p => p.StoreId == storeId.Value && p.SyncStatus == SyncStatus.NotSynced)
                    .CountAsync();

                var pendingCategories = await _context.Categories
                    .Where(c => c.SyncStatus == SyncStatus.NotSynced)
                    .CountAsync();

                var pendingSales = await _context.Sales
                    .Where(s => s.StoreId == storeId.Value && s.SyncStatus == SyncStatus.NotSynced)
                    .CountAsync();


                var pendingPromotions = await _context.Promotions
                    .Where(p => p.StoreId == storeId.Value && p.SyncStatus == SyncStatus.NotSynced)
                    .CountAsync();


                var response = new SyncPendingCountsDto
                {
                    Products = pendingProducts,
                    Categories = pendingCategories,
                    Sales = pendingSales,
                    Promotions = pendingPromotions,
                    Total = pendingProducts + pendingCategories + pendingSales + pendingPromotions
                };

                _logger.LogInformation("Retrieved pending sync counts for store {StoreId}", storeId.Value);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending sync counts");
                return StatusCode(500, "An error occurred while retrieving pending sync counts");
            }
        }

        // POST: api/sync/joingroup
        [HttpPost("joingroup")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<IActionResult> JoinSyncGroup(SyncGroupDto groupDto)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                // Add client connection to the store's group
                await _syncHub.Groups.AddToGroupAsync(groupDto.ConnectionId, storeId.Value.ToString());
                
                _logger.LogInformation("Client with connection ID {ConnectionId} joined sync group for store {StoreId}", 
                    groupDto.ConnectionId, storeId.Value);
                return Ok(new { message = "Joined sync group successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining sync group");
                return StatusCode(500, "An error occurred while joining sync group");
            }
        }

        // POST: api/sync/leavegroup
        [HttpPost("leavegroup")]
        [Authorize(Policy = Permissions.SyncData)]
        public async Task<IActionResult> LeaveSyncGroup(SyncGroupDto groupDto)
        {
            try
            {
                // Get store ID from user claims
                int? storeId = GetUserStoreId();
                if (!storeId.HasValue)
                {
                    return Forbid("User is not associated with any store");
                }

                // Remove client connection from the store's group
                await _syncHub.Groups.RemoveFromGroupAsync(groupDto.ConnectionId, storeId.Value.ToString());
                
                _logger.LogInformation("Client with connection ID {ConnectionId} left sync group for store {StoreId}", 
                    groupDto.ConnectionId, storeId.Value);
                return Ok(new { message = "Left sync group successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving sync group");
                return StatusCode(500, "An error occurred while leaving sync group");
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

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("userId");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return null;
        }
    }

    // DTOs for Sync
    public class SyncBatchFilterDto
    {
        public string Status { get; set; }
        public string DeviceId { get; set; }
        public int? UserId { get; set; }
        public DateTime? StartDateFrom { get; set; }
        public DateTime? StartDateTo { get; set; }
        public int PageSize { get; set; } = 50;
        public int PageNumber { get; set; } = 1;
    }

    public class SyncBatchCreateDto
    {
        [Required]
        public string DeviceId { get; set; }
        
        [Required]
        public int TotalRecords { get; set; }
    }

    public class SyncBatchStatusUpdateDto
    {
        [Required]
        public string Status { get; set; }
        
        public int? ProcessedRecords { get; set; }
        public int? FailedRecords { get; set; }
    }

    public class SyncLogFilterDto
    {
        public string EntityName { get; set; }
        public string EntityId { get; set; }
        public string Operation { get; set; }
        public string DeviceId { get; set; }
        public int? UserId { get; set; }
        public int? SyncBatchId { get; set; }
        public SyncStatus? SyncStatus { get; set; }
        public DateTime? OperationDateFrom { get; set; }
        public DateTime? OperationDateTo { get; set; }
        public int PageSize { get; set; } = 50;
        public int PageNumber { get; set; } = 1;
    }

    public class SyncLogCreateDto
    {
        [Required]
        public string EntityName { get; set; }
        
        [Required]
        public string EntityId { get; set; }
        
        [Required]
        public string Operation { get; set; }
        
        [Required]
        public string DeviceId { get; set; }
        
        public int? SyncBatchId { get; set; }
        
        public int Priority { get; set; } = 0;
    }

    public class SyncLogStatusUpdateDto
    {
        [Required]
        public SyncStatus SyncStatus { get; set; }
        
        public string ErrorMessage { get; set; }
    }

    public class SyncPendingCountsDto
    {
        public int Products { get; set; }
        public int Categories { get; set; }
        public int Sales { get; set; }
        public int Users { get; set; }
        public int Promotions { get; set; }
        public int Inventory { get; set; }
        public int Total { get; set; }
    }

    public class SyncGroupDto
    {
        [Required]
        public string ConnectionId { get; set; }
    }
}
