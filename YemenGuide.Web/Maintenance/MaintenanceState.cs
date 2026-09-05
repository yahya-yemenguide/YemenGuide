using System;

namespace YemenGuide.Web.Maintenance
{
    public sealed class MaintenanceState
    {
        public bool IsEnabled { get; init; }
        public bool IsActive { get; init; }
        public string Type { get; init; } = MaintenanceTypeNames.Full;

        // هوية المنصة - نفس بيانات الهيدر
        public string SiteName { get; init; } = "دليلك اليمني";
        public string? SiteDescription { get; init; }
        public string LogoPath { get; init; } = "/images/logo1.png";
        public string FaviconPath { get; init; } = "/favicon.ico";

        public string Title { get; init; } = "نعود إليكم قريبًا";
        public string Message { get; init; } = "المنصة تحت الصيانة حاليًا.";

        public bool AffectOwners { get; init; }
        public DateTime? StartAtUtc { get; init; }
        public DateTime? EndAtUtc { get; init; }
        public bool ShowContact { get; init; }

        public string? ContactEmail { get; init; }
        public string? SupportPhone { get; init; }
        public string? WhatsAppNumber { get; init; }

        public bool BlockBookings { get; init; }
        public bool BlockPayments { get; init; }
        public bool BlockOwnerChanges { get; init; }
        public bool BlockMessages { get; init; }
        public bool BlockReviews { get; init; }
        public bool BlockRegistrations { get; init; }

        public bool IsFull =>
            string.Equals(Type, MaintenanceTypeNames.Full, StringComparison.Ordinal);

        public bool IsReadOnly =>
            string.Equals(Type, MaintenanceTypeNames.ReadOnly, StringComparison.Ordinal);

        public bool IsPartial =>
            string.Equals(Type, MaintenanceTypeNames.Partial, StringComparison.Ordinal);

        public bool IsOperationBlocked(MaintenanceOperation operation)
        {
            if (!IsActive)
            {
                return false;
            }

            if (IsFull || IsReadOnly)
            {
                return true;
            }

            return operation switch
            {
                MaintenanceOperation.Bookings => BlockBookings,
                MaintenanceOperation.Payments => BlockPayments,
                MaintenanceOperation.OwnerChanges => BlockOwnerChanges,
                MaintenanceOperation.Messages => BlockMessages,
                MaintenanceOperation.Reviews => BlockReviews,
                MaintenanceOperation.Registrations => BlockRegistrations,
                _ => false
            };
        }
    }
}
