using System;

namespace MaofAPI.Models.Enums
{
    public enum ProductTransactionType
    {
        // Stock transactions
        StockIn = 1,           // Stock entry
        StockOut = 2,          // Stock exit
        StockAdjustment = 3,   // Stock count/adjustment
        StockReturn = 4,       // Return receipt
        StockTransfer = 5,     // Transfer between warehouses
        StockLoss = 6,         // Waste/loss
        SaleDeduction = 7,     // Stock reduction due to sale
        
        // Price transactions
        PriceChange = 101,     // Normal price change
        PromotionPrice = 102,  // Promotional price
        CostChange = 103,      // Cost price change
        TaxRateChange = 104,   // Tax rate change
        
        // Other product changes
        ProductCreated = 201,  // Product created
        ProductUpdated = 202,  // Product updated
        ProductActivated = 203, // Product activated
        ProductDeactivated = 204 // Product deactivated
    }
}
