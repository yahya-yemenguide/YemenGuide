using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class AdminPermission
    {
        public AdminPermission()
        {
            AdminRolePermissions = new HashSet<AdminRolePermission>();
        }

        public int AdminPermissionId { get; set; }
        public string PermissionKey { get; set; } = null!;
        public string PermissionName { get; set; } = null!;
        public string PermissionGroup { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsSensitive { get; set; }
        public bool? IsActive { get; set; }

        public virtual ICollection<AdminRolePermission> AdminRolePermissions { get; set; }
    }
}
