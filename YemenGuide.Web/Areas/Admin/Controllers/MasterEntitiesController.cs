// =============================================================
// MasterEntitiesController.cs
// إدارة الفئات والمدن - النسخة المحدثة
// =============================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YemenGuide.Core.Constants;
using YemenGuide.Core.Entities;
using YemenGuide.Web.Filters;
using YemenGuide.Data;
using YemenGuide.Web.Services.ExchangeRates;

namespace YemenGuide.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MasterEntitiesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ExchangeRateRegionResolver _exchangeRateRegionResolver;

        public MasterEntitiesController(
            AppDbContext context,
            IWebHostEnvironment webHostEnvironment,
            ExchangeRateRegionResolver exchangeRateRegionResolver)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _exchangeRateRegionResolver = exchangeRateRegionResolver;
        }

        // =============================================================
        // 1. الصفحة الرئيسية للفئات والمدن
        // =============================================================
        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.MasterData.View)]
        public async Task<IActionResult> MasterEntitiesManagement()
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            var cities = await _context.Cities
                .AsNoTracking()
                .OrderBy(c => c.CityName)
                .ToListAsync();

            var categoryPlaceCounts = await _context.Places
                .AsNoTracking()
                .GroupBy(p => p.CategoryId)
                .Select(g => new
                {
                    CategoryId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

            var cityPlaceCounts = await _context.Places
                .AsNoTracking()
                .GroupBy(p => p.CityId)
                .Select(g => new
                {
                    CityId = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.CityId, x => x.Count);

            ViewBag.Cities = cities;
            ViewBag.CategoryPlaceCounts = categoryPlaceCounts;
            ViewBag.CityPlaceCounts = cityPlaceCounts;

            return View(categories);
        }

        // =============================================================
        // 2. إدارة الفئات
        // =============================================================
        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.MasterData.ManageCategories)]
        public async Task<IActionResult> AddCategory(
            [FromForm] Category obj,
            IFormFile? imageFile)
        {
            if (obj == null || string.IsNullOrWhiteSpace(obj.CategoryName))
            {
                return Json(new
                {
                    success = false,
                    message = "يرجى التأكد من إدخال اسم الفئة."
                });
            }

            obj.CategoryName = obj.CategoryName.Trim();
            obj.ThemeColor = NormalizeColor(obj.ThemeColor);
            obj.IconClass = string.IsNullOrWhiteSpace(obj.IconClass)
                ? "bi-star"
                : obj.IconClass.Trim();
            obj.HasBookingSystem = obj.BookingSystemId > 0;

            // توافق مع أي نموذج قديم لا يرسل الحقل الجديد.
            if (!Request.Form.ContainsKey(nameof(Category.OwnerCanAddPlaces)))
            {
                obj.OwnerCanAddPlaces = true;
            }

            var normalizedInput = NormalizeName(obj.CategoryName);
            var existingNames = await _context.Categories
                .AsNoTracking()
                .Select(c => c.CategoryName)
                .ToListAsync();

            if (existingNames.Any(name => NormalizeName(name) == normalizedInput))
            {
                return Json(new
                {
                    success = false,
                    message = "عفواً، هذه الفئة موجودة مسبقاً!"
                });
            }

            try
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    obj.ImagePath = await SaveCategoryImageAsync(imageFile);
                }

                _context.Categories.Add(obj);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "تمت إضافة الفئة بنجاح!"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "حدث خطأ في قاعدة البيانات: " + ex.Message
                });
            }
        }

        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.MasterData.ManageCategories)]
        public async Task<IActionResult> GetCategory(int id)
        {
            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (category == null)
            {
                return Json(new
                {
                    success = false,
                    message = "الفئة غير موجودة!"
                });
            }

            return Json(new
            {
                success = true,
                data = category
            });
        }

        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.MasterData.ManageCategories)]
        public async Task<IActionResult> EditCategory(
            [FromForm] Category obj,
            IFormFile? imageFile)
        {
            if (obj == null ||
                obj.CategoryId <= 0 ||
                string.IsNullOrWhiteSpace(obj.CategoryName))
            {
                return Json(new
                {
                    success = false,
                    message = "بيانات الفئة غير صالحة."
                });
            }

            try
            {
                var existingCategory = await _context.Categories
                    .FirstOrDefaultAsync(c => c.CategoryId == obj.CategoryId);

                if (existingCategory == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "الفئة غير موجودة."
                    });
                }

                var normalizedInput = NormalizeName(obj.CategoryName);
                var otherNames = await _context.Categories
                    .AsNoTracking()
                    .Where(c => c.CategoryId != obj.CategoryId)
                    .Select(c => c.CategoryName)
                    .ToListAsync();

                if (otherNames.Any(name => NormalizeName(name) == normalizedInput))
                {
                    return Json(new
                    {
                        success = false,
                        message = "توجد فئة أخرى بالاسم نفسه."
                    });
                }

                existingCategory.CategoryName = obj.CategoryName.Trim();
                existingCategory.IconClass = string.IsNullOrWhiteSpace(obj.IconClass)
                    ? "bi-star"
                    : obj.IconClass.Trim();
                existingCategory.ThemeColor = NormalizeColor(obj.ThemeColor);
                existingCategory.BookingSystemId = obj.BookingSystemId;
                existingCategory.HasBookingSystem = obj.BookingSystemId > 0;

                if (Request.Form.ContainsKey(nameof(Category.OwnerCanAddPlaces)))
                {
                    existingCategory.OwnerCanAddPlaces = obj.OwnerCanAddPlaces;
                }

                if (imageFile != null && imageFile.Length > 0)
                {
                    existingCategory.ImagePath = await SaveCategoryImageAsync(imageFile);
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "تم تعديل الفئة بنجاح!"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "حدث خطأ: " + ex.Message
                });
            }
        }

        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.MasterData.ManageCategories)]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "الفئة غير موجودة."
                    });
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "تم حذف الفئة بنجاح!"
                });
            }
            catch (Exception)
            {
                return Json(new
                {
                    success = false,
                    message = "لا يمكن حذف هذه الفئة لوجود منشآت مرتبطة بها."
                });
            }
        }

        // =============================================================
        // 3. إدارة المدن
        // =============================================================
        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.MasterData.ManageCities)]
        public async Task<IActionResult> AddCity([FromBody] City obj)
        {
            if (obj == null || string.IsNullOrWhiteSpace(obj.CityName))
            {
                return Json(new
                {
                    success = false,
                    message = "يرجى إدخال اسم المدينة."
                });
            }

            obj.CityName = obj.CityName.Trim();
            obj.ThemeColor = NormalizeColor(obj.ThemeColor);

            // تحديد سوق الصرف تلقائياً من اسم المدينة داخل السيرفر.
            // لا يعتمد النظام على أي قيمة يرسلها المتصفح لهذا الحقل.
            obj.ExchangeRateRegion =
                _exchangeRateRegionResolver.Resolve(obj.CityName);

            var normalizedInput = NormalizeName(obj.CityName);
            var existingNames = await _context.Cities
                .AsNoTracking()
                .Select(c => c.CityName)
                .ToListAsync();

            if (existingNames.Any(name => NormalizeName(name) == normalizedInput))
            {
                return Json(new
                {
                    success = false,
                    message = "عفواً، هذه المدينة مضافة مسبقاً!"
                });
            }

            try
            {
                _context.Cities.Add(obj);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "تمت إضافة المدينة بنجاح!"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "حدث خطأ أثناء إضافة المدينة: " + ex.Message
                });
            }
        }

        [HttpGet]
        [RequireAdminPermission(AdminPermissionKeys.MasterData.ManageCities)]
        public async Task<IActionResult> GetCity(int id)
        {
            var city = await _context.Cities
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CityId == id);

            if (city == null)
            {
                return Json(new
                {
                    success = false,
                    message = "المدينة غير موجودة!"
                });
            }

            return Json(new
            {
                success = true,
                data = city
            });
        }

        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.MasterData.ManageCities)]
        public async Task<IActionResult> EditCity([FromBody] City obj)
        {
            if (obj == null ||
                obj.CityId <= 0 ||
                string.IsNullOrWhiteSpace(obj.CityName))
            {
                return Json(new
                {
                    success = false,
                    message = "بيانات المدينة غير صالحة."
                });
            }

            try
            {
                var existingCity = await _context.Cities
                    .FirstOrDefaultAsync(c => c.CityId == obj.CityId);

                if (existingCity == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "المدينة غير موجودة."
                    });
                }

                var normalizedInput = NormalizeName(obj.CityName);
                var otherNames = await _context.Cities
                    .AsNoTracking()
                    .Where(c => c.CityId != obj.CityId)
                    .Select(c => c.CityName)
                    .ToListAsync();

                if (otherNames.Any(name => NormalizeName(name) == normalizedInput))
                {
                    return Json(new
                    {
                        success = false,
                        message = "توجد مدينة أخرى بالاسم نفسه."
                    });
                }

                existingCity.CityName = obj.CityName.Trim();
                existingCity.ThemeColor = NormalizeColor(obj.ThemeColor);

                // عند تغيير اسم المدينة يعاد تحديد سوق الصرف تلقائياً أيضاً.
                existingCity.ExchangeRateRegion =
                    _exchangeRateRegionResolver.Resolve(existingCity.CityName);

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "تم تعديل المدينة بنجاح!"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "حدث خطأ: " + ex.Message
                });
            }
        }

        [HttpPost]
        [RequireAdminPermission(AdminPermissionKeys.MasterData.ManageCities)]
        public async Task<IActionResult> DeleteCity(int id)
        {
            try
            {
                var city = await _context.Cities.FindAsync(id);
                if (city == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "المدينة غير موجودة."
                    });
                }

                _context.Cities.Remove(city);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "تم حذف المدينة بنجاح!"
                });
            }
            catch (Exception)
            {
                return Json(new
                {
                    success = false,
                    message = "لا يمكن الحذف لارتباط هذه المدينة بمنشآت أو مستخدمين."
                });
            }
        }

        // =============================================================
        // Helpers
        // =============================================================
        private async Task<string> SaveCategoryImageAsync(IFormFile imageFile)
        {
            var uploadsFolder = Path.Combine(
                _webHostEnvironment.WebRootPath,
                "images",
                "categories");

            Directory.CreateDirectory(uploadsFolder);

            var extension = Path.GetExtension(imageFile.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            await using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }

            return "/images/categories/" + uniqueFileName;
        }

        private static string NormalizeName(string? value)
        {
            return (value ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Replace("ة", "ه")
                .Replace("أ", "ا")
                .Replace("إ", "ا")
                .Replace("آ", "ا");
        }

        private static string NormalizeColor(string? color)
        {
            if (string.IsNullOrWhiteSpace(color))
            {
                return "#00A6DA";
            }

            var value = color.Trim();
            return value.StartsWith("#", StringComparison.Ordinal)
                ? value
                : "#" + value;
        }
    }
}
