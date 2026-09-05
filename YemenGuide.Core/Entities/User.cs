using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class User
    {
        public User()
        {
            AdminAuditLogs = new HashSet<AdminAuditLog>();
            AdminRolePermissions = new HashSet<AdminRolePermission>();
            AdminRoles = new HashSet<AdminRole>();
            AdvertisementEvents = new HashSet<AdvertisementEvent>();
            AdvertisementOwners = new HashSet<Advertisement>();
            AdvertisementReviewedByUsers = new HashSet<Advertisement>();
            BookingCreatedByUsers = new HashSet<Booking>();
            BookingCustomers = new HashSet<Booking>();
            ChatReceivers = new HashSet<Chat>();
            ChatSenders = new HashSet<Chat>();
            CommissionTransactionCreatedByUsers = new HashSet<CommissionTransaction>();
            CommissionTransactionReviewedByUsers = new HashSet<CommissionTransaction>();
            Favorites = new HashSet<Favorite>();
            Notifications = new HashSet<Notification>();
            OwnerWallets = new HashSet<OwnerWallet>();
            Places = new HashSet<Place>();
            PromotionOwners = new HashSet<Promotion>();
            PromotionReviewedByUsers = new HashSet<Promotion>();
            RefundRequestCustomers = new HashSet<RefundRequest>();
            RefundRequestProcessedByUsers = new HashSet<RefundRequest>();
            ReportAgainstUsers = new HashSet<Report>();
            ReportAssignedAdminUsers = new HashSet<Report>();
            ReportReporters = new HashSet<Report>();
            ReportResolvedByUsers = new HashSet<Report>();
            ReviewLikes = new HashSet<ReviewLike>();
            Reviews = new HashSet<Review>();
            SystemSettings = new HashSet<SystemSetting>();
        }

        public int UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string? ProfileImage { get; set; }
        public string? ContactInfo { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string Role { get; set; } = null!;
        public string? AccountState { get; set; }
        public bool IsDarkMode { get; set; }
        public string? CommercialRegisterDocument { get; set; }
        public string? IdentityDocument { get; set; }
        public string? AuthorizationDocument { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
        public int WarningCount { get; set; }
        public DateTime? LastWarningDate { get; set; }
        public DateTime? FreeTrialStartedAt { get; set; }
        public DateTime? FreeTrialEndsAt { get; set; }
        public bool IsEmailVerified { get; set; }
        public string? VerificationCode { get; set; }
        public DateTime? CodeExpiry { get; set; }
        public string? Bio { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? City { get; set; }
        public decimal WalletBalance { get; set; }
        public string? FacebookUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public string PreferredCurrency { get; set; } = null!;
        public bool? ReceiveOffers { get; set; }
        public string? JobTitle { get; set; }
        public bool IsAgreedToTerms { get; set; }
        public DateTime? AgreedToTermsDate { get; set; }
        public string PreferredLanguage { get; set; } = null!;
        public int? AdminRoleId { get; set; }
        public string? EmployeeNumber { get; set; }
        public string? Department { get; set; }
        public string? EmploymentStatus { get; set; }
        public DateTime? HireDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool MustChangePassword { get; set; }

        public virtual AdminRole? AdminRole { get; set; }
        public virtual ICollection<AdminAuditLog> AdminAuditLogs { get; set; }
        public virtual ICollection<AdminRolePermission> AdminRolePermissions { get; set; }
        public virtual ICollection<AdminRole> AdminRoles { get; set; }
        public virtual ICollection<AdvertisementEvent> AdvertisementEvents { get; set; }
        public virtual ICollection<Advertisement> AdvertisementOwners { get; set; }
        public virtual ICollection<Advertisement> AdvertisementReviewedByUsers { get; set; }
        public virtual ICollection<Booking> BookingCreatedByUsers { get; set; }
        public virtual ICollection<Booking> BookingCustomers { get; set; }
        public virtual ICollection<Chat> ChatReceivers { get; set; }
        public virtual ICollection<Chat> ChatSenders { get; set; }
        public virtual ICollection<CommissionTransaction> CommissionTransactionCreatedByUsers { get; set; }
        public virtual ICollection<CommissionTransaction> CommissionTransactionReviewedByUsers { get; set; }
        public virtual ICollection<Favorite> Favorites { get; set; }
        public virtual ICollection<Notification> Notifications { get; set; }
        public virtual ICollection<OwnerWallet> OwnerWallets { get; set; }
        public virtual ICollection<Place> Places { get; set; }
        public virtual ICollection<Promotion> PromotionOwners { get; set; }
        public virtual ICollection<Promotion> PromotionReviewedByUsers { get; set; }
        public virtual ICollection<RefundRequest> RefundRequestCustomers { get; set; }
        public virtual ICollection<RefundRequest> RefundRequestProcessedByUsers { get; set; }
        public virtual ICollection<Report> ReportAgainstUsers { get; set; }
        public virtual ICollection<Report> ReportAssignedAdminUsers { get; set; }
        public virtual ICollection<Report> ReportReporters { get; set; }
        public virtual ICollection<Report> ReportResolvedByUsers { get; set; }
        public virtual ICollection<ReviewLike> ReviewLikes { get; set; }
        public virtual ICollection<Review> Reviews { get; set; }
        public virtual ICollection<SystemSetting> SystemSettings { get; set; }
    }
}
