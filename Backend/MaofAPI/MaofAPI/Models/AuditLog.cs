using System;

namespace MaofAPI.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string EntityName { get; set; }
        public string EntityId { get; set; }
        public string Action { get; set; } // Create, Update, Delete
        public string OldValues { get; set; }
        public string NewValues { get; set; }
        public int? UserId { get; set; }
        public int StoreId { get; set; } // Add StoreId for multi-store support
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; }
        
        // Navigation Properties
        public virtual User User { get; set; }
        public virtual Store Store { get; set; }
    }
}
