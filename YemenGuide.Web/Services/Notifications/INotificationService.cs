using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YemenGuide.Core.DTOs.Notifications;

namespace YemenGuide.Web.Services.Notifications
{
    public interface INotificationService
    {
        /// <summary>
        /// يضيف الإشعار إلى DbContext فقط بدون SaveChanges.
        /// يستخدم داخل العمليات التي لديها Transaction/SaveChanges خاص بها
        /// حتى يحفظ الحدث والإشعار معًا بشكل ذري.
        /// </summary>
        void Queue(CreateNotificationDto request);

        /// <summary>
        /// ينشئ الإشعار ويحفظه مباشرة.
        /// </summary>
        Task<int> CreateAsync(
            CreateNotificationDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// يضيف إشعارًا واحدًا لكل موظف إدارة نشط يملك الصلاحية المحددة حرفيًا،
        /// مع تضمين System Admin تلقائيًا. لا ينفذ SaveChanges حتى يبقى الحدث
        /// والإشعارات داخل نفس Transaction/SaveChanges في الخدمة المستدعية.
        /// </summary>
        Task<int> QueueForAdminPermissionAsync(
            string permissionKey,
            CreateNotificationDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// يوزع نفس الإشعار على اتحاد أصحاب أي صلاحية من المفاتيح المحددة،
        /// مع إزالة التكرار قبل إنشاء الصفوف. لا ينفذ SaveChanges.
        /// </summary>
        Task<int> QueueForAdminPermissionsAsync(
            IReadOnlyCollection<string> permissionKeys,
            CreateNotificationDto request,
            CancellationToken cancellationToken = default);

        Task<NotificationHeaderDto> GetHeaderAsync(
            int userId,
            int take = 7,
            CancellationToken cancellationToken = default);

        Task<NotificationCenterDto> GetCenterAsync(
            int userId,
            string? filter,
            int page = 1,
            int pageSize = 12,
            int focusNotificationId = 0,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// مركز إشعارات المالك مقيد بالمنشأة المختارة من هيدر المالك.
        /// إشعارات الحساب/الأمان العامة فقط تبقى ظاهرة عبر جميع المنشآت.
        /// </summary>
        Task<NotificationCenterDto> GetOwnerCenterAsync(
            int userId,
            int? activePlaceId,
            string? filter,
            int page = 1,
            int pageSize = 12,
            int focusNotificationId = 0,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// مركز إشعارات الإدارة بفلاتر تناسب العمل الإداري، مع بقاء
        /// نفس جدول الإشعارات وحالة القراءة لكل موظف على حدة.
        /// </summary>
        Task<NotificationCenterDto> GetAdminCenterAsync(
            int userId,
            string? filter,
            int page = 1,
            int pageSize = 12,
            int focusNotificationId = 0,
            CancellationToken cancellationToken = default);

        Task<int> GetUnreadCountAsync(
            int userId,
            CancellationToken cancellationToken = default);

        Task<int> GetOwnerUnreadCountAsync(
            int userId,
            int? activePlaceId,
            CancellationToken cancellationToken = default);

        Task<int> MarkAllOwnerScopeAsReadAsync(
            int userId,
            int? activePlaceId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// يعيد المنشأة الحقيقية المرتبطة بإشعار المالك إن أمكن،
        /// لاستخدامها في مزامنة اختيار المنشأة قبل تنفيذ الـ Deep Link.
        /// </summary>
        Task<int?> ResolveOwnerNotificationPlaceIdAsync(
            int notificationId,
            int ownerId,
            CancellationToken cancellationToken = default);

        Task<bool> MarkAsReadAsync(
            int notificationId,
            int userId,
            CancellationToken cancellationToken = default);

        Task<int> MarkAllAsReadAsync(
            int userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// يعلم الإشعار كمقروء ثم يعيد الرابط الحقيقي.
        /// إذا كان TargetUrl قديمًا/فارغًا يحاول بناء الرابط من ReferenceType/ReferenceId.
        /// </summary>
        Task<string?> MarkAsReadAndGetTargetUrlAsync(
            int notificationId,
            int userId,
            CancellationToken cancellationToken = default);
    }
}
