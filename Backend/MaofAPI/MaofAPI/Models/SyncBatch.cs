using System;
using System.Collections.Generic;

namespace MaofAPI.Models
{
    public class SyncBatch
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } // Pending, InProgress, Completed, Failed
        public string DeviceId { get; set; }
        public int UserId { get; set; }
        public int StoreId { get; set; } // Add StoreId for multi-store support
        public int TotalRecords { get; set; }
        public int ProcessedRecords { get; set; }
        public int FailedRecords { get; set; }
        
        // Navigation Properties
        public virtual User User { get; set; }
        public virtual Store Store { get; set; }
        public virtual ICollection<SyncLog> SyncLogs { get; set; }
    }
}
