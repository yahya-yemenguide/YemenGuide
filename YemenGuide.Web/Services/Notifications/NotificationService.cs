using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.Entities;
using YemenGuide.Data;
using YemenGuide.Web.Services.Employees;

namespace YemenGuide.Web.Services.Notifications
{
    public class NotificationService : INotificationService
    {
        private const int DefaultHeaderTake = 7;
        private const int MaximumHeaderTake = 20;
        private const int DefaultCenterPageSize = 12;
        private const int MaximumCenterPageSize = 30;

        private static readonly string[] BookingNotificationTypes =
        {
            NotificationTypes.BookingCreated,
            NotificationTypes.BookingAccepted,
            NotificationTypes.BookingConfirmed,
            NotificationTypes.BookingRejected,
            NotificationTypes.BookingCancelled,
            NotificationTypes.BookingExpired,
            "BookingCompleted",
            "BookingNoShow"
        };

        private static readonly string[] PaymentNotificationTypes =
        {
            NotificationTypes.PaymentRequired,
            NotificationTypes.PaymentReceived,
            NotificationTypes.PaymentApproved,
            NotificationTypes.PaymentRejected,
            NotificationTypes.RefundRequested,
            NotificationTypes.RefundReadyForTransfer,
            NotificationTypes.RefundTransferred,
            NotificationTypes.RefundCompleted
        };

        private static readonly string[] ReviewNotificationTypes =
        {
            NotificationTypes.ReviewCreated,
            NotificationTypes.ReviewReply
        };

        private static readonly string[] MessageNotificationTypes =
        {
            NotificationTypes.Message,
            "ChatMessage"
        };

        private static readonly string[] ReportNotificationTypes =
        {
            NotificationTypes.ReportCreated,
            NotificationTypes.ReportReply,
            NotificationTypes.ReportEscalated,
            NotificationTypes.ReportWithdrawn,
            NotificationTypes.ReportResolved,
            NotificationTypes.ReportRejected
        };

        private static readonly string[] AdminCaseNotificationTypes =
        {
            NotificationTypes.ReportCreated,
            NotificationTypes.ReportReply,
            NotificationTypes.ReportEscalated,
            NotificationTypes.ReportWithdrawn,
            NotificationTypes.ReportResolved,
            NotificationTypes.ReportRejected,
            NotificationTypes.AdminCaseActionRequired
        };

        private static readonly string[] AdminFinanceNotificationTypes =
        {
            NotificationTypes.PaymentRequired,
            NotificationTypes.PaymentReceived,
            NotificationTypes.PaymentApproved,
            NotificationTypes.PaymentRejected,
            NotificationTypes.RefundRequested,
            NotificationTypes.RefundReadyForTransfer,
            NotificationTypes.RefundTransferred,
            NotificationTypes.RefundCompleted,
            NotificationTypes.AdminFinanceActionRequired
        };

        private static readonly string[] AdminAdvertisementNotificationTypes =
        {
            NotificationTypes.Promotion,
            NotificationTypes.Advertisement,
            NotificationTypes.AdminAdvertisementActionRequired
        };

        private static readonly string[] AdminManagementNotificationTypes =
        {
            NotificationTypes.Account,
            NotificationTypes.AdminPlaceActionRequired,
            NotificationTypes.AdminAccountActionRequired
        };

        private static readonly string[] AdminSystemNotificationTypes =
        {
            NotificationTypes.General,
            NotificationTypes.Security,
            NotificationTypes.AdminSystemAlert
        };

        private readonly AppDbContext _context;
        private readonly IAdminPermissionService _adminPermissionService;

        public NotificationService(
            AppDbContext context,
            IAdminPermissionService adminPermissionService)
        {
            _context = context;
            _adminPermissionService = adminPermissionService;
        }

        public void Queue(CreateNotificationDto request)
        {
            _context.Notifications.Add(BuildNotification(request));
        }

        public async Task<int> CreateAsync(
            CreateNotificationDto request,
            CancellationToken cancellationToken = default)
        {
            var notification = BuildNotification(request);

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync(cancellationToken);

            return notification.NotificationId;
        }

        public async Task<int> QueueForAdminPermissionAsync(
            string permissionKey,
            CreateNotificationDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var recipients =
                await _adminPermissionService
                    .GetAdminUserIdsWithPermissionAsync(
                        permissionKey,
                        cancellationToken);

            if (recipients.Count == 0)
            {
                return 0;
            }

            var recipientIds = recipients
                .Where(userId => userId > 0)
                .Distinct()
                .ToList();

            foreach (var recipientId in recipientIds)
            {
                Queue(new CreateNotificationDto
                {
                    UserId = recipientId,
                    NotificationTitle = request.NotificationTitle,
                    NotificationText = request.NotificationText,
                    NotificationType = request.NotificationType,
                    TargetUrl = request.TargetUrl,
                    ReferenceType = request.ReferenceType,
                    ReferenceId = request.ReferenceId
                });
            }

            return recipientIds.Count;
        }

        public async Task<int> QueueForAdminPermissionsAsync(
            IReadOnlyCollection<string> permissionKeys,
            CreateNotificationDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(permissionKeys);
            ArgumentNullException.ThrowIfNull(request);

            var keys = permissionKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (keys.Length == 0)
            {
                return 0;
            }

            var recipientIds = new HashSet<int>();

            foreach (var permissionKey in keys)
            {
                var recipients = await _adminPermissionService
                    .GetAdminUserIdsWithPermissionAsync(
                        permissionKey,
                        cancellationToken);

                foreach (var recipientId in recipients)
                {
                    if (recipientId > 0)
                    {
                        recipientIds.Add(recipientId);
                    }
                }
            }

            foreach (var recipientId in recipientIds)
            {
                Queue(new CreateNotificationDto
                {
                    UserId = recipientId,
                    NotificationTitle = request.NotificationTitle,
                    NotificationText = request.NotificationText,
                    NotificationType = request.NotificationType,
                    TargetUrl = request.TargetUrl,
                    ReferenceType = request.ReferenceType,
                    ReferenceId = request.ReferenceId
                });
            }

            return recipientIds.Count;
        }

        public async Task<NotificationHeaderDto> GetHeaderAsync(
            int userId,
            int take = DefaultHeaderTake,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return new NotificationHeaderDto();
            }

            take = Math.Clamp(
                take <= 0 ? DefaultHeaderTake : take,
                1,
                MaximumHeaderTake);

            var unreadCount = await GetUnreadCountAsync(
                userId,
                cancellationToken);

            var items = await _context.Notifications
                .AsNoTracking()
                .Where(notification =>
                    notification.UserId == userId &&
                    notification.NotificationState == "Unread" &&
                    notification.ReadAt == null)
                .OrderByDescending(notification =>
                    notification.CreatedAt)
                .ThenByDescending(notification =>
                    notification.NotificationId)
                .Take(take)
                .Select(notification =>
                    new NotificationItemDto
                    {
                        NotificationId = notification.NotificationId,
                        NotificationTitle = notification.NotificationTitle ?? "إشعار",
                        NotificationText = notification.NotificationText,
                        NotificationType = notification.NotificationType ?? NotificationTypes.General,
                        NotificationState = notification.NotificationState ?? "Unread",
                        IsRead = notification.NotificationState == "Read" || notification.ReadAt != null,
                        CreatedAt = notification.CreatedAt,
                        ReadAt = notification.ReadAt,
                        ReferenceType = notification.ReferenceType,
                        ReferenceId = notification.ReferenceId,
                        TargetUrl = notification.TargetUrl
                    })
                .ToListAsync(cancellationToken);

            SetOpenUrls(items);

            return new NotificationHeaderDto
            {
                UnreadCount = unreadCount,
                Items = items
            };
        }

        public async Task<NotificationCenterDto> GetCenterAsync(
            int userId,
            string? filter,
            int page = 1,
            int pageSize = DefaultCenterPageSize,
            int focusNotificationId = 0,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return new NotificationCenterDto();
            }

            var normalizedFilter = NormalizeCenterFilter(filter);

            page = Math.Max(1, page);

            pageSize = Math.Clamp(
                pageSize <= 0
                    ? DefaultCenterPageSize
                    : pageSize,
                1,
                MaximumCenterPageSize);

            IQueryable<Notification> userQuery =
                _context.Notifications
                    .AsNoTracking()
                    .Where(notification =>
                        notification.UserId == userId);

