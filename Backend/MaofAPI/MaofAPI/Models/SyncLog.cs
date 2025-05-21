using System;
using MaofAPI.Models.Enums;

namespace MaofAPI.Models
{
    public class SyncLog
    {
        public int Id { get; set; }
        public string EntityName { get; set; }
        public string EntityId { get; set; }
        public string Operation { get; set; } // Create, Update, Delete
        public DateTime OperationDate { get; set; }
        public string DeviceId { get; set; }
        public int UserId { get; set; }
        public int StoreId { get; set; } // Add StoreId for multi-store support
        public SyncStatus SyncStatus { get; set; }
        public DateTime? SyncDate { get; set; }
        public string ErrorMessage { get; set; }
        public int RetryCount { get; set; }
        public int? SyncBatchId { get; set; }
        public int Priority { get; set; }
        
        // Navigation Properties
        public virtual User User { get; set; }
        public virtual Store Store { get; set; }
        public virtual SyncBatch SyncBatch { get; set; }
    }
}
