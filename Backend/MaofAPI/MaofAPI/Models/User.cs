using System;
using System.Collections.Generic;
using MaofAPI.Models.Enums;

namespace MaofAPI.Models
{
    public class User
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string PasswordHash { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public bool IsActive { get; set; }
        public int? StoreId { get; set; } // Null for system administrators who can access all stores
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? LastLoginDate { get; set; }
        
        // Fields added for offline-first functionality
        public SyncStatus SyncStatus { get; set; }
        public Guid SyncId { get; set; }
        
        // Navigation Properties
        public virtual Store Store { get; set; }
        public virtual ICollection<UserRole> UserRoles { get; set; }
        public virtual ICollection<Sale> Sales { get; set; }
        public virtual ICollection<SyncLog> SyncLogs { get; set; }
        public virtual ICollection<ProductTransaction> ProductTransactions { get; set; }
        public virtual ICollection<SyncBatch> SyncBatches { get; set; }
    }
}