            /*
             * مهم جداً للأداء:
             *
             * النسخة السابقة كانت تنفذ استعلام Count منفصل لكل تبويب:
             * Total + Unread + Booking + Payment + Review + Message + Report
             * ثم Count آخر للفلتر ثم استعلام العناصر.
             *
             * عند كل ضغطة على تبويب كان هذا يعني عدة رحلات متتالية إلى SQL Server،
             * لذلك كان المتصفح يبقى في حالة Loading قبل ظهور التبويب.
             *
             * الآن نجلب جميع العدادات في استعلام SQL واحد فقط،
             * ثم نجلب عناصر الصفحة في استعلام ثانٍ فقط.
             */
            var summary =
                await userQuery
                    .GroupBy(notification => 1)
                    .Select(group => new
                    {
                        TotalCount =
                            group.Count(),

                        UnreadCount =
                            group.Count(notification =>
                                notification.NotificationState == "Unread" &&
                                notification.ReadAt == null),

                        BookingCount =
                            group.Count(notification =>
                                BookingNotificationTypes.Contains(
                                    notification.NotificationType ??
                                    string.Empty)),

                        PaymentCount =
                            group.Count(notification =>
                                PaymentNotificationTypes.Contains(
                                    notification.NotificationType ??
                                    string.Empty)),

                        ReviewCount =
                            group.Count(notification =>
                                ReviewNotificationTypes.Contains(
                                    notification.NotificationType ??
                                    string.Empty)),

                        MessageCount =
                            group.Count(notification =>
                                MessageNotificationTypes.Contains(
                                    notification.NotificationType ??
                                    string.Empty)),

                        ReportCount =
                            group.Count(notification =>
                                ReportNotificationTypes.Contains(
                                    notification.NotificationType ??
                                    string.Empty))
                    })
                    .FirstOrDefaultAsync(cancellationToken);

            var totalCount =
                summary?.TotalCount ?? 0;

            var unreadCount =
                summary?.UnreadCount ?? 0;

            var bookingCount =
                summary?.BookingCount ?? 0;

            var paymentCount =
                summary?.PaymentCount ?? 0;

            var reviewCount =
                summary?.ReviewCount ?? 0;

            var messageCount =
                summary?.MessageCount ?? 0;

            var reportCount =
                summary?.ReportCount ?? 0;

            /*
             * إذا فُتح إشعار قديم لا يملك TargetUrl صالحاً،
             * نحسب الصفحة التي يوجد فيها فقط في هذه الحالة الخاصة.
             * هذا المسار لا يعمل عند التنقل العادي بين التبويبات.
             */
            if (focusNotificationId > 0 &&
                normalizedFilter == "all")
            {
                var focusRow =
                    await userQuery
                        .Where(notification =>
                            notification.NotificationId ==
                            focusNotificationId)
                        .Select(notification => new
                        {
                            notification.NotificationId,
                            notification.CreatedAt
                        })
                        .FirstOrDefaultAsync(cancellationToken);

                if (focusRow != null)
                {
                    int newerCount;

                    if (focusRow.CreatedAt.HasValue)
                    {
                        newerCount =
                            await userQuery.CountAsync(
                                notification =>
                                    notification.CreatedAt >
                                        focusRow.CreatedAt ||
                                    (
                                        notification.CreatedAt ==
                                            focusRow.CreatedAt &&
                                        notification.NotificationId >
                                            focusRow.NotificationId
                                    ),
                                cancellationToken);
                    }
                    else
                    {
                        newerCount =
                            await userQuery.CountAsync(
                                notification =>
                                    notification.CreatedAt != null ||
                                    (
                                        notification.CreatedAt == null &&
                                        notification.NotificationId >
                                            focusRow.NotificationId
                                    ),
                                cancellationToken);
                    }

                    page =
                        (newerCount / pageSize) + 1;
                }
            }

            IQueryable<Notification> filteredQuery =
                normalizedFilter switch
                {
                    "unread" =>
                        userQuery.Where(notification =>
                            notification.NotificationState == "Unread" &&
                            notification.ReadAt == null),

                    "booking" =>
                        userQuery.Where(notification =>
                            BookingNotificationTypes.Contains(
                                notification.NotificationType ??
                                string.Empty)),

                    "payment" =>
                        userQuery.Where(notification =>
                            PaymentNotificationTypes.Contains(
                                notification.NotificationType ??
                                string.Empty)),

                    "review" =>
                        userQuery.Where(notification =>
                            ReviewNotificationTypes.Contains(
                                notification.NotificationType ??
                                string.Empty)),

                    "message" =>
                        userQuery.Where(notification =>
                            MessageNotificationTypes.Contains(
                                notification.NotificationType ??
                                string.Empty)),

                    "report" =>
                        userQuery.Where(notification =>
                            ReportNotificationTypes.Contains(
                                notification.NotificationType ??
                                string.Empty)),

                    _ => userQuery
                };

            /*
             * لا نحتاج Count إضافي للفلتر:
             * العدد موجود أصلاً داخل summary.
             */
            var filteredCount =
                normalizedFilter switch
                {
                    "unread" => unreadCount,
                    "booking" => bookingCount,
                    "payment" => paymentCount,
                    "review" => reviewCount,
                    "message" => messageCount,
                    "report" => reportCount,
                    _ => totalCount
                };

            var totalPages =
                filteredCount <= 0
                    ? 0
                    : (int)Math.Ceiling(
                        filteredCount /
                        (double)pageSize);

            if (totalPages > 0 &&
                page > totalPages)
            {
                page = totalPages;
            }

            /*
             * الاستعلام الثاني والأخير في التنقل العادي:
             * عناصر الصفحة الحالية فقط.
             */
            var items =
                await filteredQuery
                    .OrderByDescending(notification =>
                        notification.CreatedAt)
                    .ThenByDescending(notification =>
                        notification.NotificationId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(notification =>
                        new NotificationItemDto
                        {
                            NotificationId =
                                notification.NotificationId,

                            NotificationTitle =
                                notification.NotificationTitle ??
                                "إشعار",

                            NotificationText =
                                notification.NotificationText,

                            NotificationType =
                                notification.NotificationType ??
                                NotificationTypes.General,

                            NotificationState =
                                notification.NotificationState ??
                                "Unread",

                            IsRead =
                                notification.NotificationState == "Read" ||
                                notification.ReadAt != null,

                            CreatedAt =
                                notification.CreatedAt,

                            ReadAt =
                                notification.ReadAt,

                            ReferenceType =
                                notification.ReferenceType,

                            ReferenceId =
                                notification.ReferenceId,

                            TargetUrl =
                                notification.TargetUrl
                        })
                    .ToListAsync(cancellationToken);

            SetOpenUrls(items);

            return new NotificationCenterDto
            {
                Filter = normalizedFilter,

                FocusNotificationId =
                    focusNotificationId > 0
                        ? focusNotificationId
                        : 0,

                TotalCount =
                    totalCount,

                UnreadCount =
                    unreadCount,

                BookingCount =
                    bookingCount,

                PaymentCount =
                    paymentCount,

                ReviewCount =
                    reviewCount,

                MessageCount =
                    messageCount,

                ReportCount =
                    reportCount,

                FilteredCount =
                    filteredCount,

                CurrentPage =
                    page,

                PageSize =
                    pageSize,

                TotalPages =
                    totalPages,

                Items =
                    items
            };
        }

        public async Task<NotificationCenterDto> GetOwnerCenterAsync(
            int userId,
            int? activePlaceId,
            string? filter,
            int page = 1,
            int pageSize = DefaultCenterPageSize,
            int focusNotificationId = 0,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return new NotificationCenterDto();
            }

            var normalizedFilter = NormalizeCenterFilter(filter);

            page = Math.Max(1, page);

            pageSize = Math.Clamp(
                pageSize <= 0
                    ? DefaultCenterPageSize
                    : pageSize,
                1,
                MaximumCenterPageSize);

            IQueryable<Notification> userQuery =
                BuildOwnerNotificationQuery(
                    _context.Notifications
                        .AsNoTracking()
                        .Where(notification =>
                            notification.UserId == userId),
                    userId,
                    activePlaceId);

