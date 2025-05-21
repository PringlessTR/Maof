using System;
using System.Collections.Generic;
using MaofAPI.Models.Enums;

namespace MaofAPI.Models
{
    public class Currency
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Symbol { get; set; }
        public decimal ExchangeRate { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool IsBaseCurrency { get; set; }
        public bool IsActive { get; set; }
        
        // Fields added for offline-first functionality
        public SyncStatus SyncStatus { get; set; }
        public Guid SyncId { get; set; }
        
        // Navigation Properties
        public virtual ICollection<Sale> Sales { get; set; }
        public virtual ICollection<Payment> Payments { get; set; }
    }
}
