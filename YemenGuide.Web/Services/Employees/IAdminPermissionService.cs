using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YemenGuide.Web.Services.Employees
{
    public interface IAdminPermissionService
    {
        Task<bool> CanAccessAdminAsync(
            int userId,
            CancellationToken cancellationToken = default);

        Task<bool> IsSuperAdminAsync(
            int userId,
            CancellationToken cancellationToken = default);

        Task<bool> HasPermissionAsync(
            int userId,
            string permissionKey,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<string>> GetPermissionKeysAsync(
            int userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// يعيد موظفي الإدارة النشطين الذين يملكون الصلاحية المحددة حرفيًا،
        /// مع تضمين أدوار النظام النشطة تلقائيًا. تستخدم لتوزيع الإشعارات
        /// الحساسة حسب صلاحية تنفيذ الإجراء، وليس حسب Role = Admin فقط.
        /// </summary>
        Task<IReadOnlyList<int>> GetAdminUserIdsWithPermissionAsync(
            string permissionKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// يعيد اتحاد موظفي الإدارة النشطين الذين يملكون أي صلاحية من المفاتيح
        /// المحددة حرفيًا. System Admin يضاف مرة واحدة فقط، والنتيجة بلا تكرار.
        /// </summary>
        Task<IReadOnlyList<int>> GetAdminUserIdsWithAnyPermissionAsync(
            IReadOnlyCollection<string> permissionKeys,
            CancellationToken cancellationToken = default);
    }
}