            /*
             * مهم جداً للأداء:
             *
             * مركز المالك يستخدم نفس العدادات بعد تقييد المصدر بالمنشأة المختارة:
             * Total + Unread + Booking + Payment + Review + Message + Report
             * ثم Count آخر للفلتر ثم استعلام العناصر.
             *
             * عند كل ضغطة على تبويب كان هذا يعني عدة رحلات متتالية إلى SQL Server،
             * لذلك كان المتصفح يبقى في حالة Loading قبل ظهور التبويب.
             *
             * الآن نجلب جميع العدادات في استعلام SQL واحد فقط،
             * ثم نجلب عناصر الصفحة في استعلام ثانٍ فقط.
             */
            var summary =
                await userQuery
                    .GroupBy(notification => 1)
                    .Select(group => new
                    {
                        TotalCount =
                            group.Count(),

                        UnreadCount =
                            group.Count(notification =>
                                notification.NotificationState == "Unread" &&
                                notification.ReadAt == null),

                        BookingCount =
                            group.Count(notification =>
                                BookingNotificationTypes.Contains(
                                    notification.NotificationType ??
                                    string.Empty)),

                        PaymentCount =
                            group.Count(notification =>
                                PaymentNotificationTypes.Contains(
                                    notification.NotificationType ??
                                    string.Empty)),

                        ReviewCount =
                            group.Count(notification =>
                                ReviewNotificationTypes.Contains(
                                    notification.NotificationType ??
                                    string.Empty)),

                        MessageCount =
                            group.Count(notification =>
                                MessageNotificationTypes.Contains(
                                    notification.NotificationType ??
                                    string.Empty)),

                        ReportCount =
                            group.Count(notification =>
                                ReportNotificationTypes.Contains(
                                    notification.NotificationType ??
                                    string.Empty))
                    })
                    .FirstOrDefaultAsync(cancellationToken);

            var totalCount =
                summary?.TotalCount ?? 0;

            var unreadCount =
                summary?.UnreadCount ?? 0;

            var bookingCount =
                summary?.BookingCount ?? 0;

            var paymentCount =
                summary?.PaymentCount ?? 0;

            var reviewCount =
                summary?.ReviewCount ?? 0;

            var messageCount =
                summary?.MessageCount ?? 0;

            var reportCount =
                summary?.ReportCount ?? 0;

            /*
             * إذا فُتح إشعار قديم لا يملك TargetUrl صالحاً،
             * نحسب الصفحة التي يوجد فيها فقط في هذه الحالة الخاصة.
             * هذا المسار لا يعمل عند التنقل العادي بين التبويبات.
             */
            if (focusNotificationId > 0 &&
                normalizedFilter == "all")
            {
                var focusRow =
                    await userQuery
                        .Where(notification =>
                            notification.NotificationId ==
                            focusNotificationId)
                        .Select(notification => new
                        {
                            notification.NotificationId,
                            notification.CreatedAt
                        })
                        .FirstOrDefaultAsync(cancellationToken);

                if (focusRow != null)
                {
                    int newerCount;

                    if (focusRow.CreatedAt.HasValue)
                    {
                        newerCount =
                            await userQuery.CountAsync(
                                notification =>
                                    notification.CreatedAt >
                                        focusRow.CreatedAt ||
                                    (
                                        notification.CreatedAt ==
                                            focusRow.CreatedAt &&
                                        notification.NotificationId >
                                            focusRow.NotificationId
                                    ),
                                cancellationToken);
                    }
                    else
                    {
                        newerCount =
                            await userQuery.CountAsync(
                                notification =>
                                    notification.CreatedAt != null ||
                                    (
                                        notification.CreatedAt == null &&
                                        notification.NotificationId >
                                            focusRow.NotificationId
                                    ),
                                cancellationToken);
                    }

                    page =
                        (newerCount / pageSize) + 1;
                }
            }

            IQueryable<Notification> filteredQuery =
                normalizedFilter switch
                {
                    "unread" =>
                        userQuery.Where(notification =>
                            notification.NotificationState == "Unread" &&
                            notification.ReadAt == null),

                    "booking" =>
                        userQuery.Where(notification =>
                            BookingNotificationTypes.Contains(
                                notification.NotificationType ??
                                string.Empty)),

                    "payment" =>
                        userQuery.Where(notification =>
                            PaymentNotificationTypes.Contains(
                                notification.NotificationType ??
                                string.Empty)),

                    "review" =>
                        userQuery.Where(notification =>
                            ReviewNotificationTypes.Contains(
                                notification.NotificationType ??
                                string.Empty)),

                    "message" =>
                        userQuery.Where(notification =>
                            MessageNotificationTypes.Contains(
                                notification.NotificationType ??
                                string.Empty)),

                    "report" =>
                        userQuery.Where(notification =>
                            ReportNotificationTypes.Contains(
                                notification.NotificationType ??
                                string.Empty)),

                    _ => userQuery
                };

            /*
             * لا نحتاج Count إضافي للفلتر:
             * العدد موجود أصلاً داخل summary.
             */
            var filteredCount =
                normalizedFilter switch
                {
                    "unread" => unreadCount,
                    "booking" => bookingCount,
                    "payment" => paymentCount,
                    "review" => reviewCount,
                    "message" => messageCount,
                    "report" => reportCount,
                    _ => totalCount
                };

            var totalPages =
                filteredCount <= 0
                    ? 0
                    : (int)Math.Ceiling(
                        filteredCount /
                        (double)pageSize);

            if (totalPages > 0 &&
                page > totalPages)
            {
                page = totalPages;
            }

            /*
             * الاستعلام الثاني والأخير في التنقل العادي:
             * عناصر الصفحة الحالية فقط.
             */
            var items =
                await filteredQuery
                    .OrderByDescending(notification =>
                        notification.CreatedAt)
                    .ThenByDescending(notification =>
                        notification.NotificationId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(notification =>
                        new NotificationItemDto
                        {
                            NotificationId =
                                notification.NotificationId,

                            NotificationTitle =
                                notification.NotificationTitle ??
                                "إشعار",

                            NotificationText =
                                notification.NotificationText,

                            NotificationType =
                                notification.NotificationType ??
                                NotificationTypes.General,

                            NotificationState =
                                notification.NotificationState ??
                                "Unread",

                            IsRead =
                                notification.NotificationState == "Read" ||
                                notification.ReadAt != null,

                            CreatedAt =
                                notification.CreatedAt,

                            ReadAt =
                                notification.ReadAt,

                            ReferenceType =
                                notification.ReferenceType,

                            ReferenceId =
                                notification.ReferenceId,

                            TargetUrl =
                                notification.TargetUrl
                        })
                    .ToListAsync(cancellationToken);

            SetOpenUrls(items);

            return new NotificationCenterDto
            {
                Filter = normalizedFilter,

                FocusNotificationId =
                    focusNotificationId > 0
                        ? focusNotificationId
                        : 0,

                TotalCount =
                    totalCount,

                UnreadCount =
                    unreadCount,

                BookingCount =
                    bookingCount,

                PaymentCount =
                    paymentCount,

                ReviewCount =
                    reviewCount,

                MessageCount =
                    messageCount,

                ReportCount =
                    reportCount,

                FilteredCount =
                    filteredCount,

                CurrentPage =
                    page,

                PageSize =
                    pageSize,

                TotalPages =
                    totalPages,

                Items =
                    items
            };
        }

        public async Task<NotificationCenterDto> GetAdminCenterAsync(
            int userId,
            string? filter,
            int page = 1,
            int pageSize = DefaultCenterPageSize,
            int focusNotificationId = 0,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return new NotificationCenterDto();
            }

            var normalizedFilter = NormalizeAdminCenterFilter(filter);

            page = Math.Max(1, page);
            pageSize = Math.Clamp(
                pageSize <= 0
                    ? DefaultCenterPageSize
                    : pageSize,
                1,
                MaximumCenterPageSize);

            IQueryable<Notification> userQuery =
                _context.Notifications
                    .AsNoTracking()
                    .Where(notification =>
                        notification.UserId == userId);

            var summary =
                await userQuery
                    .GroupBy(notification => 1)
                    .Select(group => new
                    {
                        TotalCount = group.Count(),

                        UnreadCount =
                            group.Count(notification =>
                                notification.NotificationState == "Unread" &&
                                notification.ReadAt == null),

                        CaseCount =
                            group.Count(notification =>
                                AdminCaseNotificationTypes.Contains(
                                    notification.NotificationType ??
                                    string.Empty)),

                        FinanceCount =
                            group.Count(notification =>
                                AdminFinanceNotificationTypes.Contains(
                                    notification.NotificationType ??
                                    string.Empty)),

                        AdvertisementCount =
                            group.Count(notification =>
                                AdminAdvertisementNotificationTypes.Contains(
                                    notification.NotificationType ??
                                    string.Empty)),

                        ManagementCount =
                            group.Count(notification =>
                                AdminManagementNotificationTypes.Contains(
                                    notification.NotificationType ??
                                    string.Empty)),

                        SystemCount =
                            group.Count(notification =>
                                AdminSystemNotificationTypes.Contains(
                                    notification.NotificationType ??
                                    string.Empty))
                    })
                    .FirstOrDefaultAsync(cancellationToken);

