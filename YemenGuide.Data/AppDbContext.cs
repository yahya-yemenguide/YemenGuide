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
        public virtual DbSet<AdvertisementPlan> AdvertisementPlans { get; set; } = null!;
        public virtual DbSet<Booking> Bookings { get; set; } = null!;
        public virtual DbSet<BookingAdditionalService> BookingAdditionalServices { get; set; } = null!;
        public virtual DbSet<Category> Categories { get; set; } = null!;
        public virtual DbSet<Chat> Chats { get; set; } = null!;
        public virtual DbSet<City> Cities { get; set; } = null!;
        public virtual DbSet<CommissionTransaction> CommissionTransactions { get; set; } = null!;
        public virtual DbSet<Favorite> Favorites { get; set; } = null!;
        public virtual DbSet<FinancialDocument> FinancialDocuments { get; set; } = null!;
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
        public virtual DbSet<SupportedWallet> SupportedWallets { get; set; } = null!;
        public virtual DbSet<SystemSetting> SystemSettings { get; set; } = null!;
        public virtual DbSet<User> Users { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // الاتصال بقاعدة البيانات يُضبط من appsettings.json و Program.cs.
            // لا تضع Connection String أو كلمات مرور داخل هذا الملف.
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

                entity.Property(e => e.ButtonText)
                    .HasMaxLength(50)
                    .HasDefaultValueSql("(N'عرض المنشأة')");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.Currency).HasMaxLength(50);

                entity.Property(e => e.DurationTypeSnapshot).HasMaxLength(20);

                entity.Property(e => e.EndAt).HasColumnType("datetime");

                entity.Property(e => e.ImagePath).HasMaxLength(255);

                entity.Property(e => e.OwnerId).HasColumnName("OwnerID");

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

                entity.Property(e => e.ReferenceNumber).HasMaxLength(100);

                entity.Property(e => e.RejectionReason).HasMaxLength(500);

                entity.Property(e => e.RequestedStartAt).HasColumnType("datetime");

                entity.Property(e => e.ReviewedAt).HasColumnType("datetime");

                entity.Property(e => e.ReviewedByUserId).HasColumnName("ReviewedByUserID");

                entity.Property(e => e.RowVersion)
                    .IsRowVersion()
                    .IsConcurrencyToken();

                entity.Property(e => e.ShortDescription).HasMaxLength(300);

                entity.Property(e => e.StartAt).HasColumnType("datetime");

                entity.Property(e => e.StartImmediately)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Draft')");

                entity.Property(e => e.SubmittedAt).HasColumnType("datetime");

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

                entity.HasOne(d => d.ReviewedByUser)
                    .WithMany(p => p.AdvertisementReviewedByUsers)
                    .HasForeignKey(d => d.ReviewedByUserId)
                    .HasConstraintName("FK_Advertisements_ReviewedBy");
            });

            modelBuilder.Entity<AdvertisementPlan>(entity =>
            {
                entity.HasIndex(e => new { e.PlacementType, e.IsActive }, "IX_AdvertisementPlans_Placement_IsActive");

                entity.HasIndex(e => new { e.PlanName, e.PlacementType }, "UQ_AdvertisementPlans_Name_Placement")
                    .IsUnique();

                entity.Property(e => e.AdvertisementPlanId).HasColumnName("AdvertisementPlanID");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.Currency)
                    .HasMaxLength(50)
                    .HasDefaultValueSql("(N'ريال يمني')");

                entity.Property(e => e.Description).HasMaxLength(500);

                entity.Property(e => e.DurationType).HasMaxLength(20);

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.MaxActiveAdvertisements).HasDefaultValueSql("((1))");

                entity.Property(e => e.PlacementType).HasMaxLength(30);

                entity.Property(e => e.PlanName).HasMaxLength(100);

                entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");
            });

            modelBuilder.Entity<Booking>(entity =>
            {
                entity.HasIndex(e => e.AdvertisementId, "IX_Bookings_AdvertisementID")
                    .HasFilter("([AdvertisementID] IS NOT NULL)");

                entity.HasIndex(e => e.PromotionId, "IX_Bookings_PromotionID")
                    .HasFilter("([PromotionID] IS NOT NULL)");

                entity.Property(e => e.BookingId).HasColumnName("BookingID");

                entity.Property(e => e.AdultsCount).HasDefaultValueSql("((1))");

                entity.Property(e => e.AdvertisementId).HasColumnName("AdvertisementID");

                entity.Property(e => e.BookingState)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Pending')");

                entity.Property(e => e.BookingType).HasMaxLength(50);

                // إعداد يدوي: تواريخ دورة حياة الحجز محفوظة بصيغة datetime2.
                entity.Property(e => e.LastStatusChangedAt)
                    .HasColumnType("datetime2");

                entity.Property(e => e.AcceptedAt)
                    .HasColumnType("datetime2");

                entity.Property(e => e.PaymentApprovedAt)
                    .HasColumnType("datetime2");

                entity.Property(e => e.CompletedAt)
                    .HasColumnType("datetime2");

                entity.Property(e => e.NoShowAt)
                    .HasColumnType("datetime2");

                entity.Property(e => e.ClosedAt)
                    .HasColumnType("datetime2");

                entity.Property(e => e.CancellationReason).HasMaxLength(500);

                entity.Property(e => e.ClosedByType).HasMaxLength(20);

                entity.Property(e => e.ClosedByUserId).HasColumnName("ClosedByUserID");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.CustomerId).HasColumnName("CustomerID");

                entity.Property(e => e.DiscountAmount)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.DiscountTypeSnapshot).HasMaxLength(20);

                entity.Property(e => e.DiscountValueSnapshot).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.EndDate).HasColumnType("datetime");

                entity.Property(e => e.OriginalTotalPrice).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.OwnerCancellationReason).HasMaxLength(500);

                entity.Property(e => e.PackageId).HasColumnName("PackageID");

                entity.Property(e => e.PaymentMethod).HasMaxLength(30);

                entity.Property(e => e.PaymentNotificationImage).HasMaxLength(255);

                entity.Property(e => e.PaymentProofRejectionReason).HasMaxLength(500);

                entity.Property(e => e.PaymentProofRejectedAt)
                    .HasColumnType("datetime2");

                entity.Property(e => e.PaymentProofReviewCount)
                    .HasDefaultValueSql("((0))");

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

                entity.HasOne(d => d.Customer)
                    .WithMany(p => p.Bookings)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__Bookings__Custom__5BE2A6F2");

                entity.HasOne(d => d.Package)
                    .WithMany(p => p.Bookings)
                    .HasForeignKey(d => d.PackageId)
                    .HasConstraintName("FK_Bookings_PlacePackages");

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.Bookings)
                    .HasForeignKey(d => d.PlaceId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__Bookings__PlaceI__5CD6CB2B");

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

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasIndex(e => e.CategoryName, "UQ__Categori__8517B2E07D089925")
                    .IsUnique();

                entity.Property(e => e.CategoryId).HasColumnName("CategoryID");

                entity.Property(e => e.CategoryName).HasMaxLength(50);

                entity.Property(e => e.CommissionPercentage).HasColumnType("decimal(5, 2)");

                entity.Property(e => e.IconClass)
                    .HasMaxLength(50)
                    .HasDefaultValueSql("(N'bi-star')");

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
                    .HasConstraintName("FK__Chats__ReceiverI__6B24EA82");

                entity.HasOne(d => d.Report)
                    .WithMany(p => p.Chats)
                    .HasForeignKey(d => d.ReportId)
                    .HasConstraintName("FK_Chats_Reports");

                entity.HasOne(d => d.Sender)
                    .WithMany(p => p.ChatSenders)
                    .HasForeignKey(d => d.SenderId)
                    // إعداد يدوي: منع الحذف المتسلسل وتجنب Multiple Cascade Paths.
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK__Chats__SenderID__6A30C649");
            });

            modelBuilder.Entity<City>(entity =>
            {
                entity.HasIndex(e => e.CityName, "UQ__Cities__886159E59F0CADCD")
                    .IsUnique();

                entity.Property(e => e.CityId).HasColumnName("CityID");

                entity.Property(e => e.CityName).HasMaxLength(50);

                entity.Property(e => e.ThemeColor)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'#198754')");
            });

            modelBuilder.Entity<CommissionTransaction>(entity =>
            {
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
                    .HasConstraintName("FK__Favorites__Place__5812160E");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Favorites)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__Favorites__UserI__571DF1D5");
            });

            modelBuilder.Entity<FinancialDocument>(entity =>
            {
                entity.HasIndex(e => e.BookingId, "IX_FinancialDocuments_BookingID")
                    .HasFilter("([BookingID] IS NOT NULL)");

                entity.HasIndex(e => e.CommissionTransactionId, "IX_FinancialDocuments_CommissionTransactionID")
                    .HasFilter("([CommissionTransactionID] IS NOT NULL)");

                entity.HasIndex(e => e.PlaceId, "IX_FinancialDocuments_PlaceID")
                    .HasFilter("([PlaceID] IS NOT NULL)");

                entity.HasIndex(e => e.DocumentNumber, "UQ_FinancialDocuments_DocumentNumber")
                    .IsUnique();

                entity.Property(e => e.FinancialDocumentId).HasColumnName("FinancialDocumentID");

                entity.Property(e => e.BookingId).HasColumnName("BookingID");

                entity.Property(e => e.CommissionAmount)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.CommissionPercentage)
                    .HasColumnType("decimal(5, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.CommissionTransactionId).HasColumnName("CommissionTransactionID");

                entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");

                entity.Property(e => e.Currency).HasMaxLength(50);

                entity.Property(e => e.DocumentNumber).HasMaxLength(50);

                entity.Property(e => e.DocumentType).HasMaxLength(40);

                entity.Property(e => e.IssuedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.OwnerId).HasColumnName("OwnerID");

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.Status)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Issued')");

                entity.Property(e => e.Subtotal)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.TotalAmount)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.HasOne(d => d.Booking)
                    .WithMany(p => p.FinancialDocuments)
                    .HasForeignKey(d => d.BookingId)
                    .HasConstraintName("FK_FinancialDocuments_Bookings");

                entity.HasOne(d => d.CommissionTransaction)
                    .WithMany(p => p.FinancialDocuments)
                    .HasForeignKey(d => d.CommissionTransactionId)
                    .HasConstraintName("FK_FinancialDocuments_CommissionTransactions");

                entity.HasOne(d => d.CreatedByUser)
                    .WithMany(p => p.FinancialDocumentCreatedByUsers)
                    .HasForeignKey(d => d.CreatedByUserId)
                    .HasConstraintName("FK_FinancialDocuments_CreatedBy");

                entity.HasOne(d => d.Owner)
                    .WithMany(p => p.FinancialDocumentOwners)
                    .HasForeignKey(d => d.OwnerId)
                    .HasConstraintName("FK_FinancialDocuments_Owners");

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.FinancialDocuments)
                    .HasForeignKey(d => d.PlaceId)
                    .HasConstraintName("FK_FinancialDocuments_Places");
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

                entity.Property(e => e.CommissionPaymentStatus)
                    .HasMaxLength(30)
                    .HasDefaultValueSql("(N'Unpaid')");

                entity.Property(e => e.CommissionRateSnapshot)
                    .HasColumnType("decimal(5, 2)")
                    .HasDefaultValueSql("((0.00))");

                entity.Property(e => e.CommissionReceiptImage).HasMaxLength(255);

                entity.Property(e => e.CommissionReleasedAt).HasColumnType("datetime");

                entity.Property(e => e.CommissionStatus)
                    .HasMaxLength(30)
                    .HasDefaultValueSql("(N'Calculated')");

                entity.Property(e => e.CustomerId).HasColumnName("CustomerID");

                entity.Property(e => e.DepositAmount).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.IsPaymentConfirmed).HasDefaultValueSql("((0))");

                entity.Property(e => e.PlatformCommission).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.SettledAt).HasColumnType("datetime");

                entity.Property(e => e.TransferReferenceNumber).HasMaxLength(100);

                entity.Property(e => e.UpdatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.HasOne(d => d.Booking)
                    .WithOne(p => p.FinancialRecord)
                    .HasForeignKey<FinancialRecord>(d => d.BookingId)
                    .HasConstraintName("FK__Financial__Booki__60A75C0F");

                entity.HasOne(d => d.Customer)
                    .WithMany(p => p.FinancialRecords)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__Financial__Custo__619B8048");
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
                entity.Property(e => e.NotificationId).HasColumnName("NotificationID");

                entity.Property(e => e.CreatedAt)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.NotificationState)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Unread')");

                entity.Property(e => e.UserId).HasColumnName("UserID");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Notifications)
                    .HasForeignKey(d => d.UserId)
                    .HasConstraintName("FK__Notificat__UserI__6EF57B66");
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
                    .HasConstraintName("FK__Places__Category__48CFD27E");

                entity.HasOne(d => d.City)
                    .WithMany(p => p.Places)
                    .HasForeignKey(d => d.CityId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__Places__CityID__49C3F6B7");

                entity.HasOne(d => d.Owner)
                    .WithMany(p => p.Places)
                    .HasForeignKey(d => d.OwnerId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("FK__Places__OwnerID__47DBAE45");
            });

            modelBuilder.Entity<PlaceAdditionalService>(entity =>
            {
                entity.HasKey(e => e.ServiceId);

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

                entity.HasIndex(e => new { e.PlaceId, e.ExceptionDate }, "UQ_Place_Date")
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
                    .HasConstraintName("FK_CalendarExceptions_Places");
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
                    .HasConstraintName("FK__PlaceImag__Place__4CA06362");
            });

            modelBuilder.Entity<PlacePackage>(entity =>
            {
                entity.HasKey(e => e.PackageId);

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

                entity.Property(e => e.StartTime)
                    .HasColumnType("time");

                entity.Property(e => e.EndTime)
                    .HasColumnType("time");

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
                    .HasForeignKey<PlacePolicy>(d => d.PlaceId);
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
                entity.HasIndex(e => e.WalletId, "UQ_PlatformWallets_WalletID")
                    .IsUnique();

                entity.HasIndex(e => e.IsPrimary, "UQ_PlatformWallets_Primary")
                    .IsUnique()
                    .HasFilter("[IsPrimary] = 1");

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

                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.IsAutomatic)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

                entity.Property(e => e.MaximumDiscountAmount).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.MinimumBookingAmount).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.OwnerId).HasColumnName("OwnerID");

                entity.Property(e => e.PackageId).HasColumnName("PackageID");

                entity.Property(e => e.PausedAt).HasColumnType("datetime");

                entity.Property(e => e.PlaceId).HasColumnName("PlaceID");

                entity.Property(e => e.PromoCode).HasMaxLength(50);

                entity.Property(e => e.PromotionTitle).HasMaxLength(150);

                entity.Property(e => e.RejectionReason).HasMaxLength(500);

                entity.Property(e => e.ReviewedAt).HasColumnType("datetime");

                entity.Property(e => e.ReviewedByUserId).HasColumnName("ReviewedByUserID");

                entity.Property(e => e.RoomId).HasColumnName("RoomID");

                entity.Property(e => e.ServiceId).HasColumnName("ServiceID");

                entity.Property(e => e.ShortDescription).HasMaxLength(250);

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

                entity.HasOne(d => d.Package)
                    .WithMany(p => p.Promotions)
                    .HasForeignKey(d => d.PackageId)
                    .HasConstraintName("FK_Promotions_PlacePackages");

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.Promotions)
                    .HasForeignKey(d => d.PlaceId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_Promotions_Places");

                entity.HasOne(d => d.ReviewedByUser)
                    .WithMany(p => p.PromotionReviewedByUsers)
                    .HasForeignKey(d => d.ReviewedByUserId)
                    .HasConstraintName("FK_Promotions_ReviewedBy");

                entity.HasOne(d => d.Room)
                    .WithMany(p => p.Promotions)
                    .HasForeignKey(d => d.RoomId)
                    .HasConstraintName("FK_Promotions_Rooms");

                entity.HasOne(d => d.Service)
                    .WithMany(p => p.Promotions)
                    .HasForeignKey(d => d.ServiceId)
                    .HasConstraintName("FK_Promotions_PlaceAdditionalServices");
            });

            modelBuilder.Entity<RefundRequest>(entity =>
            {
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

                entity.Property(e => e.RefundAmount)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((0.00))");

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
                    .OnDelete(DeleteBehavior.ClientSetNull)
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

                entity.Property(e => e.OwnerReplyDate).HasColumnType("datetime");

                entity.Property(e => e.Priority)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Normal')");

                entity.Property(e => e.PromotionId).HasColumnName("PromotionID");

                entity.Property(e => e.ReportNumber).HasMaxLength(30);

                entity.Property(e => e.ReportStatus)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Pending')");

                entity.Property(e => e.ReportType)
                    .HasMaxLength(30)
                    .HasDefaultValueSql("(N'ClientInfraction')");

                entity.Property(e => e.ReportedPartyReplyDeadline).HasColumnType("datetime");

                entity.Property(e => e.ReporterId).HasColumnName("ReporterID");

                entity.Property(e => e.ReporterReplyDeadline).HasColumnType("datetime");

                entity.Property(e => e.RequestKind)
                    .HasMaxLength(20)
                    .HasDefaultValueSql("(N'Report')");

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
                    .HasConstraintName("FK__Reports__PlaceId__72C60C4A");

                entity.HasOne(d => d.Promotion)
                    .WithMany(p => p.Reports)
                    .HasForeignKey(d => d.PromotionId)
                    .HasConstraintName("FK_Reports_Promotions");

                entity.HasOne(d => d.Reporter)
                    .WithMany(p => p.ReportReporters)
                    .HasForeignKey(d => d.ReporterId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__Reports__Reporte__71D1E811");

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
                    .HasForeignKey(d => d.ParentReviewId);

                entity.HasOne(d => d.Place)
                    .WithMany(p => p.Reviews)
                    .HasForeignKey(d => d.PlaceId)
                    .HasConstraintName("FK__Reviews__PlaceID__66603565");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Reviews)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK__Reviews__UserID__656C112C");
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
                entity.Property(e => e.RoomId).HasColumnName("RoomID");

                entity.Property(e => e.Description).HasMaxLength(500);

                entity.Property(e => e.IsAvailable)
                    .IsRequired()
                    .HasDefaultValueSql("((1))");

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

                entity.Property(e => e.MaintenanceMessage).HasMaxLength(500);

                entity.Property(e => e.MinimumCommissionTopUp)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((20000.00))");

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

                entity.Property(e => e.YertoSar)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((140.00))");

                entity.Property(e => e.YertoUsd)
                    .HasColumnType("decimal(18, 2)")
                    .HasDefaultValueSql("((537.00))");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.EmployeeNumber, "UQ_Users_EmployeeNumber")
                    .IsUnique()
                    .HasFilter("([EmployeeNumber] IS NOT NULL AND [EmployeeNumber]<>N'')");

                entity.HasIndex(e => e.ContactInfo, "UQ__Users__ContactInfo")
                    .IsUnique()
                    .HasFilter("([ContactInfo] IS NOT NULL AND [ContactInfo]<>N'')");

                entity.HasIndex(e => e.PhoneNumber, "UQ__Users__PhoneNumber")
                    .IsUnique()
                    .HasFilter("([PhoneNumber] IS NOT NULL AND [PhoneNumber]<>N'')");

                entity.HasIndex(e => e.Username, "UQ__Users__Username")
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
