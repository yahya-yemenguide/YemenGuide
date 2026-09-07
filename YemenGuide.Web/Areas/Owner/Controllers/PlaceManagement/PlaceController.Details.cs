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
using YemenGuide.Core.Constants;
using BCrypt.Net;

using Microsoft.AspNetCore.Hosting;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    public partial class PlaceController
    {

        // =========================================================================
        // 📁 القسم الخامس: التبويبة الخامسة - إدارة الخدمات والباقات (Details)
        // =========================================================================

        [HttpGet]
        [Authorize(Roles = "Owner,Admin")]
        public IActionResult DetailsManager()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentOwnerId = string.IsNullOrEmpty(userIdClaim) ? 0 : int.Parse(userIdClaim);

            Place? activePlace = ResolveActiveOwnerPlace(currentOwnerId);

            ViewBag.ActivePlaceHasPolicies =
                activePlace != null &&
                _context.PlacePolicies
                    .AsNoTracking()
                    .Any(policy => policy.PlaceId == activePlace.PlaceId);

            IEnumerable<Place> model = activePlace == null
                ? Enumerable.Empty<Place>()
                : new[] { activePlace };

            return PartialView("_DetailsManager", model);
        }


        [HttpPost]
        public async Task<IActionResult> ApplyDefaultPolicies([FromBody] int placeId)
        {
            if (!await CanManagePlaceAsync(placeId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "لا تملك صلاحية إدارة هذه المنشأة." });

            var place = await _context.Places.FindAsync(placeId);
            if (place == null) return NotFound(new { success = false, message = "المنشأة غير موجودة." });

            var policy = await _context.PlacePolicies.FirstOrDefaultAsync(p => p.PlaceId == placeId);

            if (policy == null)
            {
                policy = new PlacePolicy { PlaceId = placeId, CreatedAt = DateTime.Now };
                _context.PlacePolicies.Add(policy);
            }

            policy.BookingApprovalMode = "Manual";
            policy.OperationSystemMode = "FullDay";
            policy.PaymentTimeout = 12;
            policy.DepositPoliciesJson = "[{\"val\":\"20\",\"type\":\"%\",\"desc\":\"عربون مبدئي لتأكيد الحجز\"}]";
            policy.SecurityPoliciesJson = "[\"إبراز الهوية الوطنية الأصلية أو جواز السفر عند الوصول للمطابقة.\"]";
            policy.CheckInTime = "14:00";
            policy.CheckOutTime = "12:00";
            policy.BufferTimeHours = 2;
            policy.MinimumNoticeHours = 24;
            policy.MaximumHorizonMonths = 6;
            policy.FreeCancellationHours = 48;
            policy.PenaltyType = "LoseDeposit";
            policy.PenaltyValue = 0;
            policy.RefundTransferFee = "CustomerPays";
            policy.RefundProcessingDays = 3;
            policy.IsSarSupported = true;
            policy.IsUsdSupported = true;
            policy.CleaningFee = 0m;
            policy.TaxPercentage = 0m;
            policy.IsNonRefundableEnabled = false;
            policy.NonRefundableDiscount = 10;
            policy.ImmediateGracePeriod = true;
            policy.ConfiscateOnNoShow = true;
            policy.NoShowGraceMinutes = 60;
            policy.ExceptionPoliciesJson = "[]";
            policy.ShiftsJson = "[]";
            policy.ClosedDaysCsv = string.Empty;
            policy.IsChatEnabled = true;
            policy.WelcomeMessage = "أهلًا بك في منشأتنا! نسعد بخدمتك. يرجى إبراز إثبات الهوية عند الوصول.";
            policy.OfflineReplyToggle = false;
            policy.OfflineMessage = string.Empty;
            policy.QuickRepliesJson = "[]";
            policy.EmergencyContact = place.PhoneNumber ?? string.Empty;
            policy.ContactEmail = string.Empty;
            policy.IsSoundAlertEnabled = true;
            policy.IsPushNoticeEnabled = true;
            policy.ForFamiliesOnly = false;
            policy.AllowSmoking = false;
            policy.AllowParties = false;
            policy.AllowQatInside = false;

            // =================================================================
            // منشأة الأدمن الحالية لا تصبح نشطة عند الإنشاء.
            // بعد حفظ السياسات فقط تتحول إلى Active ويُفتح الحجز.
            // =================================================================
            bool isCurrentAdminOwnedPlace =
                User.IsInRole("Admin") &&
                TryGetCurrentUserId(out int currentAdminId) &&
                place.OwnerId == currentAdminId;

            if (isCurrentAdminOwnedPlace)
            {
                place.Status = "Active";
                place.IsAvailable = true;
                _context.Places.Update(place);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "تم حفظ السياسات وتفعيل منشأة الإدارة، وأصبحت جاهزة لاستقبال الحجوزات."
                });
            }
            else if (await IsPlatformOwnerAsync(place.OwnerId) && place.Status == "Active")
            {
                // دعم مسار حساب تشغيل الإدارة القديم دون تغيير سلوكه.
                place.IsAvailable = true;
                _context.Places.Update(place);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "تم تطبيق السياسات وأصبحت المنشأة جاهزة لاستقبال الحجوزات." });
            }
            else if (place.Status == "WaitOwner")
            {
                // إذا كان الأدمن هو من أضافها، نفعّلها للعملاء فوراً
                place.Status = "Active";
                place.IsAvailable = true;
                _context.Places.Update(place);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "تم تطبيق سياسات دليلك اليمني الافتراضية وعرض المنشأة للعملاء بنجاح!" });
            }
            else
            {
                // أما إذا كان المالك هو من أضافها من البداية، تذهب للأدمن للمراجعة
                place.Status = "Pending";
                place.IsAvailable = false;
                _context.Places.Update(place);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "تم تطبيق سياسات (دليلك اليمني) الافتراضية وإرسال المنشأة للإدارة." });

            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPlaceSubItems(int placeId)
        {
            if (placeId <= 0) return Json(new { success = false, message = "منشأة غير صالحة." });
            if (!await CanManagePlaceAsync(placeId, AdminPermissionKeys.Places.View))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            try
            {
                bool hasPolicies = await _context.PlacePolicies.AnyAsync(p => p.PlaceId == placeId);

                var services = await _context.PlaceAdditionalServices
                    .Where(s => s.PlaceId == placeId)
                    .Select(s => new {
                        serviceID = s.ServiceId,
                        serviceName = s.ServiceName,
                        price = (decimal?)s.Price ?? 0,
                        description = s.Description, // 🌟 السطر الجديد
                        imageUrl = s.ImageUrl        // 🌟 السطر الجديد
                    })
                    .AsNoTracking()
                    .ToListAsync();


                var amenities = await _context.PlaceAmenities
                    .Where(a => a.PlaceId == placeId)
                    .Select(a => new { amenityID = a.AmenityId, amenityName = a.AmenityName, iconClass = a.IconClass ?? "bi-patch-check-fill" })
                    .AsNoTracking()
                    .ToListAsync();

                var packages = await _context.PlacePackages
                    .Where(p => p.PlaceId == placeId)
                    .Select(p => new
                    {
                        packageID = p.PackageId,
                        packageName = p.PackageName,
                        price = (decimal?)p.Price ?? 0,
                        capacity = (int?)p.Capacity ?? 0,
                        timeFrame = p.TimeFrame ?? "غير محدد",
                        startTime = p.StartTime,
                        endTime = p.EndTime,
                        isActive = p.IsActive
                    })
                    .AsNoTracking()
                    .ToListAsync();

                var rooms = await _context.Rooms
                    .Where(r => r.PlaceId == placeId)
                    .Select(r => new {
                        roomId = r.RoomId,
                        roomType = r.RoomType,
                        roomNumber = r.RoomNumber,
                        price = (decimal?)r.Price ?? 0,
                        quantity = r.RoomUnits.Any() ? r.RoomUnits.Count : r.Quantity,
                        areaSquareMeters = r.AreaSquareMeters,
                        maxAdults = r.MaxAdults,
                        maxChildren = r.MaxChildren,
                        bedsCount = r.BedsCount,
                        bedType = r.BedType,
                        internalRoomsCount = r.InternalRoomsCount,
                        isAvailable = r.IsAvailable ?? true,
                        description = r.Description,
                        imageUrl = r.ImageUrl,
                        units = r.RoomUnits
                            .OrderBy(unit => unit.FloorNumber)
                            .ThenBy(unit => unit.RoomNumber)
                            .Select(unit => new {
                                roomUnitID = unit.RoomUnitId,
                                placeID = unit.PlaceId,
                                roomID = unit.RoomId,
                                roomNumber = unit.RoomNumber,
                                floorNumber = unit.FloorNumber,
                                floorLabel = unit.FloorLabel,
                                isAvailable = unit.IsAvailable,
                                notes = unit.Notes,
                                createdAt = unit.CreatedAt
                            }).ToList()
                    })
                    .AsNoTracking()
                    .ToListAsync();


                return Json(new { success = true, services, amenities, packages, rooms, hasPolicies });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "حدث خطأ أثناء جلب البيانات: " + ex.Message });
            }
        }



        [HttpPost]
        public async Task<IActionResult> DeleteAdditionalService(int id)
        {
            var service = await _context.PlaceAdditionalServices.FindAsync(id);
            if (service == null) return Json(new { success = false, message = "العنصر غير موجود." });
            if (!await CanManagePlaceAsync(service.PlaceId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            _context.PlaceAdditionalServices.Remove(service);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "تم حذف الخدمة بنجاح." });
        }


        [HttpPost]
        public async Task<IActionResult> AddPlaceAmenity([FromBody] PlaceAmenity dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.AmenityName))
                return Json(new { success = false, message = "البيانات غير مكتملة." });
            if (!await CanManagePlaceAsync(dto.PlaceId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            dto.CreatedAt = DateTime.Now;
            dto.IsActive = true;
            _context.PlaceAmenities.Add(dto);
            await _context.SaveChangesAsync();

            return Json(new { success = true, id = dto.AmenityId, message = "تم إضافة المرفق بنجاح." });
        }


        [HttpPost]
        public async Task<IActionResult> AddPlaceSpace([FromBody] PlaceSpace dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.SpaceName))
                return Json(new { success = false, message = "البيانات غير مكتملة." });
            if (!await CanManagePlaceAsync(dto.PlaceId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            dto.CreatedAt = DateTime.Now;
            dto.IsActive = true;
            _context.PlaceSpaces.Add(dto);
            await _context.SaveChangesAsync();

            return Json(new { success = true, id = dto.SpaceId, message = "تم إضافة المساحة بنجاح." });
        }


        [HttpPost]
        public async Task<IActionResult> DeletePlaceSpace(int id)
        {
            var space = await _context.PlaceSpaces.FindAsync(id);
            if (space == null) return Json(new { success = false, message = "العنصر غير موجود." });
            if (!await CanManagePlaceAsync(space.PlaceId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            _context.PlaceSpaces.Remove(space);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "تم حذف المساحة بنجاح." });
        }


        [HttpPost]
        public async Task<IActionResult> AddPlacePackage([FromBody] PlacePackage dto)
        {
            try
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.PackageName) || dto.Price <= 0 ||
                    dto.Capacity <= 0 || !dto.StartTime.HasValue || !dto.EndTime.HasValue ||
                    dto.StartTime == dto.EndTime)
                    return Json(new { success = false, message = "يرجى إدخال اسم الباقة وسعرها بشكل صحيح." });
                if (!await CanManagePlaceAsync(dto.PlaceId, AdminPermissionKeys.Places.Edit))
                    return StatusCode(403, new { success = false, message = "غير مصرح لك." });

                var place = await _context.Places.FindAsync(dto.PlaceId);
                if (place != null && !string.IsNullOrEmpty(place.Description))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(place.Description, @"السعة:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int maxCap))
                    {
                        if (dto.Capacity > maxCap)
                            return Json(new { success = false, message = $"خطأ: سعة الباقة ({dto.Capacity}) أكبر من السعة القصوى للمنشأة ({maxCap} شخص)!" });
                    }
                }

                dto.PackageName = dto.PackageName.Trim();
                dto.TimeFrame = string.IsNullOrWhiteSpace(dto.TimeFrame) ? "فترة مخصصة" : dto.TimeFrame.Trim();
                dto.CreatedAt = DateTime.Now;
                _context.PlacePackages.Add(dto);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = dto.PackageId, message = "تم إضافة الباقة بنجاح." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException != null ? ex.InnerException.Message : ex.Message });
            }
        }


        [HttpPost]
        public async Task<IActionResult> DeletePlacePackage(int id)
        {
            var package = await _context.PlacePackages.FindAsync(id);
            if (package == null) return Json(new { success = false, message = "العنصر غير موجود." });
            if (!await CanManagePlaceAsync(package.PlaceId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            _context.PlacePackages.Remove(package);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "تم حذف الباقة بنجاح." });
        }

        [HttpPost]
        public async Task<IActionResult> EditPlacePackage([FromBody] PlacePackage dto)
        {
            if (dto == null || dto.PackageId <= 0 || string.IsNullOrWhiteSpace(dto.PackageName) ||
                dto.Price <= 0 || dto.Capacity <= 0 || !dto.StartTime.HasValue ||
                !dto.EndTime.HasValue || dto.StartTime == dto.EndTime)
                return Json(new { success = false, message = "بيانات الباقة غير صالحة." });

            var package = await _context.PlacePackages.FindAsync(dto.PackageId);
            if (package == null)
                return Json(new { success = false, message = "الباقة غير موجودة." });
            if (!await CanManagePlaceAsync(package.PlaceId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            package.PackageName = dto.PackageName.Trim();
            package.Price = dto.Price;
            package.Capacity = dto.Capacity;
            package.TimeFrame = string.IsNullOrWhiteSpace(dto.TimeFrame) ? null : dto.TimeFrame.Trim();
            package.StartTime = dto.StartTime;
            package.EndTime = dto.EndTime;
            package.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "تم تعديل الباقة بنجاح." });
        }

        [HttpPost]
        public async Task<IActionResult> DeletePlaceAmenity(int id)
        {
            var amenity = await _context.PlaceAmenities.FindAsync(id);
            if (amenity == null)
                return Json(new { success = false, message = "المرفق غير موجود." });
            if (!await CanManagePlaceAsync(amenity.PlaceId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            _context.PlaceAmenities.Remove(amenity);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "تم حذف المرفق بنجاح." });
        }


        // 🌟 1. دالة جلب بيانات الخدمة للتعديل (أضفنا الوصف والصورة)
        [HttpGet]
        public async Task<IActionResult> GetServiceForEdit(int id)
        {
            var service = await _context.PlaceAdditionalServices.FindAsync(id);
            if (service == null) return Json(new { success = false, message = "العنصر غير موجود" });
            if (!await CanManagePlaceAsync(service.PlaceId, AdminPermissionKeys.Places.View))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            return Json(new
            {
                success = true,
                data = new
                {
                    serviceID = service.ServiceId,
                    serviceName = service.ServiceName,
                    price = service.Price,
                    description = service.Description, // جلب الوصف
                    imageUrl = service.ImageUrl        // جلب مسارات الصور
                }
            });
        }



        [HttpPost]
        public async Task<IActionResult> AddAdditionalService([FromForm] PlaceAdditionalService model, List<IFormFile> ImageFiles)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.ServiceName))
                    return Json(new { success = false, message = "البيانات غير مكتملة. يرجى التأكد من إدخال اسم الخدمة." });

                if (model.PlaceId == 0)
                    return Json(new { success = false, message = "خطأ: لم يتم التعرف على رقم المنشأة، يرجى تحديث الصفحة والمحاولة." });
                if (!await CanManagePlaceAsync(model.PlaceId, AdminPermissionKeys.Places.Edit))
                    return StatusCode(403, new { success = false, message = "غير مصرح لك." });

                // 🌟 جدار الحماية في السيرفر: فحص المساحة الإجمالية للدفعة (5 ميجا)
                if (ImageFiles != null && ImageFiles.Count > 0)
                {
                    long totalNewSize = ImageFiles.Sum(f => f.Length);
                    long maxTotalSize = 5 * 1024 * 1024; // 5 ميجابايت

                    if (totalNewSize > maxTotalSize)
                    {
                        double newMb = Math.Round((double)totalNewSize / (1024 * 1024), 2);
                        return Json(new { success = false, message = $"عذراً، مساحة الصور المحددة ({newMb} ميجا) تتجاوز الحد الأقصى المسموح به للخدمة (5 ميجا)." });
                    }

                    // فحص كل صورة على حدة احتياطياً
                    foreach (var file in ImageFiles)
                    {
                        if (file.Length > maxTotalSize)
                        {
                            return Json(new { success = false, message = $"عذراً، الصورة ({file.FileName}) وحدها تتجاوز 5 ميجابايت!" });
                        }
                    }

                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "services");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    List<string> savedImagePaths = new List<string>();

                    foreach (var file in ImageFiles)
                    {
                        if (file.Length > 0)
                        {
                            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }

                            savedImagePaths.Add("/uploads/services/" + uniqueFileName);
                        }
                    }

                    model.ImageUrl = string.Join(",", savedImagePaths);
                }

                model.CreatedAt = DateTime.Now;
                model.IsActive = true;

                _context.PlaceAdditionalServices.Add(model);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = model.ServiceId, message = "تم إضافة الخدمة بنجاح." });
            }
            catch (Exception ex)
            {
                string actualError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "حدث خطأ أثناء الحفظ: " + actualError });
            }
        }



        [HttpPost]
        public async Task<IActionResult> EditAdditionalService([FromForm] PlaceAdditionalService dto, List<IFormFile> ImageFiles)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.ServiceName))
                return Json(new { success = false, message = "بيانات غير صالحة." });

            var service = await _context.PlaceAdditionalServices.FindAsync(dto.ServiceId);
            if (service == null) return Json(new { success = false, message = "الخدمة غير موجودة." });
            if (!await CanManagePlaceAsync(service.PlaceId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            service.ServiceName = dto.ServiceName;
            service.Price = dto.Price;
            service.Description = dto.Description;
            service.IsActive = true;

            // 🌟 1. حساب المساحة الحقيقية للصور القديمة (تم إصلاح قراءة المسارات من الهاردديسك)
            long totalExistingSize = 0;
            List<string> currentImages = new List<string>();

            if (!string.IsNullOrEmpty(service.ImageUrl))
            {
                currentImages = service.ImageUrl.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

                // استخدام نفس مسار المشروع الذي تستخدمينه لضمان العثور على الملفات
                string wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

                foreach (var img in currentImages)
                {
                    string cleanPath = img.Replace("/", "\\").TrimStart('\\');
                    string physicalPath = Path.Combine(wwwrootPath, cleanPath);

                    if (System.IO.File.Exists(physicalPath))
                    {
                        totalExistingSize += new FileInfo(physicalPath).Length; // حساب الوزن الحقيقي بالبايت
                    }
                    else
                    {
                        // حماية إضافية: إذا لم يجد الصورة لأي سبب، يعتبر أن وزنها 150 كيلوبايت لكي لا يتجاهلها
                        totalExistingSize += 150 * 1024;
                    }
                }
            }



            // 🌟 3. حساب حجم الصور الجديدة المرفوعة
            long newFilesSize = 0;
            if (ImageFiles != null && ImageFiles.Count > 0)
            {
                foreach (var file in ImageFiles)
                {
                    if (file.Length > 0) newFilesSize += file.Length;
                }
            }

            // 🌟 4. الميزان الدقيق: المجموع الكلي (القديم + الجديد)
            long maxTotalSize = 5 * 1024 * 1024; // 5 ميجابايت الحد الأقصى للخدمة
            long grandTotalSize = totalExistingSize + newFilesSize;

            if (grandTotalSize > maxTotalSize)
            {
                double currentMb = Math.Round((double)totalExistingSize / (1024 * 1024), 2);
                double newMb = Math.Round((double)newFilesSize / (1024 * 1024), 2);
                double availableMb = Math.Round((double)(maxTotalSize - totalExistingSize) / (1024 * 1024), 2);

                if (availableMb < 0) availableMb = 0;

                return Json(new
                {
                    success = false,
                    message = $"عذراً! مساحة الصور القديمة لديك ({currentMb} ميجا). والمتبقي لكِ هو ({availableMb} ميجا) فقط، بينما الصور الجديدة حجمها ({newMb} ميجا). يرجى حذف صور قديمة من علامة ❌ لتفريغ مساحة."
                });
            }

            // 🌟 5. إذا كان الوزن والعدد سليمين، ندمج الصور الجديدة مع القديمة
            if (ImageFiles != null && ImageFiles.Count > 0)
            {
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/services");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                foreach (var file in ImageFiles)
                {
                    if (file.Length > 0)
                    {
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                        string fullPath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await file.CopyToAsync(fileStream);
                        }
                        currentImages.Add("/uploads/services/" + uniqueFileName);
                    }
                }

                service.ImageUrl = string.Join(",", currentImages);
            }

            _context.PlaceAdditionalServices.Update(service);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "تم تعديل الخدمة بنجاح!" });
        }





        [HttpPost]
        public async Task<IActionResult> EditPlaceAmenity([FromBody] PlaceAmenity dto)
        {
            try
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.AmenityName))
                    return Json(new { success = false, message = "بيانات غير صالحة." });

                var amenity = await _context.PlaceAmenities.FindAsync(dto.AmenityId);
                if (amenity == null)
                    return Json(new { success = false, message = "المرفق غير موجود." });
                if (!await CanManagePlaceAsync(amenity.PlaceId, AdminPermissionKeys.Places.Edit))
                    return StatusCode(403, new { success = false, message = "غير مصرح لك." });

                amenity.AmenityName = dto.AmenityName;
                amenity.IsActive = true;

                _context.PlaceAmenities.Update(amenity);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "تم تعديل المرفق بنجاح!" });
            }
            catch (Exception ex)
            {
                string error = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "خطأ في التعديل: " + error });
            }
        }




        // =====================================================================
        // HOTEL TYPES + FLOOR PLAN V1
        // النوع منفصل عن الطوابق والوحدات الفعلية.
        // =====================================================================

        public sealed class HotelFloorPlanDto
        {
            public int PlaceId { get; set; }
            public List<HotelFloorPlanUnitDto> Units { get; set; } = new();
        }

        public sealed class HotelFloorPlanUnitDto
        {
            public int RoomUnitId { get; set; }
            public int RoomId { get; set; }
            public string RoomNumber { get; set; } = string.Empty;
            public int FloorNumber { get; set; }
            public string? FloorLabel { get; set; }
            public bool IsAvailable { get; set; } = true;
            public string? Notes { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> AddRoomType([FromForm] RoomDto model, List<IFormFile> RoomImages)
        {
            if (model == null ||
                model.PlaceID <= 0 ||
                string.IsNullOrWhiteSpace(model.RoomType) ||
                string.IsNullOrWhiteSpace(model.RoomNumber) ||
                model.Price <= 0 ||
                model.MaxAdults < 1)
            {
                return Json(new { success = false, message = "أكمل نوع الوحدة واسم العرض والسعر وعدد البالغين." });
            }

            if (!await CanManagePlaceAsync(model.PlaceID, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            string roomType = model.RoomType.Trim();
            string displayName = model.RoomNumber.Trim();

            bool duplicate = await _context.Rooms.AnyAsync(item =>
                item.PlaceId == model.PlaceID &&
                item.RoomType == roomType &&
                item.RoomNumber == displayName);

            if (duplicate)
                return Json(new { success = false, message = "يوجد نوع غرفة أو جناح بالاسم نفسه." });

            bool isSuite = model.InternalRoomsCount.HasValue && model.InternalRoomsCount.Value > 0;
            if (isSuite && (model.InternalRoomsCount < 1 || model.InternalRoomsCount > 100))
                return Json(new { success = false, message = "عدد الغرف الداخلية للجناح يجب أن يكون من 1 إلى 100." });

            var imageResult = await SaveNewRoomImagesAsync(RoomImages);
            if (!imageResult.Success)
                return Json(new { success = false, message = imageResult.Error });

            var room = new Room
            {
                PlaceId = model.PlaceID,
                RoomType = roomType,
                RoomNumber = displayName,
                Quantity = 0,
                Price = model.Price,
                AreaSquareMeters = model.AreaSquareMeters,
                MaxAdults = model.MaxAdults,
                MaxChildren = Math.Max(0, model.MaxChildren),
                BedsCount = isSuite ? null : model.BedsCount,
                BedType = isSuite || string.IsNullOrWhiteSpace(model.BedType) ? null : model.BedType.Trim(),
                InternalRoomsCount = isSuite ? model.InternalRoomsCount : null,
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                IsAvailable = model.IsAvailable,
                ImageUrl = imageResult.Paths.Any() ? string.Join(",", imageResult.Paths) : null
            };

            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();
            await UpdateHotelRoomCountersAsync(model.PlaceID);

            return Json(new
            {
                success = true,
                roomId = room.RoomId,
                message = "تمت إضافة النوع. يمكنك الآن توزيع غرفه الفعلية على الطوابق."
            });
        }

        [HttpPost]
        public async Task<IActionResult> EditRoomType([FromForm] RoomDto model, List<IFormFile> RoomImages)
        {
            if (model == null ||
                model.RoomID <= 0 ||
                string.IsNullOrWhiteSpace(model.RoomType) ||
                string.IsNullOrWhiteSpace(model.RoomNumber) ||
                model.Price <= 0 ||
                model.MaxAdults < 1)
            {
                return Json(new { success = false, message = "أكمل نوع الوحدة واسم العرض والسعر وعدد البالغين." });
            }

            var room = await _context.Rooms
                .Include(item => item.RoomUnits)
                .FirstOrDefaultAsync(item => item.RoomId == model.RoomID);

            if (room == null)
                return Json(new { success = false, message = "نوع الغرفة غير موجود." });

            if (!await CanManagePlaceAsync(room.PlaceId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            string roomType = model.RoomType.Trim();
            string displayName = model.RoomNumber.Trim();

            bool duplicate = await _context.Rooms.AnyAsync(item =>
                item.PlaceId == room.PlaceId &&
                item.RoomId != room.RoomId &&
                item.RoomType == roomType &&
                item.RoomNumber == displayName);

            if (duplicate)
                return Json(new { success = false, message = "يوجد نوع آخر بالاسم نفسه." });

            bool isSuite = model.InternalRoomsCount.HasValue && model.InternalRoomsCount.Value > 0;
            if (isSuite && (model.InternalRoomsCount < 1 || model.InternalRoomsCount > 100))
                return Json(new { success = false, message = "عدد الغرف الداخلية للجناح يجب أن يكون من 1 إلى 100." });

            room.RoomType = roomType;
            room.RoomNumber = displayName;
            room.Price = model.Price;
            room.AreaSquareMeters = model.AreaSquareMeters;
            room.MaxAdults = model.MaxAdults;
            room.MaxChildren = Math.Max(0, model.MaxChildren);
            room.BedsCount = isSuite ? null : model.BedsCount;
            room.BedType = isSuite || string.IsNullOrWhiteSpace(model.BedType) ? null : model.BedType.Trim();
            room.InternalRoomsCount = isSuite ? model.InternalRoomsCount : null;
            room.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            room.IsAvailable = model.IsAvailable;
            room.Quantity = room.RoomUnits.Count;

            var mergeResult = await MergeRoomImagesAsync(room, RoomImages);
            if (!mergeResult.Success)
                return Json(new { success = false, message = mergeResult.Error });

            room.ImageUrl = mergeResult.ImageUrl;
            _context.Rooms.Update(room);

            await _context.SaveChangesAsync();
            await UpdateHotelRoomCountersAsync(room.PlaceId);

            return Json(new { success = true, message = "تم تعديل بيانات النوع مع الاحتفاظ بالطوابق والغرف الحالية." });
        }

        [HttpPost]
        public async Task<IActionResult> SaveHotelFloorPlan([FromBody] HotelFloorPlanDto dto)
        {
            if (dto == null || dto.PlaceId <= 0)
                return Json(new { success = false, message = "بيانات الفندق غير صالحة." });

            if (!await CanManagePlaceAsync(dto.PlaceId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            dto.Units ??= new List<HotelFloorPlanUnitDto>();

            var rooms = await _context.Rooms
                .Include(item => item.RoomUnits)
                .Where(item => item.PlaceId == dto.PlaceId)
                .ToListAsync();

            if (rooms.Count == 0 && dto.Units.Count > 0)
                return Json(new { success = false, message = "لا توجد أنواع غرف صالحة لهذه المنشأة." });

            var roomIds = rooms.Select(item => item.RoomId).ToHashSet();
            if (dto.Units.Any(item => !roomIds.Contains(item.RoomId)))
                return Json(new { success = false, message = "إحدى الغرف مرتبطة بنوع غير صالح." });

            var cleanNumbers = dto.Units
                .Select(item => (item.RoomNumber ?? string.Empty).Trim())
                .ToList();

            if (cleanNumbers.Any(string.IsNullOrWhiteSpace))
                return Json(new { success = false, message = "رقم/رمز كل غرفة فعلية مطلوب." });

            var duplicateNumber = cleanNumbers
                .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicateNumber != null)
                return Json(new { success = false, message = $"رقم الوحدة ({duplicateNumber.Key}) مكرر داخل الفندق." });

            var existingUnits = rooms.SelectMany(item => item.RoomUnits).ToList();
            var existingById = existingUnits.ToDictionary(item => item.RoomUnitId);

            var incomingExistingIds = dto.Units
                .Where(item => item.RoomUnitId > 0)
                .Select(item => item.RoomUnitId)
                .ToList();

            if (incomingExistingIds.Count != incomingExistingIds.Distinct().Count())
                return Json(new { success = false, message = "تم إرسال إحدى الوحدات القديمة أكثر من مرة." });

            if (incomingExistingIds.Any(id => !existingById.ContainsKey(id)))
                return Json(new { success = false, message = "إحدى الوحدات القديمة لا تتبع هذه المنشأة." });

            var incomingIdSet = incomingExistingIds.ToHashSet();
            var removedIds = existingUnits
                .Where(item => !incomingIdSet.Contains(item.RoomUnitId))
                .Select(item => item.RoomUnitId)
                .ToList();

            if (removedIds.Count > 0)
            {
                bool hasBookingHistory = await _context.BookingRoomUnits
                    .AsNoTracking()
                    .AnyAsync(item => removedIds.Contains(item.RoomUnitId));

                if (hasBookingHistory)
                {
                    return Json(new
                    {
                        success = false,
                        message = "لا يمكن حذف غرفة فعلية مرتبطة بحجز. يمكنك جعلها «غير متاحة» بدل حذفها."
                    });
                }
            }

            var assignedIncomingIds = incomingExistingIds.Count == 0
                ? new HashSet<int>()
                : (await _context.BookingRoomUnits
                    .AsNoTracking()
                    .Where(item => incomingExistingIds.Contains(item.RoomUnitId))
                    .Select(item => item.RoomUnitId)
                    .Distinct()
                    .ToListAsync())
                    .ToHashSet();

            foreach (var item in dto.Units.Where(item => item.RoomUnitId > 0))
            {
                var existing = existingById[item.RoomUnitId];

                if (assignedIncomingIds.Contains(existing.RoomUnitId) && existing.RoomId != item.RoomId)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"الغرفة ({existing.RoomNumber}) مرتبطة بحجز، لذلك لا يمكن نقلها إلى نوع آخر."
                    });
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var removed in existingUnits.Where(item => removedIds.Contains(item.RoomUnitId)))
                    _context.RoomUnits.Remove(removed);

                foreach (var item in dto.Units)
                {
                    RoomUnit unit;

                    if (item.RoomUnitId > 0)
                    {
                        unit = existingById[item.RoomUnitId];
                    }
                    else
                    {
                        unit = new RoomUnit
                        {
                            PlaceId = dto.PlaceId,
                            CreatedAt = DateTime.Now
                        };
                        _context.RoomUnits.Add(unit);
                    }

                    unit.RoomId = item.RoomId;
                    unit.PlaceId = dto.PlaceId;
                    unit.RoomNumber = item.RoomNumber.Trim();
                    unit.FloorNumber = item.FloorNumber;
                    unit.FloorLabel = string.IsNullOrWhiteSpace(item.FloorLabel) ? null : item.FloorLabel.Trim();
                    unit.IsAvailable = item.IsAvailable;
                    unit.Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim();
                }

                var quantityByRoom = dto.Units
                    .GroupBy(item => item.RoomId)
                    .ToDictionary(group => group.Key, group => group.Count());

                foreach (var room in rooms)
                {
                    room.Quantity = quantityByRoom.TryGetValue(room.RoomId, out int count) ? count : 0;
                    _context.Rooms.Update(room);
                }

                await _context.SaveChangesAsync();
                await UpdateHotelRoomCountersAsync(dto.PlaceId);
                await transaction.CommitAsync();

                return Json(new { success = true, message = "تم حفظ الطوابق والغرف الفعلية بنجاح." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                string actualError = ex.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = "تعذر حفظ الطوابق: " + actualError });
            }
        }


        [HttpPost]
        public async Task<IActionResult> AddRoom([FromForm] RoomDto model, List<IFormFile> RoomImages)
        {
            if (model == null || model.PlaceID <= 0 || string.IsNullOrWhiteSpace(model.RoomType) ||
                string.IsNullOrWhiteSpace(model.RoomNumber) || model.Price <= 0 || model.MaxAdults < 1 ||
                model.Units == null || model.Units.Count == 0)
                return Json(new { success = false, message = "أكمل بيانات النوع وأضف غرفة فعلية واحدة على الأقل." });
            if (!await CanManagePlaceAsync(model.PlaceID, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            var cleanNumbers = model.Units.Select(unit => (unit.RoomNumber ?? "").Trim()).ToList();
            if (cleanNumbers.Any(string.IsNullOrWhiteSpace) ||
                cleanNumbers.GroupBy(number => number, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                return Json(new { success = false, message = "أرقام الغرف مطلوبة ولا يجوز تكرارها." });

            bool numberExists = await _context.RoomUnits.AnyAsync(unit =>
                unit.PlaceId == model.PlaceID && cleanNumbers.Contains(unit.RoomNumber));
            if (numberExists)
                return Json(new { success = false, message = "أحد أرقام الغرف مستخدم مسبقًا في هذه المنشأة." });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var imageResult = await SaveNewRoomImagesAsync(RoomImages);
                if (!imageResult.Success)
                    return Json(new { success = false, message = imageResult.Error });

                bool isSuite = model.InternalRoomsCount.HasValue && model.InternalRoomsCount.Value > 0;

                var room = new Room
                {
                    PlaceId = model.PlaceID,
                    RoomType = model.RoomType.Trim(),
                    RoomNumber = model.RoomNumber.Trim(),
                    Price = model.Price,
                    Quantity = model.Units.Count,
                    AreaSquareMeters = model.AreaSquareMeters,
                    MaxAdults = model.MaxAdults,
                    MaxChildren = model.MaxChildren,
                    BedsCount = isSuite ? null : model.BedsCount,
                    BedType = isSuite || string.IsNullOrWhiteSpace(model.BedType) ? null : model.BedType.Trim(),
                    InternalRoomsCount = isSuite ? model.InternalRoomsCount : null,
                    Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                    IsAvailable = model.IsAvailable,
                    ImageUrl = imageResult.Paths.Any() ? string.Join(",", imageResult.Paths) : null
                };

                _context.Rooms.Add(room);
                await _context.SaveChangesAsync();

                foreach (var unitDto in model.Units)
                {
                    _context.RoomUnits.Add(new RoomUnit
                    {
                        PlaceId = model.PlaceID,
                        RoomId = room.RoomId,
                        RoomNumber = unitDto.RoomNumber.Trim(),
                        FloorNumber = unitDto.FloorNumber,
                        FloorLabel = string.IsNullOrWhiteSpace(unitDto.FloorLabel) ? null : unitDto.FloorLabel.Trim(),
                        IsAvailable = unitDto.IsAvailable,
                        Notes = string.IsNullOrWhiteSpace(unitDto.Notes) ? null : unitDto.Notes.Trim(),
                        CreatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                await UpdateHotelRoomCountersAsync(model.PlaceID);
                await transaction.CommitAsync();

                return Json(new { success = true, message = "تمت إضافة نوع الغرفة وغرفه الفعلية بنجاح." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                string actualError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "خطأ: " + actualError });
            }
        }



        [HttpPost]
        public async Task<IActionResult> EditRoom([FromForm] RoomDto updatedRoom, List<IFormFile> RoomImages)
        {
            if (updatedRoom == null || updatedRoom.RoomID <= 0 || string.IsNullOrWhiteSpace(updatedRoom.RoomType) ||
                string.IsNullOrWhiteSpace(updatedRoom.RoomNumber) || updatedRoom.Price <= 0 ||
                updatedRoom.MaxAdults < 1 || updatedRoom.Units == null || updatedRoom.Units.Count == 0)
                return Json(new { success = false, message = "أكمل بيانات النوع وأضف غرفة فعلية واحدة على الأقل." });

            var room = await _context.Rooms.Include(item => item.RoomUnits)
                .FirstOrDefaultAsync(item => item.RoomId == updatedRoom.RoomID);
            if (room == null) return Json(new { success = false, message = "الغرفة غير موجودة." });
            if (!await CanManagePlaceAsync(room.PlaceId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            var cleanNumbers = updatedRoom.Units.Select(unit => (unit.RoomNumber ?? "").Trim()).ToList();
            if (cleanNumbers.Any(string.IsNullOrWhiteSpace) ||
                cleanNumbers.GroupBy(number => number, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                return Json(new { success = false, message = "أرقام الغرف مطلوبة ولا يجوز تكرارها." });

            var incomingIds = updatedRoom.Units.Where(unit => unit.RoomUnitID > 0).Select(unit => unit.RoomUnitID).ToHashSet();
            bool foreignUnitId = incomingIds.Any(id => room.RoomUnits.All(unit => unit.RoomUnitId != id));
            if (foreignUnitId)
                return Json(new { success = false, message = "إحدى الغرف الفعلية لا تتبع هذا النوع." });

            bool duplicateOutsideType = await _context.RoomUnits.AnyAsync(unit =>
                unit.PlaceId == room.PlaceId && unit.RoomId != room.RoomId && cleanNumbers.Contains(unit.RoomNumber));
            if (duplicateOutsideType)
                return Json(new { success = false, message = "أحد أرقام الغرف مستخدم في نوع آخر داخل الفندق." });

            room.RoomType = updatedRoom.RoomType;
            room.RoomNumber = updatedRoom.RoomNumber;
            room.Price = updatedRoom.Price;
            room.Quantity = updatedRoom.Units.Count;
            room.AreaSquareMeters = updatedRoom.AreaSquareMeters;
            room.MaxAdults = updatedRoom.MaxAdults;
            room.MaxChildren = updatedRoom.MaxChildren;
            bool isSuite = updatedRoom.InternalRoomsCount.HasValue && updatedRoom.InternalRoomsCount.Value > 0;
            room.BedsCount = isSuite ? null : updatedRoom.BedsCount;
            room.BedType = isSuite || string.IsNullOrWhiteSpace(updatedRoom.BedType) ? null : updatedRoom.BedType.Trim();
            room.InternalRoomsCount = isSuite ? updatedRoom.InternalRoomsCount : null;
            room.Description = updatedRoom.Description;
            room.IsAvailable = updatedRoom.IsAvailable;

            // 🌟 1. حساب المساحة الحقيقية للصور القديمة (نسخة طبق الأصل من الخدمات)
            long totalExistingSize = 0;
            List<string> currentImages = new List<string>();

            if (!string.IsNullOrEmpty(room.ImageUrl))
            {
                currentImages = room.ImageUrl.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                string wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

                foreach (var img in currentImages)
                {
                    string cleanPath = img.Replace("/", "\\").TrimStart('\\');
                    string physicalPath = Path.Combine(wwwrootPath, cleanPath);

                    if (System.IO.File.Exists(physicalPath))
                        totalExistingSize += new FileInfo(physicalPath).Length;
                    else
                        totalExistingSize += 150 * 1024;
                }
            }

            // 🌟 2. حساب حجم الصور الجديدة المرفوعة
            long newFilesSize = 0;
            if (RoomImages != null && RoomImages.Count > 0)
            {
                foreach (var file in RoomImages)
                {
                    if (file.Length > 0) newFilesSize += file.Length;
                }
            }

            // 🌟 3. الميزان الدقيق: المجموع الكلي (القديم + الجديد)
            long maxTotalSize = 5 * 1024 * 1024; // 5 ميجابايت الحد الأقصى للغرفة
            long grandTotalSize = totalExistingSize + newFilesSize;

            if (grandTotalSize > maxTotalSize)
            {
                double currentMb = Math.Round((double)totalExistingSize / (1024 * 1024), 2);
                double newMb = Math.Round((double)newFilesSize / (1024 * 1024), 2);
                double availableMb = Math.Round((double)(maxTotalSize - totalExistingSize) / (1024 * 1024), 2);

                if (availableMb < 0) availableMb = 0;

                return Json(new
                {
                    success = false,
                    message = $"عذراً! مساحة الصور القديمة لديك ({currentMb} ميجا). والمتبقي لكِ هو ({availableMb} ميجا) فقط، بينما الصور الجديدة حجمها ({newMb} ميجا). يرجى حذف صور قديمة من علامة ❌ لتفريغ مساحة."
                });
            }

            // 🌟 4. إذا كان الوزن سليماً، ندمج الصور الجديدة مع القديمة
            if (RoomImages != null && RoomImages.Count > 0)
            {
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/rooms");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                foreach (var file in RoomImages)
                {
                    if (file.Length > 0)
                    {
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                        string fullPath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(fullPath, FileMode.Create))
                        {
                            await file.CopyToAsync(fileStream);
                        }
                        currentImages.Add("/uploads/rooms/" + uniqueFileName);
                    }
                }

                room.ImageUrl = string.Join(",", currentImages);
            }

            _context.Rooms.Update(room);

            var removedLegacyUnits = room.RoomUnits
                .Where(unit => !incomingIds.Contains(unit.RoomUnitId))
                .ToList();

            if (removedLegacyUnits.Count > 0)
            {
                var removedLegacyIds = removedLegacyUnits.Select(unit => unit.RoomUnitId).ToList();
                bool linkedToBooking = await _context.BookingRoomUnits
                    .AsNoTracking()
                    .AnyAsync(item => removedLegacyIds.Contains(item.RoomUnitId));

                if (linkedToBooking)
                    return Json(new
                    {
                        success = false,
                        message = "لا يمكن حذف غرفة فعلية مرتبطة بحجز. استخدم حالة «غير متاحة» بدل الحذف."
                    });
            }

            foreach (var existingUnit in removedLegacyUnits)
                _context.RoomUnits.Remove(existingUnit);

            foreach (var unitDto in updatedRoom.Units)
            {
                RoomUnit unit;
                if (unitDto.RoomUnitID > 0)
                {
                    unit = room.RoomUnits.First(item => item.RoomUnitId == unitDto.RoomUnitID);
                }
                else
                {
                    unit = new RoomUnit
                    {
                        PlaceId = room.PlaceId,
                        RoomId = room.RoomId,
                        CreatedAt = DateTime.Now
                    };
                    _context.RoomUnits.Add(unit);
                }

                unit.RoomNumber = unitDto.RoomNumber.Trim();
                unit.FloorNumber = unitDto.FloorNumber;
                unit.FloorLabel = string.IsNullOrWhiteSpace(unitDto.FloorLabel) ? null : unitDto.FloorLabel.Trim();
                unit.IsAvailable = unitDto.IsAvailable;
                unit.Notes = string.IsNullOrWhiteSpace(unitDto.Notes) ? null : unitDto.Notes.Trim();
            }

            await _context.SaveChangesAsync();
            await UpdateHotelRoomCountersAsync(room.PlaceId);

            return Json(new { success = true, message = "تم تعديل النوع والطوابق والغرف والصور بنجاح." });
        }




        [HttpPost]
        public async Task<IActionResult> DeleteRoom(int id)
        {
            var room = await _context.Rooms
                .Include(item => item.RoomUnits)
                .FirstOrDefaultAsync(item => item.RoomId == id);

            if (room == null)
                return Json(new { success = false, message = "نوع الغرفة غير موجود." });

            if (!await CanManagePlaceAsync(room.PlaceId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            var unitIds = room.RoomUnits.Select(item => item.RoomUnitId).ToList();

            bool hasRoomBookingHistory = await _context.Bookings
                .AsNoTracking()
                .AnyAsync(item => item.RoomId == room.RoomId);

            bool hasUnitBookingHistory = unitIds.Count > 0 &&
                await _context.BookingRoomUnits
                    .AsNoTracking()
                    .AnyAsync(item => unitIds.Contains(item.RoomUnitId));

            if (hasRoomBookingHistory || hasUnitBookingHistory)
            {
                return Json(new
                {
                    success = false,
                    message = "لا يمكن حذف هذا النوع لأنه مرتبط بحجوزات. احتفظ به أو أوقف وحداته بدل الحذف."
                });
            }

            int placeId = room.PlaceId;

            _context.RoomUnits.RemoveRange(room.RoomUnits);
            _context.Rooms.Remove(room);

            await _context.SaveChangesAsync();
            await UpdateHotelRoomCountersAsync(placeId);

            return Json(new { success = true, message = "تم حذف نوع الغرفة ووحداته غير المرتبطة بحجوزات." });
        }


        private async Task<(bool Success, string Error, string? ImageUrl)> MergeRoomImagesAsync(Room room, List<IFormFile> files)
        {
            var currentImages = string.IsNullOrWhiteSpace(room.ImageUrl)
                ? new List<string>()
                : room.ImageUrl
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(path => path.Trim())
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .ToList();

            long existingSize = 0;
            foreach (string imagePath in currentImages)
            {
                string relativePath = imagePath
                    .Replace('/', Path.DirectorySeparatorChar)
                    .TrimStart(Path.DirectorySeparatorChar);

                string physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath);

                if (System.IO.File.Exists(physicalPath))
                    existingSize += new FileInfo(physicalPath).Length;
                else
                    existingSize += 150 * 1024;
            }

            long newSize = files?
                .Where(file => file != null && file.Length > 0)
                .Sum(file => file.Length) ?? 0;

            const long maxTotalSize = 5 * 1024 * 1024;
            if (existingSize + newSize > maxTotalSize)
            {
                double currentMb = Math.Round((double)existingSize / (1024 * 1024), 2);
                double availableMb = Math.Round((double)Math.Max(0, maxTotalSize - existingSize) / (1024 * 1024), 2);

                return (
                    false,
                    $"الصور الحالية حجمها نحو {currentMb} ميجابايت، والمتاح للصور الجديدة {availableMb} ميجابايت فقط.",
                    room.ImageUrl
                );
            }

            var newImages = await SaveNewRoomImagesAsync(files ?? new List<IFormFile>());
            if (!newImages.Success)
                return (false, newImages.Error, room.ImageUrl);

            currentImages.AddRange(newImages.Paths);
            string? merged = currentImages.Count > 0 ? string.Join(",", currentImages) : null;

            return (true, string.Empty, merged);
        }


        private async Task<(bool Success, string Error, List<string> Paths)> SaveNewRoomImagesAsync(List<IFormFile> files)
        {
            var paths = new List<string>();
            if (files == null || files.Count == 0) return (true, "", paths);

            const long maxTotalSize = 5 * 1024 * 1024;
            long totalSize = files.Where(file => file != null).Sum(file => file.Length);
            if (totalSize > maxTotalSize)
                return (false, "إجمالي صور النوع يتجاوز 5 ميجابايت.", paths);
            if (files.Any(file => file != null && file.Length > maxTotalSize))
                return (false, "إحدى الصور تتجاوز 5 ميجابايت.", paths);

            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "rooms");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            foreach (var file in files.Where(file => file != null && file.Length > 0))
            {
                string uniqueFileName = Guid.NewGuid() + "_" + Path.GetFileName(file.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using var fileStream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(fileStream);
                paths.Add("/uploads/rooms/" + uniqueFileName);
            }

            return (true, "", paths);
        }

        private async Task UpdateHotelRoomCountersAsync(int placeId)
        {
            var place = await _context.Places.FindAsync(placeId);
            if (place == null) return;

            place.RoomsCount = await _context.RoomUnits.CountAsync(unit =>
                unit.PlaceId == placeId &&
                (!unit.Room.InternalRoomsCount.HasValue || unit.Room.InternalRoomsCount.Value <= 0));

            place.SuitesCount = await _context.RoomUnits.CountAsync(unit =>
                unit.PlaceId == placeId &&
                unit.Room.InternalRoomsCount.HasValue &&
                unit.Room.InternalRoomsCount.Value > 0);

            _context.Places.Update(place);
            await _context.SaveChangesAsync();
        }



        // =========================================================================
        // 📁 القسم التاسع: دوال مساعدة (Helpers)
        // =========================================================================

        [HttpGet]
        public async Task<IActionResult> GetAmenityForEdit(int id)
        {
            try
            {
                var amenity = await _context.PlaceAmenities.FindAsync(id);
                if (amenity == null)
                    return Json(new { success = false, message = "العنصر غير موجود في قاعدة البيانات." });
                if (!await CanManagePlaceAsync(amenity.PlaceId, AdminPermissionKeys.Places.View))
                    return StatusCode(403, new { success = false, message = "غير مصرح لك." });

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        amenityID = amenity.AmenityId,
                        amenityName = amenity.AmenityName
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "خطأ في السيرفر: " + ex.Message });
            }
        }



        [HttpGet]
        public async Task<IActionResult> GetPackageForEdit(int id)
        {
            var pkg = await _context.PlacePackages.FindAsync(id);
            if (pkg == null) return Json(new { success = false, message = "العنصر غير موجود" });
            if (!await CanManagePlaceAsync(pkg.PlaceId, AdminPermissionKeys.Places.View))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            return Json(new
            {
                success = true,
                data = new
                {
                    packageID = pkg.PackageId,
                    packageName = pkg.PackageName,
                    price = pkg.Price,
                    capacity = pkg.Capacity,
                    timeFrame = pkg.TimeFrame,
                    startTime = pkg.StartTime,
                    endTime = pkg.EndTime,
                    isActive = pkg.IsActive
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> TogglePlacePackage(int id)
        {
            var package = await _context.PlacePackages.FindAsync(id);
            if (package == null)
                return Json(new { success = false, message = "الباقة غير موجودة." });

            if (!await CanManagePlaceAsync(package.PlaceId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            package.IsActive = !(package.IsActive ?? true);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                isActive = package.IsActive,
                message = package.IsActive == true ? "تم تفعيل الباقة." : "تم إيقاف الباقة مؤقتًا."
            });
        }
    }
}
