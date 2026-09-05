using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class AdminRole
    {
        public AdminRole()
        {
            AdminRolePermissions = new HashSet<AdminRolePermission>();
            Users = new HashSet<User>();
        }

        public int AdminRoleId { get; set; }
        public string RoleName { get; set; } = null!;
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public bool IsSystemRole { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CreatedByUserId { get; set; }

        public virtual User? CreatedByUser { get; set; }
        public virtual ICollection<AdminRolePermission> AdminRolePermissions { get; set; }
        public virtual ICollection<User> Users { get; set; }
    }
}
