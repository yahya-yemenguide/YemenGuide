using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using YemenGuide.Core.Entities;
using YemenGuide.Core.Helpers;

namespace YemenGuide.Data
{
    public partial class AppDbContext : DbContext
    {
        public AppDbContext()
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
        public virtual DbSet<AdminAuditLog> AdminAuditLogs { get; set; } = null!;
        public virtual DbSet<AdminPermission> AdminPermissions { get; set; } = null!;
        public virtual DbSet<AdminRole> AdminRoles { get; set; } = null!;
        public virtual DbSet<AdminRolePermission> AdminRolePermissions { get; set; } = null!;
        public virtual DbSet<Advertisement> Advertisements { get; set; } = null!;
        public virtual DbSet<AdvertisementEvent> AdvertisementEvents { get; set; } = null!;
        public virtual DbSet<AdvertisementMedium> AdvertisementMedia { get; set; } = null!;
        public virtual DbSet<AdvertisementPlan> AdvertisementPlans { get; set; } = null!;
        public virtual DbSet<AdvertisementTarget> AdvertisementTargets { get; set; } = null!;
        public virtual DbSet<Booking> Bookings { get; set; } = null!;
        public virtual DbSet<BookingAdditionalService> BookingAdditionalServices { get; set; } = null!;
        public virtual DbSet<BookingRoomUnit> BookingRoomUnits { get; set; } = null!;
        public virtual DbSet<Category> Categories { get; set; } = null!;
        public virtual DbSet<Chat> Chats { get; set; } = null!;
        public virtual DbSet<City> Cities { get; set; } = null!;
        public virtual DbSet<CommissionTransaction> CommissionTransactions { get; set; } = null!;
        public virtual DbSet<Favorite> Favorites { get; set; } = null!;
        public virtual DbSet<FinancialRecord> FinancialRecords { get; set; } = null!;
        public virtual DbSet<HeroSlide> HeroSlides { get; set; } = null!;
        public virtual DbSet<Notification> Notifications { get; set; } = null!;
        public virtual DbSet<OwnerWallet> OwnerWallets { get; set; } = null!;
        public virtual DbSet<Place> Places { get; set; } = null!;
        public virtual DbSet<PlaceAdditionalService> PlaceAdditionalServices { get; set; } = null!;
        public virtual DbSet<PlaceAmenity> PlaceAmenities { get; set; } = null!;
        public virtual DbSet<PlaceCalendarException> PlaceCalendarExceptions { get; set; } = null!;
        public virtual DbSet<PlaceCommissionAccount> PlaceCommissionAccounts { get; set; } = null!;
        public virtual DbSet<PlaceImage> PlaceImages { get; set; } = null!;
        public virtual DbSet<PlacePackage> PlacePackages { get; set; } = null!;
        public virtual DbSet<PlacePolicy> PlacePolicies { get; set; } = null!;
        public virtual DbSet<PlaceSpace> PlaceSpaces { get; set; } = null!;
        public virtual DbSet<PlatformWallet> PlatformWallets { get; set; } = null!;
        public virtual DbSet<Promotion> Promotions { get; set; } = null!;
        public virtual DbSet<RefundRequest> RefundRequests { get; set; } = null!;
        public virtual DbSet<Report> Reports { get; set; } = null!;
        public virtual DbSet<Review> Reviews { get; set; } = null!;
        public virtual DbSet<ReviewLike> ReviewLikes { get; set; } = null!;
        public virtual DbSet<Room> Rooms { get; set; } = null!;
        public virtual DbSet<RoomUnit> RoomUnits { get; set; } = null!;
        public virtual DbSet<SupportedWallet> SupportedWallets { get; set; } = null!;
        public virtual DbSet<SystemSetting> SystemSettings { get; set; } = null!;
        public virtual DbSet<User> Users { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // إعداد يدوي: الاتصال بقاعدة البيانات يُضبط من appsettings.json و Program.cs.
            // لا نضع Connection String أو كلمات مرور داخل هذا الملف.
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AdminAuditLog>(entity =>
            {
                entity.HasIndex(e => new { e.EntityType, e.EntityId }, "IX_AdminAuditLogs_Entity");

                entity.HasIndex(e => new { e.UserId, e.OccurredAt }, "IX_AdminAuditLogs_UserID_OccurredAt");

                entity.Property(e => e.AdminAuditLogId).HasColumnName("AdminAuditLogID");

                entity.Property(e => e.ActionType).HasMaxLength(100);

                entity.Property(e => e.Description).HasMaxLength(1000);

                entity.Property(e => e.EntityId)
                    .HasMaxLength(100)
                    .HasColumnName("EntityID");

                entity.Property(e => e.EntityType).HasMaxLength(100);

                entity.Property(e => e.FailureReason).HasMaxLength(500);

                entity.Property(e => e.Ipaddress)
                    .HasMaxLength(50)
                    .HasColumnName("IPAddress");

                entity.Property(e => e.IsSuccessful)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.OccurredAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.UserAgent).HasMaxLength(500);

                entity.Property(e => e.UserId).HasColumnName("UserID");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.AdminAuditLogs)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("FK_AdminAuditLogs_Users");
            });

