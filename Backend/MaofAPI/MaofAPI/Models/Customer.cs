using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MaofAPI.Models.Enums;

namespace MaofAPI.Models
{
    public class Customer
    {
        public int Id { get; set; }
        public int? CompanyID { get; set; }
        public string PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string Address { get; set; }
        public string? Name { get; set; }
        public string? TaxNumber { get; set; }
        public int? Points { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        // Offline-first için
        public SyncStatus SyncStatus { get; set; }
        public Guid SyncId { get; set; }

        // Navigation Properties
        public virtual ICollection<Sale> Sales { get; set; }
        public virtual Company Company { get; set; }
    }
}