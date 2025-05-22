using System;
using MaofAPI.Models.Enums;

namespace MaofAPI.Models
{
    public class UserRole
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public DateTime CreatedAt { get; set; }
        // Navigation Properties
        public virtual User User { get; set; }
        public virtual Role Role { get; set; }
    }
}
