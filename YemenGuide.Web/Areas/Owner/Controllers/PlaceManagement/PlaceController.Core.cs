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
using BCrypt.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using YemenGuide.Core.Constants;
using YemenGuide.Web.Services.Employees;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    [Area("Owner")]
    [Authorize(Roles = "Owner,Admin")]
    [AuthorizeActiveOwner]
    public partial class PlaceController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment; // 🌟 تم إضافة هذا المتغير لحل خطأ رفع الصور

        // 🌟 تم تمرير المتغير هنا في الـ Constructor
        public PlaceController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        private bool TryGetCurrentUserId(out int userId) =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId > 0;

        private async Task<bool> HasAdminPermissionAsync(string permissionKey)
        {
            if (!User.IsInRole("Admin")) return true;
            if (!TryGetCurrentUserId(out int adminId)) return false;

            var permissionService = HttpContext.RequestServices
                .GetRequiredService<IAdminPermissionService>();

            return await permissionService.HasPermissionAsync(
                adminId,
                permissionKey,
                HttpContext.RequestAborted);
        }

        private async Task<bool> CanManagePlaceAsync(int placeId, string? adminPermission = null)
        {
            if (!TryGetCurrentUserId(out int currentUserId))
                return false;

            // داخل واجهات المالك: ملكية المنشأة الفعلية تأتي أولاً.
            // بهذا يستطيع الأدمن إدارة منشآته الخاصة كمالك كامل.
            bool ownsPlace = await _context.Places
                .AsNoTracking()
                .AnyAsync(place =>
                    place.PlaceId == placeId &&
                    place.OwnerId == currentUserId);

            if (ownsPlace)
                return true;

            // منشآت الآخرين لا تصبح "منشآت الأدمن" داخل وضع المالك.
            // تبقى صلاحية الإدارة الأصلية متاحة فقط للإجراءات التي تمرر مفتاح صلاحية صريحًا.
            if (User.IsInRole("Admin"))
                return adminPermission != null &&
                       await HasAdminPermissionAsync(adminPermission);

            return false;
        }

        private async Task<bool> IsPlatformOwnerAsync(int? ownerId)
        {
            if (!ownerId.HasValue || ownerId.Value <= 0) return false;

            int? platformOwnerId = await _context.SystemSettings
                .AsNoTracking()
                .OrderBy(setting => setting.Id)
                .Select(setting => setting.PlatformOwnerUserId)
                .FirstOrDefaultAsync();

            return platformOwnerId.HasValue && platformOwnerId.Value == ownerId.Value;
        }

        // =========================================================================
        // ⚙️ وحدة التحكم المركزية - القسم المشترك (Common & Dashboard)
        // =========================================================================

        /// <summary>
        /// الصفحة الرئيسية لإدارة المنشآت (تجمع التبويبات)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ManagePlaces(int? categoryId, int? ownerId, bool autoOpenWizard = false)
        {
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.SelectedOwnerId = ownerId;
            ViewBag.AutoOpenWizard = autoOpenWizard;

            var categories = await _context.Categories.AsNoTracking().ToListAsync();
            ViewBag.Categories = categories;

            var loggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(loggedInUserId, out int currentUserId) || currentUserId <= 0)
                return Unauthorized();

            // واجهة المالك تعرض دائمًا منشآت الحساب الحالي فقط، سواء كان Owner أو Admin.
            int targetOwnerId = currentUserId;

            // 🚀 تم حذف (p.PlaceImages) من هنا لأن القائمة لا تحتاجه للعرض الأساسي
            var places = await _context.Places
              .Include(p => p.Category)
              .AsNoTracking()
              .Where(p => p.OwnerId == targetOwnerId)
              .OrderByDescending(p => p.PlaceId) // 🌟 هذا السطر السحري لعرض الأحدث أولاً
              .ToListAsync();

            return View(places);
        }
    }
}
