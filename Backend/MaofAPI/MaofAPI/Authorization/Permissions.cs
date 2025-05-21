namespace MaofAPI.Authorization
{
    public static class Permissions
    {
        // Products permissions
        public const string ViewProducts = "products.view";
        public const string CreateProducts = "products.create";
        public const string EditProducts = "products.edit";
        public const string DeleteProducts = "products.delete";
        public const string ManageStock = "products.manage_stock";
        public const string ViewProductHistory = "products.view_history";
        public const string ViewProductPriceHistory = "products.view_price_history";
        
        // Categories permissions
        public const string ViewCategories = "categories.view";
        public const string CreateCategories = "categories.create";
        public const string EditCategories = "categories.edit";
        public const string DeleteCategories = "categories.delete";
        
        // Sales permissions
        public const string ViewSales = "sales.view";
        public const string CreateSales = "sales.create";
        public const string EditSales = "sales.edit";
        public const string DeleteSales = "sales.delete";
        public const string DiscountSales = "sales.apply_discount";
        
        // Promotions permissions
        public const string ViewPromotions = "promotions.view";
        public const string CreatePromotions = "promotions.create";
        public const string EditPromotions = "promotions.edit";
        public const string DeletePromotions = "promotions.delete";
        public const string ManagePromotions = "promotions.manage";
        
        // Users permissions
        public const string ViewUsers = "users.view";
        public const string CreateUsers = "users.create";
        public const string EditUsers = "users.edit";
        public const string DeleteUsers = "users.delete";
        public const string ManageUserRoles = "users.manage_roles";
        public const string ManageRoles = "roles.manage";
        
        // Reports permissions
        public const string ViewReports = "reports.view";
        public const string ExportReports = "reports.export";
        
        // Store settings permissions
        public const string ViewStoreSettings = "store.view_settings";
        public const string EditStoreSettings = "store.edit_settings";
        
        // System permissions
        public const string SyncData = "system.sync_data";
        public const string ViewLogs = "system.view_logs";
        
        // Admin-level permissions
        public const string ManageAllStores = "admin.manage_all_stores";
        public const string ManageSystemSettings = "admin.system_settings";
        public const string SystemSettings = "admin.settings";
    }
}
