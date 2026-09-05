using System.Security.Claims;
using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YemenGuide.Web.Services.Employees;
using YemenGuide.Web.Services.Notifications;

namespace YemenGuide.Web.Controllers
{
    [Authorize]
    [Route("Notifications")]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notificationService;
        private readonly IAdminPermissionService _adminPermissionService;
        private readonly AppDbContext _context;

        public NotificationsController(
            INotificationService notificationService,
            IAdminPermissionService adminPermissionService,
            AppDbContext context)
        {
            _notificationService = notificationService;
            _adminPermissionService = adminPermissionService;
            _context = context;
        }

        [HttpGet("")]
        public IActionResult NotificationsRoot()
        {
            if (User.IsInRole("Admin"))
            {
                return Redirect("/Admin/Notifications");
            }

            if (User.IsInRole("Customer"))
            {
                return RedirectToAction(nameof(NotificationCenter));
            }

            if (User.IsInRole("Owner"))
            {
                return Redirect("/Owner/Notifications");
            }

            return Redirect("/");
        }

        [Authorize(Roles = "Customer")]
        [HttpGet("NotificationCenter")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> NotificationCenter(
            string? filter = "all",
            int page = 1,
            int pageSize = 12,
            int focus = 0,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Challenge();
            }

            var model = await _notificationService.GetCenterAsync(
                userId,
                filter,
                page,
                pageSize,
                focus,
                cancellationToken);

            ViewData["Title"] = "مركز الإشعارات";

            return View(
                "~/Views/Notifications/NotificationCenter.cshtml",
                model);
        }


