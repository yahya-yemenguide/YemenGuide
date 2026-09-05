// =============================================================
//  OwnerDashboardController.cs - لوحة تحكم المالك
// =============================================================

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Data;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Finance;
using YemenGuide.Web.Services.Notifications;
using YemenGuide.Web.Services.OwnerDashboard;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    [Area("Owner")]
    [Authorize(Roles = "Owner")]
    [AuthorizeActiveOwner]
    public class OwnerDashboardController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ICommissionFinanceService _commissionFinanceService;
        private readonly IOwnerDashboardService _ownerDashboardService;
        private readonly INotificationService _notificationService;

        public OwnerDashboardController(
            AppDbContext context,
            ICommissionFinanceService commissionFinanceService,
            IOwnerDashboardService ownerDashboardService,
            INotificationService notificationService)
        {
            _context = context ??
                throw new ArgumentNullException(nameof(context));

            _commissionFinanceService = commissionFinanceService ??
                throw new ArgumentNullException(nameof(commissionFinanceService));

            _ownerDashboardService = ownerDashboardService ??
                throw new ArgumentNullException(nameof(ownerDashboardService));

            _notificationService = notificationService ??
                throw new ArgumentNullException(nameof(notificationService));
        }

        public async Task<IActionResult> DashboardOwner(
            CancellationToken cancellationToken = default)
        {
            var ownerIdText = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(ownerIdText, out var ownerId) ||
                ownerId <= 0)
            {
                return Challenge();
            }

            var activePlaceCookieName =
                $"ownerActivePlace_{ownerId}";

            _ = int.TryParse(
                Request.Cookies[activePlaceCookieName],
                out var requestedPlaceId);

            var model = await _ownerDashboardService
                .GetDashboardAsync(
                    ownerId,
                    requestedPlaceId > 0 ? requestedPlaceId : null,
                    cancellationToken);

            // =========================================================
            // حالة الفترة التجريبية المستمرة داخل الداشبورد.
            // التجربة نفسها تخص حساب المالك، بينما الرصيد والتشغيل
            // يظلان حسب المنشأة المختارة من الهيدر.
            // =========================================================
            var ownerTrial = await _context.Users
                .AsNoTracking()
                .Where(user => user.UserId == ownerId)
                .Select(user => new
                {
                    user.FreeTrialStartedAt,
                    user.FreeTrialEndsAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (ownerTrial == null)
            {
                return Challenge();
            }

            var settings = await _context.SystemSettings
                .AsNoTracking()
                .OrderBy(item => item.Id)
                .Select(item => new
                {
                    item.DefaultCurrency,
                    item.RequiredCommissionGuaranteeBalance
                })
                .FirstOrDefaultAsync(cancellationToken);

            var now = DateTime.Now;
            var startedAt = ownerTrial.FreeTrialStartedAt;
            var endsAt = ownerTrial.FreeTrialEndsAt;

            var hasCompleteTrialDates =
                startedAt.HasValue &&
                endsAt.HasValue;

            var trialIsActive =
                hasCompleteTrialDates &&
                startedAt!.Value <= now &&
                endsAt!.Value > now;

            var trialIsExpired =
                hasCompleteTrialDates &&
                endsAt!.Value <= now;

            var totalTrialDays = hasCompleteTrialDates
                ? Math.Max(
                    1,
                    (int)Math.Ceiling(
                        (endsAt!.Value - startedAt!.Value).TotalDays))
                : 0;

            var remainingTrialDays = trialIsActive
                ? Math.Max(
                    1,
                    (int)Math.Ceiling(
                        (endsAt!.Value - now).TotalDays))
                : 0;

            var currency = string.IsNullOrWhiteSpace(settings?.DefaultCurrency)
                ? model.Currency
                : settings!.DefaultCurrency!.Trim();

            if (string.IsNullOrWhiteSpace(currency))
            {
                currency = "ريال يمني";
            }

            var requiredGuarantee = Math.Round(
                Math.Max(
                    0m,
                    settings?.RequiredCommissionGuaranteeBalance ?? 0m),
                2);

            var availableGuarantee = Math.Round(
                Math.Max(0m, model.Guarantee.AvailableBalance),
                2);

            var missingGuarantee = Math.Round(
                Math.Max(
                    0m,
                    requiredGuarantee - availableGuarantee),
                2);

            var canAcceptNewBookings = false;

            if (model.PlaceId.HasValue && model.PlaceId.Value > 0)
            {
                var financialAccessForSelectedPlace =
                    await _commissionFinanceService
                        .GetBookingFinancialAccessAsync(
                            model.PlaceId.Value,
                            cancellationToken);

                canAcceptNewBookings =
                    financialAccessForSelectedPlace.CanAcceptNewBookings;

                if (financialAccessForSelectedPlace.RequiredGuaranteeBalance > 0m)
                {
                    requiredGuarantee = Math.Round(
                        financialAccessForSelectedPlace.RequiredGuaranteeBalance,
                        2);

                    missingGuarantee = Math.Round(
                        Math.Max(
                            0m,
                            requiredGuarantee - availableGuarantee),
                        2);
                }
            }

            var alertLevel = "normal";

            if (trialIsExpired)
            {
                alertLevel = "expired";
            }
            else if (trialIsActive && remainingTrialDays == 1)
            {
                alertLevel = "critical";
            }
            else if (trialIsActive && remainingTrialDays == 2)
            {
                alertLevel = "danger";
            }
            else if (trialIsActive && remainingTrialDays == 3)
            {
                alertLevel = "warning";
            }

            var reminderStage = trialIsExpired
                ? "expired"
                : remainingTrialDays.ToString();

            model.TrialStatus = new OwnerTrialStatusDto
            {
                HasTrialHistory =
                    startedAt.HasValue || endsAt.HasValue,
                IsActive = trialIsActive,
                IsExpired = trialIsExpired,
                IsEndingSoon =
                    trialIsActive &&
                    remainingTrialDays >= 1 &&
                    remainingTrialDays <= 3,
                RemainingTrialDays = remainingTrialDays,
                TotalTrialDays = totalTrialDays,
                StartedAt = startedAt,
                EndsAt = endsAt,
                RequiredGuaranteeBalance = requiredGuarantee,
                AvailableGuaranteeBalance = availableGuarantee,
                MissingGuaranteeBalance = missingGuarantee,
                CanAcceptNewBookings = canAcceptNewBookings,
                NeedsTopUp = missingGuarantee > 0.009m,
                Currency = currency,
                AlertLevel = alertLevel,
                ReminderKey = hasCompleteTrialDates
                    ? $"ygOwnerTrialReminder_v2_{ownerId}_" +
                      $"{endsAt!.Value:yyyyMMddHHmmss}_{reminderStage}"
                    : string.Empty
            };

            // إشعار دائم في مركز الإشعارات عند 3/2/1 أيام،
            // وعند انتهاء التجربة إذا كان الرصيد ما زال غير كافٍ.
            await EnsureTrialReminderNotificationAsync(
                ownerId,
                model,
                cancellationToken);

            // =========================================================
            // مهم: الجزء التالي يحافظ على منطق منبثقة الترحيب القديمة.
            // لا تظهر إلا أثناء تجربة فعالة ومع وجود منشأة حجز تشغيلية.
            // =========================================================
            if (!trialIsActive)
            {
                return View(model);
            }

            var bookingPlaces = await _context.Places
                .AsNoTracking()
                .Where(place =>
                    place.OwnerId == ownerId &&
                    place.Category != null &&
                    place.Category.BookingSystemId >= 1 &&
                    place.Category.BookingSystemId <= 4)
                .OrderByDescending(place =>
                    (place.Status == "Approved" ||
                     place.Status == "Active") &&
                    place.IsAvailable == true)
                .ThenBy(place => place.PlaceNameAndDesc)
                .Select(place => new OwnerDashboardBookingPlaceCandidate
                {
                    PlaceId = place.PlaceId,
                    PlaceName = place.PlaceNameAndDesc,
                    Status = place.Status,
                    IsAvailable = place.IsAvailable
                })
                .ToListAsync(cancellationToken);

            var selectedPlace = bookingPlaces
                .FirstOrDefault(place =>
                    place.PlaceId == requestedPlaceId &&
                    IsOperationalPlace(place))
                ?? bookingPlaces.FirstOrDefault(IsOperationalPlace);

            if (selectedPlace == null)
            {
                return View(model);
            }

            var financialAccess =
                await _commissionFinanceService
                    .GetBookingFinancialAccessAsync(
                        selectedPlace.PlaceId,
                        cancellationToken);

            if (!financialAccess.IsFreeTrialActive)
            {
                return View(model);
            }

            var welcomeStartedAt =
                financialAccess.FreeTrialStartedAt ??
                ownerTrial.FreeTrialStartedAt;

            var welcomeEndsAt =
                financialAccess.FreeTrialEndsAt ??
                ownerTrial.FreeTrialEndsAt;

            if (!welcomeStartedAt.HasValue || !welcomeEndsAt.HasValue)
            {
                return View(model);
            }

            var welcomeTotalTrialDays = Math.Max(
                1,
                (int)Math.Ceiling(
                    (welcomeEndsAt.Value - welcomeStartedAt.Value).TotalDays));

            var welcomeRemainingTrialDays =
                financialAccess.RemainingTrialDays > 0
                    ? financialAccess.RemainingTrialDays
                    : Math.Max(
                        1,
                        (int)Math.Ceiling(
                            (welcomeEndsAt.Value - now).TotalDays));

            var welcomeRequiredGuarantee = Math.Round(
                Math.Max(
                    0m,
                    financialAccess.RequiredGuaranteeBalance > 0m
                        ? financialAccess.RequiredGuaranteeBalance
                        : settings?.RequiredCommissionGuaranteeBalance ?? 0m),
                2);

            model.TrialWelcome = new OwnerTrialWelcomeDto
            {
                ShouldShow = true,
                IsActive = true,
                PlaceId = selectedPlace.PlaceId,
                PlaceName = string.IsNullOrWhiteSpace(selectedPlace.PlaceName)
                    ? "منشأتك"
                    : selectedPlace.PlaceName.Trim(),
                StartedAt = welcomeStartedAt,
                EndsAt = welcomeEndsAt,
                TotalTrialDays = welcomeTotalTrialDays,
                RemainingTrialDays = welcomeRemainingTrialDays,
                RequiredGuaranteeBalance = welcomeRequiredGuarantee,
                Currency = currency,
                CanAcceptNewBookings =
                    financialAccess.CanAcceptNewBookings,
                StorageKey =
                    $"ygOwnerTrialWelcome_v1_{ownerId}_" +
                    welcomeStartedAt.Value.ToString("yyyyMMddHHmmss")
            };

            return View(model);
        }

        private async Task EnsureTrialReminderNotificationAsync(
            int ownerId,
            OwnerDashboardDto model,
            CancellationToken cancellationToken)
        {
            var trial = model.TrialStatus;

            var shouldNotifyEnding =
                trial.IsEndingSoon &&
                trial.NeedsTopUp;

            var shouldNotifyExpired =
                trial.IsExpired &&
                trial.NeedsTopUp &&
                !trial.CanAcceptNewBookings;

            if (!shouldNotifyEnding && !shouldNotifyExpired)
            {
                return;
            }

            var title = shouldNotifyExpired
                ? "انتهت الفترة التجريبية"
                : trial.RemainingTrialDays switch
                {
                    1 => "الفترة التجريبية: بقي يوم واحد",
                    2 => "الفترة التجريبية: بقي يومان",
                    _ => "الفترة التجريبية: بقي 3 أيام"
                };

            var alreadyCreated = await _context.Notifications
                .AsNoTracking()
                .AnyAsync(notification =>
                    notification.UserId == ownerId &&
                    notification.NotificationType == NotificationTypes.General &&
                    notification.ReferenceType == NotificationReferenceTypes.User &&
                    notification.ReferenceId == ownerId &&
                    notification.NotificationTitle == title,
                    cancellationToken);

            if (alreadyCreated)
            {
                return;
            }

            var amountText = trial.MissingGuaranteeBalance > 0m
                ? $" ينقص رصيد الضمان {trial.MissingGuaranteeBalance:N0} {trial.Currency}."
                : string.Empty;

            var text = shouldNotifyExpired
                ? "انتهت تجربتك المجانية، وتوقفت الحجوزات الجديدة مؤقتًا حتى يكتمل رصيد ضمان العمولات." + amountText
                : $"بقي {trial.RemainingTrialDays} " +
                  (trial.RemainingTrialDays == 1 ? "يوم" : "أيام") +
                  " على انتهاء التجربة المجانية. جهّز رصيد ضمان العمولات حتى تستمر الحجوزات بدون توقف." +
                  amountText;

            var targetUrl = model.PlaceId.HasValue
                ? $"/Owner/Financials?financeTab=balance&placeId={model.PlaceId.Value}"
                : "/Owner/Financials?financeTab=balance";

            await _notificationService.CreateAsync(
                new CreateNotificationDto
                {
                    UserId = ownerId,
                    NotificationTitle = title,
                    NotificationText = text,
                    NotificationType = NotificationTypes.General,
                    TargetUrl = targetUrl,
                    ReferenceType = NotificationReferenceTypes.User,
                    ReferenceId = ownerId
                },
                cancellationToken);
        }

        private static bool IsOperationalPlace(
            OwnerDashboardBookingPlaceCandidate place)
        {
            if (place.IsAvailable != true)
            {
                return false;
            }

            return string.Equals(
                       place.Status,
                       "Approved",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       place.Status,
                       "Active",
                       StringComparison.OrdinalIgnoreCase);
        }

        private sealed class OwnerDashboardBookingPlaceCandidate
        {
            public int PlaceId { get; set; }
            public string? PlaceName { get; set; }
            public string? Status { get; set; }
            public bool? IsAvailable { get; set; }
        }
    }
}
