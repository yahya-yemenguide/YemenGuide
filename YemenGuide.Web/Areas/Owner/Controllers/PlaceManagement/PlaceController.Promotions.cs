using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Core.Entities;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    public partial class PlaceController
    {
        private static readonly HashSet<string> AllowedPromotionDays = new(StringComparer.OrdinalIgnoreCase)
        {
            "Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday"
        };

        private static readonly HashSet<string> AllowedRewardTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "PercentageDiscount",
            "FixedDiscount",
            "FreeQuantity",
            "FreeService",
            "SpecialPrice",
            "ServiceSpecialPrice"
        };

        private sealed class PromotionRewardServiceSnapshot
        {
            public int ServiceId { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal OriginalPrice { get; set; }
        }


        // =====================================================================
        // تبويب العروض والخصومات
        // =====================================================================
        [HttpGet]
        [Authorize(Roles = "Owner,Admin")]
        public async Task<IActionResult> PromotionsManager()
        {
            if (!TryGetCurrentUserId(out int ownerId))
                return Unauthorized();

            Place? activePlace = ResolveActiveOwnerPlace(ownerId);
            var model = new PromotionManagementDto();

            if (activePlace == null)
                return PartialView("_PromotionsManager", model);

            int bookingSystemId = activePlace.Category?.BookingSystemId ?? 0;

            model.PlaceId = activePlace.PlaceId;
            model.PlaceName = activePlace.PlaceNameAndDesc;
            model.PlaceImage = activePlace.LogoImage ?? activePlace.CoverImage;
            model.BookingSystemId = bookingSystemId;

            List<Promotion> promotions = await _context.Promotions
                .AsNoTracking()
                .Include(item => item.Room)
                .Include(item => item.P)
                .Include(item => item.PlaceAdditionalServiceNavigation)
                .Include(item => item.PlaceAdditionalService)
                .Where(item => item.OwnerId == ownerId && item.PlaceId == activePlace.PlaceId)
                .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                .ThenByDescending(item => item.PromotionId)
                .ToListAsync();

            int[] promotionIds = promotions.Select(item => item.PromotionId).ToArray();
            Dictionary<int, int> allBookingCounts = new();
            Dictionary<int, int> usedBookingCounts = new();

            if (promotionIds.Length > 0)
            {
                // هذه الاستعلامات لا يجب أن تُسقط تبويب العروض بالكامل.
                // إذا كانت قاعدة البيانات القديمة لم تُحدّث بعد بعمود Bookings.PromotionID
                // يبقى عرض البطاقات يعمل، وتكون عدادات الاستخدام مؤقتًا صفر بدل HTTP 500.
                try
                {
                    allBookingCounts = await _context.Bookings
                        .AsNoTracking()
                        .Where(item => item.PromotionId.HasValue && promotionIds.Contains(item.PromotionId.Value))
                        .GroupBy(item => item.PromotionId!.Value)
                        .ToDictionaryAsync(group => group.Key, group => group.Count());

                    usedBookingCounts = await _context.Bookings
                        .AsNoTracking()
                        .Where(item =>
                            item.PromotionId.HasValue &&
                            promotionIds.Contains(item.PromotionId.Value) &&
                            item.BookingState != "Cancelled" &&
                            item.BookingState != "Rejected" &&
                            item.BookingState != "Expired")
                        .GroupBy(item => item.PromotionId!.Value)
                        .ToDictionaryAsync(group => group.Key, group => group.Count());
                }
                catch (Exception)
                {
                    allBookingCounts = new Dictionary<int, int>();
                    usedBookingCounts = new Dictionary<int, int>();
                }
            }

            DateTime now = DateTime.Now;

            foreach (Promotion promotion in promotions)
            {
                string effectiveStatus = ResolvePromotionEffectiveStatus(promotion, now);
                string targetType = ResolvePromotionTargetType(promotion);
                var rewardServiceSnapshots = ParsePromotionServiceSnapshots(
                    promotion.RewardServicesJson,
                    promotion.RewardServiceId);
                decimal? bundleOriginalPrice = rewardServiceSnapshots.Count > 0
                    ? Math.Round(rewardServiceSnapshots.Sum(item => Math.Max(0m, item.OriginalPrice)), 2)
                    : null;
                decimal? bundleFinalPrice =
                    string.Equals(promotion.RewardType, "ServiceSpecialPrice", StringComparison.OrdinalIgnoreCase)
                        ? promotion.SpecialPrice
                        : null;
                decimal? bundleSavingsAmount =
                    bundleOriginalPrice.HasValue && bundleFinalPrice.HasValue
                        ? Math.Round(Math.Max(0m, bundleOriginalPrice.Value - bundleFinalPrice.Value), 2)
                        : null;

                model.Promotions.Add(new PromotionCardDto
                {
                    PromotionId = promotion.PromotionId,
                    PromotionTitle = promotion.PromotionTitle,
                    ShortDescription = promotion.ShortDescription,
                    Description = promotion.Description,
                    ImagePath = promotion.ImagePath,
                    OfferMode = string.IsNullOrWhiteSpace(promotion.OfferMode) ? "Discount" : promotion.OfferMode,
                    RewardType = promotion.RewardType,
                    RequiredQuantity = promotion.RequiredQuantity,
                    RewardQuantity = promotion.RewardQuantity,
                    IsRewardRepeatable = promotion.IsRewardRepeatable,
                    RewardServiceId = promotion.RewardServiceId,
                    RewardServiceIds = rewardServiceSnapshots.Select(item => item.ServiceId).ToList(),
                    SpecialPrice = promotion.SpecialPrice,
                    BundleOriginalPrice = bundleOriginalPrice,
                    BundleFinalPrice = bundleFinalPrice,
                    BundleSavingsAmount = bundleSavingsAmount,
                    OfferSummary = BuildPromotionSummary(promotion, targetType),
                    DiscountType = promotion.DiscountType,
                    DiscountValue = promotion.DiscountValue,
                    MaximumDiscountAmount = promotion.MaximumDiscountAmount,
                    MinimumBookingAmount = promotion.MinimumBookingAmount,
                    MinimumNights = promotion.MinimumNights,
                    MinimumPeople = promotion.MinimumPeople,
                    TotalUsageLimit = promotion.TotalUsageLimit,
                    PerCustomerUsageLimit = promotion.PerCustomerUsageLimit,
                    IsAutomatic = promotion.IsAutomatic != false,
                    PromoCode = promotion.PromoCode,
                    IsStackable = promotion.IsStackable,
                    RoomId = promotion.RoomId,
                    PackageId = promotion.PackageId,
                    ServiceId = promotion.ServiceId,
                    TargetType = targetType,
                    TargetLabel = ResolvePromotionTargetLabel(promotion, targetType),
                    StartDate = promotion.StartDate,
                    EndDate = promotion.EndDate,
                    StayStartDate = promotion.StayStartDate,
                    StayEndDate = promotion.StayEndDate,
                    ApplicableDaysCsv = promotion.ApplicableDaysCsv,
                    Status = promotion.Status,
                    EffectiveStatus = effectiveStatus,
                    StatusLabel = GetPromotionStatusLabel(effectiveStatus),
                    IsEnabled = promotion.IsActive == true,
                    HasBookings = allBookingCounts.ContainsKey(promotion.PromotionId),
                    UsedCount = usedBookingCounts.TryGetValue(promotion.PromotionId, out int usedCount)
                        ? usedCount
                        : 0
                });
            }

            model.ActiveCount = model.Promotions.Count(item => item.EffectiveStatus == "Active");
            model.ScheduledCount = model.Promotions.Count(item => item.EffectiveStatus == "Scheduled");
            model.DraftCount = model.Promotions.Count(item => item.EffectiveStatus == "Draft");
            model.ArchivedCount = model.Promotions.Count(item =>
                item.EffectiveStatus == "Paused" ||
                item.EffectiveStatus == "Expired" ||
                item.EffectiveStatus == "Cancelled");

            // ---------------------------------------------------------------
            // أهداف العرض تُجلب حسب قالب المنشأة الحالية فقط.
            // فندق: غرف + باقات + خدمات.
            // شاليه/قاعة: باقات + خدمات.
            // منشأة غير حجزية: العرض على المنشأة كاملة فقط.
            // ---------------------------------------------------------------
            if (bookingSystemId == 1)
            {
                var roomTargets = await _context.Rooms
                    .AsNoTracking()
                    .Where(item => item.PlaceId == activePlace.PlaceId && item.IsAvailable != false)
                    .OrderBy(item => item.RoomType)
                    .ThenBy(item => item.RoomNumber)
                    .Select(item => new
                    {
                        item.RoomId,
                        item.RoomType,
                        item.RoomNumber,
                        item.Price
                    })
                    .ToListAsync();

                model.Rooms = roomTargets.Select(item => new PromotionTargetOptionDto
                {
                    Id = item.RoomId,
                    Name = string.IsNullOrWhiteSpace(item.RoomType) ? "غرفة / جناح" : item.RoomType,
                    Meta = BuildTargetMeta(item.RoomNumber, item.Price),
                    Price = item.Price
                }).ToList();
            }

            if (IsBookableSystem(bookingSystemId))
            {
                var packageTargets = await _context.PlacePackages
                    .AsNoTracking()
                    .Where(item => item.PlaceId == activePlace.PlaceId && item.IsActive != false)
                    .OrderBy(item => item.PackageName)
                    .Select(item => new
                    {
                        item.PackageId,
                        item.PackageName,
                        item.Price
                    })
                    .ToListAsync();

                model.Packages = packageTargets.Select(item => new PromotionTargetOptionDto
                {
                    Id = item.PackageId,
                    Name = string.IsNullOrWhiteSpace(item.PackageName) ? "باقة" : item.PackageName,
                    Meta = item.Price.ToString("N0") + " ريال",
                    Price = item.Price
                }).ToList();

                var serviceTargets = await _context.PlaceAdditionalServices
                    .AsNoTracking()
                    .Where(item => item.PlaceId == activePlace.PlaceId)
                    .OrderBy(item => item.ServiceName)
                    .Select(item => new
                    {
                        item.ServiceId,
                        item.ServiceName,
                        item.Price
                    })
                    .ToListAsync();

                model.Services = serviceTargets.Select(item => new PromotionTargetOptionDto
                {
                    Id = item.ServiceId,
                    Name = string.IsNullOrWhiteSpace(item.ServiceName) ? "خدمة إضافية" : item.ServiceName,
                    Meta = item.Price.ToString("N0") + " ريال",
                    Price = item.Price
                }).ToList();
            }

            return PartialView("_PromotionsManager", model);
        }

        // =====================================================================
        // إضافة عرض
        // =====================================================================
        [HttpPost]
        [Authorize(Roles = "Owner,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPromotion(
            [FromForm] PromotionUpsertDto dto,
            IFormFile? imageFile)
        {
            string? imagePath = null;

            try
            {
                if (!TryGetCurrentUserId(out int ownerId))
                    return Unauthorized();

                if (dto == null)
                    return Json(new { success = false, message = "لم تصل بيانات العرض إلى الخادم." });

                Place? activePlace = ResolveActiveOwnerPlace(ownerId);
                if (activePlace == null)
                    return Json(new { success = false, message = "اختر منشأة من الهيدر أولاً." });

                int bookingSystemId = activePlace.Category?.BookingSystemId ?? 0;

                if (!TryApplyPromotionDecimalValuesFromForm(dto, out string? numericError))
                    return Json(new { success = false, message = numericError });

                NormalizePromotionDtoForPlace(dto, bookingSystemId);

                string? modelError = GetFirstPromotionModelError();
                if (modelError != null)
                    return Json(new { success = false, message = modelError });

                string? validationError = await ValidatePromotionAsync(
                    dto,
                    activePlace.PlaceId,
                    bookingSystemId,
                    null);

                if (validationError != null)
                    return Json(new { success = false, message = validationError });

                if (imageFile != null && imageFile.Length > 0)
                {
                    (imagePath, string? imageError) = await SavePromotionImageAsync(imageFile);
                    if (imageError != null)
                        return Json(new { success = false, message = imageError });
                }

                bool publishNow = string.Equals(dto.SaveMode, "Publish", StringComparison.OrdinalIgnoreCase);
                string status = publishNow
                    ? ResolvePublishedStatus(dto.StartDate, dto.EndDate)
                    : "Draft";

                var promotion = new Promotion
                {
                    OwnerId = ownerId,
                    PlaceId = activePlace.PlaceId,
                    PromotionTitle = dto.PromotionTitle.Trim(),
                    ShortDescription = CleanOptionalText(dto.ShortDescription),
                    Description = CleanOptionalText(dto.Description),
                    ImagePath = imagePath,
                    OfferMode = dto.OfferMode,
                    RewardType = dto.RewardType,
                    RequiredQuantity = dto.RequiredQuantity,
                    RewardQuantity = dto.RewardQuantity,
                    IsRewardRepeatable = dto.IsRewardRepeatable,
                    RewardServiceId = dto.RewardServiceId,
                    RewardServicesJson = dto.RewardServicesJson,
                    SpecialPrice = dto.SpecialPrice,
                    DiscountType = dto.DiscountType,
                    DiscountValue = dto.DiscountValue,
                    MaximumDiscountAmount = dto.MaximumDiscountAmount,
                    MinimumBookingAmount = dto.MinimumBookingAmount,
                    MinimumNights = dto.MinimumNights,
                    MinimumPeople = dto.MinimumPeople,
                    TotalUsageLimit = dto.TotalUsageLimit,
                    PerCustomerUsageLimit = dto.PerCustomerUsageLimit,
                    IsAutomatic = dto.IsAutomatic,
                    PromoCode = dto.IsAutomatic ? null : NormalizePromoCode(dto.PromoCode),
                    IsStackable = dto.IsStackable,
                    StartDate = dto.StartDate,
                    EndDate = dto.EndDate,
                    StayStartDate = dto.StayStartDate?.Date,
                    StayEndDate = dto.StayEndDate?.Date,
                    ApplicableDaysCsv = NormalizeApplicableDays(dto.ApplicableDaysCsv),
                    Status = status,
                    IsActive = publishNow,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                ApplyPromotionTarget(promotion, dto);

                _context.Promotions.Add(promotion);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = publishNow
                        ? status == "Scheduled"
                            ? "تم حفظ العرض وجدولته بنجاح."
                            : "تم نشر العرض بنجاح."
                        : "تم حفظ العرض كمسودة."
                });
            }
            catch (DbUpdateException ex)
            {
                await DeletePromotionImageIfOwnedAsync(imagePath);
                return BuildPromotionFailure(
                    "تعذر حفظ العرض في قاعدة البيانات.",
                    ex);
            }
            catch (Exception ex)
            {
                await DeletePromotionImageIfOwnedAsync(imagePath);
                return BuildPromotionFailure(
                    "حدث خطأ أثناء حفظ العرض.",
                    ex);
            }
        }

        // =====================================================================
        // تعديل عرض
        // =====================================================================
        [HttpPost]
        [Authorize(Roles = "Owner,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPromotion(
            [FromForm] PromotionUpsertDto dto,
            IFormFile? imageFile)
        {
            string? newImagePath = null;
            string? oldImagePath = null;

            try
            {
                if (!TryGetCurrentUserId(out int ownerId))
                    return Unauthorized();

                if (dto == null)
                    return Json(new { success = false, message = "لم تصل بيانات العرض إلى الخادم." });

                Place? activePlace = ResolveActiveOwnerPlace(ownerId);
                if (activePlace == null)
                    return Json(new { success = false, message = "اختر منشأة من الهيدر أولاً." });

                Promotion? promotion = await _context.Promotions.FirstOrDefaultAsync(item =>
                    item.PromotionId == dto.PromotionId &&
                    item.OwnerId == ownerId &&
                    item.PlaceId == activePlace.PlaceId);

                if (promotion == null)
                    return Json(new { success = false, message = "العرض غير موجود ضمن المنشأة المختارة." });

                int bookingSystemId = activePlace.Category?.BookingSystemId ?? 0;

                if (!TryApplyPromotionDecimalValuesFromForm(dto, out string? numericError))
                    return Json(new { success = false, message = numericError });

                NormalizePromotionDtoForPlace(dto, bookingSystemId);

                string? modelError = GetFirstPromotionModelError();
                if (modelError != null)
                    return Json(new { success = false, message = modelError });

                string? validationError = await ValidatePromotionAsync(
                    dto,
                    activePlace.PlaceId,
                    bookingSystemId,
                    promotion.PromotionId);

                if (validationError != null)
                    return Json(new { success = false, message = validationError });

                oldImagePath = promotion.ImagePath;

                if (imageFile != null && imageFile.Length > 0)
                {
                    (newImagePath, string? imageError) = await SavePromotionImageAsync(imageFile);
                    if (imageError != null)
                        return Json(new { success = false, message = imageError });
                }

                bool publishNow = string.Equals(dto.SaveMode, "Publish", StringComparison.OrdinalIgnoreCase);

                promotion.PromotionTitle = dto.PromotionTitle.Trim();
                promotion.ShortDescription = CleanOptionalText(dto.ShortDescription);
                promotion.Description = CleanOptionalText(dto.Description);
                promotion.OfferMode = dto.OfferMode;
                promotion.RewardType = dto.RewardType;
                promotion.RequiredQuantity = dto.RequiredQuantity;
                promotion.RewardQuantity = dto.RewardQuantity;
                promotion.IsRewardRepeatable = dto.IsRewardRepeatable;
                promotion.RewardServiceId = dto.RewardServiceId;
                promotion.RewardServicesJson = dto.RewardServicesJson;
                promotion.SpecialPrice = dto.SpecialPrice;
                promotion.DiscountType = dto.DiscountType;
                promotion.DiscountValue = dto.DiscountValue;
                promotion.MaximumDiscountAmount = dto.MaximumDiscountAmount;
                promotion.MinimumBookingAmount = dto.MinimumBookingAmount;
                promotion.MinimumNights = dto.MinimumNights;
                promotion.MinimumPeople = dto.MinimumPeople;
                promotion.TotalUsageLimit = dto.TotalUsageLimit;
                promotion.PerCustomerUsageLimit = dto.PerCustomerUsageLimit;
                promotion.IsAutomatic = dto.IsAutomatic;
                promotion.PromoCode = dto.IsAutomatic ? null : NormalizePromoCode(dto.PromoCode);
                promotion.IsStackable = dto.IsStackable;
                promotion.StartDate = dto.StartDate;
                promotion.EndDate = dto.EndDate;
                promotion.StayStartDate = dto.StayStartDate?.Date;
                promotion.StayEndDate = dto.StayEndDate?.Date;
                promotion.ApplicableDaysCsv = NormalizeApplicableDays(dto.ApplicableDaysCsv);
                promotion.Status = publishNow
                    ? ResolvePublishedStatus(dto.StartDate, dto.EndDate)
                    : "Draft";
                promotion.IsActive = publishNow;
                promotion.UpdatedAt = DateTime.Now;

                ApplyPromotionTarget(promotion, dto);

                if (newImagePath != null)
                    promotion.ImagePath = newImagePath;
                else if (dto.RemoveImage)
                    promotion.ImagePath = null;

                await _context.SaveChangesAsync();

                if (newImagePath != null || dto.RemoveImage)
                    await DeletePromotionImageIfOwnedAsync(oldImagePath);

                return Json(new
                {
                    success = true,
                    message = publishNow
                        ? promotion.Status == "Scheduled"
                            ? "تم تحديث العرض وجدولته بنجاح."
                            : "تم تحديث العرض ونشره بنجاح."
                        : "تم تحديث العرض وحفظه كمسودة."
                });
            }
            catch (DbUpdateException ex)
            {
                await DeletePromotionImageIfOwnedAsync(newImagePath);
                return BuildPromotionFailure(
                    "تعذر تحديث العرض في قاعدة البيانات.",
                    ex);
            }
            catch (Exception ex)
            {
                await DeletePromotionImageIfOwnedAsync(newImagePath);
                return BuildPromotionFailure(
                    "حدث خطأ أثناء تحديث العرض.",
                    ex);
            }
        }

        // =====================================================================
        // تفعيل / إيقاف عرض
        // =====================================================================
        [HttpPost]
        [Authorize(Roles = "Owner,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePromotionStatus([FromBody] PromotionStatusRequestDto request)
        {
            if (!TryGetCurrentUserId(out int ownerId))
                return Unauthorized();

            Place? activePlace = ResolveActiveOwnerPlace(ownerId);
            if (activePlace == null)
                return Json(new { success = false, message = "اختر منشأة من الهيدر أولاً." });

            Promotion? promotion = await _context.Promotions.FirstOrDefaultAsync(item =>
                item.PromotionId == request.PromotionId &&
                item.OwnerId == ownerId &&
                item.PlaceId == activePlace.PlaceId);

            if (promotion == null)
                return Json(new { success = false, message = "العرض غير موجود ضمن المنشأة المختارة." });

            if (request.IsActive && promotion.EndDate <= DateTime.Now)
                return Json(new
                {
                    success = false,
                    message = "لا يمكن تفعيل عرض انتهت مدته. عدّل تاريخ الانتهاء أولاً."
                });

            if (string.Equals(promotion.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return Json(new
                {
                    success = false,
                    message = "العرض الملغي لا يمكن تفعيله مجددًا."
                });

            promotion.IsActive = request.IsActive;
            promotion.Status = request.IsActive
                ? ResolvePublishedStatus(promotion.StartDate, promotion.EndDate)
                : "Paused";
            promotion.PausedAt = request.IsActive ? null : DateTime.Now;
            promotion.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = request.IsActive
                    ? promotion.Status == "Scheduled"
                        ? "تمت جدولة العرض."
                        : "تم تفعيل العرض."
                    : "تم إيقاف العرض مؤقتًا."
            });
        }

        // =====================================================================
        // حذف / إلغاء عرض
        // =====================================================================
        [HttpPost]
        [Authorize(Roles = "Owner,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePromotion([FromBody] PromotionDeleteRequestDto request)
        {
            if (!TryGetCurrentUserId(out int ownerId))
                return Unauthorized();

            Place? activePlace = ResolveActiveOwnerPlace(ownerId);
            if (activePlace == null)
                return Json(new { success = false, message = "اختر منشأة من الهيدر أولاً." });

            Promotion? promotion = await _context.Promotions.FirstOrDefaultAsync(item =>
                item.PromotionId == request.PromotionId &&
                item.OwnerId == ownerId &&
                item.PlaceId == activePlace.PlaceId);

            if (promotion == null)
                return Json(new { success = false, message = "العرض غير موجود ضمن المنشأة المختارة." });

            bool hasBookings;
            try
            {
                hasBookings = await _context.Bookings
                    .AsNoTracking()
                    .AnyAsync(item => item.PromotionId == promotion.PromotionId);
            }
            catch (Exception)
            {
                // لا نحذف عرضًا نهائيًا إذا تعذر التحقق من وجود حجوزات مرتبطة به.
                return Json(new
                {
                    success = false,
                    message = "تعذر التحقق من الحجوزات المرتبطة بالعرض. تأكد من تحديث قاعدة البيانات بعمود PromotionID داخل Bookings ثم أعد المحاولة."
                });
            }

            if (hasBookings)
            {
                promotion.IsActive = false;
                promotion.Status = "Cancelled";
                promotion.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    cancelled = true,
                    message = "العرض مرتبط بحجوزات سابقة؛ تم إلغاؤه مع الاحتفاظ بسجل الحجوزات."
                });
            }

            string? imagePath = promotion.ImagePath;
            _context.Promotions.Remove(promotion);
            await _context.SaveChangesAsync();
            await DeletePromotionImageIfOwnedAsync(imagePath);

            return Json(new
            {
                success = true,
                cancelled = false,
                message = "تم حذف العرض نهائيًا."
            });
        }

        // =====================================================================
        // التحقق والتنظيف حسب قالب المنشأة
        // =====================================================================
        private async Task<string?> ValidatePromotionAsync(
            PromotionUpsertDto dto,
            int placeId,
            int bookingSystemId,
            int? currentPromotionId)
        {
            if (string.IsNullOrWhiteSpace(dto.PromotionTitle) || dto.PromotionTitle.Trim().Length < 3)
                return "اكتب اسمًا واضحًا للعرض لا يقل عن ثلاثة أحرف.";

            if (dto.StartDate == default || dto.EndDate == default)
                return "حدد تاريخ ووقت بداية العرض ونهايته.";

            if (dto.EndDate <= dto.StartDate)
                return "تاريخ ووقت انتهاء العرض يجب أن يكون بعد البداية.";

            DateTime now = DateTime.Now;
            bool startIsPast = dto.StartDate < now.AddSeconds(-30);
            if (startIsPast)
            {
                bool unchangedLegacyStart = false;
                if (currentPromotionId.HasValue)
                {
                    DateTime? currentStart = await _context.Promotions
                        .AsNoTracking()
                        .Where(item => item.PromotionId == currentPromotionId.Value && item.PlaceId == placeId)
                        .Select(item => (DateTime?)item.StartDate)
                        .FirstOrDefaultAsync();

                    unchangedLegacyStart = currentStart.HasValue &&
                        Math.Abs((currentStart.Value - dto.StartDate).TotalSeconds) < 60d;
                }

                if (!unchangedLegacyStart)
                    return "بداية العرض لا يمكن أن تكون في وقت مضى.";
            }

            if (string.Equals(dto.SaveMode, "Publish", StringComparison.OrdinalIgnoreCase) &&
                dto.EndDate <= now)
                return "لا يمكن نشر عرض انتهت مدته.";

            if (dto.StayStartDate.HasValue != dto.StayEndDate.HasValue)
                return "حدد بداية ونهاية فترة الحجز المشمولة معًا، أو اتركهما فارغتين.";

            if (dto.StayStartDate.HasValue && dto.StayEndDate.HasValue &&
                dto.StayEndDate.Value.Date < dto.StayStartDate.Value.Date)
                return "نهاية فترة الحجز المشمولة يجب أن تكون بعد بدايتها.";

            if (dto.OfferMode != "Discount" && dto.OfferMode != "ConditionalReward")
                return "نوع العرض المحدد غير صحيح.";

            bool isBookable = IsBookableSystem(bookingSystemId);

            if (!isBookable && dto.OfferMode != "Discount")
                return "المنشآت غير الحجزية تستخدم العرض الترويجي البسيط فقط.";

            if (dto.OfferMode == "Discount")
            {
                if (dto.DiscountType != "Percentage" && dto.DiscountType != "FixedAmount")
                    return "نوع الخصم المحدد غير صحيح.";

                if (dto.DiscountValue <= 0)
                    return "قيمة الخصم يجب أن تكون أكبر من صفر.";

                if (dto.DiscountType == "Percentage" && dto.DiscountValue >= 100)
                    return "نسبة الخصم يجب أن تكون أكبر من صفر وأقل من 100%.";
            }
            else
            {
                string rewardType = dto.RewardType ?? string.Empty;

                if (!AllowedRewardTypes.Contains(rewardType))
                    return "نوع المكافأة المحدد غير صحيح.";

                if (rewardType == "PercentageDiscount" || rewardType == "FixedDiscount")
                {
                    if (dto.DiscountValue <= 0)
                        return "أدخل قيمة المكافأة بشكل صحيح.";

                    if (rewardType == "PercentageDiscount" && dto.DiscountValue >= 100)
                        return "نسبة الخصم يجب أن تكون أكبر من صفر وأقل من 100%.";
                }

                if (rewardType == "FreeQuantity")
                {
                    if (!dto.RewardQuantity.HasValue || dto.RewardQuantity.Value <= 0)
                        return "حدد الكمية المجانية التي سيحصل عليها العميل.";

                    bool hasTrigger =
                        (dto.RequiredQuantity.HasValue && dto.RequiredQuantity.Value > 0) ||
                        (dto.MinimumNights.HasValue && dto.MinimumNights.Value > 0) ||
                        (dto.MinimumPeople.HasValue && dto.MinimumPeople.Value > 0) ||
                        (dto.MinimumBookingAmount.HasValue && dto.MinimumBookingAmount.Value > 0);

                    if (!hasTrigger)
                        return "حدد شرطًا واضحًا للحصول على الكمية المجانية.";
                }

                if (rewardType == "FreeService" || rewardType == "ServiceSpecialPrice")
                {
                    NormalizePromotionRewardServiceIds(dto);
                    if (dto.RewardServiceIds.Count == 0)
                        return "اختر خدمة واحدة على الأقل ضمن المكافأة.";

                    var rewardServices = await _context.PlaceAdditionalServices
                        .AsNoTracking()
                        .Where(item =>
                            dto.RewardServiceIds.Contains(item.ServiceId) &&
                            item.PlaceId == placeId)
                        .Select(item => new PromotionRewardServiceSnapshot
                        {
                            ServiceId = item.ServiceId,
                            Name = item.ServiceName ?? "خدمة إضافية",
                            OriginalPrice = item.Price
                        })
                        .ToListAsync();

                    if (rewardServices.Count != dto.RewardServiceIds.Count)
                        return "إحدى خدمات المكافأة غير موجودة أو لا تتبع المنشأة الحالية.";

                    dto.RewardServiceId = dto.RewardServiceIds.First();
                    dto.RewardServicesJson = SerializePromotionServiceSnapshots(rewardServices);

                    if (rewardType == "ServiceSpecialPrice")
                    {
                        if (!dto.SpecialPrice.HasValue || dto.SpecialPrice.Value <= 0)
                            return "أدخل السعر النهائي لباقة الخدمات بشكل صحيح.";

                        decimal originalBundlePrice = rewardServices.Sum(item => Math.Max(0m, item.OriginalPrice));
                        if (originalBundlePrice <= 0m)
                            return "تعذر حساب السعر الأصلي للخدمات المختارة.";

                        if (dto.SpecialPrice.Value >= originalBundlePrice)
                            return $"السعر النهائي للباقة يجب أن يكون أقل من مجموع أسعار الخدمات ({originalBundlePrice:N0} ريال).";

                        dto.BundleOriginalPrice = Math.Round(originalBundlePrice, 2);
                        dto.BundleFinalPrice = Math.Round(dto.SpecialPrice.Value, 2);
                        dto.BundleSavingsAmount = Math.Round(originalBundlePrice - dto.SpecialPrice.Value, 2);
                    }
                }

                if (rewardType == "SpecialPrice")
                {
                    if (!dto.SpecialPrice.HasValue || dto.SpecialPrice.Value <= 0)
                        return "أدخل السعر الخاص بشكل صحيح.";
                }
            }

            if ((dto.MaximumDiscountAmount.HasValue && dto.MaximumDiscountAmount.Value < 0) ||
                (dto.MinimumBookingAmount.HasValue && dto.MinimumBookingAmount.Value < 0) ||
                (dto.SpecialPrice.HasValue && dto.SpecialPrice.Value <= 0))
                return "القيم المالية يجب أن تكون أكبر من صفر أو تترك فارغة.";

            if ((dto.MinimumNights.HasValue && dto.MinimumNights.Value <= 0) ||
                (dto.MinimumPeople.HasValue && dto.MinimumPeople.Value <= 0) ||
                (dto.TotalUsageLimit.HasValue && dto.TotalUsageLimit.Value <= 0) ||
                (dto.PerCustomerUsageLimit.HasValue && dto.PerCustomerUsageLimit.Value <= 0) ||
                (dto.RequiredQuantity.HasValue && dto.RequiredQuantity.Value <= 0) ||
                (dto.RewardQuantity.HasValue && dto.RewardQuantity.Value <= 0))
                return "الكميات وحدود الاستخدام يجب أن تكون أكبر من صفر.";

            if (dto.TotalUsageLimit.HasValue && dto.PerCustomerUsageLimit.HasValue &&
                dto.PerCustomerUsageLimit.Value > dto.TotalUsageLimit.Value)
                return "حد استخدام العميل لا يمكن أن يتجاوز الحد الإجمالي للعرض.";

            if (isBookable && !dto.IsAutomatic)
            {
                string code = NormalizePromoCode(dto.PromoCode) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code))
                    return "اكتب كود العرض أو اختر التطبيق التلقائي.";

                bool codeExists = await _context.Promotions
                    .AsNoTracking()
                    .AnyAsync(item =>
                        item.PromoCode == code &&
                        (!currentPromotionId.HasValue || item.PromotionId != currentPromotionId.Value));

                if (codeExists)
                    return "كود العرض مستخدم في عرض آخر. اختر كودًا مختلفًا.";
            }

            string targetType = (dto.TargetType ?? "Place").Trim();

            if (!isBookable && targetType != "Place")
                return "المنشآت غير الحجزية تدعم العرض على المنشأة كاملة فقط.";

            if (targetType == "Room")
            {
                if (bookingSystemId != 1)
                    return "خيار الغرفة أو الجناح متاح للفنادق فقط.";

                if (!dto.RoomId.HasValue ||
                    !await _context.Rooms.AsNoTracking().AnyAsync(item =>
                        item.RoomId == dto.RoomId.Value &&
                        item.PlaceId == placeId &&
                        item.IsAvailable != false))
                    return "اختر غرفة أو جناحًا متاحًا تابعًا للفندق الحالي.";
            }
            else if (targetType == "Package")
            {
                if (!isBookable)
                    return "الباقات غير متاحة لهذا النوع من المنشآت.";

                if (!dto.PackageId.HasValue ||
                    !await _context.PlacePackages.AsNoTracking().AnyAsync(item =>
                        item.PackageId == dto.PackageId.Value &&
                        item.PlaceId == placeId &&
                        item.IsActive != false))
                    return "اختر باقة نشطة تابعة للمنشأة الحالية.";
            }
            else if (targetType == "Service")
            {
                if (!isBookable)
                    return "الخدمات ليست هدف عرض مستقلًا في المنشآت غير الحجزية.";

                if (!dto.ServiceId.HasValue ||
                    !await _context.PlaceAdditionalServices.AsNoTracking().AnyAsync(item =>
                        item.ServiceId == dto.ServiceId.Value &&
                        item.PlaceId == placeId))
                    return "اختر خدمة تابعة للمنشأة الحالية.";
            }
            else if (targetType != "Place")
            {
                return "هدف العرض المحدد غير صحيح.";
            }

            // المرحلة الثانية: الخصم الثابت يجب أن يكون أقل من السعر الأصلي للهدف.
            bool usesFixedDiscount =
                (dto.OfferMode == "Discount" && dto.DiscountType == "FixedAmount") ||
                (dto.OfferMode == "ConditionalReward" && dto.RewardType == "FixedDiscount");

            if (usesFixedDiscount)
            {
                decimal? targetBasePrice = await ResolvePromotionTargetBasePriceAsync(
                    dto,
                    placeId,
                    bookingSystemId);

                if (!targetBasePrice.HasValue || targetBasePrice.Value <= 0)
                {
                    return "تعذر تحديد السعر الأصلي للهدف. اختر نسبة مئوية أو هدفًا له سعر واضح.";
                }

                if (dto.DiscountValue >= targetBasePrice.Value)
                {
                    return $"قيمة الخصم يجب أن تكون أقل من السعر الأصلي للهدف ({targetBasePrice.Value:N0} ريال).";
                }
            }

            string? normalizedDays = NormalizeApplicableDays(dto.ApplicableDaysCsv);
            if (!string.IsNullOrWhiteSpace(dto.ApplicableDaysCsv) && normalizedDays == null)
                return "أيام تطبيق العرض غير صحيحة.";

            return null;
        }

        /// <summary>
        /// يعيد السعر الأصلي للهدف بنفس منطق واجهة العروض.
        /// للمنشأة كاملة نستخدم أقل سعر حجز أساسي من الغرف/الباقات،
        /// ولا ندخل الخدمات الإضافية في أصل سعر الحجز.
        /// </summary>
        private async Task<decimal?> ResolvePromotionTargetBasePriceAsync(
            PromotionUpsertDto dto,
            int placeId,
            int bookingSystemId)
        {
            string targetType = (dto.TargetType ?? "Place").Trim();

            if (targetType == "Room")
            {
                if (!dto.RoomId.HasValue)
                    return null;

                return await _context.Rooms
                    .AsNoTracking()
                    .Where(item =>
                        item.RoomId == dto.RoomId.Value &&
                        item.PlaceId == placeId &&
                        item.IsAvailable != false)
                    .Select(item => (decimal?)item.Price)
                    .FirstOrDefaultAsync();
            }

            if (targetType == "Package")
            {
                if (!dto.PackageId.HasValue)
                    return null;

                return await _context.PlacePackages
                    .AsNoTracking()
                    .Where(item =>
                        item.PackageId == dto.PackageId.Value &&
                        item.PlaceId == placeId &&
                        item.IsActive != false)
                    .Select(item => (decimal?)item.Price)
                    .FirstOrDefaultAsync();
            }

            if (targetType == "Service")
            {
                if (!dto.ServiceId.HasValue)
                    return null;

                return await _context.PlaceAdditionalServices
                    .AsNoTracking()
                    .Where(item =>
                        item.ServiceId == dto.ServiceId.Value &&
                        item.PlaceId == placeId)
                    .Select(item => (decimal?)item.Price)
                    .FirstOrDefaultAsync();
            }

            if (targetType != "Place" || !IsBookableSystem(bookingSystemId))
                return null;

            decimal? minimumRoomPrice = null;
            if (bookingSystemId == 1)
            {
                minimumRoomPrice = await _context.Rooms
                    .AsNoTracking()
                    .Where(item =>
                        item.PlaceId == placeId &&
                        item.IsAvailable != false &&
                        item.Price > 0)
                    .Select(item => (decimal?)item.Price)
                    .MinAsync();
            }

            decimal? minimumPackagePrice = await _context.PlacePackages
                .AsNoTracking()
                .Where(item =>
                    item.PlaceId == placeId &&
                    item.IsActive != false &&
                    item.Price > 0)
                .Select(item => (decimal?)item.Price)
                .MinAsync();

            if (minimumRoomPrice.HasValue && minimumPackagePrice.HasValue)
                return Math.Min(minimumRoomPrice.Value, minimumPackagePrice.Value);

            return minimumRoomPrice ?? minimumPackagePrice;
        }

        private static void NormalizePromotionDtoForPlace(
            PromotionUpsertDto dto,
            int bookingSystemId)
        {
            dto.TargetType = string.IsNullOrWhiteSpace(dto.TargetType)
                ? "Place"
                : dto.TargetType.Trim();

            dto.OfferMode = string.Equals(dto.OfferMode, "ConditionalReward", StringComparison.OrdinalIgnoreCase)
                ? "ConditionalReward"
                : "Discount";

            dto.SaveMode = string.Equals(dto.SaveMode, "Publish", StringComparison.OrdinalIgnoreCase)
                ? "Publish"
                : "Draft";

            // النظام يدعم عرضًا واحدًا فقط لكل حجز.
            dto.IsStackable = false;

            bool isBookable = IsBookableSystem(bookingSystemId);

            if (!isBookable)
            {
                dto.OfferMode = "Discount";
                dto.TargetType = "Place";
                dto.RoomId = null;
                dto.PackageId = null;
                dto.ServiceId = null;
                dto.RewardType = null;
                dto.RequiredQuantity = null;
                dto.RewardQuantity = null;
                dto.IsRewardRepeatable = false;
                dto.RewardServiceId = null;
                dto.RewardServiceIds.Clear();
                dto.RewardServicesJson = null;
                dto.SpecialPrice = null;
                dto.BundleOriginalPrice = null;
                dto.BundleFinalPrice = null;
                dto.BundleSavingsAmount = null;
                dto.MinimumBookingAmount = null;
                dto.MinimumNights = null;
                dto.MinimumPeople = null;
                dto.TotalUsageLimit = null;
                dto.PerCustomerUsageLimit = null;
                dto.StayStartDate = null;
                dto.StayEndDate = null;
                dto.IsAutomatic = true;
                dto.PromoCode = null;
                dto.IsStackable = false;
            }

            if (dto.OfferMode == "Discount")
            {
                dto.RewardType = null;
                dto.RequiredQuantity = null;
                dto.RewardQuantity = null;
                dto.IsRewardRepeatable = false;
                dto.RewardServiceId = null;
                dto.RewardServiceIds.Clear();
                dto.RewardServicesJson = null;
                dto.SpecialPrice = null;
                dto.BundleOriginalPrice = null;
                dto.BundleFinalPrice = null;
                dto.BundleSavingsAmount = null;

                dto.DiscountType = string.Equals(dto.DiscountType, "FixedAmount", StringComparison.OrdinalIgnoreCase)
                    ? "FixedAmount"
                    : "Percentage";

                if (dto.DiscountType == "FixedAmount")
                    dto.MaximumDiscountAmount = null;
            }
            else
            {
                dto.RewardType = string.IsNullOrWhiteSpace(dto.RewardType)
                    ? null
                    : dto.RewardType.Trim();

                NormalizePromotionRewardServiceIds(dto);

                // نستخدم حقول الخصم القديمة فقط عندما تكون مكافأة العرض خصمًا ماليًا.
                if (dto.RewardType == "PercentageDiscount")
                {
                    dto.DiscountType = "Percentage";
                }
                else if (dto.RewardType == "FixedDiscount")
                {
                    dto.DiscountType = "FixedAmount";
                    dto.MaximumDiscountAmount = null;
                }
                else
                {
                    dto.DiscountType = null;
                    dto.DiscountValue = 0m;
                    dto.MaximumDiscountAmount = null;
                }

                if (dto.RewardType != "FreeQuantity")
                {
                    dto.RewardQuantity = null;
                    dto.IsRewardRepeatable = false;
                }

                if (dto.RewardType != "FreeService" &&
                    dto.RewardType != "ServiceSpecialPrice")
                {
                    dto.RewardServiceId = null;
                    dto.RewardServiceIds.Clear();
                    dto.RewardServicesJson = null;
                }

                if (dto.RewardType != "SpecialPrice" &&
                    dto.RewardType != "ServiceSpecialPrice")
                    dto.SpecialPrice = null;

                if (dto.RewardType != "ServiceSpecialPrice")
                {
                    dto.BundleOriginalPrice = null;
                    dto.BundleFinalPrice = null;
                    dto.BundleSavingsAmount = null;
                }
            }

            if (bookingSystemId != 1)
            {
                dto.RoomId = null;
                dto.MinimumNights = null;
            }

            if (bookingSystemId == 3 || bookingSystemId == 4)
            {
                dto.StayStartDate = null;
                dto.StayEndDate = null;
            }

            if (dto.IsAutomatic)
                dto.PromoCode = null;
        }

        private static void NormalizePromotionRewardServiceIds(PromotionUpsertDto dto)
        {
            var ids = (dto.RewardServiceIds ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0 && dto.RewardServiceId.HasValue && dto.RewardServiceId.Value > 0)
                ids.Add(dto.RewardServiceId.Value);

            dto.RewardServiceIds = ids;
            dto.RewardServiceId = ids.Count > 0 ? ids[0] : null;
        }

        private static string? SerializePromotionServiceSnapshots(
            IEnumerable<PromotionRewardServiceSnapshot>? services)
        {
            var values = (services ?? Enumerable.Empty<PromotionRewardServiceSnapshot>())
                .Where(item => item.ServiceId > 0)
                .GroupBy(item => item.ServiceId)
                .Select(group => group.First())
                .ToList();

            return values.Count == 0
                ? null
                : JsonSerializer.Serialize(values);
        }

        private static List<PromotionRewardServiceSnapshot> ParsePromotionServiceSnapshots(
            string? json,
            int? legacyServiceId)
        {
            var result = new List<PromotionRewardServiceSnapshot>();

            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    result = JsonSerializer.Deserialize<List<PromotionRewardServiceSnapshot>>(json) ??
                             new List<PromotionRewardServiceSnapshot>();
                }
                catch (JsonException)
                {
                    result = new List<PromotionRewardServiceSnapshot>();
                }
            }

            result = result
                .Where(item => item.ServiceId > 0)
                .GroupBy(item => item.ServiceId)
                .Select(group => group.First())
                .ToList();

            if (result.Count == 0 && legacyServiceId.HasValue && legacyServiceId.Value > 0)
            {
                result.Add(new PromotionRewardServiceSnapshot
                {
                    ServiceId = legacyServiceId.Value,
                    Name = string.Empty,
                    OriginalPrice = 0m
                });
            }

            return result;
        }

        private static void ApplyPromotionTarget(Promotion promotion, PromotionUpsertDto dto)
        {
            promotion.RoomId = null;
            promotion.PackageId = null;
            promotion.ServiceId = null;

            switch ((dto.TargetType ?? "Place").Trim())
            {
                case "Room":
                    promotion.RoomId = dto.RoomId;
                    break;
                case "Package":
                    promotion.PackageId = dto.PackageId;
                    break;
                case "Service":
                    promotion.ServiceId = dto.ServiceId;
                    break;
            }
        }

        private static bool IsBookableSystem(int bookingSystemId)
        {
            return bookingSystemId >= 1 && bookingSystemId <= 4;
        }

        private static string BuildTargetMeta(string? roomNumber, decimal price)
        {
            string number = string.IsNullOrWhiteSpace(roomNumber)
                ? "نوع غرفة"
                : roomNumber.Trim();

            return number + " • " + price.ToString("N0") + " ريال";
        }

        private static string ResolvePromotionTargetType(Promotion promotion)
        {
            if (promotion.RoomId.HasValue) return "Room";
            if (promotion.PackageId.HasValue) return "Package";
            if (promotion.ServiceId.HasValue) return "Service";
            return "Place";
        }

        private static string ResolvePromotionTargetLabel(Promotion promotion, string targetType)
        {
            return targetType switch
            {
                "Room" => promotion.Room == null
                    ? "غرفة أو جناح"
                    : BuildRoomTargetLabel(promotion.Room.RoomType, promotion.Room.RoomNumber),
                "Package" => promotion.P?.PackageName ?? "باقة",
                "Service" => promotion.PlaceAdditionalServiceNavigation?.ServiceName ?? "خدمة إضافية",
                _ => "المنشأة كاملة"
            };
        }

        private static string BuildPromotionSummary(Promotion promotion, string targetType)
        {
            string targetLabel = ResolvePromotionTargetLabel(promotion, targetType);
            string offerMode = string.IsNullOrWhiteSpace(promotion.OfferMode)
                ? "Discount"
                : promotion.OfferMode;

            if (offerMode == "Discount")
            {
                if (promotion.DiscountType == "Percentage" && promotion.DiscountValue > 0)
                    return $"خصم {promotion.DiscountValue:0.##}% على {targetLabel}";

                if (promotion.DiscountType == "FixedAmount" && promotion.DiscountValue > 0)
                    return $"خصم {promotion.DiscountValue:N0} ريال على {targetLabel}";

                return $"عرض خاص على {targetLabel}";
            }

            string condition = BuildPromotionConditionSummary(promotion);

            string reward = promotion.RewardType switch
            {
                "PercentageDiscount" when promotion.DiscountValue > 0
                    => $"خصم {promotion.DiscountValue:0.##}%",
                "FixedDiscount" when promotion.DiscountValue > 0
                    => $"خصم {promotion.DiscountValue:N0} ريال",
                "FreeQuantity" when promotion.RewardQuantity.HasValue
                    => promotion.IsRewardRepeatable
                        ? $"{promotion.RewardQuantity.Value} مجانًا (يتكرر العرض)"
                        : $"{promotion.RewardQuantity.Value} مجانًا",
                "FreeService"
                    => ParsePromotionServiceSnapshots(promotion.RewardServicesJson, promotion.RewardServiceId).Count > 1
                        ? $"{ParsePromotionServiceSnapshots(promotion.RewardServicesJson, promotion.RewardServiceId).Count} خدمات مجانًا"
                        : $"خدمة {promotion.PlaceAdditionalService?.ServiceName ?? "إضافية"} مجانًا",
                "SpecialPrice" when promotion.SpecialPrice.HasValue
                    => $"سعر خاص {promotion.SpecialPrice.Value:N0} ريال",
                "ServiceSpecialPrice" when promotion.SpecialPrice.HasValue
                    => BuildServiceBundleSummary(promotion),
                _ => "مكافأة خاصة"
            };

            return string.IsNullOrWhiteSpace(condition)
                ? $"{reward} على {targetLabel}"
                : $"{condition} واحصل على {reward}";
        }

        private static string BuildServiceBundleSummary(Promotion promotion)
        {
            var services = ParsePromotionServiceSnapshots(
                promotion.RewardServicesJson,
                promotion.RewardServiceId);

            decimal original = Math.Round(
                services.Sum(item => Math.Max(0m, item.OriginalPrice)),
                2);

            if (services.Count > 1 && original > 0m && promotion.SpecialPrice.HasValue)
            {
                decimal saving = Math.Round(
                    Math.Max(0m, original - promotion.SpecialPrice.Value),
                    2);

                return $"باقة {services.Count} خدمات بسعر {promotion.SpecialPrice.Value:N0} ريال بدل {original:N0} (توفير {saving:N0} ريال)";
            }

            string serviceName = services.FirstOrDefault()?.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(serviceName))
                serviceName = promotion.PlaceAdditionalService?.ServiceName ?? "الخدمة";

            return $"{serviceName} بسعر {promotion.SpecialPrice!.Value:N0} ريال";
        }

        private static string BuildPromotionConditionSummary(Promotion promotion)
        {
            var parts = new List<string>();

            if (promotion.MinimumNights.HasValue)
                parts.Add($"احجز {promotion.MinimumNights.Value} ليلة");

            if (promotion.RequiredQuantity.HasValue)
                parts.Add($"اختر كمية {promotion.RequiredQuantity.Value}");

            if (promotion.MinimumPeople.HasValue)
                parts.Add($"لـ {promotion.MinimumPeople.Value} أشخاص أو أكثر");

            if (promotion.MinimumBookingAmount.HasValue)
                parts.Add($"بقيمة {promotion.MinimumBookingAmount.Value:N0} ريال أو أكثر");

            return string.Join("، ", parts);
        }

        private static string BuildRoomTargetLabel(string? roomType, string? roomNumber)
        {
            string type = string.IsNullOrWhiteSpace(roomType) ? "غرفة / جناح" : roomType.Trim();
            string number = string.IsNullOrWhiteSpace(roomNumber) ? string.Empty : roomNumber.Trim();
            return string.IsNullOrEmpty(number) ? type : type + " • " + number;
        }

        private static string ResolvePromotionEffectiveStatus(Promotion promotion, DateTime now)
        {
            string status = (promotion.Status ?? string.Empty).Trim();

            if (status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)) return "Cancelled";
            if (status.Equals("Draft", StringComparison.OrdinalIgnoreCase)) return "Draft";
            if (promotion.EndDate <= now) return "Expired";
            if (status.Equals("Paused", StringComparison.OrdinalIgnoreCase) || promotion.IsActive != true) return "Paused";
            if (promotion.StartDate > now) return "Scheduled";
            return "Active";
        }

        private static string ResolvePublishedStatus(DateTime startDate, DateTime endDate)
        {
            DateTime now = DateTime.Now;
            if (endDate <= now) return "Expired";
            return startDate > now ? "Scheduled" : "Active";
        }

        private static string GetPromotionStatusLabel(string status)
        {
            return status switch
            {
                "Active" => "نشط الآن",
                "Scheduled" => "مجدول",
                "Draft" => "مسودة",
                "Paused" => "متوقف مؤقتًا",
                "Expired" => "منتهي",
                "Cancelled" => "ملغي",
                _ => "غير محدد"
            };
        }

        private static string? NormalizePromoCode(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim().ToUpperInvariant();
        }

        private static string? CleanOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static string? NormalizeApplicableDays(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string[] days = value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (days.Length == 0 || days.Any(day => !AllowedPromotionDays.Contains(day)))
                return null;

            return string.Join(',', days);
        }

        /// <summary>
        /// يقرأ الحقول العشرية مباشرة من Request.Form بطريقة لا تتأثر بثقافة ar/en.
        /// المرحلة الأولى: يمنع الإشارات والحروف و e/E والرموز في القيم الرقمية
        /// قبل تنفيذ منطق التحويل والحفظ الحالي.
        /// </summary>
        private bool TryApplyPromotionDecimalValuesFromForm(
            PromotionUpsertDto dto,
            out string? error)
        {
            error = null;

            if (!ValidatePromotionNumericInputFromForm(out error))
                return false;

            if (!TryReadOptionalDecimal(
                    "DiscountValue",
                    out decimal? discountValue,
                    out bool discountValid) ||
                !discountValid)
            {
                error = "قيمة الخصم غير صحيحة.";
                return false;
            }

            dto.DiscountValue = discountValue ?? 0m;
            ModelState.Remove(nameof(PromotionUpsertDto.DiscountValue));

            if (!TryReadOptionalDecimal(
                    "MaximumDiscountAmount",
                    out decimal? maximumDiscount,
                    out bool maximumDiscountValid) ||
                !maximumDiscountValid)
            {
                error = "قيمة أقصى مبلغ للخصم غير صحيحة.";
                return false;
            }

            dto.MaximumDiscountAmount = maximumDiscount;
            ModelState.Remove(nameof(PromotionUpsertDto.MaximumDiscountAmount));

            if (!TryReadOptionalDecimal(
                    "MinimumBookingAmount",
                    out decimal? minimumBooking,
                    out bool minimumBookingValid) ||
                !minimumBookingValid)
            {
                error = "قيمة الحد الأدنى للحجز غير صحيحة.";
                return false;
            }

            dto.MinimumBookingAmount = minimumBooking;
            ModelState.Remove(nameof(PromotionUpsertDto.MinimumBookingAmount));

            if (!TryReadOptionalDecimal(
                    "SpecialPrice",
                    out decimal? specialPrice,
                    out bool specialPriceValid) ||
                !specialPriceValid)
            {
                error = "السعر الخاص غير صحيح.";
                return false;
            }

            dto.SpecialPrice = specialPrice;
            ModelState.Remove(nameof(PromotionUpsertDto.SpecialPrice));

            return true;
        }

        private bool ValidatePromotionNumericInputFromForm(out string? error)
        {
            error = null;

            string[] decimalFields =
            {
                "DiscountValue",
                "MaximumDiscountAmount",
                "MinimumBookingAmount",
                "SpecialPrice"
            };

            foreach (string key in decimalFields)
            {
                if (!Request.Form.TryGetValue(key, out var values))
                    continue;

                foreach (var value in values)
                {
                    string raw = value ?? string.Empty;

                    if (string.IsNullOrEmpty(raw))
                        continue;

                    if (!IsStrictPromotionDecimal(raw))
                    {
                        error =
                            GetPromotionNumericFieldLabel(key) +
                            ": استخدم الأرقام فقط. يسمح بالنقطة العشرية فقط عند الحاجة، " +
                            "ولا يسمح بـ + أو - أو e/E أو الحروف أو الرموز.";

                        return false;
                    }
                }
            }

            string[] integerFields =
            {
                "PromotionId",
                "RequiredQuantity",
                "RewardQuantity",
                "MinimumNights",
                "MinimumPeople",
                "TotalUsageLimit",
                "PerCustomerUsageLimit",
                "RoomId",
                "PackageId",
                "ServiceId",
                "RewardServiceId"
            };

            foreach (string key in integerFields)
            {
                if (!Request.Form.TryGetValue(key, out var values))
                    continue;

                foreach (var value in values)
                {
                    string raw = value ?? string.Empty;

                    if (string.IsNullOrEmpty(raw))
                        continue;

                    if (!IsStrictPromotionInteger(raw))
                    {
                        error =
                            GetPromotionNumericFieldLabel(key) +
                            ": هذا الحقل يقبل أرقامًا صحيحة فقط بدون كسور أو إشارات أو حروف.";

                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsStrictPromotionInteger(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return false;

            foreach (char character in raw)
            {
                if (character < '0' || character > '9')
                    return false;
            }

            return true;
        }

        private static bool IsStrictPromotionDecimal(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return false;

            bool hasDecimalPoint = false;
            bool hasDigit = false;

            for (int index = 0; index < raw.Length; index++)
            {
                char character = raw[index];

                if (character >= '0' && character <= '9')
                {
                    hasDigit = true;
                    continue;
                }

                if (character == '.' &&
                    !hasDecimalPoint &&
                    index > 0 &&
                    index < raw.Length - 1)
                {
                    hasDecimalPoint = true;
                    continue;
                }

                return false;
            }

            return hasDigit;
        }

        private static string GetPromotionNumericFieldLabel(string key)
        {
            return key switch
            {
                "DiscountValue" => "قيمة الخصم",
                "MaximumDiscountAmount" => "أقصى مبلغ للخصم",
                "MinimumBookingAmount" => "أقل قيمة للحجز",
                "SpecialPrice" => "السعر الخاص",
                "PromotionId" => "رقم العرض",
                "RequiredQuantity" => "الكمية المطلوبة",
                "RewardQuantity" => "كمية المكافأة",
                "MinimumNights" => "عدد الليالي",
                "MinimumPeople" => "عدد الأشخاص",
                "TotalUsageLimit" => "إجمالي مرات الاستخدام",
                "PerCustomerUsageLimit" => "مرات الاستخدام للعميل",
                "RoomId" => "الغرفة",
                "PackageId" => "الباقة",
                "ServiceId" => "الخدمة",
                "RewardServiceId" => "خدمة المكافأة",
                _ => "القيمة الرقمية"
            };
        }

        private bool TryReadRequiredDecimal(string key, out decimal value)
        {
            value = 0m;
            string raw = Request.Form[key].FirstOrDefault() ?? string.Empty;
            return TryParseFlexibleDecimal(raw, out value);
        }

        private bool TryReadOptionalDecimal(
            string key,
            out decimal? value,
            out bool valid)
        {
            value = null;
            valid = true;

            string raw = Request.Form[key].FirstOrDefault() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
                return true;

            if (!TryParseFlexibleDecimal(raw, out decimal parsed))
            {
                valid = false;
                return false;
            }

            value = parsed;
            return true;
        }

        private static bool TryParseFlexibleDecimal(string raw, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string text = raw.Trim()
                .Replace("٫", ".", StringComparison.Ordinal)
                .Replace("،", ".", StringComparison.Ordinal)
                .Replace("٬", string.Empty, StringComparison.Ordinal);

            if (decimal.TryParse(
                    text,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out value))
                return true;

            if (decimal.TryParse(
                    raw,
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out value))
                return true;

            // بعض لوحات المفاتيح العربية ترسل الفاصلة العشرية العادية.
            string commaNormalized = text.Replace(',', '.');
            return decimal.TryParse(
                commaNormalized,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out value);
        }

        private string? GetFirstPromotionModelError()
        {
            if (ModelState.IsValid)
                return null;

            string? message = ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

            return string.IsNullOrWhiteSpace(message)
                ? "بعض بيانات العرض غير صالحة. راجع الحقول وحاول مرة أخرى."
                : "تحقق من بيانات العرض: " + message;
        }

        private JsonResult BuildPromotionFailure(string message, Exception exception)
        {
            string details = exception.GetBaseException().Message ?? string.Empty;
            string friendly = message;

            if (details.Contains("UQ_Promotions_PromoCode", StringComparison.OrdinalIgnoreCase))
                friendly = "كود الخصم مستخدم في عرض آخر. اختر كودًا مختلفًا.";
            else if (details.Contains("CK_Promotions_DiscountRules", StringComparison.OrdinalIgnoreCase) ||
                     details.Contains("CK_Promotions_DiscountValue", StringComparison.OrdinalIgnoreCase))
                friendly = "قيمة الخصم غير مقبولة. النسبة يجب أن تكون أكبر من صفر وأقل من 100%.";
            else if (details.Contains("CK_Promotions_ApplicationMode", StringComparison.OrdinalIgnoreCase))
                friendly = "طريقة تطبيق العرض غير صحيحة: التطبيق التلقائي لا يستخدم كودًا، أما وضع الكود فيتطلب كود خصم.";
            else if (details.Contains("FK_Promotions_Rooms", StringComparison.OrdinalIgnoreCase) ||
                     details.Contains("FK_Promotions_PlacePackages", StringComparison.OrdinalIgnoreCase) ||
                     details.Contains("FK_Promotions_PlaceAdditionalServices", StringComparison.OrdinalIgnoreCase))
                friendly = "العنصر المختار لا يتبع المنشأة الحالية. أعد فتح العرض واختر العنصر من القائمة الحالية.";
            else if (details.Contains("CK_Promotions_DateRange", StringComparison.OrdinalIgnoreCase) ||
                     details.Contains("CK_Promotions_StayDateRange", StringComparison.OrdinalIgnoreCase))
                friendly = "تحقق من تواريخ العرض؛ تاريخ النهاية لا يمكن أن يسبق تاريخ البداية.";

            // لا نرسل Stack Trace أو رسالة تقنية للواجهة؛ المستخدم يرى رسالة مفهومة فقط.
            // وفي جميع الحالات يبقى الرد JSON حتى لا تتحول نافذة العرض إلى صفحة خطأ 500.
            Response.StatusCode = StatusCodes.Status200OK;

            return Json(new
            {
                success = false,
                message = friendly
            });
        }

        // =====================================================================
        // صور العروض
        // =====================================================================
        private async Task<(string? Path, string? Error)> SavePromotionImageAsync(IFormFile file)
        {
            if (file.Length <= 0)
                return (null, null);

            if (file.Length > 5 * 1024 * 1024)
                return (null, "حجم صورة العرض يجب ألا يتجاوز 5 ميجابايت.");

            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

            if (!allowedExtensions.Contains(extension))
                return (null, "صيغة الصورة غير مدعومة. استخدم JPG أو PNG أو WEBP.");

            string webRootPath = string.IsNullOrWhiteSpace(_webHostEnvironment.WebRootPath)
                ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
                : _webHostEnvironment.WebRootPath;

            Directory.CreateDirectory(webRootPath);
            string uploadsFolder = Path.Combine(webRootPath, "uploads");
            Directory.CreateDirectory(uploadsFolder);

            string fileName = "promotion_" + Guid.NewGuid().ToString("N") + extension;
            string physicalPath = Path.Combine(uploadsFolder, fileName);

            await using var stream = new FileStream(physicalPath, FileMode.CreateNew);
            await file.CopyToAsync(stream);

            return ("/uploads/" + fileName, null);
        }

        private Task DeletePromotionImageIfOwnedAsync(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) ||
                !imagePath.StartsWith("/uploads/promotion_", StringComparison.OrdinalIgnoreCase))
                return Task.CompletedTask;

            string fileName = Path.GetFileName(imagePath);
            string webRootPath = string.IsNullOrWhiteSpace(_webHostEnvironment.WebRootPath)
                ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
                : _webHostEnvironment.WebRootPath;
            string uploadsFolder = Path.Combine(webRootPath, "uploads");
            string physicalPath = Path.Combine(uploadsFolder, fileName);

            try
            {
                if (System.IO.File.Exists(physicalPath))
                    System.IO.File.Delete(physicalPath);
            }
            catch (IOException)
            {
                // لا نفشل عملية قاعدة البيانات بسبب تعذر تنظيف ملف صورة قديم.
            }
            catch (UnauthorizedAccessException)
            {
                // يبقى العرض محفوظًا حتى لو تعذر حذف الملف مؤقتًا.
            }

            return Task.CompletedTask;
        }
    }
}
