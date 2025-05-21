using System;
using System.Collections.Generic;
using MaofAPI.Models.Enums;

namespace MaofAPI.Models
{
    public class Promotion
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int StoreId { get; set; } // Promotions belong to a specific store
        public int ProductId { get; set; } // Each promotion is for a single product
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DiscountType DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal? MinimumPurchaseAmount { get; set; }
        public bool IsActive { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Fields added for offline-first functionality
        public SyncStatus SyncStatus { get; set; }
        public Guid SyncId { get; set; }
        
        // Navigation Properties
        public virtual Store Store { get; set; }
        public virtual Product Product { get; set; }
    }
}
