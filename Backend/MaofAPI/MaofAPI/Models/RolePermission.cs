using System;
using MaofAPI.Models.Enums;

namespace MaofAPI.Models
{
    public class RolePermission
    {
        public int RoleId { get; set; }
        public int PermissionId { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Fields added for offline-first functionality
        public SyncStatus SyncStatus { get; set; }
        public Guid SyncId { get; set; }
        
        // Navigation Properties
        public virtual Role Role { get; set; }
        public virtual Permission Permission { get; set; }
    }
}
