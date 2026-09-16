using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Core.Constants;
using YemenGuide.Core.Entities;
using YemenGuide.Data;
using YemenGuide.Web.Filters;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    [Area("Owner")]
    [Authorize(Roles = "Owner,Admin")]
    [AuthorizeActiveOwner]
    public class SettingsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(
            AppDbContext context,
            ILogger<SettingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private bool TryGetCurrentOwnerId(out int ownerId)
        {
            ownerId = 0;

            return int.TryParse(
                       User.FindFirstValue(ClaimTypes.NameIdentifier),
                       out ownerId) &&
                   ownerId > 0;
        }

        private Task<bool> IsCurrentAdminAccountAsync(int userId)
        {
            return _context.Users
                .AsNoTracking()
                .AnyAsync(user =>
                    user.UserId == userId &&
                    user.Role == SystemRoles.Admin);
        }

        private IQueryable<Place> BuildOwnedBookablePlacesQuery(int ownerId)
        {
            return _context.Places
                .Where(place =>
                    place.OwnerId == ownerId &&
                    place.Category != null &&
                    place.Category.BookingSystemId >= 1 &&
                    place.Category.BookingSystemId <= 4);
        }

        private async Task<string> GetOwnerAccountStateAsync(int ownerId)
        {
            var account = await _context.Users
                .AsNoTracking()
                .Where(user => user.UserId == ownerId)
                .Select(user => new
                {
                    user.Role,
                    user.AccountState
                })
                .FirstOrDefaultAsync();

            if (account == null)
                return string.Empty;

            // حساب الأدمن الذي يملك منشأة لا يدخل رحلة توثيق حساب Owner.
            if (string.Equals(account.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                return "Active";

            return account.AccountState ?? string.Empty;
        }

        private static bool IsPreActivationState(string? state)
        {
            return string.Equals(state, "Pending", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(state, "Rejected", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> CanUseVerificationPolicyFlowAsync(
            int ownerId,
            int placeId)
        {
            if (ownerId <= 0 || placeId <= 0)
                return false;

            var state = await GetOwnerAccountStateAsync(ownerId);
            if (!IsPreActivationState(state))
                return false;

            return await _context.Places
                .AsNoTracking()
                .AnyAsync(place =>
                    place.PlaceId == placeId &&
                    place.OwnerId == ownerId &&
                    place.Status == "Pending" &&
                    place.Category != null &&
                    place.Category.BookingSystemId >= 1 &&
                    place.Category.BookingSystemId <= 4);
        }

        private async Task<bool> CanAccessPolicyEndpointAsync(
            int ownerId,
            int placeId,
            bool verificationFlow)
        {
            if (ownerId <= 0 || placeId <= 0)
                return false;

            var state = await GetOwnerAccountStateAsync(ownerId);

            if (string.Equals(state, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return await BuildOwnedBookablePlacesQuery(ownerId)
                    .AsNoTracking()
                    .AnyAsync(place => place.PlaceId == placeId);
            }

            return verificationFlow &&
                   await CanUseVerificationPolicyFlowAsync(ownerId, placeId);
        }

        private static string Clean(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static string? CleanNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static FacilitySettingsDto MapPolicyToDto(PlacePolicy policy)
        {
            return new FacilitySettingsDto
            {
                PlaceId = policy.PlaceId,
                BookingApprovalMode = policy.BookingApprovalMode,
                DepositPoliciesJson = policy.DepositPoliciesJson,
                SecurityPoliciesJson = policy.SecurityPoliciesJson,
                PaymentTimeout = policy.PaymentTimeout,
                IsSarSupported = policy.IsSarSupported,
                IsUsdSupported = policy.IsUsdSupported,
                CleaningFee = policy.CleaningFee,
                TaxPercentage = policy.TaxPercentage,
                IsNonRefundableEnabled = policy.IsNonRefundableEnabled,
                NonRefundableDiscount = policy.NonRefundableDiscount,
                FreeCancellationHours = policy.FreeCancellationHours,
                ImmediateGracePeriod = policy.ImmediateGracePeriod,
                PenaltyType = policy.PenaltyType,
                PenaltyValue = policy.PenaltyValue,
                ConfiscateOnNoShow = policy.ConfiscateOnNoShow,
                NoShowGraceMinutes = policy.NoShowGraceMinutes,
                RefundTransferFee = policy.RefundTransferFee,
                RefundProcessingDays = policy.RefundProcessingDays,
                ExceptionPoliciesJson = policy.ExceptionPoliciesJson,
                OperationSystemMode = policy.OperationSystemMode,
                CheckInTime = policy.CheckInTime,
                CheckOutTime = policy.CheckOutTime,
                ShiftsJson = policy.ShiftsJson,
                BufferTimeHours = policy.BufferTimeHours,
                MinimumNoticeHours = policy.MinimumNoticeHours,
                MaximumHorizonMonths = policy.MaximumHorizonMonths,
                ClosedDaysCsv = policy.ClosedDaysCsv,
                IsChatEnabled = policy.IsChatEnabled,
                WelcomeMessage = policy.WelcomeMessage,
                OfflineReplyToggle = policy.OfflineReplyToggle,
                OfflineMessage = policy.OfflineMessage,
                QuickRepliesJson = policy.QuickRepliesJson,
                EmergencyContact = policy.EmergencyContact ?? string.Empty,
                ContactEmail = policy.ContactEmail,
                IsSoundAlertEnabled = policy.IsSoundAlertEnabled,
                IsPushNoticeEnabled = policy.IsPushNoticeEnabled,
                ForFamiliesOnly = policy.ForFamiliesOnly,
                AllowSmoking = policy.AllowSmoking,
                AllowParties = policy.AllowParties,
                AllowQatInside = policy.AllowQatInside
            };
        }

        private static void ApplyDtoToPolicy(
            FacilitySettingsDto model,
            PlacePolicy policy)
        {
            policy.BookingApprovalMode = Clean(model.BookingApprovalMode);
            policy.DepositPoliciesJson =
                string.IsNullOrWhiteSpace(model.DepositPoliciesJson)
                    ? "[]"
                    : model.DepositPoliciesJson;
            policy.SecurityPoliciesJson =
                string.IsNullOrWhiteSpace(model.SecurityPoliciesJson)
                    ? "[]"
                    : model.SecurityPoliciesJson;
            policy.PaymentTimeout = model.PaymentTimeout;
            policy.IsSarSupported = model.IsSarSupported ?? false;
            policy.IsUsdSupported = model.IsUsdSupported ?? false;
            policy.CleaningFee = model.CleaningFee;
            policy.TaxPercentage = model.TaxPercentage;
            policy.IsNonRefundableEnabled = model.IsNonRefundableEnabled;
            policy.NonRefundableDiscount = model.NonRefundableDiscount;
            policy.FreeCancellationHours = model.FreeCancellationHours;
            policy.ImmediateGracePeriod = model.ImmediateGracePeriod ?? false;
            policy.PenaltyType = Clean(model.PenaltyType);
            policy.PenaltyValue = model.PenaltyValue;
            policy.ConfiscateOnNoShow = model.ConfiscateOnNoShow ?? false;
            policy.NoShowGraceMinutes = model.NoShowGraceMinutes;
            policy.RefundTransferFee = Clean(model.RefundTransferFee);
            policy.RefundProcessingDays = model.RefundProcessingDays;
            policy.ExceptionPoliciesJson =
                string.IsNullOrWhiteSpace(model.ExceptionPoliciesJson)
                    ? "[]"
                    : model.ExceptionPoliciesJson;
            policy.OperationSystemMode = Clean(model.OperationSystemMode);
            policy.CheckInTime = CleanNullable(model.CheckInTime);
            policy.CheckOutTime = CleanNullable(model.CheckOutTime);
            policy.ShiftsJson =
                string.IsNullOrWhiteSpace(model.ShiftsJson)
                    ? "[]"
                    : model.ShiftsJson;
            policy.BufferTimeHours = model.BufferTimeHours;
            policy.MinimumNoticeHours = model.MinimumNoticeHours;
            policy.MaximumHorizonMonths = model.MaximumHorizonMonths;
            policy.ClosedDaysCsv = Clean(model.ClosedDaysCsv);
            policy.IsChatEnabled = model.IsChatEnabled ?? false;
            policy.WelcomeMessage = Clean(model.WelcomeMessage);
            policy.OfflineReplyToggle = model.OfflineReplyToggle;
            policy.OfflineMessage = Clean(model.OfflineMessage);
            policy.QuickRepliesJson =
                string.IsNullOrWhiteSpace(model.QuickRepliesJson)
                    ? "[]"
                    : model.QuickRepliesJson;
            policy.EmergencyContact = Clean(model.EmergencyContact);
            policy.ContactEmail = CleanNullable(model.ContactEmail);
            policy.IsSoundAlertEnabled = model.IsSoundAlertEnabled ?? true;
            policy.IsPushNoticeEnabled = model.IsPushNoticeEnabled ?? true;
            policy.ForFamiliesOnly = model.ForFamiliesOnly;
            policy.AllowSmoking = model.AllowSmoking;
            policy.AllowParties = model.AllowParties;
            policy.AllowQatInside = model.AllowQatInside;
        }

        private sealed class OperationalShiftWindow
        {
            public string Name { get; init; } = string.Empty;
            public int StartMinutes { get; init; }
            public int EndMinutes { get; init; }
        }

        private static string? ValidateOperationalShiftSchedule(
            FacilitySettingsDto model)
        {
            if (!string.Equals(
                    model.OperationSystemMode,
                    "Shifts",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (model.BufferTimeHours < 0 || model.BufferTimeHours > 12)
            {
                return "وقت التجهيز والتنظيف يجب أن يكون بين 0 و12 ساعة.";
            }

            if (string.IsNullOrWhiteSpace(model.ShiftsJson))
            {
                return "أضف فترة تشغيلية واحدة على الأقل قبل الحفظ.";
            }

            try
            {
                using var document = JsonDocument.Parse(model.ShiftsJson);

                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return "بيانات الفترات التشغيلية غير صحيحة.";
                }

                var windows = new List<OperationalShiftWindow>();
                int rowNumber = 0;

                foreach (var item in document.RootElement.EnumerateArray())
                {
                    rowNumber++;

                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        return $"بيانات الفترة رقم {rowNumber} غير صحيحة.";
                    }

                    string name = GetJsonText(item, "name", "title", "label");
                    string from = GetJsonText(item, "from", "start", "startTime");
                    string to = GetJsonText(item, "to", "end", "endTime");

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        return $"اكتب اسم الفترة رقم {rowNumber}.";
                    }

                    if (!TryParseClockMinutes(from, out var startMinutes) ||
                        !TryParseClockMinutes(to, out var endMinutes))
                    {
                        return $"حدد وقت البداية والنهاية للفترة «{name}».";
                    }

                    if (startMinutes == endMinutes)
                    {
                        return $"لا يمكن أن تبدأ وتنتهي الفترة «{name}» في نفس الوقت.";
                    }

                    windows.Add(new OperationalShiftWindow
                    {
                        Name = name.Trim(),
                        StartMinutes = startMinutes,
                        EndMinutes = endMinutes
                    });
                }

                if (windows.Count == 0)
                {
                    return "أضف فترة تشغيلية واحدة على الأقل قبل الحفظ.";
                }

                for (int firstIndex = 0; firstIndex < windows.Count; firstIndex++)
                {
                    for (int secondIndex = firstIndex + 1;
                         secondIndex < windows.Count;
                         secondIndex++)
                    {
                        var first = windows[firstIndex];
                        var second = windows[secondIndex];

                        if (!ShiftWindowsOverlap(first, second))
                        {
                            continue;
                        }

                        return
                            $"لا يمكن الحفظ لأن الفترة «{first.Name}» تتداخل مع الفترة «{second.Name}». عدّل أوقات الفترات أولًا.";
                    }
                }

                return null;
            }
            catch (JsonException)
            {
                return "تعذر قراءة الفترات التشغيلية. أعد تحديد الأوقات ثم حاول الحفظ.";
            }
        }

        private static string GetJsonText(
            JsonElement item,
            params string[] names)
        {
            foreach (var property in item.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                foreach (var name in names)
                {
                    if (!string.Equals(
                            property.Name,
                            name,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return property.Value.GetString()?.Trim() ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static bool TryParseClockMinutes(
            string? value,
            out int minutes)
        {
            minutes = 0;

            if (string.IsNullOrWhiteSpace(value) ||
                !TimeSpan.TryParse(value.Trim(), out var clock) ||
                clock < TimeSpan.Zero ||
                clock >= TimeSpan.FromDays(1))
            {
                return false;
            }

            minutes = (int)clock.TotalMinutes;
            return true;
        }

        private static bool ShiftWindowsOverlap(
            OperationalShiftWindow first,
            OperationalShiftWindow second)
        {
            var firstSegments = BuildShiftSegments(first);
            var secondSegments = BuildShiftSegments(second);

            foreach (var firstSegment in firstSegments)
            {
                foreach (var secondSegment in secondSegments)
                {
                    if (Math.Max(firstSegment.Start, secondSegment.Start) <
                        Math.Min(firstSegment.End, secondSegment.End))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static List<(int Start, int End)> BuildShiftSegments(
            OperationalShiftWindow window)
        {
            if (window.StartMinutes < window.EndMinutes)
            {
                return new List<(int Start, int End)>
                {
                    (window.StartMinutes, window.EndMinutes)
                };
            }

            return new List<(int Start, int End)>
            {
                (window.StartMinutes, 24 * 60),
                (0, window.EndMinutes)
            };
        }

        private static string? ValidatePackageAgainstOperationPolicy(
            PlacePolicy? policy,
            PlacePackage package)
        {
            if (policy == null)
            {
                return null;
            }

            if (!package.StartTime.HasValue ||
                !package.EndTime.HasValue ||
                package.StartTime.Value == package.EndTime.Value)
            {
                return "وقت بداية ونهاية الباقة غير صالح.";
            }

            var operationMode = policy.OperationSystemMode?.Trim();

            if (string.Equals(operationMode, "Shifts", StringComparison.OrdinalIgnoreCase))
            {
                var shifts = ReadPackagePolicyShifts(policy.ShiftsJson);
                if (shifts.Count == 0)
                {
                    return "أضف فترة تشغيلية صالحة واحدة على الأقل قبل استخدام الباقات.";
                }

                if (PackageFitsConnectedShiftWindows(
                        package.StartTime.Value,
                        package.EndTime.Value,
                        shifts))
                {
                    return null;
                }

                return "وقت الباقة يجب أن يكون بالكامل داخل وقت تشغيل متصل؛ يمكنها عبور فترتين أو أكثر إذا كانت نهاية الفترة السابقة هي نفسها بداية الفترة التالية، ولا يمكنها عبور فجوة بين الفترات.";
            }

            if (string.Equals(operationMode, "FullDay", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(operationMode, "Daily", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParsePackagePolicyClock(policy.CheckInTime, out var checkIn) ||
                    !TryParsePackagePolicyClock(policy.CheckOutTime, out var checkOut) ||
                    checkIn == checkOut)
                {
                    return "حدد وقت دخول وخروج صالح لنظام اليوم الكامل أولًا.";
                }

                return PackageFitsOperationWindow(
                    package.StartTime.Value,
                    package.EndTime.Value,
                    checkIn,
                    checkOut)
                        ? null
                        : "وقت الباقة يجب أن يكون بالكامل داخل وقت الدخول والخروج المحدد في نظام اليوم الكامل.";
            }

            return null;
        }

        private static bool PackageFitsConnectedShiftWindows(
            TimeSpan packageStart,
            TimeSpan packageEnd,
            IReadOnlyCollection<OperationalShiftWindow> shifts)
        {
            const double minutesPerDay = 24d * 60d;
            const double toleranceMinutes = 0.0001d;

            if (shifts == null || shifts.Count == 0)
            {
                return false;
            }

            // نحول كل فترة إلى خط زمني ممتد ليومين، ثم ندمج الفترات
            // المتلامسة فقط. مثال: 06:00-12:00 + 12:00-18:00 تصبح
            // نافذة واحدة 06:00-18:00، بينما 06:00-12:00 + 13:00-18:00
            // تبقى نافذتين لأن بينهما فجوة حقيقية.
            var segments = new List<(double Start, double End)>();

            foreach (var shift in shifts)
            {
                double start = shift.StartMinutes;
                double end = shift.EndMinutes;

                if (end <= start)
                {
                    end += minutesPerDay;
                }

                for (int dayOffset = -1; dayOffset <= 1; dayOffset++)
                {
                    double offset = dayOffset * minutesPerDay;
                    segments.Add((start + offset, end + offset));
                }
            }

            var ordered = segments
                .OrderBy(segment => segment.Start)
                .ThenBy(segment => segment.End)
                .ToList();

            var merged = new List<(double Start, double End)>();

            foreach (var segment in ordered)
            {
                if (merged.Count == 0)
                {
                    merged.Add(segment);
                    continue;
                }

                var last = merged[^1];

                if (segment.Start <= last.End + toleranceMinutes)
                {
                    merged[^1] = (last.Start, Math.Max(last.End, segment.End));
                }
                else
                {
                    merged.Add(segment);
                }
            }

            double packageStartMinutes = packageStart.TotalMinutes;
            double packageEndMinutes = packageEnd.TotalMinutes;

            if (packageEnd <= packageStart)
            {
                packageEndMinutes += minutesPerDay;
            }

            return merged.Any(window =>
                packageStartMinutes + toleranceMinutes >= window.Start &&
                packageEndMinutes <= window.End + toleranceMinutes);
        }

        private static bool PackageFitsOperationWindow(
            TimeSpan packageStart,
            TimeSpan packageEnd,
            TimeSpan windowStart,
            TimeSpan windowEnd)
        {
            const double minutesPerDay = 24d * 60d;
            const double toleranceMinutes = 0.0001d;

            var packageStartMinutes = packageStart.TotalMinutes;
            var packageEndMinutes = packageEnd.TotalMinutes;
            if (packageEnd <= packageStart)
            {
                packageEndMinutes += minutesPerDay;
            }

            var windowStartMinutes = windowStart.TotalMinutes;
            var windowEndMinutes = windowEnd.TotalMinutes;
            if (windowEnd <= windowStart)
            {
                windowEndMinutes += minutesPerDay;
            }

            for (var dayOffset = -1; dayOffset <= 1; dayOffset++)
            {
                var offset = dayOffset * minutesPerDay;
                var candidateStart = windowStartMinutes + offset;
                var candidateEnd = windowEndMinutes + offset;

                if (packageStartMinutes + toleranceMinutes >= candidateStart &&
                    packageEndMinutes <= candidateEnd + toleranceMinutes)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParsePackagePolicyClock(
            string? value,
            out TimeSpan clock)
        {
            clock = default;
            return !string.IsNullOrWhiteSpace(value) &&
                   TimeSpan.TryParse(value.Trim(), out clock) &&
                   clock >= TimeSpan.Zero &&
                   clock < TimeSpan.FromDays(1);
        }

        private static List<OperationalShiftWindow> ReadPackagePolicyShifts(
            string? json)
        {
            var result = new List<OperationalShiftWindow>();
            if (string.IsNullOrWhiteSpace(json))
            {
                return result;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return result;
                }

                foreach (var item in document.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var from = GetJsonText(item, "from", "start", "startTime");
                    var to = GetJsonText(item, "to", "end", "endTime");

                    if (TryParseClockMinutes(from, out var startMinutes) &&
                        TryParseClockMinutes(to, out var endMinutes) &&
                        startMinutes != endMinutes)
                    {
                        result.Add(new OperationalShiftWindow
                        {
                            Name = GetJsonText(item, "name", "title", "label"),
                            StartMinutes = startMinutes,
                            EndMinutes = endMinutes
                        });
                    }
                }
            }
            catch (JsonException)
            {
                // الفحص الأساسي للفترات سيعرض الرسالة المناسبة قبل الوصول هنا.
            }

            return result;
        }

        private static bool PackageFitsOperationWindow(
            TimeSpan packageStart,
            TimeSpan packageEnd,
            OperationalShiftWindow shift)
        {
            return PackageFitsOperationWindow(
                packageStart,
                packageEnd,
                TimeSpan.FromMinutes(shift.StartMinutes),
                TimeSpan.FromMinutes(shift.EndMinutes));
        }

        private async Task<string?> ValidateExistingPackagesAgainstPolicyAsync(
            int placeId,
            PlacePolicy candidatePolicy)
        {
            // IMPORTANT: intentionally validate ALL packages (active and paused).
            // A paused conflicting package must not become a hidden backdoor that can
            // be activated later under an incompatible operating policy.
            var packages = await _context.PlacePackages
                .AsNoTracking()
                .Where(item => item.PlaceId == placeId)
                .OrderBy(item => item.PackageId)
                .ToListAsync();

            foreach (var package in packages)
            {
                var validationMessage =
                    ValidatePackageAgainstOperationPolicy(candidatePolicy, package);

                if (!string.IsNullOrWhiteSpace(validationMessage))
                {
                    var packageName = string.IsNullOrWhiteSpace(package.PackageName)
                        ? "باقة بدون اسم"
                        : package.PackageName.Trim();

                    var packageTime =
                        package.StartTime.HasValue && package.EndTime.HasValue
                            ? $" ({package.StartTime.Value.ToString(@"hh\:mm")} - {package.EndTime.Value.ToString(@"hh\:mm")})"
                            : string.Empty;

                    return
                        $"الباقة «{packageName}»{packageTime} غير متوافقة مع سياسة التشغيل. {validationMessage}";
                }
            }

            return null;
        }

        private async Task<string?> ValidateExistingPackagesAgainstProposedSettingsAsync(
            int placeId,
            FacilitySettingsDto model)
        {
            // Build an in-memory policy from the exact values submitted by the UI.
            // Nothing is written to the tracked/saved policy until this validation passes.
            var candidatePolicy = new PlacePolicy
            {
                PlaceId = placeId
            };

            ApplyDtoToPolicy(model, candidatePolicy);

            return await ValidateExistingPackagesAgainstPolicyAsync(
                placeId,
                candidatePolicy);
        }

        private JsonResult InvalidModelJson()
        {
            var message = ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault();

            return Json(new
            {
                success = false,
                message = string.IsNullOrWhiteSpace(message)
                    ? "راجع القيم المدخلة ثم حاول مرة أخرى."
                    : message
            });
        }

        [HttpGet]
        [AllowPendingOwnerVerification]
        public async Task<IActionResult> FacilitySettings(
            int? autoPlaceId = null,
            bool verificationFlow = false)
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return Challenge();
            }

            var accountState = await GetOwnerAccountStateAsync(ownerId);

            // رحلة التوثيق قبل التفعيل:
            // لا نفتح إعدادات عامة ولا نعتمد كوكي الهيدر. نسمح فقط للمنشأة
            // Pending التي أنشأها نفس المالك داخل الرحلة.
            if (!string.Equals(accountState, "Active", StringComparison.OrdinalIgnoreCase))
            {
                var requestedVerificationPlaceId =
                    autoPlaceId.GetValueOrDefault();

                if (!verificationFlow ||
                    requestedVerificationPlaceId <= 0 ||
                    !await CanUseVerificationPolicyFlowAsync(
                        ownerId,
                        requestedVerificationPlaceId))
                {
                    return Redirect("/Account/VerifyAccount");
                }

                var verificationPlace = await _context.Places
                    .AsNoTracking()
                    .Where(place =>
                        place.PlaceId == requestedVerificationPlaceId &&
                        place.OwnerId == ownerId)
                    .Select(place => new
                    {
                        place.PlaceId,
                        Name = place.PlaceNameAndDesc
                    })
                    .FirstOrDefaultAsync();

                if (verificationPlace == null)
                {
                    return Redirect("/Account/VerifyAccount");
                }

                ViewBag.ActivePlaceId = verificationPlace.PlaceId;
                ViewBag.ActivePlaceName = verificationPlace.Name;
                ViewBag.CanManagePolicies = true;
                ViewBag.VerificationFlow = true;

                return View("FacilitySettingsVerification");
            }

            var ownerPlaces = await _context.Places
                .AsNoTracking()
                .Where(place => place.OwnerId == ownerId)
                .Select(place => new
                {
                    place.PlaceId,
                    Name = place.PlaceNameAndDesc,
                    Status = place.Status,
                    IsBookable =
                        place.Category != null &&
                        place.Category.BookingSystemId >= 1 &&
                        place.Category.BookingSystemId <= 4
                })
                .OrderByDescending(place =>
                    place.Status == "Approved" ||
                    place.Status == "Active")
                .ThenBy(place => place.Name)
                .ToListAsync();

            if (ownerPlaces.Count == 0)
            {
                ViewBag.ActivePlaceId = 0;
                ViewBag.ActivePlaceName = "لا توجد منشأة";
                ViewBag.CanManagePolicies = false;
                ViewBag.VerificationFlow = false;

                return View();
            }

            var cookieName = $"ownerActivePlace_{ownerId}";
            _ = int.TryParse(
                Request.Cookies[cookieName],
                out var cookiePlaceId);

            var requestedPlaceId =
                autoPlaceId.HasValue && autoPlaceId.Value > 0
                    ? autoPlaceId.Value
                    : cookiePlaceId;

            var activePlace = ownerPlaces
                .FirstOrDefault(place => place.PlaceId == requestedPlaceId)
                ?? ownerPlaces.First();

            if (cookiePlaceId != activePlace.PlaceId)
            {
                Response.Cookies.Append(
                    cookieName,
                    activePlace.PlaceId.ToString(),
                    new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddYears(1),
                        HttpOnly = false,
                        IsEssential = true,
                        SameSite = SameSiteMode.Lax,
                        Secure = Request.IsHttps,
                        Path = "/"
                    });
            }

            ViewBag.ActivePlaceId = activePlace.PlaceId;
            ViewBag.ActivePlaceName = activePlace.Name;
            ViewBag.CanManagePolicies = activePlace.IsBookable;
            ViewBag.VerificationFlow = false;

            return View();
        }

        [HttpGet]
        [AllowPendingOwnerVerification]
        public async Task<IActionResult> GetFacilitySettings(
            int placeId,
            bool verificationFlow = false)
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return Json(new
                {
                    success = false,
                    message = "تعذر التعرف على حساب المالك."
                });
            }

            if (placeId <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "رقم المنشأة غير صحيح."
                });
            }

            var ownsPlace = await CanAccessPolicyEndpointAsync(
                ownerId,
                placeId,
                verificationFlow);

            if (!ownsPlace)
            {
                return Json(new
                {
                    success = false,
                    message = "المنشأة غير موجودة أو لا تدعم إعدادات الحجز."
                });
            }

            var policy = await _context.PlacePolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.PlaceId == placeId);

            if (policy == null)
            {
                return Json(new
                {
                    success = true,
                    isNew = true,
                    data = new FacilitySettingsDto
                    {
                        PlaceId = placeId,
                        NoShowGraceMinutes = 60
                    }
                });
            }

            return Json(new
            {
                success = true,
                isNew = false,
                data = MapPolicyToDto(policy)
            });
        }

        [HttpPost]
        [AllowPendingOwnerVerification]
        public async Task<IActionResult> SaveFacilitySettings(
            [FromBody] FacilitySettingsDto model,
            [FromQuery] bool verificationFlow = false)
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return Json(new
                {
                    success = false,
                    message = "تعذر التعرف على حساب المالك."
                });
            }

            if (model == null || model.PlaceId <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "بيانات المنشأة غير صالحة."
                });
            }

            if (!ModelState.IsValid)
            {
                return InvalidModelJson();
            }

            var operationalValidationMessage =
                ValidateOperationalShiftSchedule(model);

            if (!string.IsNullOrWhiteSpace(operationalValidationMessage))
            {
                return Json(new
                {
                    success = false,
                    message = operationalValidationMessage
                });
            }

            var canAccessPlace = await CanAccessPolicyEndpointAsync(
                ownerId,
                model.PlaceId,
                verificationFlow);

            if (!canAccessPlace)
            {
                return Json(new
                {
                    success = false,
                    message = "لا تملك صلاحية تعديل سياسات هذه المنشأة."
                });
            }

            var ownedPlace = await BuildOwnedBookablePlacesQuery(ownerId)
                .FirstOrDefaultAsync(place => place.PlaceId == model.PlaceId);

            if (ownedPlace == null)
            {
                return Json(new
                {
                    success = false,
                    message = "لا تملك صلاحية تعديل سياسات هذه المنشأة."
                });
            }

            // HARD GUARD: validate every existing package against the exact new
            // operating values BEFORE touching the saved policy.
            // Example: FullDay 10:00-12:00 rejects a 09:00-13:00 package.
            var packagesValidationMessage =
                await ValidateExistingPackagesAgainstProposedSettingsAsync(
                    model.PlaceId,
                    model);

            if (!string.IsNullOrWhiteSpace(packagesValidationMessage))
            {
                return Json(new
                {
                    success = false,
                    message = $"لا يمكن حفظ سياسة التشغيل. {packagesValidationMessage}"
                });
            }

            try
            {
                var policy = await _context.PlacePolicies
                    .FirstOrDefaultAsync(item => item.PlaceId == model.PlaceId);

                if (policy == null)
                {
                    policy = new PlacePolicy
                    {
                        PlaceId = model.PlaceId,
                        NoShowGraceMinutes = 60,
                        CreatedAt = DateTime.Now
                    };

                    ApplyDtoToPolicy(model, policy);
                    _context.PlacePolicies.Add(policy);
                }
                else
                {
                    ApplyDtoToPolicy(model, policy);
                }

                int? platformOwnerId = await _context.SystemSettings
                    .AsNoTracking()
                    .OrderBy(setting => setting.Id)
                    .Select(setting => setting.PlatformOwnerUserId)
                    .FirstOrDefaultAsync();

                // نعتمد الدور من قاعدة البيانات نفسها، وليس فقط من Cookie/Claims.
                // إذا كانت المنشأة مملوكة مباشرة لحساب Admin الحالي، فحفظ السياسات
                // هو آخر خطوة مطلوبة: تصبح Active ومتاحة للعميل فورًا دون موافقة إدارة ثانية.
                bool isCurrentAdmin =
                    await IsCurrentAdminAccountAsync(ownerId);

                bool isAdministrationOwnedPlace =
                    (isCurrentAdmin && ownedPlace.OwnerId == ownerId) ||
                    platformOwnerId == ownerId;

                if (isAdministrationOwnedPlace &&
                    string.Equals(ownedPlace.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                {
                    ownedPlace.Status = "Active";
                    ownedPlace.IsAvailable = true;
                }
                else if (string.Equals(ownedPlace.Status, "WaitOwner", StringComparison.OrdinalIgnoreCase))
                {
                    ownedPlace.Status = "Active";
                    ownedPlace.IsAvailable = true;
                }
                else if (isAdministrationOwnedPlace &&
                         string.Equals(ownedPlace.Status, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    ownedPlace.IsAvailable = true;
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "تم حفظ سياسات المنشأة بنجاح."
                });
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to save facility settings for PlaceId {PlaceId}",
                    model.PlaceId);

                return Json(new
                {
                    success = false,
                    message = "تعذر حفظ السياسات في قاعدة البيانات."
                });
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unexpected error while saving facility settings for PlaceId {PlaceId}",
                    model.PlaceId);

                return Json(new
                {
                    success = false,
                    message = "حدث خطأ غير متوقع أثناء حفظ السياسات."
                });
            }
        }

        [HttpPost]
        [AllowPendingOwnerVerification]
        public async Task<IActionResult> ApplyDefaultPolicies(
            [FromBody] int placeId,
            [FromQuery] bool verificationFlow = false)
        {
            if (!TryGetCurrentOwnerId(out var ownerId))
            {
                return Json(new
                {
                    success = false,
                    message = "تعذر التعرف على حساب المالك."
                });
            }

            if (placeId <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "رقم المنشأة غير صحيح."
                });
            }

            var canAccessPlace = await CanAccessPolicyEndpointAsync(
                ownerId,
                placeId,
                verificationFlow);

            if (!canAccessPlace)
            {
                return Json(new
                {
                    success = false,
                    message = "المنشأة غير موجودة أو لا تملك صلاحية إدارتها."
                });
            }

            var place = await BuildOwnedBookablePlacesQuery(ownerId)
                .Include(item => item.PlacePolicy)
                .FirstOrDefaultAsync(item => item.PlaceId == placeId);

            if (place == null)
            {
                return Json(new
                {
                    success = false,
                    message = "المنشأة غير موجودة أو لا تملك صلاحية إدارتها."
                });
            }

            if (place.PlacePolicy != null)
            {
                return Json(new
                {
                    success = false,
                    message = "تم إعداد السياسات مسبقًا لهذه المنشأة."
                });
            }

            var defaultPolicy = new PlacePolicy
            {
                PlaceId = placeId,
                BookingApprovalMode = "Manual",
                DepositPoliciesJson =
                    "[{\"val\":\"20\",\"type\":\"%\",\"desc\":\"عربون مبدئي لتأكيد الحجز\"}]",
                SecurityPoliciesJson =
                    "[\"إبراز الهوية الوطنية الأصلية أو جواز السفر عند الوصول للمطابقة.\"]",
                PaymentTimeout = 12,
                IsSarSupported = true,
                IsUsdSupported = true,
                CleaningFee = 0m,
                TaxPercentage = 0m,
                IsNonRefundableEnabled = false,
                NonRefundableDiscount = 10,
                FreeCancellationHours = 48,
                ImmediateGracePeriod = true,
                PenaltyType = "LoseDeposit",
                PenaltyValue = 0m,
                ConfiscateOnNoShow = true,
                NoShowGraceMinutes = 60,
                RefundTransferFee = "CustomerPays",
                RefundProcessingDays = 3,
                ExceptionPoliciesJson = "[]",
                OperationSystemMode = "FullDay",
                CheckInTime = "14:00",
                CheckOutTime = "12:00",
                ShiftsJson = "[]",
                BufferTimeHours = 2,
                MinimumNoticeHours = 24,
                MaximumHorizonMonths = 6,
                ClosedDaysCsv = string.Empty,
                IsChatEnabled = true,
                WelcomeMessage =
                    "أهلًا بك في منشأتنا! نسعد بخدمتك. يرجى إبراز إثبات الهوية عند الوصول.",
                OfflineReplyToggle = false,
                OfflineMessage = string.Empty,
                QuickRepliesJson = "[]",
                EmergencyContact = place.PhoneNumber ?? string.Empty,
                ContactEmail = string.Empty,
                IsSoundAlertEnabled = true,
                IsPushNoticeEnabled = true,
                ForFamiliesOnly = false,
                AllowSmoking = false,
                AllowParties = false,
                AllowQatInside = false,
                CreatedAt = DateTime.Now
            };

            var defaultPackagesValidationMessage =
                await ValidateExistingPackagesAgainstPolicyAsync(
                    placeId,
                    defaultPolicy);

            if (!string.IsNullOrWhiteSpace(defaultPackagesValidationMessage))
            {
                return Json(new
                {
                    success = false,
                    message = $"لا يمكن تطبيق السياسة الافتراضية. {defaultPackagesValidationMessage}"
                });
            }

            _context.PlacePolicies.Add(defaultPolicy);

            int? platformOwnerId = await _context.SystemSettings
                .AsNoTracking()
                .OrderBy(setting => setting.Id)
                .Select(setting => setting.PlatformOwnerUserId)
                .FirstOrDefaultAsync();

            bool isCurrentAdmin =
                await IsCurrentAdminAccountAsync(ownerId);

            bool isAdministrationOwnedPlace =
                (isCurrentAdmin && place.OwnerId == ownerId) ||
                platformOwnerId == ownerId;

            if (isAdministrationOwnedPlace &&
                string.Equals(place.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                place.Status = "Active";
                place.IsAvailable = true;
            }
            else if (string.Equals(place.Status, "WaitOwner", StringComparison.OrdinalIgnoreCase))
            {
                place.Status = "Active";
                place.IsAvailable = true;
            }
            else if (isAdministrationOwnedPlace &&
                     string.Equals(place.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                place.IsAvailable = true;
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "تم تطبيق السياسات الافتراضية بنجاح."
            });
        }
    }
}
