using System;
using MaofAPI.Models.Enums;

namespace MaofAPI.Models
{
    public class SaleItem
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public int ProductId { get; set; }
        public int StoreId { get; set; } // Each sale item belongs to a specific store
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountRate { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal LineTotal { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Fields added for offline-first functionality
        public SyncStatus SyncStatus { get; set; }
        public Guid SyncId { get; set; }
        
        // Navigation Properties
        public virtual Sale Sale { get; set; }
        public virtual Product Product { get; set; }
        public virtual Store Store { get; set; }
    }
}