            var totalCount = summary?.TotalCount ?? 0;
            var unreadCount = summary?.UnreadCount ?? 0;
            var caseCount = summary?.CaseCount ?? 0;
            var financeCount = summary?.FinanceCount ?? 0;
            var advertisementCount = summary?.AdvertisementCount ?? 0;
            var managementCount = summary?.ManagementCount ?? 0;
            var systemCount = summary?.SystemCount ?? 0;

            // عند الرجوع إلى إشعار قديم بلا رابط صالح نحدد الصفحة التي يقع فيها
            // حتى يظهر للمستخدم مميزًا بدل رميه إلى أول صفحة.
            if (focusNotificationId > 0 &&
                normalizedFilter == "all")
            {
                var focusRow =
                    await userQuery
                        .Where(notification =>
                            notification.NotificationId ==
                            focusNotificationId)
                        .Select(notification => new
                        {
                            notification.NotificationId,
                            notification.CreatedAt
                        })
                        .FirstOrDefaultAsync(cancellationToken);

                if (focusRow != null)
                {
                    int newerCount;

                    if (focusRow.CreatedAt.HasValue)
                    {
                        newerCount =
                            await userQuery.CountAsync(
                                notification =>
                                    notification.CreatedAt >
                                        focusRow.CreatedAt ||
                                    (
                                        notification.CreatedAt ==
                                            focusRow.CreatedAt &&
                                        notification.NotificationId >
                                            focusRow.NotificationId
                                    ),
                                cancellationToken);
                    }
                    else
                    {
                        newerCount =
                            await userQuery.CountAsync(
                                notification =>
                                    notification.CreatedAt != null ||
                                    (
                                        notification.CreatedAt == null &&
                                        notification.NotificationId >
                                            focusRow.NotificationId
                                    ),
                                cancellationToken);
                    }

                    page = (newerCount / pageSize) + 1;
                }
            }

            IQueryable<Notification> filteredQuery =
                normalizedFilter switch
                {
                    "unread" =>
                        userQuery.Where(notification =>
                            notification.NotificationState == "Unread" &&
                            notification.ReadAt == null),

                    "case" =>
                        userQuery.Where(notification =>
                            AdminCaseNotificationTypes.Contains(
                                notification.NotificationType ??
                                string.Empty)),

                    "finance" =>
                        userQuery.Where(notification =>
                            AdminFinanceNotificationTypes.Contains(
                                notification.NotificationType ??
                                string.Empty)),

                    "advertisement" =>
                        userQuery.Where(notification =>
                            AdminAdvertisementNotificationTypes.Contains(
                                notification.NotificationType ??
                                string.Empty)),

                    "management" =>
                        userQuery.Where(notification =>
                            AdminManagementNotificationTypes.Contains(
                                notification.NotificationType ??
                                string.Empty)),

                    "system" =>
                        userQuery.Where(notification =>
                            AdminSystemNotificationTypes.Contains(
                                notification.NotificationType ??
                                string.Empty)),

                    _ => userQuery
                };

            var filteredCount =
                normalizedFilter switch
                {
                    "unread" => unreadCount,
                    "case" => caseCount,
                    "finance" => financeCount,
                    "advertisement" => advertisementCount,
                    "management" => managementCount,
                    "system" => systemCount,
                    _ => totalCount
                };

            var totalPages =
                filteredCount <= 0
                    ? 0
                    : (int)Math.Ceiling(
                        filteredCount /
                        (double)pageSize);

            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            var items =
                await filteredQuery
                    .OrderByDescending(notification =>
                        notification.CreatedAt)
                    .ThenByDescending(notification =>
                        notification.NotificationId)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(notification =>
                        new NotificationItemDto
                        {
                            NotificationId =
                                notification.NotificationId,
                            NotificationTitle =
                                notification.NotificationTitle ??
                                "إشعار",
                            NotificationText =
                                notification.NotificationText,
                            NotificationType =
                                notification.NotificationType ??
                                NotificationTypes.General,
                            NotificationState =
                                notification.NotificationState ??
                                "Unread",
                            IsRead =
                                notification.NotificationState == "Read" ||
                                notification.ReadAt != null,
                            CreatedAt = notification.CreatedAt,
                            ReadAt = notification.ReadAt,
                            ReferenceType = notification.ReferenceType,
                            ReferenceId = notification.ReferenceId,
                            TargetUrl = notification.TargetUrl
                        })
                    .ToListAsync(cancellationToken);

            SetOpenUrls(items);

