using System;
using MaofAPI.Models.Enums;

namespace MaofAPI.Models
{
    public class ProductTransaction
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int StoreId { get; set; }
        public ProductTransactionType TransactionType { get; set; }
        
        // Stok değişiklikleri için
        public int? QuantityBefore { get; set; }
        public int? QuantityAfter { get; set; }
        public int? QuantityChange { get; set; }
        
        // Fiyat değişiklikleri için
        public decimal? PriceBefore { get; set; }
        public decimal? PriceAfter { get; set; }
        
        // Maliyet değişiklikleri için
        public decimal? CostBefore { get; set; }
        public decimal? CostAfter { get; set; }
        
        // Vergi oranı değişiklikleri için
        public decimal? TaxRateBefore { get; set; }
        public decimal? TaxRateAfter { get; set; }
        
        // Referans bilgileri
        public int? ReferenceId { get; set; }  // Satış ID, sipariş ID, vb.
        public string ReferenceType { get; set; }  // "Sale", "Order", "Manual", vb.
        public string Notes { get; set; }
        
        // Kullanıcı bilgisi
        public int? UserId { get; set; }  // İşlemi yapan kullanıcı
        
        // İşlem tarihi
        public DateTime TransactionDate { get; set; }
               
        // Navigation Properties
        public virtual Product Product { get; set; }
        public virtual Store Store { get; set; }
        public virtual User User { get; set; }
    }
}
