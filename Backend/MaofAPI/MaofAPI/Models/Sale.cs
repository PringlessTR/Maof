using System;
using System.Collections.Generic;
using MaofAPI.Models.Enums;

namespace MaofAPI.Models
{
    public class Sale
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int StoreId { get; set; } // Add StoreId for multi-tenant support
        public DateTime SaleDate { get; set; }
        public decimal SubTotal { get; set; } // Before tax and discount
        public decimal? DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; } // Final amount to pay
        public string Status { get; set; } // Draft, Completed, Canceled, Partially Paid, Fully Paid, etc.
        public int? CustomerId { get; set; }
        public decimal? EarnedPoints { get; set; }
        public decimal? UsedPoints { get; set; }
        public string Notes { get; set; }
        public int CurrencyId { get; set; }
        public decimal ExchangeRate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Fields added for offline-first functionality
        public SyncStatus SyncStatus { get; set; }
        public Guid SyncId { get; set; }
        
        // Navigation Properties
        public virtual User User { get; set; }
        public virtual Store Store { get; set; } // Add Store navigation property
        public virtual Currency Currency { get; set; }
        public virtual Customer Customer { get; set; } // Customer navigation property
        public virtual ICollection<SaleItem> SaleItems { get; set; }
        public virtual ICollection<Payment> Payments { get; set; }
    }
}
