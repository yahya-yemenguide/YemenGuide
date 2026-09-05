using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class AdminRolePermission
    {
        public int AdminRoleId { get; set; }
        public int AdminPermissionId { get; set; }
        public DateTime GrantedAt { get; set; }
        public int? GrantedByUserId { get; set; }

        public virtual AdminPermission AdminPermission { get; set; } = null!;
        public virtual AdminRole AdminRole { get; set; } = null!;
        public virtual User? GrantedByUser { get; set; }
    }
}
