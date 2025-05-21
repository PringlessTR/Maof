using System;

namespace MaofAPI.Models
{
    public class AppSetting
    {
        public int Id { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string Description { get; set; }
        public bool IsEncrypted { get; set; }
        public string SettingGroup { get; set; }
        public int? StoreId { get; set; } // Null for global settings, specific value for store-specific settings
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Navigation Properties
        public virtual Store Store { get; set; }
    }
}
