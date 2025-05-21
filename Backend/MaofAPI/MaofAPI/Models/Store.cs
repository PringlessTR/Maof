using System;
using System.Collections.Generic;
using MaofAPI.Models.Enums;

namespace MaofAPI.Models
{
    public class Store
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string TaxNumber { get; set; }
        public string ReceiptFooter { get; set; }
        public string ReceiptHeader { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Fields added for offline-first functionality
        public SyncStatus SyncStatus { get; set; }
        public Guid SyncId { get; set; }
        
        // Navigation Properties
        public virtual ICollection<User> Users { get; set; }
        public virtual ICollection<Product> Products { get; set; }
        public virtual ICollection<ProductTransaction> ProductTransactions { get; set; }
        public virtual ICollection<Sale> Sales { get; set; }
        public virtual ICollection<SaleItem> SaleItems { get; set; }
        public virtual ICollection<Payment> Payments { get; set; }
        public virtual ICollection<Promotion> Promotions { get; set; }
        public virtual ICollection<SyncLog> SyncLogs { get; set; }
        public virtual ICollection<SyncBatch> SyncBatches { get; set; }
        public virtual ICollection<AppSetting> AppSettings { get; set; }
        public virtual ICollection<AuditLog> AuditLogs { get; set; }
    }
}
