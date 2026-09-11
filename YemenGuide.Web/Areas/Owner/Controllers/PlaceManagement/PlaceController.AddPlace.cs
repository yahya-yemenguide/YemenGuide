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
        // 📁 القسم الثاني: التبويبة الثانية - إضافة منشأة جديدة (AddPlace)
        // =========================================================================

        /// <summary>
        /// تحميل واجهة معالج إضافة المنشأة (Wizard)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Owner")]
        [AllowPendingOwnerVerification]
        public IActionResult AddPlace()
        {
            // المالك يرى فقط الفئات المسموح له بإضافة منشآت ضمنها.
            // الفئات المخصصة للإدارة تظل موجودة في النظام وتظهر للعملاء،
            // لكنها لا تظهر هنا ضمن خيارات إضافة منشأة للمالك.
            var categories = _context.Categories
                .AsNoTracking()
                .Where(c => c.OwnerCanAddPlaces == true)
                .ToList();

            if (categories == null || !categories.Any())
                return Content("لا توجد فئات متاحة للمالك لإضافة منشأة حاليًا.");

            ViewBag.Categories = categories;
            return PartialView("~/Areas/Owner/Views/Place/_AddPlaceWizard.cshtml");
        }


        /// <summary>
        /// تحميل نموذج الاستبيان الديناميكي بناءً على الفئة المختارة
        /// </summary>
        [HttpGet]
        [AllowPendingOwnerVerification]
        public async Task<IActionResult> LoadPlaceForm(int categoryId)
        {
            if (User.IsInRole("Admin") && !await HasAdminPermissionAsync(AdminPermissionKeys.Places.Create))
                return StatusCode(403, "ليس لديك صلاحية إضافة منشأة.");

            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

            if (category == null)
                return NotFound("عذراً، التصنيف غير موجود.");

            // حماية الخادم: إخفاء الفئة من الواجهة وحده لا يكفي،
            // لذلك نمنع المالك من فتح نموذج فئة مخصصة للإدارة حتى عبر رابط مباشر.
            bool currentUserIsAdmin = false;
            var currentUserIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(currentUserIdClaim) &&
                int.TryParse(currentUserIdClaim, out int currentUserId))
            {
                currentUserIsAdmin = await _context.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.UserId == currentUserId && u.Role == "Admin");
            }

            if (category.OwnerCanAddPlaces != true && !currentUserIsAdmin)
            {
                return StatusCode(403,
                    "هذه الفئة مخصصة لإضافة المنشآت من قِبل الإدارة فقط.");
            }

            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.BookingSystemId = category.BookingSystemId;
            ViewBag.CategoryName = category.CategoryName;
            ViewBag.Cities = await _context.Cities.AsNoTracking().ToListAsync();

            // جلب بيانات المالك الحالي لتعبئتها تلقائياً
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
            {
                var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
                if (currentUser != null)
                {
                    ViewBag.CurrentOwnerName = currentUser.FullName;
                    ViewBag.CurrentOwnerPhone = currentUser.PhoneNumber ?? "";

                    // رقم الحجوزات يُؤخذ من آخر منشأة تخص المالك الحالي فقط.
                    // لا نستخدم localStorage حتى لا ينتقل الرقم بين الحسابات على نفس المتصفح.
                    if (currentUser.Role == SystemRoles.Owner)
                    {
                        ViewBag.CurrentBookingPhone = await _context.Places
                            .AsNoTracking()
                            .Where(p => p.OwnerId == userId && !string.IsNullOrWhiteSpace(p.PhoneNumber))
                            .OrderByDescending(p => p.PlaceId)
                            .Select(p => p.PhoneNumber)
                            .FirstOrDefaultAsync() ?? "";
                    }
                }
            }

            return PartialView("~/Areas/Owner/Views/Place/Templates/_AddPlaceForm.cshtml");
        }




        /// <summary>
        /// حفظ المنشأة الكاملة (مالك جديد، موجود، أو المالك نفسه)
        /// </summary>
        [HttpPost]
        [AllowPendingOwnerVerification]
        [RequestFormLimits(ValueCountLimit = 10000, MultipartBodyLengthLimit = 104857600)]
        [RequestSizeLimit(104857600)]
        public async Task<IActionResult> AddFullPlace([FromForm] FullPlaceWizardDto model, IFormFile CoverImageUpload, List<IFormFile> GalleryImages)
        {
            string errorStep = string.Empty;

            try
            {
                if (model == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "لم تصل بيانات المنشأة إلى الخادم. أعيدي فتح النموذج وحاولي مرة أخرى."
                    });
                }

                if (User.IsInRole("Admin") && !await HasAdminPermissionAsync(AdminPermissionKeys.Places.Create))
                    return StatusCode(403, new { success = false, message = "ليس لديك صلاحية إضافة منشأة." });

                // ================================================================
                // حماية الفئات المخصصة للإدارة قبل تنفيذ أي إنشاء أو تعديل.
                // لا نعتمد على OwnerMode القادم من الـ Form لأنه قابل للتلاعب،
                // بل نتحقق من الحساب المسجل فعليًا في قاعدة البيانات.
                // ================================================================
                var requestedCategory = await _context.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CategoryId == model.CategoryId);

                if (requestedCategory == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "عذراً، الفئة المحددة غير موجودة."
                    });
                }

                bool currentUserIsAdmin = User.IsInRole("Admin");
                var currentUserIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (!currentUserIsAdmin && !string.IsNullOrWhiteSpace(currentUserIdClaim) &&
                    int.TryParse(currentUserIdClaim, out int currentUserId))
                {
                    currentUserIsAdmin = await _context.Users
                        .AsNoTracking()
                        .AnyAsync(u => u.UserId == currentUserId && u.Role == "Admin");
                }

                if (requestedCategory.OwnerCanAddPlaces != true && !currentUserIsAdmin)
                {
                    return Json(new
                    {
                        success = false,
                        message = "هذه الفئة مخصصة لإضافة المنشآت من قِبل الإدارة فقط، ولا يمكن للمالك إضافة منشأة ضمنها."
                    });
                }

                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    string submittedOwnerPhone = Request.Form.ContainsKey("OwnerPhone")
                        ? Request.Form["OwnerPhone"].ToString().Trim()
                        : string.Empty;
                    string submittedBookingPhone = model.PhoneNumber?.Trim() ?? string.Empty;

                    static string NormalizePhoneForComparison(string phone)
                    {
                        string digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());

                        if (digits.StartsWith("00", StringComparison.Ordinal))
                            digits = digits.Substring(2);

                        // الرقم اليمني المحلي يُوحّد مع صيغته الدولية للمقارنة الصحيحة.
                        if (digits.Length == 9 &&
                            (digits.StartsWith("77") || digits.StartsWith("78") ||
                             digits.StartsWith("70") || digits.StartsWith("73") ||
                             digits.StartsWith("71")))
                        {
                            digits = "967" + digits;
                        }

                        return digits;
                    }

                    static bool IsAcceptablePhone(string phone)
                    {
                        string digits = NormalizePhoneForComparison(phone);
                        if (string.IsNullOrWhiteSpace(digits)) return false;

                        if (digits.StartsWith("967", StringComparison.Ordinal))
                        {
                            string localNumber = digits.Substring(3);
                            return localNumber.Length == 9 &&
                                   (localNumber.StartsWith("77") || localNumber.StartsWith("78") ||
                                    localNumber.StartsWith("70") || localNumber.StartsWith("73") ||
                                    localNumber.StartsWith("71"));
                        }

                        // دعم الأرقام الدولية بصيغة E.164 بعد إزالة علامة + والفواصل.
                        return digits.Length >= 8 && digits.Length <= 15 && digits[0] != '0';
                    }

                    if (!IsAcceptablePhone(submittedOwnerPhone))
                    {
                        return Json(new
                        {
                            success = false,
                            step = "step_owner",
                            message = "رقم هاتف المالك غير صحيح. يرجى مراجعته قبل المتابعة."
                        });
                    }

                    if (!IsAcceptablePhone(submittedBookingPhone))
                    {
                        return Json(new
                        {
                            success = false,
                            step = "step_owner",
                            message = "رقم هاتف الحجوزات غير صحيح. يرجى مراجعته قبل المتابعة."
                        });
                    }

                    if (NormalizePhoneForComparison(submittedOwnerPhone) ==
                        NormalizePhoneForComparison(submittedBookingPhone))
                    {
                        return Json(new
                        {
                            success = false,
                            step = "step_owner",
                            message = "رقم هاتف الحجوزات يجب أن يكون مختلفًا عن رقم هاتف المالك الشخصي."
                        });
                    }

                    int ownerId = 0;
                    string ownerMode = Request.Form.ContainsKey("OwnerMode")
                        ? Request.Form["OwnerMode"].ToString().Trim().ToLowerInvariant()
                        : string.Empty;

                    // أوضاع الربط الخاصة بالأدمن لا يقبلها الخادم إلا من مستخدم Admin فعلي.
                    if (!currentUserIsAdmin)
                        ownerMode = "current";

                    // 1. وضع الإدارة يدعم حالتين فقط:
                    //    أ) «حسابي الإداري» الجديد: OwnerId هو نفس UserId للأدمن الحالي.
                    //    ب) حساب التشغيل القديم الذي قد يكون موجودًا من بيانات سابقة أو من خيار
                    //       SetAsPlatformOwner داخل مسار «مالك جديد».
                    // أي OwnerId آخر يُرفض حتى لا يستطيع الأدمن استخدام واجهة المالك على حساب عشوائي.
                    if (currentUserIsAdmin && ownerMode == "admin")
                    {
                        var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

                        if (!int.TryParse(adminIdClaim, out int adminId) || adminId <= 0)
                        {
                            return Json(new
                            {
                                success = false,
                                message = "تعذر تحديد حساب الإدارة الحالي. سجّل الدخول من جديد ثم حاول مرة أخرى."
                            });
                        }

                        var currentAdmin = await _context.Users
                            .AsNoTracking()
                            .FirstOrDefaultAsync(u =>
                                u.UserId == adminId &&
                                u.Role == SystemRoles.Admin);

                        if (currentAdmin == null)
                        {
                            return Json(new
                            {
                                success = false,
                                message = "حساب الإدارة الحالي غير موجود أو لم يعد يملك صلاحية Admin."
                            });
                        }

                        int requestedOwnerId = adminId;
                        if (Request.Form.ContainsKey("OwnerId") &&
                            int.TryParse(Request.Form["OwnerId"], out int submittedAdminOwnerId) &&
                            submittedAdminOwnerId > 0)
                        {
                            requestedOwnerId = submittedAdminOwnerId;
                        }

                        if (requestedOwnerId == adminId)
                        {
                            // المسار الجديد: نفس حساب الأدمن الحالي، بلا أي حساب وسيط.
                            ownerId = adminId;
                        }
                        else
                        {
                            // توافق خلفي فقط مع حساب التشغيل القديم؛ لا يُقبل أي Owner آخر.
                            int? platformOwnerId = await _context.SystemSettings
                                .AsNoTracking()
                                .OrderBy(setting => setting.Id)
                                .Select(setting => setting.PlatformOwnerUserId)
                                .FirstOrDefaultAsync();

                            if (!platformOwnerId.HasValue || platformOwnerId.Value != requestedOwnerId)
                            {
                                return Json(new
                                {
                                    success = false,
                                    message = "حساب مالك منشأة الإدارة لا يطابق حساب الأدمن الحالي ولا حساب التشغيل المعتمد."
                                });
                            }

                            bool validPlatformOwner = await _context.Users
                                .AsNoTracking()
                                .AnyAsync(u =>
                                    u.UserId == requestedOwnerId &&
                                    u.Role == SystemRoles.Owner &&
                                    u.AccountState == "Active");

                            if (!validPlatformOwner)
                            {
                                return Json(new
                                {
                                    success = false,
                                    message = "حساب تشغيل منشآت الإدارة غير موجود أو غير نشط."
                                });
                            }

                            ownerId = requestedOwnerId;
                        }
                    }
                    // 2. معالجة المالك الجديد
                    else if (currentUserIsAdmin && ownerMode == "new")
                    {
                        string firstName = Request.Form["NewFirstName"].ToString();
                        string lastName = Request.Form["NewLastName"].ToString();
                        string newEmail = Request.Form["NewEmail"].ToString();
                        string newUsername = Request.Form["NewOwnerUsername"].ToString();
                        string newPassword = Request.Form["NewOwnerPassword"].ToString();

                        string fullName = firstName + " " + lastName;

                        // فحص التكرار لاسم المستخدم أو الإيميل
                        if (await _context.Users.AnyAsync(u => u.Username == newUsername || u.ContactInfo == newEmail))
                        {
                            return Json(new { success = false, message = "حدث خطأ: اسم المستخدم أو البريد الإلكتروني مكرر." });
                        }

                        var newOwner = new User
                        {
                            FullName = fullName,
                            Username = newUsername,
                            ContactInfo = newEmail,
                            PasswordHash = HashPassword(newPassword ?? "123456"),
                            Role = "Owner",
                            CreatedAt = DateTime.Now,
                            PreferredLanguage = "ar",
                            AccountState = "Active",
                            IsDarkMode = false,
                            IsOnline = false,
                            WarningCount = 0,
                            IsEmailVerified = false
                        };

                        _context.Users.Add(newOwner);
                        await _context.SaveChangesAsync();
                        ownerId = newOwner.UserId;
                    }
                    // 3. معالجة المالك الموجود الذي اختاره الأدمن
                    else if (currentUserIsAdmin && ownerMode == "existing" &&
                             Request.Form.ContainsKey("OwnerId") &&
                             int.TryParse(Request.Form["OwnerId"], out int injectedOwnerId))
                    {
                        bool validOwner = await _context.Users.AsNoTracking().AnyAsync(u =>
                            u.UserId == injectedOwnerId &&
                            u.Role == "Owner" &&
                            u.AccountState == "Active");

                        if (!validOwner)
                            return Json(new { success = false, message = "حساب مالك المنشأة غير موجود أو غير نشط." });

                        ownerId = injectedOwnerId;
                    }
                    // 4. معالجة المالك الحالي في رحلة المالك العادية
                    else
                    {
                        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                        if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int parsedOwnerId))
                        {
                            ownerId = parsedOwnerId;

                            var existingOwner = await _context.Users.FindAsync(ownerId);
                            if (existingOwner != null)
                            {
                                string formPhone = Request.Form.ContainsKey("OwnerPhone")
                                    ? Request.Form["OwnerPhone"].ToString().Trim().Replace(" ", "")
                                    : string.Empty;
                                string formRole = Request.Form.ContainsKey("FinalOwnerRole") ? Request.Form["FinalOwnerRole"].ToString().Trim() : "";
                                string formName = Request.Form.ContainsKey("OwnerName") ? Request.Form["OwnerName"].ToString().Trim() : "";

                                // لا نسمح بحفظ المنشأة بصمت إذا لم يصل هاتف المسؤول من الواجهة؛
                                // لأن رحلة التوثيق تعتمد لاحقًا على Users.PhoneNumber.
                                if (string.IsNullOrWhiteSpace(formPhone))
                                {
                                    return Json(new
                                    {
                                        success = false,
                                        message = "رقم هاتف مسؤول المنشأة لم يُرسل بشكل صحيح. راجع الرقم في خطوة بيانات المسؤول ثم حاول الحفظ مرة أخرى."
                                    });
                                }

                                bool isUpdated = false;
                                if (existingOwner.PhoneNumber != formPhone)
                                {
                                    existingOwner.PhoneNumber = formPhone;
                                    isUpdated = true;
                                }
                                if (!string.IsNullOrEmpty(formRole) && existingOwner.JobTitle != formRole)
                                {
                                    existingOwner.JobTitle = formRole;
                                    isUpdated = true;
                                }
                                if (!string.IsNullOrEmpty(formName) && existingOwner.FullName != formName)
                                {
                                    existingOwner.FullName = formName;
                                    isUpdated = true;
                                }
                                if (isUpdated)
                                {
                                    _context.Users.Update(existingOwner);
                                    await _context.SaveChangesAsync();
                                }
                            }
                        }
                    }

                    if (ownerId == 0)
                    {
                        return Json(new { success = false, message = "خطأ: لم يتم تحديد مالك للمنشأة. يرجى العودة للخلف وتحديد المالك." });
                    }

                    // تم حسم هوية مالك وضع admin أعلاه: إما نفس الأدمن الحالي،
                    // أو حساب التشغيل القديم المعتمد فقط للتوافق مع البيانات السابقة.

                    // 1. قراءة الإحداثيات من الجافاسكريبت
                    string latStr = Request.Form.ContainsKey("Latitude") ? Request.Form["Latitude"].ToString().Replace(",", ".") : "0";
                    string lngStr = Request.Form.ContainsKey("Longitude") ? Request.Form["Longitude"].ToString().Replace(",", ".") : "0";

                    bool isBookable = requestedCategory.BookingSystemId >= 1 && requestedCategory.BookingSystemId <= 4;
                    string status;
                    bool isAvailable;

                    if (currentUserIsAdmin)
                    {
                        bool isAdminOperation = string.Equals(
                            ownerMode,
                            "admin",
                            StringComparison.OrdinalIgnoreCase);

                        if (isAdminOperation)
                        {
                            // منشأة يضيفها الأدمن لنفس حسابه الإداري:
                            // غير الحجزية تظهر مباشرة، والحجزية تنتظر اكتمال السياسات.
                            status = isBookable ? "Pending" : "Active";
                            isAvailable = !isBookable;
                        }
                        else
                        {
                            // منشأة يضيفها الأدمن لمالك جديد أو مالك موجود:
                            // لا تظهر للعملاء مطلقًا قبل موافقة المالك، سواء كانت حجزية أو غير حجزية.
                            status = "WaitOwner";
                            isAvailable = false;
                        }
                    }
                    else
                    {
                        // منشأة يضيفها المالك بنفسه تمر بالمراجعة المعتادة.
                        status = "Pending";
                        isAvailable = false;
                    }

                    // 2. إنشاء كائن المنشأة
                    var place = new Place
                    {
                        OwnerId = ownerId > 0 ? ownerId : null,
                        CategoryId = model.CategoryId,
                        CityId = model.CityId,
                        PlaceNameAndDesc = model.Name,
                        Description = model.Description ?? "",
                        AddressDetail = model.AddressDetail,
                        PhoneNumber = model.PhoneNumber?.Replace(" ", ""),
                        Price = 0,
                        Status = status,
                        IsAvailable = isAvailable,
                        Coordinates = Convert.ToDouble(latStr, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                                      Convert.ToDouble(lngStr, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    };

                    // 3. دمج التفاصيل الوصفية بذكاء
                    var category = requestedCategory;

                    if (category != null)
                    {
                        if (!string.IsNullOrWhiteSpace(place.Description))
                            place.Description += "\n--- تفاصيل المنشأة ---\n";
                        else
                            place.Description = "--- تفاصيل المنشأة ---\n";

                        // ==========================================
                        // 🌟 نظام القاعات (قاعات الأفراح والمؤتمرات)
                        // ==========================================
                        if (category.BookingSystemId == 3 || category.BookingSystemId == 4)
                        {
                            if (!string.IsNullOrWhiteSpace(model.HallArea)) place.Description += $"المساحة: {model.HallArea}\n";
                            if (model.Capacity > 0) place.Description += $"السعة: {model.Capacity} شخص\n";
                            if (model.HallTablesCapacity > 0) place.Description += $"عدد الطاولات: {model.HallTablesCapacity}\n";

                            // 🌟 الحل الجذري: سحب البيانات بقوة من الـ Request.Form مباشرة وتجاهل الموديل
                            var eventTypes = Request.Form["EventTypes"].Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                            if (eventTypes.Any()) place.Description += $"المناسبات: {string.Join(" - ", eventTypes)}\n";

                            var seatingTypes = Request.Form["SeatingTypes"].Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                            if (seatingTypes.Any()) place.Description += $"المقاعد: {string.Join(" - ", seatingTypes)}\n";

                            List<string> hallFac = new List<string>();
                            if (model.HallBathroomsCount > 0) hallFac.Add($"حمامات ({model.HallBathroomsCount})");
                            if (model.ACsCount > 0) hallFac.Add($"مكيفات ({model.ACsCount})");
                            if (model.KitchensCount > 0) hallFac.Add($"مطابخ ({model.KitchensCount})");
                            if (model.ParkingCount > 0) hallFac.Add($"مواقف ({model.ParkingCount})");
                            if (model.SpeakersCount > 0) hallFac.Add($"سماعات ({model.SpeakersCount})");

                            if (hallFac.Any()) place.Description += $"مرافق القاعة: {string.Join(" - ", hallFac)}\n";

                            var customHallFac = Request.Form["CustomHallFacilities"].Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                            if (customHallFac.Any()) place.Description += $"مرافق أخرى: {string.Join(" - ", customHallFac)}\n";
                        }
                        // ==========================================
                        // 🌟 نظام الشاليهات والمزارع
                        // ==========================================
                        else if (category.BookingSystemId == 2)
                        {
                            List<string> buildingDetails = new List<string>();
                            if (model.RoomsCount > 0) buildingDetails.Add($"غرف ({model.RoomsCount})");
                            if (model.BathroomsCount > 0) buildingDetails.Add($"حمامات ({model.BathroomsCount})");
                            if (model.KitchensCount > 0) buildingDetails.Add($"مطابخ ({model.KitchensCount})");
                            if (model.MajlisCount > 0) buildingDetails.Add($"مجالس ({model.MajlisCount})");
                            if (model.ReceptionHallsCount > 0) buildingDetails.Add($"صالات استقبال ({model.ReceptionHallsCount})");

                            if (buildingDetails.Any()) place.Description += $"تفاصيل المبنى: {string.Join(" | ", buildingDetails)}\n";

                            // 🌟 سحب قوي ومباشر من الفورم
                            List<string> entList = Request.Form["ChaletEntertainment"].Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                            var customEnt = Request.Form["CustomChaletEntertainment"].Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                            entList.AddRange(customEnt);
                            if (entList.Any()) place.Description += $"الترفيه والألعاب: {string.Join(" - ", entList)}\n";

                            List<string> srvList = Request.Form["ChaletServices"].Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                            var customSrv = Request.Form["CustomChaletServices"].Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                            srvList.AddRange(customSrv);
                            if (srvList.Any()) place.Description += $"الخدمات المجانية: {string.Join(" - ", srvList)}\n";

                            List<string> hvacList = Request.Form["ChaletHVAC"].Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                            var customHvac = Request.Form["CustomChaletHVAC"].Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                            hvacList.AddRange(customHvac);
                            if (hvacList.Any()) place.Description += $"التدفئة والتبريد: {string.Join(" - ", hvacList)}\n";

                            List<string> yardList = Request.Form["ChaletYard"].Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                            var customYard = Request.Form["CustomChaletYard"].Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                            yardList.AddRange(customYard);
                            if (yardList.Any()) place.Description += $"مرافق الحوش: {string.Join(" - ", yardList)}\n";
                        }
                        // ==========================================
                        // 🌟 نظام الفنادق (BookingSystemId == 1)
                        // ==========================================
                        else if (category.BookingSystemId == 1)
                        {
                            List<string> extraUnits = new List<string>();

                            // سحب قيم الشاليهات والقاعات التابعة للفندق بقوة من الـ Form
                            if (!string.IsNullOrEmpty(Request.Form["HasHotelChalet"]) && Request.Form["HasHotelChalet"].ToString().Contains("true"))
                                extraUnits.Add("شاليهات خاصة للإيجار");
                            if (!string.IsNullOrEmpty(Request.Form["HasHotelHall"]) && Request.Form["HasHotelHall"].ToString().Contains("true"))
                                extraUnits.Add("قاعات مؤتمرات/مناسبات");
                            if (!string.IsNullOrEmpty(Request.Form["HasTairamana"]) && Request.Form["HasTairamana"].ToString().Contains("true"))
                                extraUnits.Add("طيرمانات/مجالس");

                            if (extraUnits.Any())
                                place.Description += $"الوحدات المتاحة: {string.Join(" - ", extraUnits)}\n";

                            List<string> hotelFac = new List<string>();

                            // سحب المرافق المحددة (مثل مسبح كبار) وحل مشكلة الفواصل المتعددة
                            var selectedAmenitiesStr = Request.Form["SelectedAmenities"].ToString();
                            var selectedAmenities = selectedAmenitiesStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                                        .Select(x => x.Trim())
                                                                        .Where(x => !string.IsNullOrWhiteSpace(x))
                                                                        .ToList();
                            if (selectedAmenities.Any()) hotelFac.AddRange(selectedAmenities);

                            // سحب المرافق المكتوبة يدوياً
                            var customAmenitiesStr = Request.Form["CustomAmenities"].ToString();
                            var customHotelFac = customAmenitiesStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                                   .Select(x => x.Trim())
                                                                   .Where(x => !string.IsNullOrWhiteSpace(x))
                                                                   .ToList();
                            if (customHotelFac.Any()) hotelFac.AddRange(customHotelFac);

                            if (hotelFac.Any())
                                place.Description += $"الخدمات والمرافق: {string.Join(" - ", hotelFac)}\n";
                        }

                    }

                    _context.Places.Add(place);
                    await _context.SaveChangesAsync();

                    // 5. إضافة أنواع الفندق ثم الغرف الفعلية داخل الطوابق.
                    // المعرف RoomId ينشأ تلقائيًا من SQL Server، أما RoomNumber فهو الرقم الظاهر للعميل.
                    if (category.BookingSystemId == 1)
                    {
                        errorStep = "step_rooms";
                        var roomTypes = model.HotelRoomTypes ?? new List<FullPlaceWizardDto.HotelRoomTypeItemDto>();
                        var floors = model.HotelFloors ?? new List<FullPlaceWizardDto.HotelFloorItemDto>();

                        // الواجهة ترسل الأنواع المفعلة فقط. نوحّد النصوص هنا حتى لا
                        // تتسبب المسافات أو غياب اسم العرض المخفي في فشل الحفظ.
                        foreach (var item in roomTypes)
                        {
                            item.ClientKey = (item.ClientKey ?? string.Empty).Trim();
                            item.RoomType = (item.RoomType ?? string.Empty).Trim();
                            item.DisplayName = string.IsNullOrWhiteSpace(item.DisplayName)
                                ? item.RoomType
                                : item.DisplayName.Trim();

                            // عدد الغرف الداخلية يخص الجناح فقط. إفراغه للغرفة يمنع
                            // تصنيف غرفة عادية كجناح عند القراءة لاحقًا.
                            if (!item.IsSuite)
                                item.InternalRoomsCount = null;
                        }

                        if (!roomTypes.Any(item => !item.IsSuite))
                            return Json(new { success = false, step = "step_rooms", message = "يجب تفعيل نوع غرفة واحد على الأقل." });
                        if (!floors.Any())
                            return Json(new { success = false, step = "step_floors", message = "يجب إضافة طابق واحد على الأقل." });

                        var duplicateClientKey = roomTypes
                            .GroupBy(item => (item.ClientKey ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                            .FirstOrDefault(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);
                        if (duplicateClientKey != null)
                            return Json(new { success = false, step = "step_rooms", message = "تعذر ربط أنواع الغرف. راجعي الأنواع المفعلة ثم حاولي مرة أخرى." });

                        var invalidRoomType = roomTypes.FirstOrDefault(item => !item.IsSuite &&
                            (string.IsNullOrWhiteSpace(item.RoomType) ||
                             item.Price <= 0 || item.MaxAdults < 1 || item.MaxChildren < 0 ||
                             (item.AreaSquareMeters.HasValue && item.AreaSquareMeters.Value <= 0) ||
                             (item.BedsCount.HasValue && item.BedsCount.Value < 0)));
                        if (invalidRoomType != null)
                            return Json(new { success = false, step = "step_rooms", message = $"أكملي السعر والسعة والقيم الرقمية لنوع الغرفة ({invalidRoomType.DisplayName})." });

                        var invalidSuiteType = roomTypes.FirstOrDefault(item => item.IsSuite &&
                            (string.IsNullOrWhiteSpace(item.RoomType) ||
                             string.IsNullOrWhiteSpace(item.DisplayName) ||
                             item.Price <= 0 || item.MaxAdults < 1 || item.MaxChildren < 0 ||
                             (item.AreaSquareMeters.HasValue && item.AreaSquareMeters.Value <= 0) ||
                             !item.InternalRoomsCount.HasValue || item.InternalRoomsCount.Value < 1 || item.InternalRoomsCount.Value > 100));
                        if (invalidSuiteType != null)
                            return Json(new { success = false, step = "step_suites", message = $"أكملي اسم الجناح وعدد غرفه الداخلية والسعر والسعة للنوع ({invalidSuiteType.RoomType})." });

                        var duplicateTypeName = roomTypes
                            .GroupBy(item => item.RoomType, StringComparer.OrdinalIgnoreCase)
                            .FirstOrDefault(group => group.Count() > 1);
                        if (duplicateTypeName != null)
                        {
                            var duplicateIsSuite = duplicateTypeName.First().IsSuite;
                            return Json(new
                            {
                                success = false,
                                step = duplicateIsSuite ? "step_suites" : "step_rooms",
                                message = $"نوع الوحدة ({duplicateTypeName.Key}) مكرر."
                            });
                        }

                        var duplicateFloor = floors.GroupBy(item => item.FloorNumber).FirstOrDefault(group => group.Count() > 1);
                        if (duplicateFloor != null)
                            return Json(new { success = false, step = "step_floors", message = $"رقم الطابق ({duplicateFloor.Key}) مكرر." });
                        if (floors.Any(floor => floor.Units == null || floor.Units.Count == 0))
                            return Json(new { success = false, step = "step_floors", message = "كل طابق مضاف يجب أن يحتوي على وحدة فعلية واحدة على الأقل." });

                        var allUnits = floors.SelectMany(floor => (floor.Units ?? new List<FullPlaceWizardDto.HotelRoomUnitItemDto>())
                            .Select(unit => new { Floor = floor, Unit = unit })).ToList();
                        if (!allUnits.Any())
                            return Json(new { success = false, step = "step_floors", message = "يجب إضافة وحدة فعلية واحدة على الأقل داخل الطوابق." });

                        var duplicateRoomNumber = allUnits
                            .GroupBy(item => (item.Unit.RoomNumber ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                            .FirstOrDefault(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);
                        if (duplicateRoomNumber != null)
                            return Json(new
                            {
                                success = false,
                                step = "step_floors",
                                message = string.IsNullOrWhiteSpace(duplicateRoomNumber.Key)
                                ? "يوجد رقم وحدة فارغ."
                                : $"رقم الوحدة ({duplicateRoomNumber.Key}) مكرر داخل الفندق."
                            });

                        var typeKeys = roomTypes.Select(item => item.ClientKey.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        if (allUnits.Any(item => !typeKeys.Contains((item.Unit.RoomTypeClientKey ?? "").Trim())))
                            return Json(new { success = false, step = "step_floors", message = "إحدى الوحدات غير مرتبطة بنوع صالح." });

                        var unusedType = roomTypes.FirstOrDefault(type => !allUnits.Any(item =>
                            string.Equals(item.Unit.RoomTypeClientKey?.Trim(), type.ClientKey.Trim(), StringComparison.OrdinalIgnoreCase)));
                        if (unusedType != null)
                            return Json(new { success = false, step = "step_floors", message = $"النوع ({unusedType.DisplayName}) لا يحتوي على أي وحدة فعلية." });

                        var roomByClientKey = new Dictionary<string, Room>(StringComparer.OrdinalIgnoreCase);
                        for (int typeIndex = 0; typeIndex < roomTypes.Count; typeIndex++)
                        {
                            var item = roomTypes[typeIndex];
                            errorStep = item.IsSuite ? "step_suites" : "step_rooms";
                            int unitCount = allUnits.Count(entry => string.Equals(
                                entry.Unit.RoomTypeClientKey?.Trim(), item.ClientKey.Trim(), StringComparison.OrdinalIgnoreCase));

                            IReadOnlyList<IFormFile> images = Request.Form.Files
                                .GetFiles($"HotelRoomTypes[{typeIndex}].Images");
                            if (images.Count == 0 && item.Images != null)
                                images = item.Images;

                            var room = new Room
                            {
                                PlaceId = place.PlaceId,
                                RoomType = item.RoomType.Trim(),
                                RoomNumber = item.DisplayName.Trim(),
                                Quantity = unitCount,
                                Price = item.Price,
                                AreaSquareMeters = item.AreaSquareMeters,
                                MaxAdults = item.MaxAdults,
                                MaxChildren = item.MaxChildren,
                                BedsCount = item.IsSuite ? null : item.BedsCount,
                                BedType = item.IsSuite || string.IsNullOrWhiteSpace(item.BedType) ? null : item.BedType.Trim(),
                                InternalRoomsCount = item.IsSuite ? item.InternalRoomsCount : null,
                                Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
                                IsAvailable = item.IsAvailable,
                                ImageUrl = await ProcessRoomImagesFromWizardAsync(images)
                            };

                            _context.Rooms.Add(room);
                            roomByClientKey[item.ClientKey.Trim()] = room;
                        }

                        await _context.SaveChangesAsync();

                        errorStep = "step_floors";
                        foreach (var entry in allUnits)
                        {
                            var unit = entry.Unit;
                            var room = roomByClientKey[unit.RoomTypeClientKey.Trim()];
                            _context.RoomUnits.Add(new RoomUnit
                            {
                                PlaceId = place.PlaceId,
                                RoomId = room.RoomId,
                                RoomNumber = unit.RoomNumber.Trim(),
                                FloorNumber = entry.Floor.FloorNumber,
                                FloorLabel = string.IsNullOrWhiteSpace(entry.Floor.FloorLabel) ? null : entry.Floor.FloorLabel.Trim(),
                                IsAvailable = unit.IsAvailable,
                                Notes = string.IsNullOrWhiteSpace(unit.Notes) ? null : unit.Notes.Trim(),
                                CreatedAt = DateTime.Now
                            });
                        }

                        place.RoomsCount = allUnits.Count(entry =>
                            !roomByClientKey[entry.Unit.RoomTypeClientKey.Trim()].InternalRoomsCount.HasValue);
                        place.SuitesCount = allUnits.Count(entry =>
                            roomByClientKey[entry.Unit.RoomTypeClientKey.Trim()].InternalRoomsCount.HasValue);
                        _context.Places.Update(place);
                        await _context.SaveChangesAsync();
                    }


                    // =========================================================
                    // 🌟 5.5 إضافة باقات الحجز (للشاليهات والمزارع والقاعات)
                    // =========================================================
                    if (!string.IsNullOrEmpty(Request.Form["MorningPrice"]))
                    {
                        _context.PlacePackages.Add(new PlacePackage
                        {
                            PlaceId = place.PlaceId,
                            PackageName = "باقة صباحية",
                            TimeFrame = "صباحي (من 9 صباحاً إلى 1 ظهراً)",
                            Price = Convert.ToDecimal(Request.Form["MorningPrice"]),
                            Capacity = string.IsNullOrEmpty(Request.Form["MorningCapacity"]) ? 0 : Convert.ToInt32(Request.Form["MorningCapacity"])
                        });
                    }

                    if (!string.IsNullOrEmpty(Request.Form["EveningPrice"]))
                    {
                        _context.PlacePackages.Add(new PlacePackage
                        {
                            PlaceId = place.PlaceId,
                            PackageName = "باقة مسائية / مقيل",
                            TimeFrame = "مسائي - مقيل (من 2 عصراً إلى 10 ليلاً)",
                            Price = Convert.ToDecimal(Request.Form["EveningPrice"]),
                            Capacity = string.IsNullOrEmpty(Request.Form["EveningCapacity"]) ? 0 : Convert.ToInt32(Request.Form["EveningCapacity"])
                        });
                    }

                    if (!string.IsNullOrEmpty(Request.Form["NightPrice"]))
                    {
                        _context.PlacePackages.Add(new PlacePackage
                        {
                            PlaceId = place.PlaceId,
                            PackageName = "باقة ليلية / سمرة",
                            TimeFrame = "ليلي - سمرة (من 6 مغرب إلى الصباح)",
                            Price = Convert.ToDecimal(Request.Form["NightPrice"]),
                            Capacity = string.IsNullOrEmpty(Request.Form["NightCapacity"]) ? 0 : Convert.ToInt32(Request.Form["NightCapacity"])
                        });
                    }

                    if (!string.IsNullOrEmpty(Request.Form["FullDayPrice"]))
                    {
                        _context.PlacePackages.Add(new PlacePackage
                        {
                            PlaceId = place.PlaceId,
                            PackageName = "باقة يوم كامل",
                            TimeFrame = "يوم كامل (من 9 صباحاً إلى 12 ليلاً)",
                            Price = Convert.ToDecimal(Request.Form["FullDayPrice"]),
                            Capacity = string.IsNullOrEmpty(Request.Form["FullDayCapacity"]) ? 0 : Convert.ToInt32(Request.Form["FullDayCapacity"])
                        });
                    }

                    // =========================================================
                    // الباقات المخصصة الديناميكية
                    // كل عنصر داخل Packages يُحفظ كسجل مستقل في PlacePackages.
                    // =========================================================
                    var customPackages = model.Packages ?? new List<FullPlaceWizardDto.PackageItemDto>();

                    if (customPackages.Count > 50)
                    {
                        return Json(new
                        {
                            success = false,
                            step = category.BookingSystemId == 2 ? "step_chalet_details" : "step_hall_details",
                            message = "عدد الباقات المخصصة المرسلة أكبر من الحد المسموح."
                        });
                    }

                    if (customPackages.Any() && category.BookingSystemId != 2 &&
                        category.BookingSystemId != 3 && category.BookingSystemId != 4)
                    {
                        return Json(new
                        {
                            success = false,
                            message = "الباقات المخصصة غير متاحة لهذا النوع من المنشآت."
                        });
                    }

                    if (customPackages.Any())
                    {
                        errorStep = category.BookingSystemId == 2
                            ? "step_chalet_details"
                            : "step_hall_details";
                    }

                    foreach (var package in customPackages)
                        package.Name = (package.Name ?? string.Empty).Trim();

                    var duplicateCustomPackage = customPackages
                        .GroupBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
                        .FirstOrDefault(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1);

                    if (duplicateCustomPackage != null)
                    {
                        return Json(new
                        {
                            success = false,
                            step = errorStep,
                            message = $"اسم الباقة ({duplicateCustomPackage.Key}) مكرر."
                        });
                    }

                    var selectedDefaultPackageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(Request.Form["MorningPrice"])) selectedDefaultPackageNames.Add("باقة صباحية");
                    if (!string.IsNullOrEmpty(Request.Form["EveningPrice"])) selectedDefaultPackageNames.Add("باقة مسائية / مقيل");
                    if (!string.IsNullOrEmpty(Request.Form["NightPrice"])) selectedDefaultPackageNames.Add("باقة ليلية / سمرة");
                    if (!string.IsNullOrEmpty(Request.Form["FullDayPrice"])) selectedDefaultPackageNames.Add("باقة يوم كامل");

                    var duplicateDefaultPackage = customPackages
                        .FirstOrDefault(package => selectedDefaultPackageNames.Contains(package.Name));

                    if (duplicateDefaultPackage != null)
                    {
                        return Json(new
                        {
                            success = false,
                            step = errorStep,
                            message = $"الباقة ({duplicateDefaultPackage.Name}) مضافة ضمن الباقات الأساسية بالفعل."
                        });
                    }

                    foreach (var package in customPackages)
                    {
                        if (string.IsNullOrWhiteSpace(package.Name) || package.Name.Length > 100)
                        {
                            return Json(new
                            {
                                success = false,
                                step = errorStep,
                                message = "اسم كل باقة مخصصة مطلوب ويجب ألا يتجاوز 100 حرف."
                            });
                        }

                        if (package.Price <= 0)
                        {
                            return Json(new
                            {
                                success = false,
                                step = errorStep,
                                message = $"سعر الباقة ({package.Name}) يجب أن يكون أكبر من صفر."
                            });
                        }

                        if (package.Capacity <= 0)
                        {
                            return Json(new
                            {
                                success = false,
                                step = errorStep,
                                message = $"سعة الباقة ({package.Name}) يجب أن تكون شخصًا واحدًا على الأقل."
                            });
                        }

                        if ((category.BookingSystemId == 3 || category.BookingSystemId == 4) &&
                            model.Capacity > 0 && package.Capacity > model.Capacity)
                        {
                            return Json(new
                            {
                                success = false,
                                step = errorStep,
                                message = $"سعة الباقة ({package.Name}) لا يمكن أن تتجاوز سعة القاعة ({model.Capacity})."
                            });
                        }

                        if (!package.StartTime.HasValue || !package.EndTime.HasValue ||
                            package.StartTime.Value == package.EndTime.Value)
                        {
                            return Json(new
                            {
                                success = false,
                                step = errorStep,
                                message = $"حددي وقت بداية ونهاية صحيحين ومختلفين للباقة ({package.Name})."
                            });
                        }

                        _context.PlacePackages.Add(new PlacePackage
                        {
                            PlaceId = place.PlaceId,
                            PackageName = package.Name,
                            Price = package.Price,
                            Capacity = package.Capacity,
                            StartTime = package.StartTime.Value,
                            EndTime = package.EndTime.Value,
                            TimeFrame = $"من {package.StartTime.Value:hh\\:mm} إلى {package.EndTime.Value:hh\\:mm}",
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        });
                    }

                    await _context.SaveChangesAsync();

                    // 6. رفع وحفظ الصور
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/places");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    bool isPlaceUpdatedForImage = false;
                    if (CoverImageUpload != null && CoverImageUpload.Length > 0)
                    {
                        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(CoverImageUpload.FileName);
                        var filePath = Path.Combine(uploadsFolder, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create)) { await CoverImageUpload.CopyToAsync(stream); }
                        place.CoverImage = "/images/places/" + fileName;
                        isPlaceUpdatedForImage = true;
                    }

                    if (GalleryImages != null && GalleryImages.Count > 0)
                    {
                        for (int i = 0; i < GalleryImages.Count; i++)
                        {
                            var file = GalleryImages[i];
                            if (file.Length > 0)
                            {
                                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                                var filePath = Path.Combine(uploadsFolder, fileName);
                                using (var stream = new FileStream(filePath, FileMode.Create)) { await file.CopyToAsync(stream); }
                                _context.PlaceImages.Add(new PlaceImage { PlaceId = place.PlaceId, ImagePath = "/images/places/" + fileName });
                            }
                        }
                        isPlaceUpdatedForImage = true;
                    }

                    if (isPlaceUpdatedForImage)
                    {
                        _context.Places.Update(place);
                        await _context.SaveChangesAsync();
                    }

                    // ================================================================
                    // إشعار المالك عند إضافة الإدارة منشأة إلى حسابه:
                    // - ينفذ فقط عندما تكون المنشأة مضافة لمالك آخر (new/existing).
                    // - لا ينفذ لمنشأة الإدارة الخاصة بها (OwnerMode = admin).
                    // - الإشعار عام على مستوى حساب المالك (ReferenceType = User) حتى
                    //   يظهر في جرس المالك مهما كانت المنشأة المختارة في الهيدر.
                    // ================================================================
                    if (currentUserIsAdmin &&
                        !string.Equals(ownerMode, "admin", StringComparison.OrdinalIgnoreCase) &&
                        place.OwnerId.HasValue &&
                        place.OwnerId.Value > 0)
                    {
                        var notificationService = HttpContext.RequestServices
                            .GetRequiredService<INotificationService>();

                        notificationService.Queue(
                            new CreateNotificationDto
                            {
                                UserId = place.OwnerId.Value,
                                NotificationTitle = "منشأة جديدة تحتاج موافقتك",
                                NotificationText =
                                    $"أضافت الإدارة منشأة «{place.PlaceNameAndDesc}» إلى حسابك. يرجى مراجعتها والموافقة عليها أو رفضها.",
                                NotificationType = NotificationTypes.Account,
                                TargetUrl = "/Owner/Place/ManagePlaces",
                                ReferenceType = NotificationReferenceTypes.User,
                                ReferenceId = place.OwnerId.Value
                            });

                        // Queue لا تحفظ وحدها؛ نحفظ داخل نفس Transaction الحالية.
                        await _context.SaveChangesAsync();
                    }

                    // ================================================================
                    // إشعار مراجعة المنشأة:
                    // - لا نرسله أثناء رحلة توثيق مالك جديد لأن حسابه ما يزال Pending.
                    // - بعد تفعيل/اعتماد المالك، أي منشأة جديدة Pending تحتاج مراجعة مستقلة.
                    // - التوزيع يتم فقط لموظفي الإدارة الذين يملكون Places.Review
                    //   (مع مدير النظام) عبر محرك الإشعارات المركزي.
                    // ================================================================
                    if (!currentUserIsAdmin &&
                        string.Equals(place.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                    {
                        var ownerAccountState = await _context.Users
                            .AsNoTracking()
                            .Where(user => user.UserId == ownerId && user.Role == SystemRoles.Owner)
                            .Select(user => user.AccountState)
                            .FirstOrDefaultAsync();

                        var ownerIsAlreadyApproved =
                            string.Equals(ownerAccountState, "Active", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(ownerAccountState, "Accepted", StringComparison.OrdinalIgnoreCase);

                        if (ownerIsAlreadyApproved)
                        {
                            var notificationService = HttpContext.RequestServices
                                .GetRequiredService<INotificationService>();

                            await notificationService.QueueForAdminPermissionAsync(
                                AdminPermissionKeys.Places.Review,
                                new CreateNotificationDto
                                {
                                    NotificationTitle = "منشأة جديدة تحتاج مراجعة",
                                    NotificationText =
                                        $"أضاف المالك منشأة «{place.PlaceNameAndDesc}» وأصبحت جاهزة للمراجعة.",
                                    NotificationType = NotificationTypes.AdminPlaceActionRequired,
                                    TargetUrl =
                                        $"/Admin/Places/PlaceManagement?focusPlace={place.PlaceId}",
                                    ReferenceType = NotificationReferenceTypes.Place,
                                    ReferenceId = place.PlaceId
                                },
                                HttpContext.RequestAborted);

                            await _context.SaveChangesAsync();
                        }
                    }

                    var trialService = HttpContext.RequestServices.GetRequiredService<YemenGuide.Web.Services.Finance.ICommissionFinanceService>();
                    var trialResult = await trialService.TryStartOwnerFreeTrialForPlaceAsync(place.PlaceId, HttpContext.RequestAborted);
                    if (!trialResult.Success) throw new InvalidOperationException(trialResult.Message);
                    await transaction.CommitAsync();
                    return Json(new { success = true, message = "تم الحفظ بنجاح", placeId = place.PlaceId });
                }
                catch (Exception ex)
                {
                    try
                    {
                        await transaction.RollbackAsync();
                    }
                    catch
                    {
                        // لا نخفي خطأ الحفظ الأصلي إذا تعذر التراجع بسبب انقطاع الاتصال.
                    }

                    string actualError = ex.GetBaseException().Message;
                    return StatusCode(500, new
                    {
                        success = false,
                        step = errorStep,
                        message = "حدث خطأ داخلي أثناء الحفظ: " + actualError
                    });
                }
            }
            catch (Exception ex)
            {
                string actualError = ex.GetBaseException().Message;
                return StatusCode(500, new
                {
                    success = false,
                    step = errorStep,
                    message = "تعذر بدء عملية الحفظ: " + actualError
                });
            }
        }
    }
}

