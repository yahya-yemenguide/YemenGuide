using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Data;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Globalization;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Notifications;

namespace YemenGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;

        private static string L(string ar, string en)
        {
            return string.Equals(
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                "ar",
                StringComparison.OrdinalIgnoreCase)
                ? ar
                : en;
        }

        public UsersController(
            AppDbContext context,
            INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // =============================================================
        //  1. عرض صفحة إدارة المستخدمين
        // =============================================================
        [RequireAdminPermission(AdminPermissionKeys.Users.View)]
        public async Task<IActionResult> UserManagement()
        {
            var users = await _context.Users
                .AsNoTracking()
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return View(users);
        }

        // =============================================================
        //  2. تحديث الحالة (الفصل الذكي المطور بين المالك الجديد والقديم)
        // =============================================================
        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.Users.ChangeState)]
        public async Task<IActionResult> UpdateState([FromBody] UserActionRequest request)
        {
            if (request == null || request.Id == 0)
                return Json(new { success = false, message = L("بيانات غير صالحة", "Invalid request data") });

            var user = await _context.Users.FindAsync(request.Id);
            if (user == null)
                return Json(new { success = false, message = L("المستخدم غير موجود", "User not found") });

            var previousAccountState = user.AccountState;
            var isNewOwnerApproval =
                string.Equals(user.Role, "Owner", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(previousAccountState, "Pending", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(previousAccountState, "Rejected", StringComparison.OrdinalIgnoreCase)) &&
                (request.State == "Activate" || request.State == "Unblock");

            // 🎯 الفحص الذكي المطور:
            if (request.State == "Activate" || request.State == "Unblock")
            {
                // 1️⃣ إذا كان المستخدم مالكاً جديداً وحالته الحالية قيد الانتظار أو الرفض
                if (isNewOwnerApproval)
                {
                    // نضعه Accepted لكي يمر على صفحة الاحتفال والتهنئة أولاً
                    user.AccountState = "Accepted";
                }
                else
                {
                    // 2️⃣ إذا كان مالكاً قديماً (معلق Suspended أو محظور Blocked) أو عميلاً عادياً
                    // نضعه Active مباشرة لكي يطير طوالي للوحته دون المرور بالاحتفال
                    user.AccountState = "Active";
                }
            }
            else
            {
                // الاحتفاظ بالحالات الأخرى (Suspended, Blocked, Rejected) كما هي دون تغيير
                user.AccountState = request.State;
            }

            if (isNewOwnerApproval)
            {
                var pendingPlaces = await _context.Places
                    .AsNoTracking()
                    .Where(place =>
                        place.OwnerId == user.UserId &&
                        place.Status == "Pending")
                    .Select(place => new
                    {
                        place.PlaceId,
                        place.PlaceNameAndDesc
                    })
                    .ToListAsync();

                foreach (var place in pendingPlaces)
                {
                    await _notificationService.QueueForAdminPermissionAsync(
                        AdminPermissionKeys.Places.Review,
                        new CreateNotificationDto
                        {
                            NotificationTitle = "منشأة جديدة جاهزة للمراجعة",
                            NotificationText =
                                $"تم قبول توثيق المالك {user.FullName} وأصبحت المنشأة «{place.PlaceNameAndDesc}» جاهزة للمراجعة.",
                            NotificationType = NotificationTypes.AdminPlaceActionRequired,
                            TargetUrl = $"/Admin/Places/PlaceManagement?focusPlace={place.PlaceId}",
                            ReferenceType = NotificationReferenceTypes.Place,
                            ReferenceId = place.PlaceId
                        },
                        HttpContext.RequestAborted);
                }
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = L("تم تحديث حالة المستخدم بنجاح.", "User status updated successfully.") });
        }

        // =============================================================
        //  3. جلب وثائق المالك المعلقة للمراجعة تفاعلياً (AJAX)
        // =============================================================
        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.Users.ViewVerificationDocuments)]
        public async Task<IActionResult> GetVerificationDocuments(int userId)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return Json(new { success = false, message = L("المستخدم غير موجود.", "User not found.") });

            string idFront = "";
            string idBack = "";

            if (!string.IsNullOrEmpty(user.IdentityDocument))
            {
                var idParts = user.IdentityDocument.Split('|');
                if (idParts.Length >= 2)
                {
                    idFront = idParts[0];
                    idBack = idParts[1];
                }
                else
                {
                    idFront = idParts[0];
                }
            }

            return Json(new
            {
                success = true,
                commercial = user.CommercialRegisterDocument,
                idFront = idFront,
                idBack = idBack,
                auth = user.AuthorizationDocument
            });
        }

        // =============================================================
        //  4. الحذف النهائي للملاك المرفوضين نهائياً (تنظيف الداتا بيز)
        // =============================================================
        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.Users.Delete)]
        public async Task<IActionResult> PermanentDelete([FromBody] UserActionRequest request)
        {
            if (request == null || request.Id == 0)
                return Json(new { success = false, message = L("بيانات غير صالحة", "Invalid request data") });

            var user = await _context.Users.FindAsync(request.Id);
            if (user == null)
                return Json(new { success = false, message = L("المستخدم غير موجود", "User not found") });

            // حذف الكائن كلياً وجذرياً من قاعدة البيانات في حالة الرفض النهائي فقط بطلب صريح
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = L("تم حذف الحساب نهائياً بنجاح.", "The account was permanently deleted successfully.") });
        }
    }

    // =============================================================
    //  5. كلاس مساعد لاستقبال البيانات من واجهات AJAX
    // =============================================================
    public class UserActionRequest
    {
        public int Id { get; set; }
        public string State { get; set; }
    }
}
