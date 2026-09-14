using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Data;
using System.Threading.Tasks;
using System.Linq;
using System;
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
                .Include(u => u.AdminRole)
                .Where(u =>
                    u.Role == SystemRoles.Owner ||
                    u.Role == SystemRoles.Customer ||
                    u.Role == SystemRoles.Admin)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return View(users);
        }

        // =============================================================
        //  2. تحديث الحالة (الفصل الصريح بين اعتماد المالك واعتماد المنشأة)
        // =============================================================
        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.Users.ChangeState)]
        public async Task<IActionResult> UpdateState([FromBody] UserActionRequest request)
        {
            if (request == null || request.Id == 0)
                return Json(new { success = false, message = "بيانات غير صالحة" });

            var user = await _context.Users.FindAsync(request.Id);
            if (user == null)
                return Json(new { success = false, message = "المستخدم غير موجود" });

            if (string.Equals(user.Role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                return Json(new
                {
                    success = false,
                    message = "حساب المدير الرئيسي محمي، والموظفون تتم إدارتهم من صفحة الموظفين والصلاحيات."
                });
            }

            if (!string.Equals(user.Role, SystemRoles.Owner, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(user.Role, SystemRoles.Customer, StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "نوع الحساب غير مدعوم في إدارة المستخدمين." });
            }

            var requestedState = (request.State ?? string.Empty).Trim();
            var allowedStates = new[]
            {
                "Activate",
                "Unblock",
                "Suspended",
                "Blocked",
                "Rejected"
            };

            if (!allowedStates.Contains(requestedState, StringComparer.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "الحالة المطلوبة غير مدعومة." });
            }

            var previousAccountState = user.AccountState;
            var isNewOwnerApproval =
                string.Equals(user.Role, SystemRoles.Owner, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(previousAccountState, "Pending", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(previousAccountState, "Rejected", StringComparison.OrdinalIgnoreCase)) &&
                (string.Equals(requestedState, "Activate", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(requestedState, "Unblock", StringComparison.OrdinalIgnoreCase));

            // اعتماد الحساب لا يعني اعتماد أي منشأة.
            if (string.Equals(requestedState, "Activate", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requestedState, "Unblock", StringComparison.OrdinalIgnoreCase))
            {
                if (isNewOwnerApproval)
                {
                    // يمر المالك الجديد بمرحلة Accepted ثم يكمل الدخول/التفعيل.
                    user.AccountState = "Accepted";
                }
                else
                {
                    user.AccountState = "Active";
                }
            }
            else
            {
                user.AccountState = requestedState;
            }

            if (isNewOwnerApproval)
            {
                /*
                 * حماية مهمة:
                 * قبول حساب المالك لا يعتمد منشآته إطلاقًا.
                 *
                 * - Pending تبقى Pending وتظل غير متاحة.
                 * - WaitOwner تبقى WaitOwner؛ قبول الحساب لا يعني قبول إسناد المنشأة.
                 * - UnderReview تبقى UnderReview وغير متاحة.
                 * - أي Active وُجدت خطأً لمالك لم يكن معتمدًا تُعاد إلى Pending.
                 * - Rejected / Suspended لا نغيّر حالتها.
                 */
                var ownerPlaces = await _context.Places
                    .Where(place =>
                        place.OwnerId == user.UserId &&
                        (place.Status == "Active" ||
                         place.Status == "Pending" ||
                         place.Status == "WaitOwner" ||
                         place.Status == "UnderReview"))
                    .ToListAsync();

                foreach (var place in ownerPlaces)
                {
                    if (string.Equals(
                            place.Status,
                            "Active",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        // Active قبل اعتماد المنشأة من الإدارة حالة غير صحيحة لمالك جديد.
                        place.Status = "Pending";
                    }

                    // أي منشأة لم تُعتمد بصورة مستقلة لا تظهر للعميل.
                    place.IsAvailable = false;
                }

                var pendingPlaces = ownerPlaces
                    .Where(place =>
                        string.Equals(
                            place.Status,
                            "Pending",
                            StringComparison.OrdinalIgnoreCase))
                    .Select(place => new
                    {
                        place.PlaceId,
                        place.PlaceNameAndDesc
                    })
                    .ToList();

                foreach (var place in pendingPlaces)
                {
                    await _notificationService.QueueForAdminPermissionAsync(
                        AdminPermissionKeys.Places.Review,
                        new CreateNotificationDto
                        {
                            NotificationTitle = "منشأة جديدة جاهزة للمراجعة",
                            NotificationText =
                                $"تم قبول توثيق المالك {user.FullName}، والمنشأة «{place.PlaceNameAndDesc}» ما زالت بانتظار اعتماد مستقل من الإدارة.",
                            NotificationType = NotificationTypes.AdminPlaceActionRequired,
                            TargetUrl = $"/Admin/Places/PlaceManagement?focusPlace={place.PlaceId}",
                            ReferenceType = NotificationReferenceTypes.Place,
                            ReferenceId = place.PlaceId
                        },
                        HttpContext.RequestAborted);
                }
            }

            QueueAccountStateNotification(
                user,
                previousAccountState);

            await _context.SaveChangesAsync();

            var successMessage = user.AccountState switch
            {
                "Suspended" => "تم توقيف الحساب مؤقتًا. يستطيع المستخدم تسجيل الدخول والتصفح فقط دون تنفيذ عمليات.",
                "Blocked" => "تم حظر الحساب ومنعه من تسجيل الدخول.",
                "Active" => "تم تنشيط الحساب وإعادة صلاحياته الطبيعية.",
                "Accepted" => "تم قبول حساب المالك فقط. المنشآت التابعة له لا تزال تحتاج اعتمادًا مستقلًا من إدارة المنشآت.",
                "Rejected" => "تم رفض طلب انضمام المالك.",
                _ => "تم تحديث حالة المستخدم بنجاح."
            };

            return Json(new { success = true, message = successMessage });
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
                return Json(new { success = false, message = "المستخدم غير موجود." });

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
                return Json(new { success = false, message = "بيانات غير صالحة" });

            var user = await _context.Users.FindAsync(request.Id);
            if (user == null)
                return Json(new { success = false, message = "المستخدم غير موجود" });

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "تم حذف الحساب نهائياً وبنجاح من قاعدة البيانات!" });
        }

        private void QueueAccountStateNotification(
            YemenGuide.Core.Entities.User user,
            string? previousAccountState)
        {
            if (!string.Equals(user.Role, SystemRoles.Owner, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(user.Role, SystemRoles.Customer, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var currentState = user.AccountState ?? string.Empty;
            if (string.Equals(currentState, previousAccountState, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string? title = null;
            string? text = null;

            if (string.Equals(currentState, "Suspended", StringComparison.OrdinalIgnoreCase))
            {
                title = "تم توقيف حسابك مؤقتًا";
                text = string.Equals(user.Role, SystemRoles.Owner, StringComparison.OrdinalIgnoreCase)
                    ? "يمكنك تسجيل الدخول وتصفح لوحة المالك وبياناتك، لكن جميع عمليات الإضافة والتعديل والحجز والدفع والمراسلة والتقييم متوقفة مؤقتًا حتى تعيد الإدارة تفعيل الحساب."
                    : "يمكنك تسجيل الدخول وتصفح المنصة، لكن جميع العمليات مثل الحجز والدفع والإلغاء والمراسلة والتقييم وتعديل البيانات متوقفة مؤقتًا حتى تعيد الإدارة تفعيل الحساب.";
            }
            else if (string.Equals(currentState, "Active", StringComparison.OrdinalIgnoreCase) &&
                     (string.Equals(previousAccountState, "Suspended", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(previousAccountState, "Blocked", StringComparison.OrdinalIgnoreCase)))
            {
                title = "تم إعادة تفعيل حسابك";
                text = "أعادت الإدارة تفعيل حسابك، ويمكنك الآن استخدام جميع العمليات المتاحة لحسابك بصورة طبيعية.";
            }

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _notificationService.Queue(
                new CreateNotificationDto
                {
                    UserId = user.UserId,
                    NotificationTitle = title,
                    NotificationText = text,
                    NotificationType = NotificationTypes.Security,
                    TargetUrl = string.Equals(user.Role, SystemRoles.Owner, StringComparison.OrdinalIgnoreCase)
                        ? "/Owner/OwnerDashboard/DashboardOwner"
                        : "/Home/Index",
                    ReferenceType = NotificationReferenceTypes.User,
                    ReferenceId = user.UserId
                });
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
