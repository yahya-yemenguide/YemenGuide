using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using YemenGuide.Data;

namespace YemenGuide.Web.Maintenance
{
    public sealed class MaintenanceService : IMaintenanceService
    {
        private readonly AppDbContext _db;
        private MaintenanceState? _cachedState;

        public MaintenanceService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<MaintenanceState> GetStateAsync(
            CancellationToken cancellationToken = default)
        {
            if (_cachedState != null)
            {
                return _cachedState;
            }

            var settings = await _db.SystemSettings
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (settings == null)
            {
                _cachedState = new MaintenanceState
                {
                    IsEnabled = false,
                    IsActive = false
                };

                return _cachedState;
            }

            var startAtUtc = NormalizeUtc(settings.MaintenanceStartAt);
            var endAtUtc = NormalizeUtc(settings.MaintenanceEndAt);
            var nowUtc = DateTime.UtcNow;

            var isInsideSchedule =
                (!startAtUtc.HasValue || nowUtc >= startAtUtc.Value) &&
                (!endAtUtc.HasValue || nowUtc < endAtUtc.Value);

            _cachedState = new MaintenanceState
            {
                IsEnabled = settings.MaintenanceMode,
                IsActive = settings.MaintenanceMode && isInsideSchedule,
                Type = MaintenanceTypeNames.Normalize(settings.MaintenanceType),

                // نفس الهوية التي يعتمد عليها الهيدر
                SiteName = string.IsNullOrWhiteSpace(settings.SiteName)
                    ? "دليلك اليمني"
                    : settings.SiteName.Trim(),

                SiteDescription = string.IsNullOrWhiteSpace(settings.SiteDescription)
                    ? null
                    : settings.SiteDescription.Trim(),

                LogoPath = string.IsNullOrWhiteSpace(settings.LogoPath)
                    ? "/images/logo1.png"
                    : settings.LogoPath.Trim(),

                FaviconPath = string.IsNullOrWhiteSpace(settings.FaviconPath)
                    ? "/favicon.ico"
                    : settings.FaviconPath.Trim(),

                Title = string.IsNullOrWhiteSpace(settings.MaintenanceTitle)
                    ? "نعود إليكم قريبًا"
                    : settings.MaintenanceTitle.Trim(),

                Message = string.IsNullOrWhiteSpace(settings.MaintenanceMessage)
                    ? "نعمل حاليًا على تحسين المنصة وسنعود إليكم قريبًا."
                    : settings.MaintenanceMessage.Trim(),

                AffectOwners = settings.MaintenanceAffectOwners == true,
                StartAtUtc = startAtUtc,
                EndAtUtc = endAtUtc,
                ShowContact = settings.MaintenanceShowContact == true,

                ContactEmail = settings.ContactEmail,
                SupportPhone = settings.SupportPhone,
                WhatsAppNumber = settings.WhatsAppNumber,

                BlockBookings = settings.MaintenanceBlockBookings,
                BlockPayments = settings.MaintenanceBlockPayments,
                BlockOwnerChanges = settings.MaintenanceBlockOwnerChanges,
                BlockMessages = settings.MaintenanceBlockMessages,
                BlockReviews = settings.MaintenanceBlockReviews,
                BlockRegistrations = settings.MaintenanceBlockRegistrations
            };

            return _cachedState;
        }

        public bool IsUserAffected(
            ClaimsPrincipal? user,
            MaintenanceState state)
        {
            if (!state.IsActive)
            {
                return false;
            }

            // الإدارة لا تتأثر بالصيانة مطلقًا.
            if (user?.Identity?.IsAuthenticated == true &&
                user.IsInRole("Admin"))
            {
                return false;
            }

            // المالك يتأثر فقط إذا فعّل الأدمن هذا الخيار.
            if (user?.Identity?.IsAuthenticated == true &&
                user.IsInRole("Owner"))
            {
                return state.AffectOwners;
            }

            // الزوار والعملاء يتأثرون بالصيانة عند تفعيلها.
            return true;
        }

        public async Task<bool> IsOperationBlockedAsync(
            MaintenanceOperation operation,
            ClaimsPrincipal? user,
            CancellationToken cancellationToken = default)
        {
            var state = await GetStateAsync(cancellationToken);

            if (!IsUserAffected(user, state))
            {
                return false;
            }

            return state.IsOperationBlocked(operation);
        }

        private static DateTime? NormalizeUtc(DateTime? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return value.Value.Kind switch
            {
                DateTimeKind.Utc => value.Value,
                DateTimeKind.Local => value.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            };
        }
    }
}
