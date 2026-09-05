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


        [HttpPost]
        public async Task<IActionResult> DeleteRoomImage(int roomId, string imagePath)
        {
            try
            {
                var room = await _context.Rooms.FindAsync(roomId);
                if (room == null) return Json(new { success = false, message = "الغرفة غير موجودة." });
                if (!await CanManagePlaceAsync(room.PlaceId, AdminPermissionKeys.Places.Edit))
                    return StatusCode(403, new { success = false, message = "غير مصرح لك." });

                if (!string.IsNullOrEmpty(room.ImageUrl))
                {
                    var images = room.ImageUrl.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

                    if (images.Contains(imagePath))
                    {
                        images.Remove(imagePath);
                        room.ImageUrl = images.Any() ? string.Join(",", images) : null;
                        _context.Rooms.Update(room);
                        await _context.SaveChangesAsync();

                        // حذف الصورة فعلياً
                        var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, imagePath.TrimStart('/'));
                        if (System.IO.File.Exists(physicalPath)) System.IO.File.Delete(physicalPath);

                        // 🌟 السحر هنا: حساب المساحة الحالية بعد الحذف لإرسالها للواجهة
                        long totalExistingSize = 0;
                        if (!string.IsNullOrEmpty(room.ImageUrl))
                        {
                            var remainingImages = room.ImageUrl.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                            string wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                            foreach (var img in remainingImages)
                            {
                                string cleanPath = img.Replace("/", "\\").TrimStart('\\');
                                string pPath = Path.Combine(wwwrootPath, cleanPath);
                                if (System.IO.File.Exists(pPath)) totalExistingSize += new FileInfo(pPath).Length;
                                else totalExistingSize += 150 * 1024;
                            }
                        }
                        double currentMb = Math.Round((double)totalExistingSize / (1024 * 1024), 2);

                        return Json(new { success = true, currentSizeMb = currentMb, message = "تم مسح الصورة." });
                    }
                }
                return Json(new { success = false, message = "الصورة غير موجودة." });
            }
            catch (Exception ex) { return Json(new { success = false, message = "حدث خطأ." }); }
        }


        [HttpPost]
        public async Task<IActionResult> DeleteServiceImage(int serviceId, string imagePath)
        {
            try
            {
                var service = await _context.PlaceAdditionalServices.FindAsync(serviceId);
                if (service == null) return Json(new { success = false, message = "الخدمة غير موجودة." });
                if (!await CanManagePlaceAsync(service.PlaceId, AdminPermissionKeys.Places.Edit))
                    return StatusCode(403, new { success = false, message = "غير مصرح لك." });

                if (!string.IsNullOrEmpty(service.ImageUrl))
                {
                    var images = service.ImageUrl.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (images.Contains(imagePath))
                    {
                        images.Remove(imagePath);
                        service.ImageUrl = images.Any() ? string.Join(",", images) : null;
                        _context.PlaceAdditionalServices.Update(service);
                        await _context.SaveChangesAsync();

                        var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, imagePath.TrimStart('/'));
                        if (System.IO.File.Exists(physicalPath)) System.IO.File.Delete(physicalPath);

                        // 🌟 حساب المساحة الحالية للخدمة
                        long totalExistingSize = 0;
                        if (!string.IsNullOrEmpty(service.ImageUrl))
                        {
                            var remainingImages = service.ImageUrl.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                            string wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                            foreach (var img in remainingImages)
                            {
                                string cleanPath = img.Replace("/", "\\").TrimStart('\\');
                                string pPath = Path.Combine(wwwrootPath, cleanPath);
                                if (System.IO.File.Exists(pPath)) totalExistingSize += new FileInfo(pPath).Length;
                                else totalExistingSize += 150 * 1024;
                            }
                        }
                        double currentMb = Math.Round((double)totalExistingSize / (1024 * 1024), 2);

                        return Json(new { success = true, currentSizeMb = currentMb, message = "تم مسح الصورة." });
                    }
                }
                return Json(new { success = false, message = "الصورة غير موجودة." });
            }
            catch (Exception ex) { return Json(new { success = false, message = "حدث خطأ." }); }
        }


        // =========================================================================
        // 📁 القسم الثامن: دوال الصور (Images CRUD)
        // =========================================================================

        [HttpGet]
        [Authorize(Roles = "Owner, Admin")]
        public async Task<IActionResult> GetPlaceImages(int placeId)
        {
            var place = await _context.Places.Include(p => p.PlaceImages).FirstOrDefaultAsync(p => p.PlaceId == placeId);
            if (place == null) return Json(new { success = false, message = "المنشأة غير موجودة" });
            if (!await CanManagePlaceAsync(placeId, AdminPermissionKeys.Places.View))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            var gallery = place.PlaceImages.Select(i => new { id = i.ImageId, path = i.ImagePath }).ToList();
            return Json(new { success = true, coverImage = place.CoverImage, gallery = gallery });
        }


        [HttpPost]
        [Authorize(Roles = "Owner, Admin")]
        public async Task<IActionResult> DeletePlaceImage(int placeId, int imageId, bool isCover)
        {
            var place = await _context.Places.Include(p => p.PlaceImages).FirstOrDefaultAsync(p => p.PlaceId == placeId);
            if (place == null) return Json(new { success = false });
            if (!await CanManagePlaceAsync(placeId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            if (isCover)
            {
                if (!string.IsNullOrWhiteSpace(place.CoverImage))
                {
                    var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", place.CoverImage.TrimStart('/'));
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }
                place.CoverImage = null;
                _context.Update(place);
            }
            else
            {
                var img = place.PlaceImages.FirstOrDefault(i => i.ImageId == imageId);
                if (img != null)
                {
                    var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", img.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    _context.PlaceImages.Remove(img);
                }
            }
            await _context.SaveChangesAsync();

            // 🌟 السحر الجديد: حساب المساحة المتبقية للصور الإضافية فوراً بعد الحذف لإرسالها للشاشة
            long totalExistingSize = 0;
            string wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            if (place.PlaceImages != null && place.PlaceImages.Any())
            {
                foreach (var img in place.PlaceImages)
                {
                    if (!string.IsNullOrEmpty(img.ImagePath))
                    {
                        string cleanPath = img.ImagePath.Replace("/", "\\").TrimStart('\\');
                        string physicalPath = Path.Combine(wwwrootPath, cleanPath);
                        if (System.IO.File.Exists(physicalPath)) totalExistingSize += new FileInfo(physicalPath).Length;
                        else totalExistingSize += 150 * 1024;
                    }
                }
            }

            double currentMb = Math.Round((double)totalExistingSize / (1024 * 1024), 2);

            // إرسال المساحة المتبقية (currentSizeMb) مع رسالة النجاح
            return Json(new { success = true, currentSizeMb = currentMb });
        }


        [HttpPost]
        [Authorize(Roles = "Owner, Admin")]
        public async Task<IActionResult> UploadNewPlaceImages(int placeId, List<IFormFile> newImages)
        {
            var place = await _context.Places.Include(p => p.PlaceImages).FirstOrDefaultAsync(p => p.PlaceId == placeId);
            if (place == null) return Json(new { success = false, message = "المنشأة غير موجودة" });
            if (!await CanManagePlaceAsync(placeId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            // 🌟 1. حساب المساحة للصور الإضافية فقط (تم استبعاد الغلاف تماماً من الحساب)
            long totalExistingSize = 0;
            string wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            if (place.PlaceImages != null && place.PlaceImages.Any())
            {
                foreach (var img in place.PlaceImages)
                {
                    if (!string.IsNullOrEmpty(img.ImagePath))
                    {
                        string cleanPath = img.ImagePath.Replace("/", "\\").TrimStart('\\');
                        string physicalPath = Path.Combine(wwwrootPath, cleanPath);
                        if (System.IO.File.Exists(physicalPath)) totalExistingSize += new FileInfo(physicalPath).Length;
                        else totalExistingSize += 150 * 1024;
                    }
                }
            }

            // 🌟 2. حساب مساحة الصور الجديدة
            long newFilesSize = 0;
            if (newImages != null && newImages.Count > 0)
            {
                foreach (var file in newImages)
                {
                    if (file.Length > 0) newFilesSize += file.Length;
                }
            }

            // 🌟 3. الميزان الدقيق: المجموع الكلي (10 ميجا لصور الألبوم فقط)
            long maxTotalSize = 10 * 1024 * 1024;
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
                    message = $"عذراً! مساحة الصور الإضافية الحالية ({currentMb} ميجا). المتبقي لك ({availableMb} ميجا) فقط، بينما الصور الجديدة حجمها ({newMb} ميجا). يرجى حذف صور إضافية قديمة لتفريغ مساحة."
                });
            }

            // 🌟 4. الحفظ
            var uploadsFolder = Path.Combine(wwwrootPath, "images/places");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            int uploadedCount = 0;
            string newCoverPath = null;

            if (newImages != null && newImages.Count > 0)
            {
                foreach (var file in newImages)
                {
                    if (file.Length > 0)
                    {
                        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                        var filePath = Path.Combine(uploadsFolder, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create)) { await file.CopyToAsync(stream); }

                        string dbPath = "/images/places/" + fileName;

                        // إذا كان الغلاف فارغاً، نضع أول صورة كغلاف، وإلا نرسلها للألبوم
                        if (string.IsNullOrEmpty(place.CoverImage))
                        {
                            place.CoverImage = dbPath;
                            newCoverPath = dbPath;
                            _context.Update(place);
                        }
                        else
                        {
                            _context.PlaceImages.Add(new PlaceImage { PlaceId = placeId, ImagePath = dbPath });
                        }
                        uploadedCount++;
                    }
                }
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true, message = $"تم رفع {uploadedCount} صور بنجاح", newCoverPath = newCoverPath });
        }


        [HttpPost]
        [Authorize(Roles = "Owner, Admin")]
        public async Task<IActionResult> UploadCoverImage(int placeId, IFormFile coverImage)
        {
            var place = await _context.Places.FirstOrDefaultAsync(p => p.PlaceId == placeId);
            if (place == null) return Json(new { success = false, message = "المنشأة غير موجودة" });
            if (!await CanManagePlaceAsync(placeId, AdminPermissionKeys.Places.Edit))
                return StatusCode(403, new { success = false, message = "غير مصرح لك." });

            if (coverImage == null || coverImage.Length <= 0)
                return Json(new { success = false, message = "يرجى اختيار صورة غلاف صالحة." });

            // 🌟 ميزان مستقل لصورة الغلاف فقط (3 ميجا) ولا يتأثر بصور الألبوم
            long newFileSize = coverImage != null ? coverImage.Length : 0;
            long maxCoverSize = 3 * 1024 * 1024; // 3 ميجا

            if (newFileSize > maxCoverSize)
            {
                return Json(new { success = false, message = "عذراً، مساحة صورة الغلاف تتجاوز الحد الأقصى (3 ميجا)." });
            }

            string wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(coverImage.FileName);
            var filePath = Path.Combine(wwwrootPath, "images/places", fileName);

            using (var stream = new FileStream(filePath, FileMode.Create)) { await coverImage.CopyToAsync(stream); }

            string newPath = "/images/places/" + fileName;
            place.CoverImage = newPath;
            await _context.SaveChangesAsync();

            return Json(new { success = true, newCoverPath = newPath });
        }
    }
}
