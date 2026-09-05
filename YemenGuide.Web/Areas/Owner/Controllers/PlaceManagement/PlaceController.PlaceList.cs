using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.IO;
using YemenGuide.Data;
using YemenGuide.Core.Entities;
using YemenGuide.Web.Filters;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.Constants;
using YemenGuide.Web.Services.Notifications;
using Microsoft.Extensions.DependencyInjection;
using BCrypt.Net;

using Microsoft.AspNetCore.Hosting;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    public partial class PlaceController
    {

        // =========================================================================
        // 📁 القسم الأول: التبويبة الأولى - قائمة منشآتي (PlaceList)
        // =========================================================================

        /// <summary>
        /// تحميل قائمة المنشآت الخاصة بالمالك (Partial View)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Owner")]
        public IActionResult PlaceList()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentOwnerId = string.IsNullOrEmpty(userId) ? 0 : int.Parse(userId);

            var activePlace = ResolveActiveOwnerPlace(currentOwnerId);
            int activePlaceId = activePlace?.PlaceId ?? 0;

            var places = _context.Places
                .Include(p => p.Category)
                .AsNoTracking()
                .Where(p => p.OwnerId == currentOwnerId)
                .OrderByDescending(p => p.PlaceId)
                .ToList();

            // المنشأة المحددة من الهيدر تظهر أولاً، ثم بقية المنشآت من الأحدث إلى الأقدم.
            if (activePlaceId > 0)
            {
                places = places
                    .OrderByDescending(p => p.PlaceId == activePlaceId)
                    .ThenByDescending(p => p.PlaceId)
                    .ToList();
            }

            ViewBag.ActivePlaceId = activePlaceId;
            return PartialView("_PlaceList", places);
        }


        [HttpPost]
        public async Task<IActionResult> ResubmitPlace(int placeId)
        {
            var place = await _context.Places.FindAsync(placeId);
            if (place == null)
                return Json(new { success = false, message = "المنشأة غير موجودة." });
            if (!await CanManagePlaceAsync(placeId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            place.Status = "Pending";

            // إعادة الإرسال إجراء جديد يحتاج انتباه فريق مراجعة المنشآت.
            // لا نرسل لكل Admin؛ بل فقط لمن يملك Places.Review (ومدير النظام).
            if (!User.IsInRole("Admin"))
            {
                var notificationService = HttpContext.RequestServices
                    .GetRequiredService<INotificationService>();

                await notificationService.QueueForAdminPermissionAsync(
                    AdminPermissionKeys.Places.Review,
                    new CreateNotificationDto
                    {
                        NotificationTitle = "منشأة أُعيد إرسالها للمراجعة",
                        NotificationText =
                            $"أعاد المالك إرسال منشأة «{place.PlaceNameAndDesc}» بعد التعديل.",
                        NotificationType = NotificationTypes.AdminPlaceActionRequired,
                        TargetUrl =
                            $"/Admin/Places/PlaceManagement?focusPlace={place.PlaceId}",
                        ReferenceType = NotificationReferenceTypes.Place,
                        ReferenceId = place.PlaceId
                    },
                    HttpContext.RequestAborted);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "تم إعادة إرسال المنشأة للإدارة بنجاح، وهي الآن قيد المراجعة." });
        }

        [HttpGet]
        [Authorize(Roles = "Owner, Admin")]

        public async Task<IActionResult> GetPlaceForEdit(int placeId)
        {
            var place = await _context.Places
                                .Include(p => p.Category)
                                .FirstOrDefaultAsync(p => p.PlaceId == placeId);

            if (place == null) return Json(new { success = false });
            if (!await CanManagePlaceAsync(placeId, AdminPermissionKeys.Places.View))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            return Json(new
            {
                success = true,
                place = new
                {
                    place.PlaceId,
                    place.PlaceNameAndDesc,
                    place.Description,
                    place.AddressDetail,
                    place.PhoneNumber,
                    place.Coordinates,
                    HasBookingSystem = place.Category != null ? place.Category.HasBookingSystem : false,
                    SystemId = place.Category != null ? place.Category.BookingSystemId : 0  // 🌟 هذا هو السطر الناقص الذي دمر الدنيا!
                }
            });
        }



        [HttpPost]
        [Authorize(Roles = "Owner, Admin")]

        public async Task<IActionResult> EditPlace([FromBody] EditPlaceDto updatedData)
        {
            var place = await _context.Places.FindAsync(updatedData.PlaceId);
            if (place == null) return Json(new { success = false, message = "المنشأة غير موجودة" });
            if (!await CanManagePlaceAsync(updatedData.PlaceId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            place.PlaceNameAndDesc = updatedData.PlaceNameAndDesc;
            place.Description = updatedData.Description;
            place.AddressDetail = updatedData.AddressDetail;
            place.PhoneNumber = updatedData.PhoneNumber;

            string cleanLat = updatedData.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string cleanLng = updatedData.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);

            place.Coordinates = $"{cleanLat},{cleanLng}";

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }


        [HttpPost]
        public async Task<IActionResult> DeletePlaceConfirmed(int placeId)
        {
            var place = await _context.Places.FindAsync(placeId);
            if (place == null) return Json(new { success = false, message = "المنشأة غير موجودة" });
            if (!await CanManagePlaceAsync(placeId, AdminPermissionKeys.Places.Delete))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            try
            {
                // 1. حذف الغرف المرتبطة
                var rooms = _context.Rooms.Where(r => r.PlaceId == placeId);
                _context.Rooms.RemoveRange(rooms);

                // 2. حذف الباقات المرتبطة
                var packages = _context.PlacePackages.Where(p => p.PlaceId == placeId);
                _context.PlacePackages.RemoveRange(packages);

                // 3. حذف الخدمات الإضافية
                var services = _context.PlaceAdditionalServices.Where(s => s.PlaceId == placeId);
                _context.PlaceAdditionalServices.RemoveRange(services);

                // 4. حذف المرافق المحددة
                var amenities = _context.PlaceAmenities.Where(a => a.PlaceId == placeId);
                _context.PlaceAmenities.RemoveRange(amenities);

                // 5. حذف السياسات المالية للإلغاء
                var policies = _context.PlacePolicies.Where(p => p.PlaceId == placeId);
                _context.PlacePolicies.RemoveRange(policies);

                // 6. حذف استثناءات التقويم 
                var calendarExceptions = _context.PlaceCalendarExceptions.Where(c => c.PlaceId == placeId);
                _context.PlaceCalendarExceptions.RemoveRange(calendarExceptions);

                // 7. حذف العروض والخصومات المرتبطة بها
                var promotions = _context.Promotions.Where(p => p.PlaceId == placeId);
                _context.Promotions.RemoveRange(promotions);

                // 8. حذف الحجوزات المرتبطة بهذه المنشأة (إن وجدت)
                var bookings = _context.Bookings.Where(b => b.PlaceId == placeId);
                _context.Bookings.RemoveRange(bookings);

                // 9. مسح صور الألبوم من قاعدة البيانات ومن السيرفر الفعلي
                var images = _context.PlaceImages.Where(i => i.PlaceId == placeId).ToList();
                foreach (var img in images)
                {
                    if (!string.IsNullOrEmpty(img.ImagePath))
                    {
                        var path = Path.Combine(_webHostEnvironment.WebRootPath, img.ImagePath.TrimStart('/'));
                        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    }
                }
                _context.PlaceImages.RemoveRange(images);

                // 10. مسح صورة الغلاف الأساسية من السيرفر
                if (!string.IsNullOrEmpty(place.CoverImage))
                {
                    var coverPath = Path.Combine(_webHostEnvironment.WebRootPath, place.CoverImage.TrimStart('/'));
                    if (System.IO.File.Exists(coverPath)) System.IO.File.Delete(coverPath);
                }

                // 11. أخيراً.. حذف المنشأة نفسها
                _context.Places.Remove(place);

                // تنفيذ جميع عمليات الحذف دفعة واحدة
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "تم حذف المنشأة وجميع بياناتها بنجاح." });
            }
            catch (Exception ex)
            {
                // التقاط أي خطأ آخر قد يظهر
                string actualError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "حدث خطأ أثناء محاولة الحذف: " + actualError });
            }
        }



        [HttpPost]
        public async Task<IActionResult> TogglePlaceStatus(int placeId, bool isAvailable)
        {
            var place = await _context.Places.FindAsync(placeId);
            if (place == null) return Json(new { success = false });
            if (!await CanManagePlaceAsync(placeId, AdminPermissionKeys.Places.ChangeStatus))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            if (isAvailable)
            {
                int bookingSystemId = await _context.Categories
                    .Where(category => category.CategoryId == place.CategoryId)
                    .Select(category => category.BookingSystemId)
                    .FirstOrDefaultAsync();

                bool needsPolicies = bookingSystemId >= 1 && bookingSystemId <= 4;
                bool hasPolicies = !needsPolicies || await _context.PlacePolicies.AnyAsync(policy => policy.PlaceId == placeId);

                if (!string.Equals(place.Status, "Active", StringComparison.OrdinalIgnoreCase) || !hasPolicies)
                    return Json(new { success = false, message = "لا يمكن فتح الحجز قبل اعتماد المنشأة وإكمال السياسات." });
            }

            place.IsAvailable = isAvailable;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ApprovePlaceByOwner(int placeId)
        {
            var place = await _context.Places.Include(p => p.Category).FirstOrDefaultAsync(p => p.PlaceId == placeId);
            if (place == null) return Json(new { success = false, message = "المنشأة غير موجودة." });

            // التأكد من صاحب الصلاحية
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            if (place.OwnerId != userId) return Json(new { success = false, message = "غير مصرح لك." });

            bool needsPolicies = place.Category != null &&
                place.Category.BookingSystemId >= 1 &&
                place.Category.BookingSystemId <= 4;
            bool hasPolicies = await _context.PlacePolicies.AnyAsync(p => p.PlaceId == placeId);

            if (needsPolicies && !hasPolicies)
            {
                // 🌟 السر هنا: لا نغير الحالة إلى Active! نتركها مخفية لكي لا يراها العملاء
                place.IsAvailable = false;

                await _context.SaveChangesAsync();
                return Json(new
                {
                    success = true,
                    hasPolicies = false,
                    message = "تمت الموافقة المبدئية، يرجى إكمال إعداد السياسات لتفعيل المنشأة للعملاء."
                });
            }
            else
            {
                // 🟢 إذا لم تكن تحتاج سياسات، أو تم إدخال السياسات مسبقاً، نفعّلها للعملاء
                place.Status = "Active";
                place.IsAvailable = true;
                await _context.SaveChangesAsync();
                return Json(new
                {
                    success = true,
                    hasPolicies = true,
                    message = "تم اعتماد المنشأة وتفعيلها، وهي الآن تظهر للعملاء بنجاح!"
                });
            }
        }


        [HttpPost]
        public async Task<IActionResult> RejectPlaceByOwner([FromBody] YemenGuide.Core.DTOs.Owner.FullPlaceWizardDto.PlaceRejectbyOwnerModel model)
        {
            var place = await _context.Places.FindAsync(model.Id);
            if (place == null) return Json(new { success = false, message = "المنشأة غير موجودة" });

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            if (place.OwnerId != userId)
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            place.Status = "RejectedByOwner";
            place.RejectionReason = model.Reason;
            place.OwnerId = null;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
