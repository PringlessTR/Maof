using System;
using MaofAPI.Models.Enums;

namespace MaofAPI.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public int StoreId { get; set; } // Each payment belongs to a specific store
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } // Cash, Credit Card, Debit Card, etc.
        public string ReferenceNumber { get; set; } // Credit card slip number, check number, etc.
        public DateTime PaymentDate { get; set; }
        public string Status { get; set; } // Approved, Declined, Refunded, etc.
        public int CurrencyId { get; set; }
        public decimal ExchangeRate { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Fields added for offline-first functionality
        public SyncStatus SyncStatus { get; set; }
        public Guid SyncId { get; set; }
        
        // Navigation Properties
        public virtual Sale Sale { get; set; }
        public virtual Currency Currency { get; set; }
        public virtual Store Store { get; set; }
    }
}
