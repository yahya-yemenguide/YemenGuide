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
            if (User.IsInRole("Admin"))
                return adminPermission == null || await HasAdminPermissionAsync(adminPermission);
            if (!TryGetCurrentUserId(out int ownerId)) return false;

            return await _context.Places
                .AsNoTracking()
                .AnyAsync(place => place.PlaceId == placeId && place.OwnerId == ownerId);
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
            if (User.IsInRole("Admin") && !await HasAdminPermissionAsync(AdminPermissionKeys.Places.View))
                return Forbid();

            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.SelectedOwnerId = ownerId;
            ViewBag.AutoOpenWizard = autoOpenWizard;

            var categories = await _context.Categories.AsNoTracking().ToListAsync();
            ViewBag.Categories = categories;

            var loggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(loggedInUserId, out int currentUserId) || currentUserId <= 0)
                return Unauthorized();

            // المالك لا يستطيع تمرير رقم مالك آخر في الرابط. السماح بذلك محصور بالإدارة.
            int targetOwnerId = User.IsInRole("Admin") && ownerId.HasValue
                ? ownerId.Value
                : currentUserId;

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
