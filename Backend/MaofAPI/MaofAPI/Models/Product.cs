using System;
using System.Collections.Generic;
using MaofAPI.Models.Enums;

namespace MaofAPI.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Barcode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int CategoryId { get; set; }
        public int StoreId { get; set; } // Products belong to a specific store
        public decimal PurchasePrice { get; set; }
        public decimal SalesPrice { get; set; }
        public decimal TaxRate { get; set; } // Direct tax rate, e.g., 0.18 for 18%
        public int StockQuantity { get; set; }
        public int MinimumStockLevel { get; set; }
        public string ImageUrl { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Fields added for offline-first functionality
        public SyncStatus SyncStatus { get; set; }
        public Guid SyncId { get; set; }
        
        // Navigation Properties
        public virtual Store Store { get; set; }
        public virtual Category Category { get; set; }
        public virtual ICollection<SaleItem> SaleItems { get; set; }
        public virtual ICollection<ProductTransaction> ProductTransactions { get; set; }
        public virtual ICollection<Promotion> Promotions { get; set; }
    }
}
