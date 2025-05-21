using System;
using MaofAPI.Models.Enums;

namespace MaofAPI.Models
{
    public class UserRole
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Fields added for offline-first functionality
        public SyncStatus SyncStatus { get; set; }
        public Guid SyncId { get; set; }
        
        // Navigation Properties
        public virtual User User { get; set; }
        public virtual Role Role { get; set; }
    }
}