            return new NotificationCenterDto
            {
                Filter = normalizedFilter,
                FocusNotificationId =
                    focusNotificationId > 0
                        ? focusNotificationId
                        : 0,
                TotalCount = totalCount,
                UnreadCount = unreadCount,
                CaseCount = caseCount,
                FinanceCount = financeCount,
                AdvertisementCount = advertisementCount,
                ManagementCount = managementCount,
                SystemCount = systemCount,
                FilteredCount = filteredCount,
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                Items = items
            };
        }

        public async Task<int> GetUnreadCountAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return 0;
            }

            return await _context.Notifications
                .AsNoTracking()
                .CountAsync(
                    notification =>
                        notification.UserId == userId &&
                        notification.NotificationState == "Unread" &&
                        notification.ReadAt == null,
                    cancellationToken);
        }

        public async Task<int> GetOwnerUnreadCountAsync(
            int userId,
            int? activePlaceId,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return 0;
            }

            return await BuildOwnerNotificationQuery(
                    _context.Notifications.AsNoTracking(),
                    userId,
                    activePlaceId)
                .CountAsync(
                    notification =>
                        notification.NotificationState == "Unread" &&
                        notification.ReadAt == null,
                    cancellationToken);
        }

        public async Task<int> MarkAllOwnerScopeAsReadAsync(
            int userId,
            int? activePlaceId,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return 0;
            }

            var unreadNotifications = await BuildOwnerNotificationQuery(
                    _context.Notifications,
                    userId,
                    activePlaceId)
                .Where(notification =>
                    notification.NotificationState == "Unread" &&
                    notification.ReadAt == null)
                .ToListAsync(cancellationToken);

            if (unreadNotifications.Count == 0)
            {
                return 0;
            }

            var readAt = DateTime.Now;

            foreach (var notification in unreadNotifications)
            {
                notification.NotificationState = "Read";
                notification.ReadAt = readAt;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return unreadNotifications.Count;
        }

        public async Task<int?> ResolveOwnerNotificationPlaceIdAsync(
            int notificationId,
            int ownerId,
            CancellationToken cancellationToken = default)
        {
            if (notificationId <= 0 || ownerId <= 0)
            {
                return null;
            }

            var notification = await _context.Notifications
                .AsNoTracking()
                .Where(item =>
                    item.NotificationId == notificationId &&
                    item.UserId == ownerId)
                .Select(item => new
                {
                    item.ReferenceType,
                    item.ReferenceId
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (notification == null ||
                string.IsNullOrWhiteSpace(notification.ReferenceType) ||
                !notification.ReferenceId.HasValue ||
                notification.ReferenceId.Value <= 0)
            {
                return null;
            }

            var referenceType = notification.ReferenceType.Trim();
            var referenceId = notification.ReferenceId.Value;

            if (string.Equals(referenceType, NotificationReferenceTypes.Booking, StringComparison.OrdinalIgnoreCase))
            {
                return await _context.Bookings
                    .AsNoTracking()
                    .Where(item =>
                        item.BookingId == referenceId &&
                        item.Place.OwnerId == ownerId)
                    .Select(item => (int?)item.PlaceId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (string.Equals(referenceType, NotificationReferenceTypes.Review, StringComparison.OrdinalIgnoreCase))
            {
                return await _context.Reviews
                    .AsNoTracking()
                    .Where(item =>
                        item.ReviewId == referenceId &&
                        item.Place.OwnerId == ownerId)
                    .Select(item => (int?)item.PlaceId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (string.Equals(referenceType, NotificationReferenceTypes.Chat, StringComparison.OrdinalIgnoreCase))
            {
                return await _context.Chats
                    .AsNoTracking()
                    .Where(item =>
                        item.MessageId == referenceId &&
                        item.PlaceId.HasValue &&
                        (item.SenderId == ownerId || item.ReceiverId == ownerId) &&
                        _context.Places.Any(place =>
                            place.PlaceId == item.PlaceId.Value &&
                            place.OwnerId == ownerId))
                    .Select(item => item.PlaceId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (string.Equals(referenceType, NotificationReferenceTypes.Report, StringComparison.OrdinalIgnoreCase))
            {
                return await ResolveOwnerReportPlaceIdAsync(
                    referenceId,
                    ownerId,
                    cancellationToken);
            }

            if (string.Equals(referenceType, NotificationReferenceTypes.Promotion, StringComparison.OrdinalIgnoreCase))
            {
                return await _context.Promotions
                    .AsNoTracking()
                    .Where(item =>
                        item.PromotionId == referenceId &&
                        item.OwnerId == ownerId)
                    .Select(item => (int?)item.PlaceId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (string.Equals(referenceType, NotificationReferenceTypes.Advertisement, StringComparison.OrdinalIgnoreCase))
            {
                return await _context.Advertisements
                    .AsNoTracking()
                    .Where(item =>
                        item.AdvertisementId == referenceId &&
                        item.OwnerId == ownerId)
                    .Select(item => (int?)item.PlaceId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (string.Equals(referenceType, NotificationReferenceTypes.Place, StringComparison.OrdinalIgnoreCase))
            {
                return await _context.Places
                    .AsNoTracking()
                    .Where(item =>
                        item.PlaceId == referenceId &&
                        item.OwnerId == ownerId)
                    .Select(item => (int?)item.PlaceId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (string.Equals(referenceType, NotificationReferenceTypes.CommissionTransaction, StringComparison.OrdinalIgnoreCase))
            {
                return await _context.CommissionTransactions
                    .AsNoTracking()
                    .Where(item =>
                        item.CommissionTransactionId == referenceId &&
                        item.Place.OwnerId == ownerId)
                    .Select(item => (int?)item.PlaceId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return null;
        }

        public async Task<bool> MarkAsReadAsync(
            int notificationId,
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (notificationId <= 0 || userId <= 0)
            {
                return false;
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(
                    item =>
                        item.NotificationId == notificationId &&
                        item.UserId == userId,
                    cancellationToken);

            if (notification == null)
            {
                return false;
            }

            if (!IsRead(notification))
            {
                notification.NotificationState = "Read";
                notification.ReadAt = DateTime.Now;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return true;
        }

        public async Task<int> MarkAllAsReadAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return 0;
            }

            var unreadNotifications = await _context.Notifications
                .Where(notification =>
                    notification.UserId == userId &&
                    notification.NotificationState == "Unread" &&
                    notification.ReadAt == null)
                .ToListAsync(cancellationToken);

            if (unreadNotifications.Count == 0)
            {
                return 0;
            }

            var readAt = DateTime.Now;

            foreach (var notification in unreadNotifications)
            {
                notification.NotificationState = "Read";
                notification.ReadAt = readAt;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return unreadNotifications.Count;
        }

        public async Task<string?> MarkAsReadAndGetTargetUrlAsync(
            int notificationId,
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (notificationId <= 0 || userId <= 0)
            {
                return null;
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(
                    item =>
                        item.NotificationId == notificationId &&
                        item.UserId == userId,
                    cancellationToken);

            if (notification == null)
            {
                return null;
            }

            if (!IsRead(notification))
            {
                notification.NotificationState = "Read";
                notification.ReadAt = DateTime.Now;
                await _context.SaveChangesAsync(cancellationToken);
            }

            var directTarget =
                NormalizeLegacyOwnerTargetUrl(
                    NormalizeTargetUrl(
                        notification.TargetUrl));

            /*
             * إشعارات المالك Structured deep links: إذا كان لدينا Reference صالح
             * نحل الوجهة من السجل الحقيقي أولاً، حتى لا يربح TargetUrl قديم أو عام.
             * هذا هو ما يجعل الرسالة تفتح محادثتها، والدفع يفتح الحجز الصحيح،
             * والتقييم/الإعلان/المنشأة تفتح العنصر نفسه.
             *
             * للأدوار الأخرى نحافظ على السلوك السابق حتى لا نتسبب في Regression
             * في إشعارات الإدارة أو العميل.
             */
            var referenceType =
                notification.ReferenceType?.Trim();

            var hasValidReference =
                notification.ReferenceId.HasValue &&
                notification.ReferenceId.Value > 0;

            var currentUserRole = await _context.Users
                .AsNoTracking()
                .Where(user => user.UserId == userId)
                .Select(user => user.Role)
                .FirstOrDefaultAsync(cancellationToken);

            var isOwner = string.Equals(
                currentUserRole,
                "Owner",
                StringComparison.OrdinalIgnoreCase);

            var isReviewReference =
                string.Equals(
                    referenceType,
                    NotificationReferenceTypes.Review,
                    StringComparison.OrdinalIgnoreCase);

            var isOwnerStructuredReference =
                !string.IsNullOrWhiteSpace(directTarget) &&
                directTarget.StartsWith(
                    "/Owner/",
                    StringComparison.OrdinalIgnoreCase) &&
                (
                    string.Equals(referenceType, NotificationReferenceTypes.Booking, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(referenceType, NotificationReferenceTypes.Report, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(referenceType, NotificationReferenceTypes.Chat, StringComparison.OrdinalIgnoreCase)
                );

            if (hasValidReference &&
                (isOwner || isReviewReference || isOwnerStructuredReference))
            {
                var resolvedStructuredTarget =
                    await ResolveReferenceTargetUrlAsync(
                        notification,
                        userId,
                        cancellationToken);

                if (!string.IsNullOrWhiteSpace(resolvedStructuredTarget))
                {
                    return resolvedStructuredTarget;
                }
            }

            if (!string.IsNullOrWhiteSpace(
                    directTarget))
            {
                return directTarget;
            }

            var resolvedReferenceTarget =
                await ResolveReferenceTargetUrlAsync(
                    notification,
                    userId,
                    cancellationToken);

            if (!string.IsNullOrWhiteSpace(resolvedReferenceTarget))
            {
                return resolvedReferenceTarget;
            }

            // دعم الإشعارات القديمة التي كان ReservationController يحفظها كنص فقط
            // بدون TargetUrl/Reference. نستخدمها فقط للعميل، وبعد التحقق من ملكية الحجز.
            if (string.Equals(
                    currentUserRole,
                    "Customer",
                    StringComparison.OrdinalIgnoreCase))
            {
                return await ResolveLegacyCustomerTargetUrlAsync(
                    notification,
                    userId,
                    cancellationToken);
            }

            return null;
        }

        private async Task<string?> ResolveLegacyCustomerTargetUrlAsync(
            Notification notification,
            int customerId,
            CancellationToken cancellationToken)
        {
            var text = notification.NotificationText?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // صيغ الإشعارات القديمة كلها تحتوي "الحجز رقم N" أو "للحجز رقم N".
            var numberStart = text.IndexOf("رقم", StringComparison.OrdinalIgnoreCase);

            if (numberStart < 0)
            {
                return null;
            }

            numberStart += 3;

            while (numberStart < text.Length &&
                   !char.IsDigit(text[numberStart]))
            {
                numberStart++;
            }

            if (numberStart >= text.Length)
            {
                return null;
            }

            var numberEnd = numberStart;

            while (numberEnd < text.Length &&
                   char.IsDigit(text[numberEnd]))
            {
                numberEnd++;
            }

            if (!int.TryParse(
                    text.Substring(numberStart, numberEnd - numberStart),
                    out var bookingId) ||
                bookingId <= 0)
            {
                return null;
            }

            var booking = await _context.Bookings
                .AsNoTracking()
                .Where(item =>
                    item.BookingId == bookingId &&
                    item.CustomerId == customerId)
                .Select(item => new
                {
                    item.BookingId,
                    item.BookingState
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (booking == null)
            {
                return null;
            }

            var paymentAction =
                text.Contains("دفع العربون", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("رفع إشعار جديد", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("رفع إثبات جديد", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    booking.BookingState,
                    "AwaitingPayment",
                    StringComparison.OrdinalIgnoreCase);

            return paymentAction
                ? $"/BookingFlow/Payment/{booking.BookingId}"
                : $"/Places/BookingDetails/{booking.BookingId}";
        }

        private async Task<string?> ResolveReferenceTargetUrlAsync(
            Notification notification,
            int userId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(notification.ReferenceType) ||
                !notification.ReferenceId.HasValue ||
                notification.ReferenceId.Value <= 0)
            {
                return null;
            }

            var referenceType = notification.ReferenceType.Trim();
            var referenceId = notification.ReferenceId.Value;

            var userRole = await _context.Users
                .AsNoTracking()
                .Where(user => user.UserId == userId)
                .Select(user => user.Role)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.Equals(
                    referenceType,
                    NotificationReferenceTypes.Booking,
                    StringComparison.OrdinalIgnoreCase))
            {
                var isAdmin = string.Equals(
                    userRole,
                    "Admin",
                    StringComparison.OrdinalIgnoreCase);

                var booking = await _context.Bookings
                    .AsNoTracking()
                    .Where(item =>
                        item.BookingId == referenceId)
                    .Select(item => new
                    {
                        item.BookingId,
                        item.CustomerId,
                        OwnerId = item.Place.OwnerId,
                        item.BookingState
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (booking == null ||
                    (
                        booking.CustomerId != userId &&
                        booking.OwnerId != userId &&
                        !isAdmin
                    ))
                {
                    return null;
                }

                if (string.Equals(userRole, "Owner", StringComparison.OrdinalIgnoreCase))
                {
                    // الاسترداد وظيفة مالية مستقلة: نفتح تبويب الاستردادات
                    // ونمرر الحجز نفسه حتى تُفتح بطاقة الطلب المرتبطة به.
                    if (IsRefundNotification(notification.NotificationType))
                    {
                        return
                            $"/Owner/Financials?financeTab=refunds" +
                            $"&BookingId={referenceId}" +
                            $"&focusBookingId={referenceId}";
                    }

                    var bookingState = booking.BookingState?.Trim();

                    // إثبات دفع جديد يبقى في Queue المراجعة حتى لو كان هناك رابط قديم
                    // أو localStorage يشير إلى تبويب آخر.
                    var reservationTab =
                        string.Equals(
                            notification.NotificationType,
                            NotificationTypes.PaymentReceived,
                            StringComparison.OrdinalIgnoreCase)
                            ? "pending-requests-tab"
                            : bookingState switch
                            {
                                "Completed" => "reservation-history-tab",
                                "Rejected" => "reservation-history-tab",
                                "Cancelled" => "reservation-history-tab",
                                "NoShow" => "reservation-history-tab",
                                "Expired" => "reservation-history-tab",
                                "Confirmed" => "active-reservations-tab",
                                _ => "pending-requests-tab"
                            };

                    return
                        $"/Owner/Reservation/ManageReservations" +
                        $"?reservationTab={reservationTab}" +
                        $"&bookingId={referenceId}";
                }

                if (string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return "/Admin/Financials/Manage";
                }

                if (IsPaymentAction(notification.NotificationType))
                {
                    return $"/BookingFlow/Payment/{referenceId}";
                }

                return $"/Places/BookingDetails/{referenceId}";
            }

            if (string.Equals(
                    referenceType,
                    NotificationReferenceTypes.Review,
                    StringComparison.OrdinalIgnoreCase))
            {
                var review = await _context.Reviews
                    .AsNoTracking()
                    .Where(item => item.ReviewId == referenceId)
                    .Select(item => new
                    {
                        item.ReviewId,
                        item.PlaceId,
                        item.ParentReviewId,
                        PlaceOwnerId = item.Place.OwnerId
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (review == null)
                {
                    return null;
                }

                var rootReviewId = await ResolveRootReviewIdAsync(
                    review.ReviewId,
                    review.ParentReviewId,
                    cancellationToken);

                var replyPart = review.ParentReviewId.HasValue
                    ? $"&replyId={review.ReviewId}"
                    : string.Empty;

                if (string.Equals(userRole, "Owner", StringComparison.OrdinalIgnoreCase) &&
                    review.PlaceOwnerId == userId)
                {
                    return
                        $"/Owner/Chat/ChatInterface?communicationTab=reviews" +
                        $"&reviewId={rootReviewId}{replyPart}";
                }

                return $"/Places/Details/{review.PlaceId}?reviewId={rootReviewId}{replyPart}";
            }

            if (string.Equals(
                    referenceType,
                    NotificationReferenceTypes.Report,
                    StringComparison.OrdinalIgnoreCase))
            {
                var report = await _context.Reports
                    .AsNoTracking()
                    .Where(item => item.ReportId == referenceId)
                    .Select(item => new
                    {
                        item.ReportId,
                        item.ReportNumber,
                        item.ReporterId,
                        item.AgainstUserId
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (report == null)
                {
                    return null;
                }

                if (string.Equals(userRole, "Owner", StringComparison.OrdinalIgnoreCase))
                {
                    // قد يكون البلاغ موجهاً لمنشأة/حجز/تقييم يملكها المالك
                    // بدون أن يكون AgainstUserId مساوياً للمالك مباشرة.
                    // لذلك نتحقق من المنشأة الحقيقية المرتبطة بالقضية.
                    var ownerReportPlaceId = await ResolveOwnerReportPlaceIdAsync(
                        report.ReportId,
                        userId,
                        cancellationToken);

                    if (!ownerReportPlaceId.HasValue)
                    {
                        return null;
                    }

                    return $"/Owner/Reports/OwnerReportDetails/{report.ReportId}";
                }

                if (string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return $"/Admin/Reports/Details/{report.ReportId}";
                }

                if (report.ReporterId != userId)
                {
                    return null;
                }

                return $"/Reports/ReportDetails/{report.ReportId}";
            }

            if (string.Equals(
            referenceType,
            NotificationReferenceTypes.Chat,
            StringComparison.OrdinalIgnoreCase))
            {
                var chat = await _context.Chats
                    .AsNoTracking()
                    .Where(item => item.MessageId == referenceId)
                    .Select(item => new
                    {
                        item.MessageId,
                        item.SenderId,
                        item.ReceiverId,
                        item.PlaceId
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (chat == null ||
                    (chat.SenderId != userId && chat.ReceiverId != userId))
                {
                    return null;
                }

                var otherUserId = chat.SenderId == userId
                    ? chat.ReceiverId
                    : chat.SenderId;


                // نتحقق هل المستخدم مالك أو أدمن يملك منشأة
                var hasOwnedPlace = await _context.Places
                    .AsNoTracking()
                    .AnyAsync(
                        place => place.OwnerId == userId,
                        cancellationToken);


                if (string.Equals(userRole, "Owner", StringComparison.OrdinalIgnoreCase)
                    || hasOwnedPlace)
                {
                    var placePart = chat.PlaceId.HasValue && chat.PlaceId.Value > 0
                        ? $"&placeId={chat.PlaceId.Value}"
                        : string.Empty;

                    return
                        $"/Owner/Chat/ChatInterface?communicationTab=messages" +
                        $"&withUserId={otherUserId}" +
                        $"&messageId={chat.MessageId}" +
                        placePart;
                }


                // العميل يفتح رسائل العميل
                if (chat.PlaceId.HasValue)
                {
                    return
                        $"/Messages/MyMessages?placeId={chat.PlaceId.Value}" +
                        $"&withOwnerId={otherUserId}" +
                        $"&messageId={chat.MessageId}";
                }


                return
                    $"/Messages/MyMessages?withOwnerId={otherUserId}" +
                    $"&messageId={chat.MessageId}";
            }

            if (string.Equals(
                    referenceType,
                    NotificationReferenceTypes.Promotion,
                    StringComparison.OrdinalIgnoreCase))
            {
                var promotion = await _context.Promotions
                    .AsNoTracking()
                    .Where(item => item.PromotionId == referenceId)
                    .Select(item => new
                    {
                        item.PromotionId,
                        item.PlaceId,
                        item.OwnerId
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (promotion == null)
                {
                    return null;
                }

                if (string.Equals(userRole, "Owner", StringComparison.OrdinalIgnoreCase) &&
                    promotion.OwnerId == userId)
                {
                    return
                        $"/Owner/Place/ManagePlaces?placeTab=promotions-manager-tab" +
                        $"&promotionId={promotion.PromotionId}";
                }

                return $"/Places/Details/{promotion.PlaceId}";
            }

            if (string.Equals(
                    referenceType,
                    NotificationReferenceTypes.Advertisement,
                    StringComparison.OrdinalIgnoreCase))
            {
                var advertisement = await _context.Advertisements
                    .AsNoTracking()
                    .Where(item => item.AdvertisementId == referenceId)
                    .Select(item => new
                    {
                        item.AdvertisementId,
                        item.PlaceId,
                        item.OwnerId
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (advertisement == null)
                {
                    return null;
                }

                if (string.Equals(
                        userRole,
                        "Admin",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return "/Admin/Advertisements/AdvertisementManagement?tab=ads";
                }

                if (string.Equals(userRole, "Owner", StringComparison.OrdinalIgnoreCase) &&
                    advertisement.OwnerId == userId)
                {
                    return
                        $"/Owner/Advertisements/AdvertisementManagement?tab=advertisements" +
                        $"&advertisementId={advertisement.AdvertisementId}";
                }

                return $"/Places/Details/{advertisement.PlaceId}";
            }

            if (string.Equals(
                    referenceType,
                    NotificationReferenceTypes.User,
                    StringComparison.OrdinalIgnoreCase))
            {
                var userExists = await _context.Users
                    .AsNoTracking()
                    .AnyAsync(
                        user => user.UserId == referenceId,
                        cancellationToken);

                if (!userExists)
                {
                    return null;
                }

                return string.Equals(
                        userRole,
                        "Admin",
                        StringComparison.OrdinalIgnoreCase)
                    ? "/Admin/Users/UserManagement"
                    : null;
            }

            if (string.Equals(
                    referenceType,
                    NotificationReferenceTypes.Place,
                    StringComparison.OrdinalIgnoreCase))
            {
                var place = await _context.Places
                    .AsNoTracking()
                    .Where(item => item.PlaceId == referenceId)
                    .Select(item => new
                    {
                        item.PlaceId,
                        item.OwnerId
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (place == null)
                {
                    return null;
                }

                if (string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return $"/Admin/Places/PlaceManagement?focusPlace={referenceId}";
                }

                if (string.Equals(userRole, "Owner", StringComparison.OrdinalIgnoreCase) &&
                    place.OwnerId == userId)
                {
                    return
                        $"/Owner/Place/ManagePlaces?placeTab=place-list-tab" +
                        $"&focusPlace={referenceId}";
                }

                return $"/Places/Details/{referenceId}";
            }

            if (string.Equals(
                    referenceType,
                    NotificationReferenceTypes.CommissionTransaction,
                    StringComparison.OrdinalIgnoreCase))
            {
                var transaction =
                    await _context.CommissionTransactions
                        .AsNoTracking()
                        .Where(item =>
                            item.CommissionTransactionId == referenceId)
                        .Select(item => new
                        {
                            item.CommissionTransactionId,
                            item.PlaceId,
                            OwnerId = item.Place.OwnerId
                        })
                        .FirstOrDefaultAsync(cancellationToken);

                if (transaction == null)
                {
                    return null;
                }

                if (string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return "/Admin/Financials/Manage?financeTab=transactions";
                }

                if (string.Equals(userRole, "Owner", StringComparison.OrdinalIgnoreCase))
                {
                    if (transaction.OwnerId != userId)
                    {
                        return null;
                    }

                    return
                        $"/Owner/Financials?financeTab=balance" +
                        $"&Search={referenceId}" +
                        $"&focusTransactionId={referenceId}";
                }

                return null;
            }

            return null;
        }

        private IQueryable<Notification> BuildOwnerNotificationQuery(
            IQueryable<Notification> query,
            int ownerId,
            int? activePlaceId)
        {
            // الإشعارات العامة الخاصة بالحساب/الأمان لا تنتمي إلى منشأة محددة.
            // أما إشعارات الحجوزات/الدفع/البلاغات/المحادثات... فلا تظهر
            // إلا إذا استطعنا إثبات أنها تخص المنشأة المختارة في هيدر المالك.
            var ownerWideQuery = query.Where(notification =>
                notification.UserId == ownerId &&
                (
                    (
                        (notification.ReferenceType == null || notification.ReferenceType == string.Empty) &&
                        (
                            notification.NotificationType == NotificationTypes.General ||
                            notification.NotificationType == NotificationTypes.Account ||
                            notification.NotificationType == NotificationTypes.Security
                        )
                    ) ||
                    (
                        notification.ReferenceType == NotificationReferenceTypes.User &&
                        notification.ReferenceId == ownerId
                    )
                ));

            if (!activePlaceId.HasValue || activePlaceId.Value <= 0)
            {
                return ownerWideQuery;
            }

            var placeId = activePlaceId.Value;

            var placeScopedQuery = query.Where(notification =>
                notification.UserId == ownerId &&
                notification.ReferenceId.HasValue &&
                (
                    (
                        notification.ReferenceType == NotificationReferenceTypes.Booking &&
                        _context.Bookings.Any(booking =>
                            booking.BookingId == notification.ReferenceId.Value &&
                            booking.PlaceId == placeId &&
                            booking.Place.OwnerId == ownerId)
                    ) ||
                    (
                        notification.ReferenceType == NotificationReferenceTypes.Review &&
                        _context.Reviews.Any(review =>
                            review.ReviewId == notification.ReferenceId.Value &&
                            review.PlaceId == placeId &&
                            review.Place.OwnerId == ownerId)
                    ) ||
                    (
                        notification.ReferenceType == NotificationReferenceTypes.Chat &&
                        _context.Chats.Any(chat =>
                            chat.MessageId == notification.ReferenceId.Value &&
                            chat.PlaceId == placeId &&
                            (chat.SenderId == ownerId || chat.ReceiverId == ownerId) &&
                            _context.Places.Any(place =>
                                place.PlaceId == placeId &&
                                place.OwnerId == ownerId))
                    ) ||
                    (
                        notification.ReferenceType == NotificationReferenceTypes.Report &&
                        _context.Reports.Any(report =>
                            report.ReportId == notification.ReferenceId.Value &&
                            (
                                (
                                    report.PlaceId == placeId &&
                                    _context.Places.Any(place =>
                                        place.PlaceId == placeId &&
                                        place.OwnerId == ownerId)
                                ) ||
                                (
                                    report.BookingId.HasValue &&
                                    _context.Bookings.Any(booking =>
                                        booking.BookingId == report.BookingId.Value &&
                                        booking.PlaceId == placeId &&
                                        booking.Place.OwnerId == ownerId)
                                ) ||
                                (
                                    report.ReviewId.HasValue &&
                                    _context.Reviews.Any(review =>
                                        review.ReviewId == report.ReviewId.Value &&
                                        review.PlaceId == placeId &&
                                        review.Place.OwnerId == ownerId)
                                ) ||
                                (
                                    report.PromotionId.HasValue &&
                                    _context.Promotions.Any(promotion =>
                                        promotion.PromotionId == report.PromotionId.Value &&
                                        promotion.PlaceId == placeId &&
                                        promotion.OwnerId == ownerId)
                                ) ||
                                (
                                    report.AdvertisementId.HasValue &&
                                    _context.Advertisements.Any(advertisement =>
                                        advertisement.AdvertisementId == report.AdvertisementId.Value &&
                                        advertisement.PlaceId == placeId &&
                                        advertisement.OwnerId == ownerId)
                                )
                            ))
                    ) ||
                    (
                        notification.ReferenceType == NotificationReferenceTypes.Promotion &&
                        _context.Promotions.Any(promotion =>
                            promotion.PromotionId == notification.ReferenceId.Value &&
                            promotion.PlaceId == placeId &&
                            promotion.OwnerId == ownerId)
                    ) ||
                    (
                        notification.ReferenceType == NotificationReferenceTypes.Advertisement &&
                        _context.Advertisements.Any(advertisement =>
                            advertisement.AdvertisementId == notification.ReferenceId.Value &&
                            advertisement.PlaceId == placeId &&
                            advertisement.OwnerId == ownerId)
                    ) ||
                    (
                        notification.ReferenceType == NotificationReferenceTypes.Place &&
                        notification.ReferenceId == placeId &&
                        _context.Places.Any(place =>
                            place.PlaceId == placeId &&
                            place.OwnerId == ownerId)
                    ) ||
                    (
                        notification.ReferenceType == NotificationReferenceTypes.CommissionTransaction &&
                        _context.CommissionTransactions.Any(transaction =>
                            transaction.CommissionTransactionId == notification.ReferenceId.Value &&
                            transaction.PlaceId == placeId &&
                            transaction.Place.OwnerId == ownerId)
                    )
                ));

            return ownerWideQuery.Concat(placeScopedQuery);
        }

        private async Task<int?> ResolveOwnerReportPlaceIdAsync(
            int reportId,
            int ownerId,
            CancellationToken cancellationToken)
        {
            var report = await _context.Reports
                .AsNoTracking()
                .Where(item => item.ReportId == reportId)
                .Select(item => new
                {
                    item.PlaceId,
                    item.BookingId,
                    item.ReviewId,
                    item.PromotionId,
                    item.AdvertisementId
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (report == null)
            {
                return null;
            }

            if (report.PlaceId.HasValue)
            {
                var directPlaceId = await _context.Places
                    .AsNoTracking()
                    .Where(place =>
                        place.PlaceId == report.PlaceId.Value &&
                        place.OwnerId == ownerId)
                    .Select(place => (int?)place.PlaceId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (directPlaceId.HasValue)
                {
                    return directPlaceId;
                }
            }

            if (report.BookingId.HasValue)
            {
                var bookingPlaceId = await _context.Bookings
                    .AsNoTracking()
                    .Where(booking =>
                        booking.BookingId == report.BookingId.Value &&
                        booking.Place.OwnerId == ownerId)
                    .Select(booking => (int?)booking.PlaceId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (bookingPlaceId.HasValue)
                {
                    return bookingPlaceId;
                }
            }

            if (report.ReviewId.HasValue)
            {
                var reviewPlaceId = await _context.Reviews
                    .AsNoTracking()
                    .Where(review =>
                        review.ReviewId == report.ReviewId.Value &&
                        review.Place.OwnerId == ownerId)
                    .Select(review => (int?)review.PlaceId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (reviewPlaceId.HasValue)
                {
                    return reviewPlaceId;
                }
            }

            if (report.PromotionId.HasValue)
            {
                var promotionPlaceId = await _context.Promotions
                    .AsNoTracking()
                    .Where(promotion =>
                        promotion.PromotionId == report.PromotionId.Value &&
                        promotion.OwnerId == ownerId)
                    .Select(promotion => (int?)promotion.PlaceId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (promotionPlaceId.HasValue)
                {
                    return promotionPlaceId;
                }
            }

            if (report.AdvertisementId.HasValue)
            {
                var advertisementPlaceId = await _context.Advertisements
                    .AsNoTracking()
                    .Where(advertisement =>
                        advertisement.AdvertisementId == report.AdvertisementId.Value &&
                        advertisement.OwnerId == ownerId)
                    .Select(advertisement => (int?)advertisement.PlaceId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (advertisementPlaceId.HasValue)
                {
                    return advertisementPlaceId;
                }
            }

            return null;
        }

        private async Task<int> ResolveRootReviewIdAsync(
            int reviewId,
            int? parentReviewId,
            CancellationToken cancellationToken)
        {
            var rootId = reviewId;
            var nextParentId = parentReviewId;
            var guard = 0;

            while (nextParentId.HasValue && guard < 20)
            {
                guard++;
                rootId = nextParentId.Value;

                nextParentId = await _context.Reviews
                    .AsNoTracking()
                    .Where(review => review.ReviewId == rootId)
                    .Select(review => review.ParentReviewId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return rootId;
        }

        private static bool IsRefundNotification(string? notificationType)
        {
            return string.Equals(notificationType, NotificationTypes.RefundRequested, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(notificationType, NotificationTypes.RefundReadyForTransfer, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(notificationType, NotificationTypes.RefundTransferred, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(notificationType, NotificationTypes.RefundCompleted, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPaymentAction(string? notificationType)
        {
            return string.Equals(
                       notificationType,
                       NotificationTypes.PaymentRequired,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       notificationType,
                       NotificationTypes.PaymentRejected,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static void SetOpenUrls(
            IEnumerable<NotificationItemDto> items)
        {
            foreach (var item in items)
            {
                item.OpenUrl = $"/Notifications/Open/{item.NotificationId}";
            }
        }

        private static string NormalizeCenterFilter(string? filter)
        {
            var normalized = filter?.Trim().ToLowerInvariant();

            return normalized switch
            {
                "unread" => "unread",
                "booking" => "booking",
                "payment" => "payment",
                "review" => "review",
                "message" => "message",
                "report" => "report",
                _ => "all"
            };
        }

        private static string NormalizeAdminCenterFilter(string? filter)
        {
            var normalized = filter?.Trim().ToLowerInvariant();

            return normalized switch
            {
                "unread" => "unread",
                "case" => "case",
                "finance" => "finance",
                "advertisement" => "advertisement",
                "management" => "management",
                "system" => "system",
                _ => "all"
            };
        }

        private static Notification BuildNotification(
            CreateNotificationDto request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.UserId <= 0)
            {
                throw new ArgumentException(
                    "UserId غير صالح لإنشاء الإشعار.",
                    nameof(request));
            }

            var notificationText = request.NotificationText?.Trim();

            if (string.IsNullOrWhiteSpace(notificationText))
            {
                throw new ArgumentException(
                    "نص الإشعار مطلوب.",
                    nameof(request));
            }

            return new Notification
            {
                UserId = request.UserId,
                NotificationTitle =
                    NormalizeOptional(request.NotificationTitle, 150) ?? "إشعار",
                NotificationText = notificationText,
                NotificationType =
                    NormalizeOptional(request.NotificationType, 50) ?? NotificationTypes.General,
                TargetUrl = NormalizeTargetUrl(request.TargetUrl),
                ReferenceType = NormalizeOptional(request.ReferenceType, 50),
                ReferenceId = request.ReferenceId.HasValue && request.ReferenceId.Value > 0
                    ? request.ReferenceId.Value
                    : null,
                NotificationState = "Unread",
                CreatedAt = DateTime.Now,
                ReadAt = null
            };
        }

        private static bool IsRead(Notification notification)
        {
            return string.Equals(
                       notification.NotificationState,
                       "Read",
                       StringComparison.OrdinalIgnoreCase) ||
                   notification.ReadAt.HasValue;
        }

        private static string? NormalizeOptional(
            string? value,
            int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim();

            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength];
        }


        private static string? NormalizeLegacyOwnerTargetUrl(
            string? targetUrl)
        {
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                return targetUrl;
            }

            const string oldOwnerReportPath =
                "/Owner/Reports/ReportDetails/";

            if (targetUrl.StartsWith(
                    oldOwnerReportPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                var idPart =
                    targetUrl[oldOwnerReportPath.Length..]
                        .Split('?', '#')[0];

                if (int.TryParse(idPart, out var reportId) &&
                    reportId > 0)
                {
                    return
                        $"/Owner/Reports/OwnerReportDetails/{reportId}";
                }
            }

            const string oldOwnerSupportPath =
                "/Owner/Support/ReportDetails";

            if (targetUrl.StartsWith(
                    oldOwnerSupportPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                var queryIndex =
                    targetUrl.IndexOf('?', StringComparison.Ordinal);

                if (queryIndex >= 0 &&
                    queryIndex < targetUrl.Length - 1)
                {
                    var query =
                        targetUrl[(queryIndex + 1)..]
                            .Split('&', StringSplitOptions.RemoveEmptyEntries);

                    foreach (var part in query)
                    {
                        var pair =
                            part.Split('=', 2);

                        if (pair.Length == 2 &&
                            string.Equals(
                                pair[0],
                                "id",
                                StringComparison.OrdinalIgnoreCase) &&
                            int.TryParse(pair[1], out var reportId) &&
                            reportId > 0)
                        {
                            return
                                $"/Owner/Reports/OwnerReportDetails/{reportId}";
                        }
                    }
                }
            }

            return targetUrl;
        }

        private static string? NormalizeTargetUrl(string? targetUrl)
        {
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                return null;
            }

            var normalized = targetUrl.Trim();

            if (normalized.Length > 500)
            {
                normalized = normalized[..500];
            }

            if (!normalized.StartsWith("/", StringComparison.Ordinal) ||
                normalized.StartsWith("//", StringComparison.Ordinal) ||
                normalized.StartsWith(@"/\", StringComparison.Ordinal))
            {
                return null;
            }

            return normalized;
        }
    }
}