            modelBuilder.Entity<AdminPermission>(entity =>
            {
                entity.HasIndex(e => e.PermissionKey, "UQ_AdminPermissions_PermissionKey")
                    .IsUnique();

                entity.Property(e => e.AdminPermissionId).HasColumnName("AdminPermissionID");

                entity.Property(e => e.Description).HasMaxLength(500);

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.PermissionGroup).HasMaxLength(100);

                entity.Property(e => e.PermissionKey).HasMaxLength(150);

                entity.Property(e => e.PermissionName).HasMaxLength(150);
            });

            modelBuilder.Entity<AdminRole>(entity =>
            {
                entity.HasIndex(e => e.RoleName, "UQ_AdminRoles_RoleName")
                    .IsUnique();

                entity.Property(e => e.AdminRoleId).HasColumnName("AdminRoleID");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");

                entity.Property(e => e.Description).HasMaxLength(500);

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.RoleName).HasMaxLength(100);

                entity.HasOne(d => d.CreatedByUser)
                    .WithMany(p => p.AdminRoles)
                    .HasForeignKey(d => d.CreatedByUserId)
                    .HasConstraintName("FK_AdminRoles_CreatedBy");
            });

            modelBuilder.Entity<AdminRolePermission>(entity =>
            {
                entity.HasKey(e => new { e.AdminRoleId, e.AdminPermissionId });

                entity.Property(e => e.AdminRoleId).HasColumnName("AdminRoleID");

                entity.Property(e => e.AdminPermissionId).HasColumnName("AdminPermissionID");

                entity.Property(e => e.GrantedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.GrantedByUserId).HasColumnName("GrantedByUserID");

                entity.HasOne(d => d.AdminPermission)
                    .WithMany(p => p.AdminRolePermissions)
                    .HasForeignKey(d => d.AdminPermissionId)
                    .HasConstraintName("FK_AdminRolePermissions_AdminPermissions");

                entity.HasOne(d => d.AdminRole)
                    .WithMany(p => p.AdminRolePermissions)
                    .HasForeignKey(d => d.AdminRoleId)
                    .HasConstraintName("FK_AdminRolePermissions_AdminRoles");

                entity.HasOne(d => d.GrantedByUser)
                    .WithMany(p => p.AdminRolePermissions)
                    .HasForeignKey(d => d.GrantedByUserId)
                    .HasConstraintName("FK_AdminRolePermissions_GrantedBy");
            });

            modelBuilder.Entity<Advertisement>(entity =>
            {
                entity.HasIndex(e => new { e.Status, e.PaymentStatus, e.PlacementTypeSnapshot, e.StartAt, e.EndAt, e.PrioritySnapshot, e.LastServedAt }, "IX_Advertisements_ActiveServing");

                entity.HasIndex(e => new { e.BillingModelSnapshot, e.Status, e.SpentAmount, e.TotalBudget }, "IX_Advertisements_BudgetStatus");

                entity.HasIndex(e => new { e.OwnerId, e.Status }, "IX_Advertisements_OwnerID_Status");

                entity.HasIndex(e => new { e.PaymentStatus, e.SubmittedAt }, "IX_Advertisements_PaymentStatus");

                entity.HasIndex(e => new { e.PlaceId, e.Status }, "IX_Advertisements_PlaceID_Status");

                entity.HasIndex(e => new { e.PlacementTypeSnapshot, e.StartAt, e.EndAt }, "IX_Advertisements_Placement_Dates");

                entity.HasIndex(e => new { e.PlatformWalletId, e.ReferenceNumber }, "UQ_Advertisements_PaymentReference")
                    .IsUnique()
                    .HasFilter("([PlatformWalletID] IS NOT NULL AND [ReferenceNumber] IS NOT NULL AND [ReferenceNumber]<>N'')");

                entity.Property(e => e.AdvertisementId).HasColumnName("AdvertisementID");

                entity.Property(e => e.AdvertisementPlanId).HasColumnName("AdvertisementPlanID");

                entity.Property(e => e.AdvertisementTitle).HasMaxLength(150);

                entity.Property(e => e.BillingModelSnapshot)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Flat')");

                entity.Property(e => e.ButtonText)
                    .HasMaxLength(50)
                    .HasDefaultValueSql("(N'عرض المنشأة')");

                entity.Property(e => e.CancellationReason).HasMaxLength(500);

                entity.Property(e => e.CostPerClickSnapshot).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.CostPerThousandSnapshot).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.Currency).HasMaxLength(50);

                entity.Property(e => e.DailyBudget).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.DestinationPath).HasMaxLength(500);

                entity.Property(e => e.DestinationType)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Place')");

                entity.Property(e => e.DeviceTarget)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'All')");

                entity.Property(e => e.DurationTypeSnapshot).HasMaxLength(20);

                entity.Property(e => e.EndAt).HasColumnType("datetime");

                entity.Property(e => e.FrequencyCapPerUserPerDay).HasDefaultValueSql("((3))");

                entity.Property(e => e.ImagePath).HasMaxLength(255);

                entity.Property(e => e.OwnerId).HasColumnName("OwnerID");

                entity.Property(e => e.PauseReason).HasMaxLength(500);

                entity.Property(e => e.PausedAt).HasColumnType("datetime");

                entity.Property(e => e.PaymentStatus)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Unpaid')");

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.PlacementTypeSnapshot).HasMaxLength(30);

                entity.Property(e => e.PlatformWalletId).HasColumnName("PlatformWalletID");

                entity.Property(e => e.PriceSnapshot).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.PromotionId).HasColumnName("PromotionID");

                entity.Property(e => e.ReceiptImage).HasMaxLength(255);

                // إعداد يدوي: تاريخا طلب الاسترداد وتنفيذه محفوظان كـ datetime2.
                entity.Property(e => e.RefundRequestedAt).HasColumnType("datetime2");

                entity.Property(e => e.ReferenceNumber).HasMaxLength(100);

                entity.Property(e => e.RefundAccountNumber).HasMaxLength(100);

                entity.Property(e => e.RefundAmount).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.RefundBeneficiaryName).HasMaxLength(150);

                entity.Property(e => e.RefundNote).HasMaxLength(500);

                entity.Property(e => e.RefundProofImage).HasMaxLength(255);

                entity.Property(e => e.RefundReferenceNumber).HasMaxLength(100);

                entity.Property(e => e.RefundWalletId).HasColumnName("RefundWalletID");

                entity.Property(e => e.RefundedAt).HasColumnType("datetime2");

                entity.Property(e => e.RefundedByUserId).HasColumnName("RefundedByUserID");

                entity.Property(e => e.RejectionReason).HasMaxLength(500);

                entity.Property(e => e.RequestedStartAt).HasColumnType("datetime");

                entity.Property(e => e.ReviewedAt).HasColumnType("datetime");

                entity.Property(e => e.ReviewedByUserId).HasColumnName("ReviewedByUserID");

                entity.Property(e => e.RotationWeight).HasDefaultValueSql("((100))");

                entity.Property(e => e.RowVersion)
                    .IsRowVersion()
                    .IsConcurrencyToken();

                entity.Property(e => e.ShortDescription).HasMaxLength(300);

                entity.Property(e => e.SpentAmount).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.StartAt).HasColumnType("datetime");

                entity.Property(e => e.StartImmediately)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Draft')");

                entity.Property(e => e.SubmittedAt).HasColumnType("datetime");

                entity.Property(e => e.TargetLatitude).HasColumnType("decimal(9, 6)");

                entity.Property(e => e.TargetLongitude).HasColumnType("decimal(9, 6)");

                entity.Property(e => e.TargetRadiusKm).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.TargetingMode)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'All')");

                entity.Property(e => e.TotalBudget).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.TransferDate).HasColumnType("datetime");

                entity.Property(e => e.TransferredAmount).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.HasOne(d => d.AdvertisementPlan)
                    .WithMany(p => p.Advertisements)
                    .HasForeignKey(d => d.AdvertisementPlanId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Advertisements_Plans");

                entity.HasOne(d => d.Owner)
                    .WithMany(p => p.AdvertisementOwners)
                    .HasForeignKey(d => d.OwnerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Advertisements_Owners");

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.Advertisements)
                    .HasForeignKey(d => d.PlaceId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Advertisements_Places");

                entity.HasOne(d => d.PlatformWallet)
                    .WithMany(p => p.Advertisements)
                    .HasForeignKey(d => d.PlatformWalletId)
                    .HasConstraintName("FK_Advertisements_PlatformWallets");

                entity.HasOne(d => d.Promotion)
                    .WithMany(p => p.Advertisements)
                    .HasForeignKey(d => d.PromotionId)
                    .HasConstraintName("FK_Advertisements_Promotions");

                entity.HasOne(d => d.RefundWallet)
                    .WithMany(p => p.Advertisements)
                    .HasForeignKey(d => d.RefundWalletId)
                    .HasConstraintName("FK_Advertisements_RefundWallet");

                entity.HasOne(d => d.RefundedByUser)
                    .WithMany(p => p.AdvertisementRefundedByUsers)
                    .HasForeignKey(d => d.RefundedByUserId)
                    .HasConstraintName("FK_Advertisements_RefundedBy");

                entity.HasOne(d => d.ReviewedByUser)
                    .WithMany(p => p.AdvertisementReviewedByUsers)
                    .HasForeignKey(d => d.ReviewedByUserId)
                    .HasConstraintName("FK_Advertisements_ReviewedBy");
            });

            modelBuilder.Entity<AdvertisementEvent>(entity =>
            {
                entity.HasIndex(e => new { e.AdvertisementId, e.EventType, e.EventAt }, "IX_AdvertisementEvents_Advertisement_EventAt");

                entity.HasIndex(e => new { e.UserId, e.EventAt }, "IX_AdvertisementEvents_User_EventAt")
                    .HasFilter("([UserID] IS NOT NULL)");

                entity.HasIndex(e => new { e.AdvertisementId, e.EventType, e.RequestKey }, "UQ_AdvertisementEvents_RequestKey")
                    .IsUnique()
                    .HasFilter("([RequestKey] IS NOT NULL)");

                entity.Property(e => e.AdvertisementEventId).HasColumnName("AdvertisementEventID");

                entity.Property(e => e.AdvertisementId).HasColumnName("AdvertisementID");

                entity.Property(e => e.BookingId).HasColumnName("BookingID");

                entity.Property(e => e.CostAmount).HasColumnType("decimal(18, 4)");

                entity.Property(e => e.DeviceType).HasMaxLength(20);

                entity.Property(e => e.EventAt).HasDefaultValueSql("(sysdatetime())");

                entity.Property(e => e.EventType).HasMaxLength(30);

                entity.Property(e => e.IpHash).HasMaxLength(128);

                entity.Property(e => e.PlacementType).HasMaxLength(30);

                entity.Property(e => e.RequestKey).HasMaxLength(128);

                entity.Property(e => e.SessionKey).HasMaxLength(100);

                entity.Property(e => e.UserAgentHash).HasMaxLength(128);

                entity.Property(e => e.UserId).HasColumnName("UserID");

                entity.Property(e => e.VisitorHash).HasMaxLength(128);

                entity.HasOne(d => d.Advertisement)
                    .WithMany(p => p.AdvertisementEvents)
                    .HasForeignKey(d => d.AdvertisementId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_AdvertisementEvents_Advertisements");

                entity.HasOne(d => d.Booking)
                    .WithMany(p => p.AdvertisementEvents)
                    .HasForeignKey(d => d.BookingId)
                    .HasConstraintName("FK_AdvertisementEvents_Bookings");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.AdvertisementEvents)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("FK_AdvertisementEvents_Users");
            });

            modelBuilder.Entity<AdvertisementMedium>(entity =>
            {
                entity.HasKey(e => e.AdvertisementMediaId);

                entity.HasIndex(e => new { e.AdvertisementId, e.IsActive, e.DisplayOrder }, "IX_AdvertisementMedia_Advertisement_Order");

                entity.HasIndex(e => e.AdvertisementId, "UQ_AdvertisementMedia_OnePrimary")
                    .IsUnique()
                    .HasFilter("([IsPrimary]=(1))");

                entity.Property(e => e.AdvertisementMediaId).HasColumnName("AdvertisementMediaID");

                entity.Property(e => e.AdvertisementId).HasColumnName("AdvertisementID");

                entity.Property(e => e.AltText).HasMaxLength(200);

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime2")
                    .HasDefaultValueSql("(sysdatetime())");

                entity.Property(e => e.FilePath).HasMaxLength(500);

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.MediaType).HasMaxLength(20);

                entity.Property(e => e.ThumbnailPath).HasMaxLength(500);

                entity.HasOne(d => d.Advertisement)
                    .WithOne(p => p.AdvertisementMedium)
                    .HasForeignKey<AdvertisementMedium>(d => d.AdvertisementId)
                    .HasConstraintName("FK_AdvertisementMedia_Advertisements");
            });

            modelBuilder.Entity<AdvertisementPlan>(entity =>
            {
                entity.HasIndex(e => new { e.PlacementType, e.IsActive }, "IX_AdvertisementPlans_Placement_IsActive");

                entity.HasIndex(e => new { e.PlanName, e.PlacementType }, "UQ_AdvertisementPlans_Name_Placement")
                    .IsUnique();

                entity.Property(e => e.AdvertisementPlanId).HasColumnName("AdvertisementPlanID");

                entity.Property(e => e.BillingModel)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Flat')");

                entity.Property(e => e.CostPerClick).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.CostPerThousandImpressions).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.Currency)
                    .HasMaxLength(50)
                    .HasDefaultValueSql("(N'ريال يمني')");

                entity.Property(e => e.DefaultDailyBudget).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.Description).HasMaxLength(500);

                entity.Property(e => e.DurationType).HasMaxLength(20);

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.MaxActiveAdvertisements).HasDefaultValueSql("((1))");

                entity.Property(e => e.MaxMediaItems).HasDefaultValueSql("((5))");

                entity.Property(e => e.MinimumTotalBudget).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.PlacementType).HasMaxLength(30);

                entity.Property(e => e.PlanName).HasMaxLength(100);

                entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");
            });

            modelBuilder.Entity<AdvertisementTarget>(entity =>
            {
                entity.HasIndex(e => new { e.CategoryId, e.AdvertisementId }, "IX_AdvertisementTargets_Category_Advertisement")
                    .HasFilter("([CategoryID] IS NOT NULL)");

                entity.HasIndex(e => new { e.CityId, e.AdvertisementId }, "IX_AdvertisementTargets_City_Advertisement")
                    .HasFilter("([CityID] IS NOT NULL)");

                entity.HasIndex(e => new { e.AdvertisementId, e.CategoryId }, "UQ_AdvertisementTargets_Category")
                    .IsUnique()
                    .HasFilter("([CategoryID] IS NOT NULL)");

                entity.HasIndex(e => new { e.AdvertisementId, e.CityId }, "UQ_AdvertisementTargets_City")
                    .IsUnique()
                    .HasFilter("([CityID] IS NOT NULL)");

                entity.Property(e => e.AdvertisementTargetId).HasColumnName("AdvertisementTargetID");

                entity.Property(e => e.AdvertisementId).HasColumnName("AdvertisementID");

                entity.Property(e => e.CategoryId).HasColumnName("CategoryID");

                entity.Property(e => e.CityId).HasColumnName("CityID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

                entity.HasOne(d => d.Advertisement)
                    .WithMany(p => p.AdvertisementTargets)
                    .HasForeignKey(d => d.AdvertisementId)
                    .HasConstraintName("FK_AdvertisementTargets_Advertisements");

                entity.HasOne(d => d.Category)
                    .WithMany(p => p.AdvertisementTargets)
                    .HasForeignKey(d => d.CategoryId)
                    .HasConstraintName("FK_AdvertisementTargets_Categories");

                entity.HasOne(d => d.City)
                    .WithMany(p => p.AdvertisementTargets)
                    .HasForeignKey(d => d.CityId)
                    .HasConstraintName("FK_AdvertisementTargets_Cities");
            });

            modelBuilder.Entity<Booking>(entity =>
            {
                entity.HasIndex(e => e.AdvertisementId, "IX_Bookings_AdvertisementID")
                    .HasFilter("([AdvertisementID] IS NOT NULL)");

                entity.HasIndex(e => e.GuestPhone, "IX_Bookings_GuestPhone")
                    .HasFilter("([GuestPhone] IS NOT NULL)");

                entity.HasIndex(e => new { e.PlaceId, e.BookingSource, e.StartDate }, "IX_Bookings_Place_BookingSource_StartDate");

                entity.HasIndex(e => e.PromotionId, "IX_Bookings_PromotionID")
                    .HasFilter("([PromotionID] IS NOT NULL)");

                entity.Property(e => e.BookingId).HasColumnName("BookingID");

                entity.Property(e => e.AdultsCount).HasDefaultValueSql("((1))");

                entity.Property(e => e.AdvertisementId).HasColumnName("AdvertisementID");

                entity.Property(e => e.BookingSource)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Online')");

                entity.Property(e => e.BookingState)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Pending')");

                entity.Property(e => e.BookingType).HasMaxLength(50);

                entity.Property(e => e.CancellationReason).HasMaxLength(500);

                entity.Property(e => e.ClosedByType).HasMaxLength(20);

                entity.Property(e => e.ClosedByUserId).HasColumnName("ClosedByUserID");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");

                entity.Property(e => e.CustomerId).HasColumnName("CustomerID");

                entity.Property(e => e.DiscountAmount)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.DiscountTypeSnapshot).HasMaxLength(20);

                entity.Property(e => e.DiscountValueSnapshot).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.EndDate).HasColumnType("datetime");

                entity.Property(e => e.GuestEmail).HasMaxLength(150);

                entity.Property(e => e.GuestName).HasMaxLength(150);

                entity.Property(e => e.GuestPhone).HasMaxLength(50);

                entity.Property(e => e.ManualBookingChannel).HasMaxLength(20);

                // إعداد يدوي: تواريخ دورة حياة الحجز محفوظة بصيغة datetime2.
                entity.Property(e => e.LastStatusChangedAt).HasColumnType("datetime2");

                entity.Property(e => e.AcceptedAt).HasColumnType("datetime2");

                entity.Property(e => e.PaymentApprovedAt).HasColumnType("datetime2");

                entity.Property(e => e.CompletedAt).HasColumnType("datetime2");

                entity.Property(e => e.NoShowAt).HasColumnType("datetime2");

                entity.Property(e => e.ClosedAt).HasColumnType("datetime2");

                entity.Property(e => e.OriginalTotalPrice).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.OwnerCancellationReason).HasMaxLength(500);

                entity.Property(e => e.OwnerInternalNotes).HasMaxLength(1000);

                entity.Property(e => e.PackageId).HasColumnName("PackageID");

                entity.Property(e => e.PaymentMethod).HasMaxLength(30);

                entity.Property(e => e.PaymentNotificationImage).HasMaxLength(255);

                entity.Property(e => e.PaymentProofRejectionReason).HasMaxLength(500);

                // إعداد يدوي: حقول مراجعة إثبات الدفع.
                entity.Property(e => e.PaymentProofRejectedAt).HasColumnType("datetime2");

                entity.Property(e => e.PaymentProofReviewCount).HasDefaultValueSql("((0))");

                entity.Property(e => e.PaymentStatus)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Unpaid')");

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.PromotionCodeSnapshot).HasMaxLength(50);

                entity.Property(e => e.PromotionId).HasColumnName("PromotionID");

                entity.Property(e => e.PromotionTitleSnapshot).HasMaxLength(150);

                entity.Property(e => e.Quantity).HasDefaultValueSql("((1))");

                entity.Property(e => e.RefundStatus)
                    .HasMaxLength(30)
                    .HasDefaultValueSql("(N'NotApplicable')");

                entity.Property(e => e.RoomId).HasColumnName("RoomID");

                entity.Property(e => e.RowVersion)
                    .IsRowVersion()
                    .IsConcurrencyToken();

                entity.Property(e => e.SeatingSetup).HasMaxLength(100);

                entity.Property(e => e.StartDate).HasColumnType("datetime");

                entity.Property(e => e.TimerExpiredAt).HasColumnType("datetime");

                entity.Property(e => e.TotalPrice).HasColumnType("decimal(18, 2)");

                entity.HasOne(d => d.Advertisement)
                    .WithMany(p => p.Bookings)
                    .HasForeignKey(d => d.AdvertisementId)
                    .HasConstraintName("FK_Bookings_Advertisements");

                entity.HasOne(d => d.CreatedByUser)
                    .WithMany(p => p.BookingCreatedByUsers)
                    .HasForeignKey(d => d.CreatedByUserId)
                    .HasConstraintName("FK_Bookings_CreatedByUser");

                entity.HasOne(d => d.Customer)
                    .WithMany(p => p.BookingCustomers)
                    .HasForeignKey(d => d.CustomerId)
                    .HasConstraintName("FK_Bookings_Customers");

                entity.HasOne(d => d.Package)
                    .WithMany(p => p.Bookings)
                    .HasForeignKey(d => d.PackageId)
                    .HasConstraintName("FK_Bookings_PlacePackages");

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.Bookings)
                    .HasForeignKey(d => d.PlaceId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Bookings_Places");

                entity.HasOne(d => d.Promotion)
                    .WithMany(p => p.Bookings)
                    .HasForeignKey(d => d.PromotionId)
                    .HasConstraintName("FK_Bookings_Promotions");

                entity.HasOne(d => d.Room)
                    .WithMany(p => p.Bookings)
                    .HasForeignKey(d => d.RoomId)
                    .HasConstraintName("FK_Bookings_Rooms");
            });

            modelBuilder.Entity<BookingAdditionalService>(entity =>
            {
                entity.HasIndex(e => new { e.BookingId, e.ServiceId }, "UQ_BookingAdditionalServices")
                    .IsUnique();

                entity.Property(e => e.BookingAdditionalServiceId).HasColumnName("BookingAdditionalServiceID");

                entity.Property(e => e.BookingId).HasColumnName("BookingID");

                entity.Property(e => e.Quantity).HasDefaultValueSql("((1))");

                entity.Property(e => e.ServiceId).HasColumnName("ServiceID");

                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

                entity.HasOne(d => d.Booking)
                    .WithMany(p => p.BookingAdditionalServices)
                    .HasForeignKey(d => d.BookingId)
                    .HasConstraintName("FK_BookingAdditionalServices_Bookings");

                entity.HasOne(d => d.Service)
                    .WithMany(p => p.BookingAdditionalServices)
                    .HasForeignKey(d => d.ServiceId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_BookingAdditionalServices_Services");
            });

            modelBuilder.Entity<BookingRoomUnit>(entity =>
            {
                entity.HasIndex(e => new { e.RoomUnitId, e.AssignmentStatus }, "IX_BookingRoomUnits_RoomUnit_Status");

                entity.HasIndex(e => new { e.BookingId, e.RoomUnitId }, "UQ_BookingRoomUnits_Booking_RoomUnit")
                    .IsUnique();

                entity.Property(e => e.BookingRoomUnitId).HasColumnName("BookingRoomUnitID");

                entity.Property(e => e.AssignedAt).HasDefaultValueSql("(sysdatetime())");

                // إعداد يدوي: العمود في قاعدة البيانات datetime2.
                entity.Property(e => e.ReleasedAt).HasColumnType("datetime2");

                entity.Property(e => e.AssignmentStatus)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Held')");

                entity.Property(e => e.BookingId).HasColumnName("BookingID");

                entity.Property(e => e.RoomUnitId).HasColumnName("RoomUnitID");

                entity.HasOne(d => d.Booking)
                    .WithMany(p => p.BookingRoomUnits)
                    .HasForeignKey(d => d.BookingId)
                    .HasConstraintName("FK_BookingRoomUnits_Bookings");

                entity.HasOne(d => d.RoomUnit)
                    .WithMany(p => p.BookingRoomUnits)
                    .HasForeignKey(d => d.RoomUnitId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_BookingRoomUnits_RoomUnits");
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasIndex(e => e.CategoryName, "UQ_Categories_CategoryName")
                    .IsUnique();

                entity.Property(e => e.CategoryId).HasColumnName("CategoryID");

                entity.Property(e => e.CategoryName).HasMaxLength(50);

                entity.Property(e => e.CommissionPercentage).HasColumnType("decimal(5, 2)");

                entity.Property(e => e.IconClass)
                    .HasMaxLength(50)
                    .HasDefaultValueSql("(N'bi-star')");

                entity.Property(e => e.OwnerCanAddPlaces)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.ThemeColor)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'#0d6efd')");
            });

            modelBuilder.Entity<Chat>(entity =>
            {
                entity.HasKey(e => e.MessageId);

                entity.HasIndex(e => new { e.ReportId, e.SendTime }, "IX_Chats_ReportID_SendTime")
                    .HasFilter("([ReportID] IS NOT NULL)");

                entity.Property(e => e.MessageId).HasColumnName("MessageID");

                // إعداد يدوي: تشفير نص الرسالة عند الحفظ وفك تشفيره عند القراءة.
                entity.Property(e => e.MessageText)
                    .HasConversion(
                        value => EncryptionHelper.Encrypt(value),
                        value => EncryptionHelper.Decrypt(value)
                    );

                entity.Property(e => e.AttachmentUrl).HasMaxLength(255);

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.ReceiverId).HasColumnName("ReceiverID");

                entity.Property(e => e.ReportId).HasColumnName("ReportID");

                entity.Property(e => e.SendTime)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.SenderId).HasColumnName("SenderID");

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.Chats)
                    .HasForeignKey(d => d.PlaceId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("FK_Chats_Places");

                entity.HasOne(d => d.Receiver)
                    .WithMany(p => p.ChatReceivers)
                    .HasForeignKey(d => d.ReceiverId)
                    // إعداد يدوي: منع الحذف المتسلسل وتجنب Multiple Cascade Paths.
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_Chats_Receiver");

                entity.HasOne(d => d.Report)
                    .WithMany(p => p.Chats)
                    .HasForeignKey(d => d.ReportId)
                    .HasConstraintName("FK_Chats_Reports");

                entity.HasOne(d => d.Sender)
                    .WithMany(p => p.ChatSenders)
                    .HasForeignKey(d => d.SenderId)
                    // إعداد يدوي: منع الحذف المتسلسل وتجنب Multiple Cascade Paths.
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_Chats_Sender");
            });

            modelBuilder.Entity<City>(entity =>
            {
                entity.HasIndex(e => e.CityName, "UQ_Cities_CityName")
                    .IsUnique();

                entity.Property(e => e.CityId).HasColumnName("CityID");

                entity.Property(e => e.CityName).HasMaxLength(50);

                entity.Property(e => e.ExchangeRateRegion).HasMaxLength(10);

                entity.Property(e => e.ThemeColor)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'#198754')");
            });

            modelBuilder.Entity<CommissionTransaction>(entity =>
            {
                entity.HasIndex(e => e.BookingId, "IX_CommissionTransactions_BookingID")
                    .HasFilter("([BookingID] IS NOT NULL)");

                entity.HasIndex(e => new { e.PlaceId, e.CreatedAt }, "IX_CommissionTransactions_Place_CreatedAt");

                entity.HasIndex(e => new { e.PlatformWalletId, e.ReferenceNumber }, "UQ_CommissionTransactions_TopUpReference")
                    .IsUnique()
                    .HasFilter("([TransactionType]=N'TopUp' AND [PlatformWalletID] IS NOT NULL AND [ReferenceNumber] IS NOT NULL)");

                entity.Property(e => e.CommissionTransactionId).HasColumnName("CommissionTransactionID");

                entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.AvailableBalanceAfter)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.AvailableBalanceBefore)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.BookingId).HasColumnName("BookingID");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");

                entity.Property(e => e.DebtAfter)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.DebtBefore)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.HeldBalanceAfter)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.HeldBalanceBefore)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.PlatformWalletId).HasColumnName("PlatformWalletID");

                entity.Property(e => e.ReceiptImage).HasMaxLength(255);

                entity.Property(e => e.ReferenceNumber).HasMaxLength(100);

                entity.Property(e => e.RejectionReason).HasMaxLength(500);

                entity.Property(e => e.ReviewedAt).HasColumnType("datetime");

                entity.Property(e => e.ReviewedByUserId).HasColumnName("ReviewedByUserID");

                entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Completed')");

                entity.Property(e => e.TransactionType).HasMaxLength(30);

                entity.Property(e => e.TransferDate).HasColumnType("datetime");

                entity.HasOne(d => d.Booking)
                    .WithMany(p => p.CommissionTransactions)
                    .HasForeignKey(d => d.BookingId)
                    .HasConstraintName("FK_CommissionTransactions_Bookings");

                entity.HasOne(d => d.CreatedByUser)
                    .WithMany(p => p.CommissionTransactionCreatedByUsers)
                    .HasForeignKey(d => d.CreatedByUserId)
                    .HasConstraintName("FK_CommissionTransactions_CreatedBy");

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.CommissionTransactions)
                    .HasForeignKey(d => d.PlaceId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_CommissionTransactions_Places");

                entity.HasOne(d => d.PlatformWallet)
                    .WithMany(p => p.CommissionTransactions)
                    .HasForeignKey(d => d.PlatformWalletId)
                    .HasConstraintName("FK_CommissionTransactions_PlatformWallets");

                entity.HasOne(d => d.ReviewedByUser)
                    .WithMany(p => p.CommissionTransactionReviewedByUsers)
                    .HasForeignKey(d => d.ReviewedByUserId)
                    .HasConstraintName("FK_CommissionTransactions_ReviewedBy");
            });

            modelBuilder.Entity<Favorite>(entity =>
            {
                entity.Property(e => e.FavoriteId).HasColumnName("FavoriteID");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.UserId).HasColumnName("UserID");

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.Favorites)
                    .HasForeignKey(d => d.PlaceId)
                    .HasConstraintName("FK_Favorites_Places");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Favorites)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Favorites_Users");
            });

            modelBuilder.Entity<FinancialRecord>(entity =>
            {
                entity.HasKey(e => e.RecordId);

                entity.HasIndex(e => e.BookingId, "UQ_FinancialRecords_BookingID")
                    .IsUnique();

                entity.Property(e => e.RecordId).HasColumnName("RecordID");

                entity.Property(e => e.AmountCollectedAtProperty)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.AmountPaidOnline)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.AmountToPayAtProperty)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.BookingId).HasColumnName("BookingID");

                entity.Property(e => e.CollectedAt).HasColumnType("datetime");

                entity.Property(e => e.CommissionBaseAmount)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.CommissionChargedAt).HasColumnType("datetime");

                entity.Property(e => e.CommissionHeldAt).HasColumnType("datetime");

                entity.Property(e => e.CommissionRateSnapshot)
                    .HasColumnType("decimal(5, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.CommissionReleasedAt).HasColumnType("datetime");

                entity.Property(e => e.CommissionStatus)
                    .HasMaxLength(30)
                    .HasDefaultValueSql("(N'Calculated')");

                entity.Property(e => e.DepositAmount).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.OnlinePaymentType).HasMaxLength(20);

                entity.Property(e => e.PlatformCommission).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.RowVersion)
                    .IsRowVersion()
                    .IsConcurrencyToken();

                entity.Property(e => e.TransferReferenceNumber).HasMaxLength(100);

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.HasOne(d => d.Booking)
                    .WithOne(p => p.FinancialRecord)
                    .HasForeignKey<FinancialRecord>(d => d.BookingId)
                    .HasConstraintName("FK_FinancialRecords_Bookings");
            });

            modelBuilder.Entity<HeroSlide>(entity =>
            {
                entity.Property(e => e.HeroSlideId).HasColumnName("HeroSlideID");

                entity.Property(e => e.AccentColor)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'#ff5a5f')");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.ImagePath).HasMaxLength(255);

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.MainTextColor)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'#ffffff')");

                entity.Property(e => e.TypingText).HasMaxLength(150);
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId }, "IX_Notifications_Reference")
                    .HasFilter("([ReferenceID] IS NOT NULL)");

                entity.HasIndex(e => new { e.UserId, e.NotificationState, e.CreatedAt }, "IX_Notifications_User_State_CreatedAt");

                entity.Property(e => e.NotificationId).HasColumnName("NotificationID");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.NotificationState)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Unread')");

                entity.Property(e => e.NotificationTitle).HasMaxLength(150);

                entity.Property(e => e.NotificationType)
                    .HasMaxLength(50)
                    .HasDefaultValueSql("(N'General')");

                entity.Property(e => e.ReadAt).HasColumnType("datetime");

                entity.Property(e => e.ReferenceId).HasColumnName("ReferenceID");

                entity.Property(e => e.ReferenceType).HasMaxLength(50);

                entity.Property(e => e.TargetUrl).HasMaxLength(500);

                entity.Property(e => e.UserId).HasColumnName("UserID");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Notifications)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("FK_Notifications_Users");
            });

            modelBuilder.Entity<OwnerWallet>(entity =>
            {
                entity.HasIndex(e => new { e.OwnerId, e.WalletId, e.AccountNumber }, "UQ_OwnerWallets_Account")
                    .IsUnique();

                entity.Property(e => e.OwnerWalletId).HasColumnName("OwnerWalletID");

                entity.Property(e => e.AccountNumber).HasMaxLength(100);

                entity.Property(e => e.BeneficiaryName).HasMaxLength(150);

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.OwnerId).HasColumnName("OwnerID");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.WalletId).HasColumnName("WalletID");

                entity.HasOne(d => d.Owner)
                    .WithMany(p => p.OwnerWallets)
                    .HasForeignKey(d => d.OwnerId)
                    .HasConstraintName("FK_OwnerWallets_Users");

                entity.HasOne(d => d.Wallet)
                    .WithMany(p => p.OwnerWallets)
                    .HasForeignKey(d => d.WalletId)
                    .HasConstraintName("FK_OwnerWallets_SupportedWallets");
            });

            modelBuilder.Entity<Place>(entity =>
            {
                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.AddressDetail).HasMaxLength(500);

                entity.Property(e => e.AllowCashOnArrival)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.CancellationPolicy).HasMaxLength(50);

                entity.Property(e => e.CategoryId).HasColumnName("CategoryID");

                entity.Property(e => e.CheckInTime).HasMaxLength(10);

                entity.Property(e => e.CheckOutTime).HasMaxLength(10);

                entity.Property(e => e.CityId).HasColumnName("CityID");

                entity.Property(e => e.Coordinates).HasMaxLength(100);

                entity.Property(e => e.CoverImage).HasMaxLength(255);

                entity.Property(e => e.DepositAmount)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.DepositCurrency)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'ريال يمني')");

                entity.Property(e => e.EmailAddress).HasMaxLength(100);

                entity.Property(e => e.IsAvailable)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.IsCashOnArrival)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.IsFeatured).HasDefaultValueSql("((0))");

                entity.Property(e => e.LogoImage).HasMaxLength(255);

                entity.Property(e => e.OwnerId).HasColumnName("OwnerID");

                entity.Property(e => e.PhoneNumber).HasMaxLength(50);

                entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.RejectionReason).HasMaxLength(500);

                entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Pending')");

                entity.HasOne(d => d.Category)
                    .WithMany(p => p.Places)
                    .HasForeignKey(d => d.CategoryId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Places_Category");

                entity.HasOne(d => d.City)
                    .WithMany(p => p.Places)
                    .HasForeignKey(d => d.CityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Places_City");

                entity.HasOne(d => d.Owner)
                    .WithMany(p => p.Places)
                    .HasForeignKey(d => d.OwnerId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("FK_Places_Owner");
            });

            modelBuilder.Entity<PlaceAdditionalService>(entity =>
            {
                entity.HasKey(e => e.ServiceId);

                entity.HasIndex(e => new { e.ServiceId, e.PlaceId }, "UQ_PlaceAdditionalServices_ServiceID_PlaceID")
                    .IsUnique();

                entity.Property(e => e.ServiceId).HasColumnName("ServiceID");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.ServiceName).HasMaxLength(100);

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.PlaceAdditionalServices)
                    .HasForeignKey(d => d.PlaceId)
                    .HasConstraintName("FK_PlaceAdditionalServices_Places");
            });

            modelBuilder.Entity<PlaceAmenity>(entity =>
            {
                entity.HasKey(e => e.AmenityId);

                entity.Property(e => e.AmenityId).HasColumnName("AmenityID");

                entity.Property(e => e.AmenityName).HasMaxLength(100);

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.IconClass).HasMaxLength(50);

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.PlaceAmenities)
                    .HasForeignKey(d => d.PlaceId)
                    .HasConstraintName("FK_PlaceAmenities_Places");
            });

            modelBuilder.Entity<PlaceCalendarException>(entity =>
            {
                entity.HasKey(e => e.ExceptionId);

                entity.HasIndex(e => new { e.PlaceId, e.ExceptionDate }, "UQ_PlaceCalendarExceptions_Place_Date")
                    .IsUnique();

                entity.Property(e => e.ExceptionId).HasColumnName("ExceptionID");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.CustomPrice).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.ExceptionDate).HasColumnType("date");

                entity.Property(e => e.ExceptionType).HasMaxLength(20);

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.Reason).HasMaxLength(255);

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.PlaceCalendarExceptions)
                    .HasForeignKey(d => d.PlaceId)
                    .HasConstraintName("FK_PlaceCalendarExceptions_Places");
            });

            modelBuilder.Entity<PlaceCommissionAccount>(entity =>
            {
                entity.HasIndex(e => e.PlaceId, "UQ_PlaceCommissionAccounts_PlaceID")
                    .IsUnique();

                entity.Property(e => e.PlaceCommissionAccountId).HasColumnName("PlaceCommissionAccountID");

                entity.Property(e => e.AccountStatus)
                    .HasMaxLength(30)
                    .HasDefaultValueSql("(N'NeedsTopUp')");

                entity.Property(e => e.AvailableBalance)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.DebtAmount)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.DebtLimit).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.HeldBalance)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.LastTopUpAt).HasColumnType("datetime");

                entity.Property(e => e.LowBalanceThreshold).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.RowVersion)
                    .IsRowVersion()
                    .IsConcurrencyToken();

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.HasOne(d => d.Place)
                    .WithOne(p => p.PlaceCommissionAccount)
                    .HasForeignKey<PlaceCommissionAccount>(d => d.PlaceId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PlaceCommissionAccounts_Places");
            });

            modelBuilder.Entity<PlaceImage>(entity =>
            {
                entity.HasKey(e => e.ImageId);

                entity.Property(e => e.ImageId).HasColumnName("ImageID");

                entity.Property(e => e.ImagePath).HasMaxLength(255);

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.PlaceImages)
                    .HasForeignKey(d => d.PlaceId)
                    .HasConstraintName("FK_PlaceImages_Places");
            });

            modelBuilder.Entity<PlacePackage>(entity =>
            {
                entity.HasKey(e => e.PackageId);

                entity.HasIndex(e => new { e.PackageId, e.PlaceId }, "UQ_PlacePackages_PackageID_PlaceID")
                    .IsUnique();

                entity.Property(e => e.PackageId).HasColumnName("PackageID");

                entity.Property(e => e.Capacity).HasDefaultValueSql("((30))");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.PackageName).HasMaxLength(100);

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");

                // إعداد يدوي: أوقات الباقة محفوظة كـ TIME في SQL Server.
                entity.Property(e => e.StartTime).HasColumnType("time");

                entity.Property(e => e.EndTime).HasColumnType("time");

                entity.Property(e => e.TimeFrame).HasMaxLength(100);

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.PlacePackages)
                    .HasForeignKey(d => d.PlaceId)
                    .HasConstraintName("FK_PlacePackages_Places");
            });

            modelBuilder.Entity<PlacePolicy>(entity =>
            {
                entity.HasKey(e => e.PlaceId);

                entity.Property(e => e.PlaceId).ValueGeneratedNever();

                entity.Property(e => e.BookingApprovalMode)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Instant')");

                entity.Property(e => e.BufferTimeHours).HasDefaultValueSql("((2))");

                entity.Property(e => e.CheckInTime)
                    .HasMaxLength(50)
                    .HasDefaultValueSql("(N'14:00')");

                entity.Property(e => e.CheckOutTime)
                    .HasMaxLength(50)
                    .HasDefaultValueSql("(N'12:00')");

                entity.Property(e => e.CleaningFee).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.ClosedDaysCsv).HasDefaultValueSql("(N'')");

                entity.Property(e => e.ConfiscateOnNoShow)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.ContactEmail)
                    .HasMaxLength(255)
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");

                entity.Property(e => e.DepositPoliciesJson).HasDefaultValueSql("(N'[]')");

                entity.Property(e => e.EmergencyContact)
                    .HasMaxLength(100)
                    .HasDefaultValueSql("(N'')");

                entity.Property(e => e.ExceptionPoliciesJson).HasDefaultValueSql("(N'[]')");

                entity.Property(e => e.FreeCancellationHours).HasDefaultValueSql("((48))");

                entity.Property(e => e.ImmediateGracePeriod)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.IsChatEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.IsPushNoticeEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.IsSarSupported)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.IsSoundAlertEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.IsUsdSupported)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.MaximumHorizonMonths).HasDefaultValueSql("((6))");

                entity.Property(e => e.MinimumNoticeHours).HasDefaultValueSql("((24))");

                entity.Property(e => e.NoShowGraceMinutes).HasDefaultValueSql("((60))");

                entity.Property(e => e.NonRefundableDiscount).HasDefaultValueSql("((10))");

                entity.Property(e => e.OfflineMessage).HasDefaultValueSql("(N'')");

                entity.Property(e => e.OperationSystemMode)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'FullDay')");

                entity.Property(e => e.PaymentTimeout).HasDefaultValueSql("((12))");

                entity.Property(e => e.PenaltyType)
                    .HasMaxLength(30)
                    .HasDefaultValueSql("(N'LoseDeposit')");

                entity.Property(e => e.PenaltyValue).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.QuickRepliesJson).HasDefaultValueSql("(N'[]')");

                entity.Property(e => e.RefundProcessingDays).HasDefaultValueSql("((3))");

                entity.Property(e => e.RefundTransferFee)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'CustomerPays')");

                entity.Property(e => e.SecurityPoliciesJson).HasDefaultValueSql("(N'[]')");

                entity.Property(e => e.ShiftsJson).HasDefaultValueSql("(N'[]')");

                entity.Property(e => e.TaxPercentage).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.WelcomeMessage).HasDefaultValueSql("(N'')");

                entity.HasOne(d => d.Place)
                    .WithOne(p => p.PlacePolicy)
                    .HasForeignKey<PlacePolicy>(d => d.PlaceId)
                    .HasConstraintName("FK_PlacePolicies_Places");
            });

            modelBuilder.Entity<PlaceSpace>(entity =>
            {
                entity.HasKey(e => e.SpaceId);

                entity.Property(e => e.SpaceId).HasColumnName("SpaceID");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.IconClass).HasMaxLength(50);

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.SpaceName).HasMaxLength(100);

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.PlaceSpaces)
                    .HasForeignKey(d => d.PlaceId)
                    .HasConstraintName("FK_PlaceSpaces_Places");
            });

            modelBuilder.Entity<PlatformWallet>(entity =>
            {
                entity.HasIndex(e => e.IsPrimary, "UQ_PlatformWallets_Primary")
                    .IsUnique()
                    .HasFilter("([IsPrimary]=(1))");

                entity.HasIndex(e => e.WalletId, "UQ_PlatformWallets_WalletID")
                    .IsUnique();

                entity.Property(e => e.PlatformWalletId).HasColumnName("PlatformWalletID");

                entity.Property(e => e.AccountNumber).HasMaxLength(100);

                entity.Property(e => e.BeneficiaryName).HasMaxLength(150);

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.WalletId).HasColumnName("WalletID");

                entity.HasOne(d => d.Wallet)
                    .WithOne(p => p.PlatformWallet)
                    .HasForeignKey<PlatformWallet>(d => d.WalletId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_PlatformWallets_SupportedWallets");
            });

            modelBuilder.Entity<Promotion>(entity =>
            {
                entity.HasIndex(e => new { e.OwnerId, e.Status }, "IX_Promotions_OwnerID_Status");

                entity.HasIndex(e => new { e.PlaceId, e.Status }, "IX_Promotions_PlaceID_Status");

                entity.HasIndex(e => new { e.StartDate, e.EndDate }, "IX_Promotions_StartDate_EndDate");

                entity.HasIndex(e => e.PromoCode, "UQ_Promotions_PromoCode")
                    .IsUnique()
                    .HasFilter("([PromoCode] IS NOT NULL AND [PromoCode]<>N'')");

                entity.Property(e => e.PromotionId).HasColumnName("PromotionID");

                entity.Property(e => e.ApplicableDaysCsv).HasMaxLength(100);

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.DiscountType).HasMaxLength(20);

                entity.Property(e => e.DiscountValue).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.EndDate).HasColumnType("date");

                entity.Property(e => e.ImagePath).HasMaxLength(255);

                // إعداد يدوي: القيمة الافتراضية لتكرار مكافأة العرض.
                entity.Property(e => e.IsRewardRepeatable)
                    .HasDefaultValue(false);

                entity.Property(e => e.IsAutomatic)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.MaximumDiscountAmount).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.MinimumBookingAmount).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.OfferMode)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Discount')");

                entity.Property(e => e.OwnerId).HasColumnName("OwnerID");

                entity.Property(e => e.PackageId).HasColumnName("PackageID");

                entity.Property(e => e.PausedAt).HasColumnType("datetime");

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.PromoCode).HasMaxLength(50);

                entity.Property(e => e.PromotionTitle).HasMaxLength(150);

                entity.Property(e => e.RejectionReason).HasMaxLength(500);

                entity.Property(e => e.ReviewedAt).HasColumnType("datetime");

                entity.Property(e => e.ReviewedByUserId).HasColumnName("ReviewedByUserID");

                entity.Property(e => e.RewardServiceId).HasColumnName("RewardServiceID");

                entity.Property(e => e.RewardType).HasMaxLength(30);

                entity.Property(e => e.RoomId).HasColumnName("RoomID");

                entity.Property(e => e.ServiceId).HasColumnName("ServiceID");

                entity.Property(e => e.ShortDescription).HasMaxLength(250);

                entity.Property(e => e.SpecialPrice).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.StartDate).HasColumnType("date");

                entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Draft')");

                entity.Property(e => e.StayEndDate).HasColumnType("date");

                entity.Property(e => e.StayStartDate).HasColumnType("date");

                entity.Property(e => e.SubmittedAt).HasColumnType("datetime");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.HasOne(d => d.Owner)
                    .WithMany(p => p.PromotionOwners)
                    .HasForeignKey(d => d.OwnerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Promotions_Owner");

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.Promotions)
                    .HasForeignKey(d => d.PlaceId)
                    .HasConstraintName("FK_Promotions_Places");

                entity.HasOne(d => d.ReviewedByUser)
                    .WithMany(p => p.PromotionReviewedByUsers)
                    .HasForeignKey(d => d.ReviewedByUserId)
                    .HasConstraintName("FK_Promotions_ReviewedBy");

                entity.HasOne(d => d.P)
                    .WithMany(p => p.Promotions)
                    .HasPrincipalKey(p => new { p.PackageId, p.PlaceId })
                    .HasForeignKey(d => new { d.PackageId, d.PlaceId })
                    .HasConstraintName("FK_Promotions_PlacePackages");

                entity.HasOne(d => d.PlaceAdditionalService)
                    .WithMany(p => p.PromotionPlaceAdditionalServices)
                    .HasPrincipalKey(p => new { p.ServiceId, p.PlaceId })
                    .HasForeignKey(d => new { d.RewardServiceId, d.PlaceId })
                    .HasConstraintName("FK_Promotions_RewardService");

                entity.HasOne(d => d.Room)
                    .WithMany(p => p.Promotions)
                    .HasPrincipalKey(p => new { p.RoomId, p.PlaceId })
                    .HasForeignKey(d => new { d.RoomId, d.PlaceId })
                    .HasConstraintName("FK_Promotions_Rooms");

                entity.HasOne(d => d.PlaceAdditionalServiceNavigation)
                    .WithMany(p => p.PromotionPlaceAdditionalServiceNavigations)
                    .HasPrincipalKey(p => new { p.ServiceId, p.PlaceId })
                    .HasForeignKey(d => new { d.ServiceId, d.PlaceId })
                    .HasConstraintName("FK_Promotions_PlaceAdditionalServices");
            });

            modelBuilder.Entity<RefundRequest>(entity =>
            {
                entity.HasIndex(e => new { e.PlaceId, e.Status, e.RequestedAt }, "IX_RefundRequests_Place_Status");

                entity.HasIndex(e => e.RefundWalletId, "IX_RefundRequests_RefundWalletID")
                    .HasFilter("([RefundWalletID] IS NOT NULL)");

                entity.Property(e => e.RefundRequestId).HasColumnName("RefundRequestID");

                entity.Property(e => e.BookingId).HasColumnName("BookingID");

                entity.Property(e => e.CancellationFee)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.CommissionReleased)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.CustomerId).HasColumnName("CustomerID");

                entity.Property(e => e.DepositPaid)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.OwnerShare)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.PlatformCommission)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.ProcessedAt).HasColumnType("datetime");

                entity.Property(e => e.ProcessedByUserId).HasColumnName("ProcessedByUserID");

                entity.Property(e => e.ProofImage).HasMaxLength(255);

                entity.Property(e => e.ReferenceNumber).HasMaxLength(100);

                entity.Property(e => e.RefundAccountNumber).HasMaxLength(100);

                entity.Property(e => e.RefundAmount)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.RefundBeneficiaryName).HasMaxLength(150);

                entity.Property(e => e.RefundWalletId).HasColumnName("RefundWalletID");

                entity.Property(e => e.RequestedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Pending')");

                entity.HasOne(d => d.Booking)
                    .WithMany(p => p.RefundRequests)
                    .HasForeignKey(d => d.BookingId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_RefundRequests_Bookings");

                entity.HasOne(d => d.Customer)
                    .WithMany(p => p.RefundRequestCustomers)
                    .HasForeignKey(d => d.CustomerId)
                    .HasConstraintName("FK_RefundRequests_Customers");

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.RefundRequests)
                    .HasForeignKey(d => d.PlaceId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_RefundRequests_Places");

                entity.HasOne(d => d.ProcessedByUser)
                    .WithMany(p => p.RefundRequestProcessedByUsers)
                    .HasForeignKey(d => d.ProcessedByUserId)
                    .HasConstraintName("FK_RefundRequests_ProcessedBy");

                entity.HasOne(d => d.RefundWallet)
                    .WithMany(p => p.RefundRequests)
                    .HasForeignKey(d => d.RefundWalletId)
                    .HasConstraintName("FK_RefundRequests_RefundWallet");
            });

            modelBuilder.Entity<Report>(entity =>
            {
                entity.HasIndex(e => new { e.AgainstUserId, e.CreatedAt }, "IX_Reports_AgainstUserID")
                    .HasFilter("([AgainstUserID] IS NOT NULL)");

                entity.HasIndex(e => new { e.AssignedAdminUserId, e.ReportStatus }, "IX_Reports_AssignedAdmin")
                    .HasFilter("([AssignedAdminUserID] IS NOT NULL)");

                entity.HasIndex(e => e.BookingId, "IX_Reports_BookingID")
                    .HasFilter("([BookingID] IS NOT NULL)");

                entity.HasIndex(e => new { e.ReporterId, e.CreatedAt }, "IX_Reports_ReporterID");

                entity.HasIndex(e => new { e.ReportStatus, e.Priority, e.CreatedAt }, "IX_Reports_Status_Priority");

                entity.HasIndex(e => e.ReportNumber, "UQ_Reports_ReportNumber")
                    .IsUnique();

                entity.Property(e => e.ReportId).HasColumnName("ReportID");

                entity.Property(e => e.AdminComment).HasMaxLength(500);

                entity.Property(e => e.AdvertisementId).HasColumnName("AdvertisementID");

                entity.Property(e => e.AgainstUserId).HasColumnName("AgainstUserID");

                entity.Property(e => e.AssignedAdminUserId).HasColumnName("AssignedAdminUserID");

                entity.Property(e => e.BookingId).HasColumnName("BookingID");

                entity.Property(e => e.Category).HasMaxLength(50);

                entity.Property(e => e.ClosedAt).HasColumnType("datetime");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.EscalatedAt).HasColumnType("datetime");

                entity.Property(e => e.EvidenceImage).HasMaxLength(255);

                entity.Property(e => e.OtherCategoryDescription).HasMaxLength(200);

                entity.Property(e => e.OtherReportTypeDescription).HasMaxLength(200);

                entity.Property(e => e.OwnerReplyDate).HasColumnType("datetime");

                entity.Property(e => e.Priority)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Normal')");

                entity.Property(e => e.PromotionId).HasColumnName("PromotionID");

                entity.Property(e => e.ReportNumber).HasMaxLength(30);

                entity.Property(e => e.ReportStatus)
                    .HasMaxLength(30)
                    .HasDefaultValueSql("(N'Pending')");

                entity.Property(e => e.ReportType)
                    .HasMaxLength(30)
                    .HasDefaultValueSql("(N'ClientInfraction')");

                entity.Property(e => e.ReportedPartyReplyDeadline).HasColumnType("datetime");

                entity.Property(e => e.ReporterId).HasColumnName("ReporterID");

                entity.Property(e => e.ReporterReplyDate).HasColumnType("datetime");

                entity.Property(e => e.ReporterReplyDeadline).HasColumnType("datetime");

                entity.Property(e => e.RequestKind)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Report')");

                entity.Property(e => e.RequiredAction).HasMaxLength(30);
                entity.Property(e => e.ResponseContentType).HasMaxLength(20);

                entity.Property(e => e.ResolutionAction).HasMaxLength(100);

                entity.Property(e => e.ResolvedAt).HasColumnType("datetime");

                entity.Property(e => e.ResolvedByUserId).HasColumnName("ResolvedByUserID");

                entity.Property(e => e.ReviewId).HasColumnName("ReviewID");

                entity.Property(e => e.RowVersion)
                    .IsRowVersion()
                    .IsConcurrencyToken();

                entity.Property(e => e.Title).HasMaxLength(200);

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.WarningIssuedAt).HasColumnType("datetime");

                entity.Property(e => e.WithdrawnAt).HasColumnType("datetime");

                entity.Property(e => e.WithdrawnReason).HasMaxLength(500);

                entity.HasOne(d => d.Advertisement)
                    .WithMany(p => p.Reports)
                    .HasForeignKey(d => d.AdvertisementId)
                    .HasConstraintName("FK_Reports_Advertisements");

                entity.HasOne(d => d.AgainstUser)
                    .WithMany(p => p.ReportAgainstUsers)
                    .HasForeignKey(d => d.AgainstUserId)
                    .HasConstraintName("FK_Reports_AgainstUser");

                entity.HasOne(d => d.AssignedAdminUser)
                    .WithMany(p => p.ReportAssignedAdminUsers)
                    .HasForeignKey(d => d.AssignedAdminUserId)
                    .HasConstraintName("FK_Reports_AssignedAdmin");

                entity.HasOne(d => d.Booking)
                    .WithMany(p => p.Reports)
                    .HasForeignKey(d => d.BookingId)
                    .HasConstraintName("FK_Reports_Bookings");

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.Reports)
                    .HasForeignKey(d => d.PlaceId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("FK_Reports_Places");

                entity.HasOne(d => d.Promotion)
                    .WithMany(p => p.Reports)
                    .HasForeignKey(d => d.PromotionId)
                    .HasConstraintName("FK_Reports_Promotions");

                entity.HasOne(d => d.Reporter)
                    .WithMany(p => p.ReportReporters)
                    .HasForeignKey(d => d.ReporterId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Reports_Reporter");

                entity.HasOne(d => d.ResolvedByUser)
                    .WithMany(p => p.ReportResolvedByUsers)
                    .HasForeignKey(d => d.ResolvedByUserId)
                    .HasConstraintName("FK_Reports_ResolvedBy");

                entity.HasOne(d => d.Review)
                    .WithMany(p => p.Reports)
                    .HasForeignKey(d => d.ReviewId)
                    .HasConstraintName("FK_Reports_Reviews");
            });

            modelBuilder.Entity<Review>(entity =>
            {
                entity.Property(e => e.ReviewId).HasColumnName("ReviewID");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.ReplyCreatedAt).HasColumnType("datetime");

                entity.Property(e => e.UserId).HasColumnName("UserID");

                entity.HasOne(d => d.ParentReview)
                    .WithMany(p => p.InverseParentReview)
                    .HasForeignKey(d => d.ParentReviewId)
                    .HasConstraintName("FK_Reviews_Parent");

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.Reviews)
                    .HasForeignKey(d => d.PlaceId)
                    .HasConstraintName("FK_Reviews_Places");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Reviews)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Reviews_Users");
            });

            modelBuilder.Entity<ReviewLike>(entity =>
            {
                entity.HasKey(e => e.LikeId);

                entity.HasIndex(e => new { e.UserId, e.ReviewId }, "UQ_ReviewLikes_User_Review")
                    .IsUnique();

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.HasOne(d => d.Review)
                    .WithMany(p => p.ReviewLikes)
                    .HasForeignKey(d => d.ReviewId)
                    .HasConstraintName("FK_ReviewLikes_Reviews");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.ReviewLikes)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("FK_ReviewLikes_Users");
            });

            modelBuilder.Entity<Room>(entity =>
            {
                entity.HasIndex(e => new { e.RoomId, e.PlaceId }, "UQ_Rooms_RoomID_PlaceID")
                    .IsUnique();

                entity.Property(e => e.RoomId).HasColumnName("RoomID");

                entity.Property(e => e.AreaSquareMeters).HasColumnType("decimal(8, 2)");

                entity.Property(e => e.BedType).HasMaxLength(50);

                entity.Property(e => e.Description).HasMaxLength(500);

                entity.Property(e => e.IsAvailable)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.MaxAdults).HasDefaultValueSql("((1))");

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.Quantity).HasDefaultValueSql("((1))");

                entity.Property(e => e.RoomNumber).HasMaxLength(50);

                entity.Property(e => e.RoomType).HasMaxLength(50);

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.Rooms)
                    .HasForeignKey(d => d.PlaceId)
                    .HasConstraintName("FK_Rooms_Places");
            });

            modelBuilder.Entity<RoomUnit>(entity =>
            {
                // إعداد يدوي: تثبيت المفتاح الأساسي صراحةً.
                entity.HasKey(e => e.RoomUnitId);

                entity.HasIndex(e => new { e.PlaceId, e.RoomNumber }, "UQ_RoomUnits_Place_RoomNumber")
                    .IsUnique();

                entity.Property(e => e.RoomUnitId).HasColumnName("RoomUnitID");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");

                entity.Property(e => e.FloorLabel).HasMaxLength(50);

                entity.Property(e => e.IsAvailable)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.Notes).HasMaxLength(250);

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.RoomId).HasColumnName("RoomID");

                entity.Property(e => e.RoomNumber).HasMaxLength(50);

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.RoomUnits)
                    .HasForeignKey(d => d.PlaceId)
                    // إعداد يدوي: قاعدة البيانات تستخدم NO ACTION لهذا المفتاح.
                    .OnDelete(DeleteBehavior.NoAction)
                    .HasConstraintName("FK_RoomUnits_Places");

                entity.HasOne(d => d.Room)
                    .WithMany(p => p.RoomUnits)
                    .HasPrincipalKey(p => new { p.RoomId, p.PlaceId })
                    .HasForeignKey(d => new { d.RoomId, d.PlaceId })
                    .HasConstraintName("FK_RoomUnits_Rooms");
            });

            modelBuilder.Entity<SupportedWallet>(entity =>
            {
                entity.HasKey(e => e.WalletId);

                entity.HasIndex(e => e.WalletName, "UQ_SupportedWallets_WalletName")
                    .IsUnique();

                entity.Property(e => e.WalletId).HasColumnName("WalletID");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.LogoPath).HasMaxLength(255);

                entity.Property(e => e.WalletName).HasMaxLength(100);
            });

            modelBuilder.Entity<SystemSetting>(entity =>
            {
                entity.Property(e => e.AdenSarRate)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((500.00))");

                entity.Property(e => e.AdenUsdRate)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((1900.00))");

                entity.Property(e => e.AllowNewRegistrations)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                // إعداد يدوي: القيمة الافتراضية لتفعيل الفترة التجريبية للمالك.
                entity.Property(e => e.OwnerFreeTrialEnabled)
                    .IsRequired()
                    .HasDefaultValueSql("((0))");

                entity.Property(e => e.AutoUpdateExchangeRates)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.BannerText).HasDefaultValueSql("(N'إدارة منصة دليلك اليمني تهنئكم بحلول المناسبة السعيدة، كل عام وأنتم بخير! ✨')");

                entity.Property(e => e.CommissionDebtGraceDays).HasDefaultValueSql("((3))");

                entity.Property(e => e.CommissionPercentage)
                    .HasColumnType("decimal(5, 2)")
                    .HasDefaultValueSql("((10.00))");

                entity.Property(e => e.ContactEmail).HasMaxLength(100);

                entity.Property(e => e.DefaultCommissionDebtLimit)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.DefaultCurrency)
                    .HasMaxLength(50)
                    .HasDefaultValueSql("(N'ريال يمني')");

                entity.Property(e => e.DefaultLowBalanceThreshold)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((2000.00))");

                entity.Property(e => e.DefaultWalletLogoPath)
                    .HasMaxLength(255)
                    .HasDefaultValueSql("(N'/images/wallets/default-wallet.png')");

                // إعداد يدوي: أعمدة متابعة تحديث أسعار الصرف من نوع datetime2.
                entity.Property(e => e.ExchangeRateLastAttemptAt).HasColumnType("datetime2");

                entity.Property(e => e.ExchangeRateLastUpdatedAt).HasColumnType("datetime2");

                entity.Property(e => e.ExchangeRateLastError).HasMaxLength(500);

                entity.Property(e => e.ExchangeRateLastSource).HasMaxLength(100);

                entity.Property(e => e.ExchangeRateLastStatus)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Never')");

                entity.Property(e => e.FacebookLink).HasMaxLength(255);

                entity.Property(e => e.FaviconPath).HasMaxLength(255);

                entity.Property(e => e.HeroTitleMain)
                    .HasMaxLength(150)
                    .HasDefaultValueSql("(N'دليلك اليمني بين يديك..')");

                entity.Property(e => e.HeroTitleSub)
                    .HasMaxLength(150)
                    .HasDefaultValueSql("(N'اكتشف')");

                entity.Property(e => e.InstagramLink).HasMaxLength(255);

                entity.Property(e => e.IsBannerActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.LogoPath).HasMaxLength(255);

                entity.Property(e => e.MaintenanceAffectOwners)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.MaintenanceMessage).HasMaxLength(500);

                entity.Property(e => e.MaintenanceShowContact)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.MaintenanceTitle)
                    .HasMaxLength(150)
                    .HasDefaultValueSql("(N'نعود إليكم قريبًا')");

                entity.Property(e => e.MaintenanceType)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Full')");

                // إعداد يدوي: إعدادات الصيانة الإضافية.
                entity.Property(e => e.MaintenanceStartAt).HasColumnType("datetime2");

                entity.Property(e => e.MaintenanceEndAt).HasColumnType("datetime2");

                entity.Property(e => e.MaintenanceBlockBookings).HasDefaultValueSql("((0))");

                entity.Property(e => e.MaintenanceBlockPayments).HasDefaultValueSql("((0))");

                entity.Property(e => e.MaintenanceBlockOwnerChanges).HasDefaultValueSql("((0))");

                entity.Property(e => e.MaintenanceBlockMessages).HasDefaultValueSql("((0))");

                entity.Property(e => e.MaintenanceBlockReviews).HasDefaultValueSql("((0))");

                entity.Property(e => e.MaintenanceBlockRegistrations).HasDefaultValueSql("((0))");

                entity.Property(e => e.MaxImageCount).HasDefaultValueSql("((10))");

                entity.Property(e => e.MaxImageSize).HasDefaultValueSql("((5))");

                entity.Property(e => e.MinimumCommissionTopUp)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((20000.00))");

                entity.Property(e => e.OwnerFreeTrialDays).HasDefaultValueSql("((14))");

                entity.Property(e => e.PlatformOwnerUserId).HasColumnName("PlatformOwnerUserID");

                entity.Property(e => e.RequiredCommissionGuaranteeBalance)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.SanaaSarRate)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((140.00))");

                entity.Property(e => e.SanaaUsdRate)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((537.00))");

                entity.Property(e => e.SeasonTheme)
                    .HasMaxLength(50)
                    .HasDefaultValueSql("(N'Normal')");

                entity.Property(e => e.SiteDescription).HasMaxLength(500);

                entity.Property(e => e.SiteName)
                    .HasMaxLength(100)
                    .HasDefaultValueSql("(N'دليلك اليمني')");

                entity.Property(e => e.SupportPhone).HasMaxLength(50);

                entity.Property(e => e.SuspendBookingWhenBalanceInsufficient)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.TwitterLink).HasMaxLength(255);

                entity.Property(e => e.WhatsAppNumber).HasMaxLength(50);

                entity.HasOne(d => d.PlatformOwnerUser)
                    .WithMany(p => p.SystemSettings)
                    .HasForeignKey(d => d.PlatformOwnerUserId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("FK_SystemSettings_PlatformOwnerUser");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.ContactInfo, "UQ_Users_ContactInfo")
                    .IsUnique()
                    .HasFilter("([ContactInfo] IS NOT NULL AND [ContactInfo]<>N'')");

                entity.HasIndex(e => e.EmployeeNumber, "UQ_Users_EmployeeNumber")
                    .IsUnique()
                    .HasFilter("([EmployeeNumber] IS NOT NULL AND [EmployeeNumber]<>N'')");

                entity.HasIndex(e => e.PhoneNumber, "UQ_Users_PhoneNumber")
                    .IsUnique()
                    .HasFilter("([PhoneNumber] IS NOT NULL AND [PhoneNumber]<>N'')");

                entity.HasIndex(e => e.Username, "UQ_Users_Username")
                    .IsUnique();

                entity.Property(e => e.UserId).HasColumnName("UserID");

                entity.Property(e => e.AccountState)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Active')");

                entity.Property(e => e.AdminRoleId).HasColumnName("AdminRoleID");

                entity.Property(e => e.AgreedToTermsDate).HasColumnType("datetime");

                entity.Property(e => e.AuthorizationDocument).HasMaxLength(255);

                entity.Property(e => e.City).HasMaxLength(100);

                entity.Property(e => e.CodeExpiry).HasColumnType("datetime");

                entity.Property(e => e.CommercialRegisterDocument).HasMaxLength(255);

                entity.Property(e => e.ContactInfo).HasMaxLength(100);

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.DateOfBirth).HasColumnType("date");

                entity.Property(e => e.Department).HasMaxLength(100);

                entity.Property(e => e.EmployeeNumber).HasMaxLength(30);

                entity.Property(e => e.EmploymentStatus).HasMaxLength(20);

                // إعداد يدوي: تواريخ الفترة التجريبية محفوظة كـ datetime2.
                entity.Property(e => e.FreeTrialStartedAt).HasColumnType("datetime2");

                entity.Property(e => e.FreeTrialEndsAt).HasColumnType("datetime2");

                entity.Property(e => e.EndDate).HasColumnType("date");

                entity.Property(e => e.FacebookUrl).HasMaxLength(255);

                entity.Property(e => e.FullName).HasMaxLength(100);

                entity.Property(e => e.Gender).HasMaxLength(10);

                entity.Property(e => e.HireDate).HasColumnType("date");

                entity.Property(e => e.IdentityDocument).HasMaxLength(255);

                entity.Property(e => e.InstagramUrl).HasMaxLength(255);

                entity.Property(e => e.JobTitle).HasMaxLength(100);

                // إعداد يدوي: المحافظة على القيمة الافتراضية لميثاق تهيئة المالك.
                entity.Property(e => e.IsAgreedToTerms)
                    .IsRequired()
                    .HasDefaultValueSql("((0))");

                entity.Property(e => e.LastLoginAt).HasColumnType("datetime");

                entity.Property(e => e.LastSeen).HasColumnType("datetime");

                entity.Property(e => e.LastWarningDate).HasColumnType("datetime");

                entity.Property(e => e.PasswordHash).HasMaxLength(255);

                entity.Property(e => e.PhoneNumber).HasMaxLength(50);

                entity.Property(e => e.PreferredCurrency)
                    .HasMaxLength(50)
                    .HasDefaultValueSql("(N'ريال يمني (قديم)')");

                entity.Property(e => e.PreferredLanguage)
                    .HasMaxLength(10)
                    .HasDefaultValueSql("(N'ar')");

                entity.Property(e => e.ProfileImage).HasMaxLength(255);

                entity.Property(e => e.ReceiveOffers)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.Role).HasMaxLength(20);

                entity.Property(e => e.TwitterUrl).HasMaxLength(255);

                entity.Property(e => e.Username).HasMaxLength(50);

                entity.Property(e => e.VerificationCode).HasMaxLength(10);

                entity.Property(e => e.WalletBalance)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.HasOne(d => d.AdminRole)
                    .WithMany(p => p.Users)
                    .HasForeignKey(d => d.AdminRoleId)
                    .HasConstraintName("FK_Users_AdminRoles");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
