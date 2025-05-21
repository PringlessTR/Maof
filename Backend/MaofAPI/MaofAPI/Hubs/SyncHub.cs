using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MaofAPI.Data;
using MaofAPI.Models;
using MaofAPI.Models.Enums;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MaofAPI.Hubs
{
    [Authorize]
    public class SyncHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SyncHub> _logger;
        
        // Claim type constants
        private const string USER_ID_CLAIM = "sub";
        private const string STORE_ID_CLAIM = "storeId";
        
        public SyncHub(
            ApplicationDbContext context,
            ILogger<SyncHub> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SyncBatchResponse> StartSync(int storeId)
        {
            try
            {
                _logger.LogInformation("Starting sync for store ID: {StoreId}", storeId);
                
                // Get current user info from JWT claims
                var userId = GetUserId();
                if (userId == null)
                {
                    _logger.LogWarning("Failed to get user ID from token claims");
                    throw new HubException("Invalid authentication token");
                }
                
                // Validate store access permission
                if (!await HasStoreAccessAsync(storeId))
                {
                    _logger.LogWarning("User {UserId} attempted unauthorized access to store {StoreId}", userId, storeId);
                    throw new HubException("Unauthorized access to store");
                }
                
                // Create a new sync batch
                var syncBatch = new SyncBatch
                {
                    StartDate = DateTime.UtcNow,
                    Status = "InProgress",
                    DeviceId = Context.ConnectionId,
                    UserId = int.Parse(userId),
                    StoreId = storeId,
                    TotalRecords = 0,
                    ProcessedRecords = 0,
                    FailedRecords = 0
                };
                
                _context.SyncBatches.Add(syncBatch);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Sync batch created with ID: {SyncBatchId} for store {StoreId}", syncBatch.Id, storeId);
                
                // Return the response with batch ID
                return new SyncBatchResponse 
                { 
                    BatchId = syncBatch.Id,
                    StartDate = syncBatch.StartDate,
                    Status = syncBatch.Status
                };
            }
            catch (HubException)
            {
                // Rethrow HubException to preserve original message
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting sync for store {StoreId}", storeId);
                throw new HubException($"Error starting sync: {ex.Message}");
            }
        }
        
        public async Task<EntitySyncResponse> SendChanges(SyncChangesRequest request)
        {
            try
            {
                // Validate request
                if (request == null)
                {
                    throw new HubException("Invalid sync request");
                }
                
                if (string.IsNullOrEmpty(request.EntityName) || 
                    string.IsNullOrEmpty(request.EntityId) || 
                    string.IsNullOrEmpty(request.Operation))
                {
                    throw new HubException("Missing required sync information");
                }
                
                _logger.LogInformation(
                    "Processing sync for entity {EntityName}, ID {EntityId}, operation {Operation}", 
                    request.EntityName, request.EntityId, request.Operation);
                
                var userId = GetUserId();
                var storeId = GetStoreId();
                
                if (!storeId.HasValue)
                {
                    throw new HubException("Store ID not found in token");
                }
                
                // Validate batch access
                var batch = await _context.SyncBatches.FindAsync(request.SyncBatchId);
                if (batch == null || batch.StoreId != storeId.Value)
                {
                    _logger.LogWarning(
                        "User {UserId} attempted to access invalid sync batch {SyncBatchId}", 
                        userId, request.SyncBatchId);
                    throw new HubException("Invalid sync batch ID");
                }
                
                var syncLog = new SyncLog
                {
                    EntityName = request.EntityName,
                    EntityId = request.EntityId,
                    Operation = request.Operation,
                    OperationDate = DateTime.UtcNow,
                    DeviceId = Context.ConnectionId,
                    UserId = int.Parse(userId),
                    StoreId = storeId.Value,
                    SyncStatus = SyncStatus.Syncing,
                    SyncBatchId = request.SyncBatchId,
                    Priority = request.Priority ?? 1
                };
                
                _context.SyncLogs.Add(syncLog);
                await _context.SaveChangesAsync();
                
                // Process sync (in a real app, this would be more complex with validation, etc.)
                try 
                {
                    // TODO: Implement actual entity sync logic here
                    // This would handle specific entity types and operations
                    
                    syncLog.SyncStatus = SyncStatus.Synced;
                    syncLog.SyncDate = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    
                    // Update sync batch statistics
                    batch.TotalRecords++;
                    batch.ProcessedRecords++;
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation(
                        "Successfully synced {EntityName} with ID {EntityId}", 
                        request.EntityName, request.EntityId);
                    
                    return new EntitySyncResponse
                    {
                        Success = true,
                        EntityName = request.EntityName,
                        EntityId = request.EntityId,
                        SyncDate = syncLog.SyncDate.Value
                    };
                }
                catch (Exception ex)
                {
                    // Handle sync processing error
                    syncLog.SyncStatus = SyncStatus.Failed;
                    syncLog.ErrorMessage = ex.Message;
                    await _context.SaveChangesAsync();
                    
                    batch.FailedRecords++;
                    await _context.SaveChangesAsync();
                    
                    _logger.LogError(
                        ex, "Failed to sync {EntityName} with ID {EntityId}", 
                        request.EntityName, request.EntityId);
                    
                    throw new HubException($"Sync processing failed: {ex.Message}");
                }
            }
            catch (HubException)
            {
                // Rethrow HubException to preserve original message
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, "Error in SendChanges for batchId {SyncBatchId}", 
                    request?.SyncBatchId);
                throw new HubException($"Error processing sync: {ex.Message}");
            }
        }
        
        public async Task<SyncBatchResponse> CompleteSyncBatch(int syncBatchId)
        {
            try
            {
                _logger.LogInformation("Completing sync batch {SyncBatchId}", syncBatchId);
                
                var storeId = GetStoreId();
                if (!storeId.HasValue)
                {
                    throw new HubException("Store ID not found in token");
                }
                
                var batch = await _context.SyncBatches.FindAsync(syncBatchId);
                if (batch == null)
                {
                    throw new HubException("Sync batch not found");
                }
                
                if (batch.StoreId != storeId.Value)
                {
                    _logger.LogWarning(
                        "User attempted to complete sync batch {SyncBatchId} belonging to a different store", 
                        syncBatchId);
                    throw new HubException("Unauthorized access to sync batch");
                }
                
                batch.Status = "Completed";
                batch.EndDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                
                _logger.LogInformation(
                    "Sync batch {SyncBatchId} completed. Processed: {Processed}, Failed: {Failed}", 
                    syncBatchId, batch.ProcessedRecords, batch.FailedRecords);
                
                return new SyncBatchResponse
                {
                    BatchId = batch.Id,
                    Status = batch.Status,
                    StartDate = batch.StartDate,
                    EndDate = batch.EndDate,
                    ProcessedRecords = batch.ProcessedRecords,
                    FailedRecords = batch.FailedRecords
                };
            }
            catch (HubException)
            {
                // Rethrow HubException to preserve original message
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing sync batch {SyncBatchId}", syncBatchId);
                throw new HubException($"Error completing sync batch: {ex.Message}");
            }
        }
        
        // Methods for server-to-client sync (when server has updates for clients)
        public async Task RequestClientSync(int storeId)
        {
            try
            {
                _logger.LogInformation("Requesting sync for clients of store {StoreId}", storeId);
                
                // Validate caller's permissions to request sync 
                if (!await HasStoreAccessAsync(storeId))
                {
                    _logger.LogWarning("Unauthorized sync request for store {StoreId}", storeId);
                    throw new HubException("Unauthorized store access");
                }
                
                // Group clients by store for targeted updates
                await Clients.Group($"store_{storeId}").SendAsync("SyncRequested");
                
                _logger.LogInformation("Sync request sent to clients of store {StoreId}", storeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting client sync for store {StoreId}", storeId);
                throw new HubException($"Error requesting client sync: {ex.Message}");
            }
        }
        
        public override async Task OnConnectedAsync()
        {
            try
            {
                var storeId = GetStoreId();
                var userId = GetUserId();
                
                _logger.LogInformation("User {UserId} connected to sync hub", userId);
                
                if (storeId.HasValue)
                {
                    // Add connection to store group for targeted updates
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"store_{storeId.Value}");
                    _logger.LogInformation("Added connection to store group {StoreId}", storeId.Value);
                }
                
                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnConnectedAsync");
                throw;
            }
        }
        
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            _logger.LogInformation(
                "Client disconnected from sync hub. Reason: {Reason}", 
                exception?.Message ?? "Unknown");
                
            await base.OnDisconnectedAsync(exception);
        }
        
        #region Helper Methods
        
        private string GetUserId()
        {
            return Context.User?.FindFirst(USER_ID_CLAIM)?.Value;
        }
        
        private int? GetStoreId()
        {
            var storeIdString = Context.User?.FindFirst(STORE_ID_CLAIM)?.Value;
            if (int.TryParse(storeIdString, out int storeId))
            {
                return storeId;
            }
            return null;
        }
        
        private async Task<bool> HasStoreAccessAsync(int storeId)
        {
            // Check if user has admin role
            if (Context.User.IsInRole("Admin"))
            {
                return true;
            }
            
            // Check if user belongs to the specified store
            var userStoreId = GetStoreId();
            return userStoreId.HasValue && userStoreId.Value == storeId;
        }
        
        #endregion
    }
    
    #region Request/Response DTOs
    
    public class SyncChangesRequest
    {
        public int SyncBatchId { get; set; }
        public string EntityName { get; set; }
        public string EntityId { get; set; }
        public string Operation { get; set; }
        public int? Priority { get; set; }
    }
    
    public class SyncBatchResponse
    {
        public int BatchId { get; set; }
        public string Status { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? ProcessedRecords { get; set; }
        public int? FailedRecords { get; set; }
    }
    
    public class EntitySyncResponse
    {
        public bool Success { get; set; }
        public string EntityName { get; set; }
        public string EntityId { get; set; }
        public DateTime SyncDate { get; set; }
        public string ErrorMessage { get; set; }
    }
    
    #endregion
}