        [Authorize(Roles = "Customer")]
        [HttpGet("CenterData")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> CenterData(
            string? filter = "all",
            int page = 1,
            int pageSize = 12,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var model =
                await _notificationService.GetCenterAsync(
                    userId,
                    filter,
                    page,
                    pageSize,
                    0,
                    cancellationToken);

            return Ok(model);
        }


        [Authorize(Roles = "Owner")]
        [HttpGet("/Owner/Notifications")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> OwnerNotificationCenter(
            string? filter = "all",
            int page = 1,
            int pageSize = 12,
            int focus = 0,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Challenge();
            }

            var activePlaceId = await ResolveOwnerActivePlaceIdAsync(
                userId,
                cancellationToken);

            var model = await _notificationService.GetOwnerCenterAsync(
                userId,
                activePlaceId,
                filter,
                page,
                pageSize,
                focus,
                cancellationToken);

            ViewData["Title"] = "مركز إشعارات المالك";

            return View(
                "~/Areas/Owner/Views/Notifications/NotificationCenter.cshtml",
                model);
        }

        [Authorize(Roles = "Owner")]
        [HttpGet("/Owner/Notifications/Data")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> OwnerCenterData(
            string? filter = "all",
            int page = 1,
            int pageSize = 12,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var activePlaceId = await ResolveOwnerActivePlaceIdAsync(
                userId,
                cancellationToken);

            var model = await _notificationService.GetOwnerCenterAsync(
                userId,
                activePlaceId,
                filter,
                page,
                pageSize,
                0,
                cancellationToken);

            return Ok(model);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("/Admin/Notifications")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> AdminNotificationCenter(
            string? filter = "all",
            int page = 1,
            int pageSize = 12,
            int focus = 0,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Challenge();
            }

            if (!await _adminPermissionService.CanAccessAdminAsync(
                    userId,
                    cancellationToken))
            {
                return Forbid();
            }

            var model = await _notificationService.GetAdminCenterAsync(
                userId,
                filter,
                page,
                pageSize,
                focus,
                cancellationToken);

            ViewData["Title"] = "مركز إشعارات الإدارة";

            return View(
                "~/Areas/Admin/Views/Notifications/NotificationCenter.cshtml",
                model);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("/Admin/Notifications/Data")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> AdminCenterData(
            string? filter = "all",
            int page = 1,
            int pageSize = 12,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            if (!await _adminPermissionService.CanAccessAdminAsync(
                    userId,
                    cancellationToken))
            {
                return Forbid();
            }

            var model = await _notificationService.GetAdminCenterAsync(
                userId,
                filter,
                page,
                pageSize,
                0,
                cancellationToken);

            return Ok(model);
        }

        [HttpGet("Header")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Header(
            int take = 7,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            // الإدارة والمالك: جرس الهيدر يعرض غير المقروء فقط.
            // بعد فتح الإشعار يبقى محفوظًا في مركز الإشعارات لكنه يختفي من الجرس.
            if (User.IsInRole("Admin"))
            {
                var adminUnread = await _notificationService.GetAdminCenterAsync(
                    userId,
                    "unread",
                    1,
                    take,
                    0,
                    cancellationToken);

                return Ok(new
                {
                    unreadCount = adminUnread.UnreadCount,
                    items = adminUnread.Items
                });
            }

            if (User.IsInRole("Owner"))
            {
                var activePlaceId = await ResolveOwnerActivePlaceIdAsync(
                    userId,
                    cancellationToken);

                var ownerUnread = await _notificationService.GetOwnerCenterAsync(
                    userId,
                    activePlaceId,
                    "unread",
                    1,
                    take,
                    0,
                    cancellationToken);

                return Ok(new
                {
                    unreadCount = ownerUnread.UnreadCount,
                    items = ownerUnread.Items
                });
            }

            var result = await _notificationService.GetHeaderAsync(
                userId,
                take,
                cancellationToken);

            return Ok(result);
        }

        [HttpGet("UnreadCount")]
        [ResponseCache(
            NoStore = true,
            Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> UnreadCount(
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var count = User.IsInRole("Owner")
                ? await _notificationService.GetOwnerUnreadCountAsync(
                    userId,
                    await ResolveOwnerActivePlaceIdAsync(userId, cancellationToken),
                    cancellationToken)
                : await _notificationService.GetUnreadCountAsync(
                    userId,
                    cancellationToken);

            return Ok(new
            {
                success = true,
                unreadCount = count
            });
        }

        [HttpPost("MarkAsRead/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(
            int id,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var success = await _notificationService.MarkAsReadAsync(
                id,
                userId,
                cancellationToken);

            if (!success)
            {
                return NotFound(new
                {
                    success = false,
                    message = "الإشعار غير موجود أو لا يخص المستخدم الحالي."
                });
            }

            var unreadCount = User.IsInRole("Owner")
                ? await _notificationService.GetOwnerUnreadCountAsync(
                    userId,
                    await ResolveOwnerActivePlaceIdAsync(userId, cancellationToken),
                    cancellationToken)
                : await _notificationService.GetUnreadCountAsync(
                    userId,
                    cancellationToken);

            return Ok(new
            {
                success = true,
                unreadCount
            });
        }

        [HttpPost("MarkAllAsRead")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead(
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            if (User.IsInRole("Owner"))
            {
                var activePlaceId = await ResolveOwnerActivePlaceIdAsync(
                    userId,
                    cancellationToken);

                var updatedOwnerCount = await _notificationService.MarkAllOwnerScopeAsReadAsync(
                    userId,
                    activePlaceId,
                    cancellationToken);

                return Ok(new
                {
                    success = true,
                    updatedCount = updatedOwnerCount,
                    unreadCount = 0
                });
            }

            var updatedCount = await _notificationService.MarkAllAsReadAsync(
                userId,
                cancellationToken);

            return Ok(new
            {
                success = true,
                updatedCount,
                unreadCount = 0
            });
        }

        [HttpGet("Open/{id:int}")]
        public async Task<IActionResult> Open(
            int id,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Challenge();
            }

            // أهم نقطة في إصلاح سياق المالك:
            // نحدد المنشأة الحقيقية للإشعار ونكتبها في نفس Cookie الهيدر قبل التحويل.
            if (User.IsInRole("Owner"))
            {
                var notificationPlaceId = await _notificationService
                    .ResolveOwnerNotificationPlaceIdAsync(
                        id,
                        userId,
                        cancellationToken);

                if (notificationPlaceId.HasValue &&
                    notificationPlaceId.Value > 0)
                {
                    SetOwnerActivePlaceCookie(
                        userId,
                        notificationPlaceId.Value);
                }
            }

            var targetUrl = await _notificationService
                .MarkAsReadAndGetTargetUrlAsync(
                    id,
                    userId,
                    cancellationToken);

            if (!string.IsNullOrWhiteSpace(targetUrl) &&
                Url.IsLocalUrl(targetUrl))
            {
                return LocalRedirect(targetUrl);
            }

            // الإشعار القديم الذي لا يملك TargetUrl/Reference صالح
            // لا نرمي المستخدم إلى الرئيسية؛ نفتح مركز الإشعارات ونبرز نفس الإشعار.
            if (User.IsInRole("Customer"))
            {
                return RedirectToAction(
                    nameof(NotificationCenter),
                    new
                    {
                        filter = "all",
                        focus = id
                    });
            }

            if (User.IsInRole("Admin"))
            {
                return Redirect(
                    $"/Admin/Notifications?filter=all&focus={id}");
            }

            if (User.IsInRole("Owner"))
            {
                return Redirect(
                    $"/Owner/Notifications?filter=all&focus={id}");
            }

            return Redirect("/");
        }

        private async Task<int?> ResolveOwnerActivePlaceIdAsync(
            int ownerId,
            CancellationToken cancellationToken)
        {
            if (ownerId <= 0)
            {
                return null;
            }

            var cookieName = $"ownerActivePlace_{ownerId}";
            _ = int.TryParse(Request.Cookies[cookieName], out var requestedPlaceId);

            // نفس ترتيب _HeaderOwner.cshtml:
            // المنشأة المختارة إن كانت مملوكة، وإلا Active/Approved ثم الاسم.
            var ownerPlaces = await _context.Places
                .AsNoTracking()
                .Where(place => place.OwnerId == ownerId)
                .OrderByDescending(place =>
                    place.Status == "Approved" ||
                    place.Status == "Active")
                .ThenBy(place => place.PlaceNameAndDesc)
                .Select(place => place.PlaceId)
                .ToListAsync(cancellationToken);

            if (ownerPlaces.Count == 0)
            {
                return null;
            }

            if (requestedPlaceId > 0 && ownerPlaces.Contains(requestedPlaceId))
            {
                return requestedPlaceId;
            }

            return ownerPlaces[0];
        }

        private void SetOwnerActivePlaceCookie(
            int ownerId,
            int placeId)
        {
            Response.Cookies.Append(
                $"ownerActivePlace_{ownerId}",
                placeId.ToString(),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = false,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps,
                    Path = "/"
                });
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            userId = 0;

            var userIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            return !string.IsNullOrWhiteSpace(userIdValue) &&
                   int.TryParse(userIdValue, out userId) &&
                   userId > 0;
        }
    }
}
