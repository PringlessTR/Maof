using System;
using System.Collections.Generic;
using MaofAPI.Models.Enums;

namespace MaofAPI.Models
{
    public class Permission
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Offline-first için eklenen alanlar
        public SyncStatus SyncStatus { get; set; }
        public Guid SyncId { get; set; }
        
        // Navigation Properties
        public virtual ICollection<RolePermission> RolePermissions { get; set; }
    }
}
