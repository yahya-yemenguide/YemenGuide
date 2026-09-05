using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.DTOs.Owner;
using YemenGuide.Core.Entities;
using YemenGuide.Data;
using YemenGuide.Web.Services.Notifications;

namespace YemenGuide.Web.Services.Reports
{
    /// <summary>
    /// محرك بلاغات المالك.
    ///
    /// قواعد الخصوصية:
    /// - المالك لا يرى بلاغ عميل ضده إلا بعد أن تطلب الإدارة منه ردًا فعليًا؛
    ///   ويثبت ذلك بوجود رسالة قضية بين الإدارة والمالك داخل Chats.
    /// - في القضايا المحالة للمالك لا تظهر له رسائل Admin↔Customer.
    /// - في بلاغ المالك ضد العميل لا يصل للعميل أي إشعار أو رابط قضية من هذه الخدمة.
    /// - رد المالك لا ينشئ Report جديدًا؛ يحفظ داخل نفس ReportID.
    /// </summary>
    public sealed class OwnerReportsService : IOwnerReportsService
    {
        private static readonly string[] AllowedEvidenceExtensions =
        {
            ".jpg", ".jpeg", ".png", ".webp", ".pdf"
        };

        private static readonly string[] AllowedEvidenceContentTypes =
        {
            "image/jpeg", "image/png", "image/webp", "application/pdf"
        };

        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _environment;
        private readonly INotificationService _notifications;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<OwnerReportsService> _logger;

        public OwnerReportsService(
            AppDbContext db,
            IWebHostEnvironment environment,
            INotificationService notifications,
            IHttpContextAccessor httpContextAccessor,
            ILogger<OwnerReportsService> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OwnerReportsPageDto> GetReportsPageAsync(
            int ownerId,
            OwnerReportsFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(ownerId);
            filter ??= new OwnerReportsFilterDto();

            var normalized = new OwnerReportsFilterDto
            {
                Scope = OwnerReportValues.NormalizeScope(filter.Scope),
                StatusGroup = OwnerReportValues.NormalizeStatusGroup(filter.StatusGroup),
                Search = NormalizeOptionalText(filter.Search, 150),
                Page = filter.Page <= 0 ? 1 : filter.Page,
                PageSize = NormalizePageSize(filter.PageSize)
            };

            var activePlaceId = await ResolveActivePlaceIdAsync(ownerId, cancellationToken);
            if (!activePlaceId.HasValue)
            {
                return new OwnerReportsPageDto
                {
                    Filter = normalized,
                    Summary = new OwnerReportSummaryDto(),
                    Items = new List<OwnerReportListItemDto>(),
                    FilteredCount = 0,
                    CurrentPage = 1,
                    PageSize = normalized.PageSize,
                    TotalPages = 0
                };
            }

            var incomingBase = BuildIncomingQuery(ownerId, activePlaceId.Value);
            var mineBase = BuildMineQuery(ownerId, activePlaceId.Value);

            var summary = new OwnerReportSummaryDto
            {
                IncomingCount = await incomingBase.CountAsync(cancellationToken),
                IncomingNeedsResponseCount = await incomingBase.CountAsync(
                    report =>
                        report.ReportStatus == ReportConstants.Statuses.WaitingForReportedParty &&
                        report.RequiredAction == ReportConstants.RequiredActions.ReportedPartyResponse,
                    cancellationToken),
                MineCount = await mineBase.CountAsync(cancellationToken),
                MineActiveCount = await mineBase.CountAsync(
                    report =>
                        report.ReportStatus != ReportConstants.Statuses.Resolved &&
                        report.ReportStatus != ReportConstants.Statuses.Rejected &&
                        report.ReportStatus != ReportConstants.Statuses.Closed &&
                        report.ReportStatus != ReportConstants.Statuses.Withdrawn,
                    cancellationToken),
                MineCompletedCount = await mineBase.CountAsync(
                    report =>
                        report.ReportStatus == ReportConstants.Statuses.Resolved ||
                        report.ReportStatus == ReportConstants.Statuses.Rejected ||
                        report.ReportStatus == ReportConstants.Statuses.Closed ||
                        report.ReportStatus == ReportConstants.Statuses.Withdrawn,
                    cancellationToken)
            };

            var query = normalized.Scope == OwnerReportValues.ScopeMine
                ? mineBase
                : incomingBase;

            query = ApplyStatusFilter(query, normalized.StatusGroup);

            if (!string.IsNullOrWhiteSpace(normalized.Search))
            {
                var search = normalized.Search;
                query = query.Where(report =>
                    report.ReportNumber.Contains(search) ||
                    (report.Title != null && report.Title.Contains(search)) ||
                    report.ReportReason.Contains(search) ||
                    (report.Place != null && report.Place.PlaceNameAndDesc.Contains(search)) ||
                    (report.Booking != null && report.Booking.Place.PlaceNameAndDesc.Contains(search)) ||
                    (report.Reporter != null && report.Reporter.FullName.Contains(search)) ||
                    (report.AgainstUser != null && report.AgainstUser.FullName.Contains(search)));
            }

            var filteredCount = await query.CountAsync(cancellationToken);
            var totalPages = filteredCount == 0
                ? 0
                : (int)Math.Ceiling(filteredCount / (double)normalized.PageSize);

            if (totalPages > 0 && normalized.Page > totalPages)
                normalized.Page = totalPages;

            var skip = (normalized.Page - 1) * normalized.PageSize;
            var now = DateTime.Now;

            var rows = await query
                .AsNoTracking()
                .Include(report => report.Reporter)
                .Include(report => report.AgainstUser)
                .Include(report => report.Place)
                .Include(report => report.Booking)
                    .ThenInclude(booking => booking!.Place)
                .Include(report => report.Review)
                    .ThenInclude(review => review!.Place)
                .OrderByDescending(report =>
                    report.ReportStatus == ReportConstants.Statuses.WaitingForReportedParty &&
                    report.RequiredAction == ReportConstants.RequiredActions.ReportedPartyResponse)
                .ThenByDescending(report => report.UpdatedAt)
                .ThenByDescending(report => report.ReportId)
                .Skip(skip)
                .Take(normalized.PageSize)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var items = rows
                .Select(report => BuildListItem(report, ownerId, normalized.Scope, now))
                .ToList();

            return new OwnerReportsPageDto
            {
                Filter = normalized,
                Summary = summary,
                Items = items,
                FilteredCount = filteredCount,
                CurrentPage = normalized.Page,
                PageSize = normalized.PageSize,
                TotalPages = totalPages
            };
        }

        public async Task<OwnerCreateReportPageDto> GetCreatePageAsync(
            int ownerId,
            OwnerCreateReportRequestDto? form = null,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(ownerId);
            form ??= new OwnerCreateReportRequestDto();

            var activePlaceId = await ResolveActivePlaceIdAsync(ownerId, cancellationToken);
            if (!activePlaceId.HasValue)
            {
                return new OwnerCreateReportPageDto
                {
                    Form = form,
                    ReportTypes = BuildReportTypeOptions(),
                    Categories = BuildCategoryOptions(),
                    RecentBookings = Array.Empty<OwnerReportReferenceOptionDto>(),
                    RecentReviews = Array.Empty<OwnerReportReferenceOptionDto>(),
                    SelectedReference = null
                };
            }

            var recentBookings = await SearchBookingsAsync(
                ownerId,
                search: null,
                take: 12,
                skip: 0,
                cancellationToken);

            var recentReviews = await SearchReviewsAsync(
                ownerId,
                search: null,
                take: 12,
                skip: 0,
                cancellationToken);

            OwnerReportReferenceOptionDto? selectedReference = null;
            if (form.ReferenceId > 0)
            {
                if (string.Equals(
                    form.ReferenceType,
                    OwnerReportValues.ReferenceReview,
                    StringComparison.OrdinalIgnoreCase))
                {
                    selectedReference = await ResolveReviewReferenceForDisplayAsync(
                        ownerId,
                        activePlaceId.Value,
                        form.ReferenceId,
                        cancellationToken);
                }
                else
                {
                    selectedReference = await ResolveBookingReferenceForDisplayAsync(
                        ownerId,
                        activePlaceId.Value,
                        form.ReferenceId,
                        cancellationToken);
                }
            }

            return new OwnerCreateReportPageDto
            {
                Form = form,
                ReportTypes = BuildReportTypeOptions(),
                Categories = BuildCategoryOptions(),
                RecentBookings = recentBookings,
                RecentReviews = recentReviews,
                SelectedReference = selectedReference
            };
        }

        public async Task<IReadOnlyList<OwnerReportReferenceOptionDto>> SearchBookingsAsync(
            int ownerId,
            string? search,
            int take = 10,
            int skip = 0,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(ownerId);

            var activePlaceId = await ResolveActivePlaceIdAsync(ownerId, cancellationToken);
            if (!activePlaceId.HasValue)
                return Array.Empty<OwnerReportReferenceOptionDto>();

            var normalizedSearch = NormalizeOptionalText(search, 100);
            var normalizedTake = Math.Clamp(take, 1, 30);
            var normalizedSkip = Math.Max(0, skip);

            var query = _db.Bookings
                .AsNoTracking()
                .Where(booking =>
                    booking.PlaceId == activePlaceId.Value &&
                    booking.Place.OwnerId == ownerId &&
                    booking.Customer.Role == "Customer");

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(booking =>
                    booking.BookingId.ToString().Contains(normalizedSearch) ||
                    booking.Customer.FullName.Contains(normalizedSearch) ||
                    booking.Place.PlaceNameAndDesc.Contains(normalizedSearch));
            }

            return await query
                .OrderByDescending(booking => booking.CreatedAt)
                .ThenByDescending(booking => booking.BookingId)
                .Skip(normalizedSkip)
                .Take(normalizedTake)
                .Select(booking => new OwnerReportReferenceOptionDto
                {
                    ReferenceType = OwnerReportValues.ReferenceBooking,
                    ReferenceId = booking.BookingId,
                    Title = $"الحجز #{booking.BookingId}",
                    Subtitle = booking.BookingState,
                    PlaceId = booking.PlaceId,
                    PlaceName = booking.Place.PlaceNameAndDesc,
                    CustomerId = booking.Customer.UserId,
                    CustomerName = booking.Customer.FullName,
                    CustomerWarningCount = booking.Customer.WarningCount,
                    Date = booking.StartDate
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<OwnerReportReferenceOptionDto>> SearchReviewsAsync(
            int ownerId,
            string? search,
            int take = 10,
            int skip = 0,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(ownerId);

            var activePlaceId = await ResolveActivePlaceIdAsync(ownerId, cancellationToken);
            if (!activePlaceId.HasValue)
                return Array.Empty<OwnerReportReferenceOptionDto>();

            var normalizedSearch = NormalizeOptionalText(search, 100);
            var normalizedTake = Math.Clamp(take, 1, 30);
            var normalizedSkip = Math.Max(0, skip);

            var query = _db.Reviews
                .AsNoTracking()
                .Where(review =>
                    review.PlaceId == activePlaceId.Value &&
                    review.Place.OwnerId == ownerId &&
                    review.User.Role == "Customer");

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(review =>
                    review.ReviewId.ToString().Contains(normalizedSearch) ||
                    review.User.FullName.Contains(normalizedSearch) ||
                    review.Place.PlaceNameAndDesc.Contains(normalizedSearch) ||
                    (review.ReviewText != null && review.ReviewText.Contains(normalizedSearch)));
            }

            return await query
                .OrderByDescending(review => review.CreatedAt)
                .ThenByDescending(review => review.ReviewId)
                .Skip(normalizedSkip)
                .Take(normalizedTake)
                .Select(review => new OwnerReportReferenceOptionDto
                {
                    ReferenceType = OwnerReportValues.ReferenceReview,
                    ReferenceId = review.ReviewId,
                    Title = $"التقييم #{review.ReviewId}",
                    Subtitle = review.ReviewText,
                    PlaceId = review.PlaceId,
                    PlaceName = review.Place.PlaceNameAndDesc,
                    CustomerId = review.UserId,
                    CustomerName = review.User.FullName,
                    CustomerWarningCount = review.User.WarningCount,
                    Date = review.CreatedAt
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<OwnerReportDetailsDto?> GetDetailsAsync(
            int ownerId,
            int reportId,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(ownerId);
            if (reportId <= 0) return null;

            var activePlaceId = await ResolveActivePlaceIdAsync(ownerId, cancellationToken);
            if (!activePlaceId.HasValue) return null;

            var reportQuery = _db.Reports
                .AsNoTracking()
                .Include(item => item.Reporter)
                .Include(item => item.AgainstUser)
                .Include(item => item.Place)
                .Include(item => item.Booking)
                    .ThenInclude(booking => booking!.Place)
                .Include(item => item.Booking)
                    .ThenInclude(booking => booking!.Customer)
                .Include(item => item.Review)
                    .ThenInclude(review => review!.Place)
                .Include(item => item.Review)
                    .ThenInclude(review => review!.User)
                .AsSplitQuery()
                .AsQueryable();

            reportQuery = ApplyPlaceScope(reportQuery, activePlaceId.Value);

            var report = await reportQuery
                .FirstOrDefaultAsync(item => item.ReportId == reportId, cancellationToken);

            if (report == null) return null;

            var isMine = report.ReporterId == ownerId && IsRole(report.Reporter, "Owner");
            var hasOwnerCaseMessage = await _db.Chats
                .AsNoTracking()
                .AnyAsync(chat =>
                    chat.ReportId == reportId &&
                    chat.IsReportMessage &&
                    (chat.SenderId == ownerId || chat.ReceiverId == ownerId),
                    cancellationToken);

            var isIncoming =
                !isMine &&
                IsRole(report.Reporter, "Customer") &&
                IsOwnerTarget(report, ownerId) &&
                hasOwnerCaseMessage;

            if (!isMine && !isIncoming)
                return null;

            if (isMine && !IsRole(report.AgainstUser, "Customer"))
                return null;

            var messages = await _db.Chats
                .AsNoTracking()
                .Where(chat =>
                    chat.ReportId == reportId &&
                    chat.IsReportMessage &&
                    (chat.SenderId == ownerId || chat.ReceiverId == ownerId))
                .OrderBy(chat => chat.SendTime)
                .ThenBy(chat => chat.MessageId)
                .Select(chat => new OwnerMessageProjection
                {
                    SenderId = chat.SenderId,
                    SenderRole = chat.Sender.Role,
                    MessageText = chat.MessageText,
                    AttachmentUrl = chat.AttachmentUrl,
                    SendTime = chat.SendTime
                })
                .ToListAsync(cancellationToken);

            var customer = isMine ? report.AgainstUser : report.Reporter;
            var status = report.ReportStatus ?? ReportConstants.Statuses.Pending;
            var needsOwnerResponse =
                isIncoming &&
                status == ReportConstants.Statuses.WaitingForReportedParty &&
                string.Equals(
                    report.RequiredAction,
                    ReportConstants.RequiredActions.ReportedPartyResponse,
                    StringComparison.OrdinalIgnoreCase);

            var deadline = needsOwnerResponse
                ? report.ReportedPartyReplyDeadline
                : null;

            return new OwnerReportDetailsDto
            {
                ReportId = report.ReportId,
                ReportNumber = report.ReportNumber ?? string.Empty,
                Scope = isMine ? OwnerReportValues.ScopeMine : OwnerReportValues.ScopeIncoming,
                Title = string.IsNullOrWhiteSpace(report.Title) ? "قضية" : report.Title.Trim(),
                ReportReason = report.ReportReason ?? string.Empty,
                ReportType = report.ReportType ?? ReportConstants.Types.Other,
                ReportTypeLabel = GetReportTypeLabel(report),
                Category = report.Category,
                CategoryLabel = GetCategoryLabel(report),
                ReportStatus = status,
                StatusLabel = ReportConstants.GetStatusLabel(status),
                StatusTone = ReportConstants.GetStatusTone(status),
                Priority = report.Priority ?? ReportConstants.Priorities.Normal,
                PriorityLabel = GetPriorityLabel(report.Priority),
                EvidenceImage = NormalizePublicPath(report.EvidenceImage),
                Resolution = report.Resolution,
                ResolutionAction = report.ResolutionAction,
                CustomerId = customer?.UserId,
                CustomerName = customer?.FullName,
                CustomerProfileImage = NormalizePublicPath(customer?.ProfileImage),
                CustomerWarningCount = Math.Max(0, customer?.WarningCount ?? 0),
                PlaceId = report.Booking?.PlaceId ?? report.Review?.PlaceId ?? report.PlaceId,
                PlaceName = report.Booking?.Place?.PlaceNameAndDesc ??
                            report.Review?.Place?.PlaceNameAndDesc ??
                            report.Place?.PlaceNameAndDesc,
                PlaceImage = NormalizePublicPath(
                    report.Booking?.Place?.CoverImage ??
                    report.Review?.Place?.CoverImage ??
                    report.Place?.CoverImage),
                BookingId = report.BookingId,
                BookingStartDate = report.Booking?.StartDate,
                BookingEndDate = report.Booking?.EndDate,
                BookingState = report.Booking?.BookingState,
                ReviewId = report.ReviewId,
                ReviewText = report.Review?.ReviewText,
                RequiredAction = report.RequiredAction,
                NeedsOwnerResponse = needsOwnerResponse,
                ReplyDeadline = deadline,
                IsReplyOverdue = deadline.HasValue && deadline.Value < DateTime.Now,
                CanSubmitResponse = needsOwnerResponse,
                CreatedAt = report.CreatedAt ?? report.UpdatedAt,
                UpdatedAt = report.UpdatedAt,
                Timeline = BuildTimeline(report, messages, ownerId, isMine)
            };
        }

        public async Task<OwnerReportOperationResultDto> CreateAsync(
            int ownerId,
            OwnerCreateReportRequestDto request,
            IFormFile? evidenceFile,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(ownerId);
            request ??= new OwnerCreateReportRequestDto();
            NormalizeCreateRequest(request);

            var activePlaceId = await ResolveActivePlaceIdAsync(ownerId, cancellationToken);
            if (!activePlaceId.HasValue)
                return OwnerReportOperationResultDto.Fail(
                    "لا توجد منشأة نشطة مختارة لهذا الحساب.",
                    "active-place-missing");

            var validationError = ValidateCreateRequest(request);
            if (validationError != null)
                return OwnerReportOperationResultDto.Fail(validationError, "validation");

            var reference = await ResolveCreateReferenceAsync(
                ownerId,
                activePlaceId.Value,
                request.ReferenceType,
                request.ReferenceId,
                cancellationToken);

            if (!reference.Success)
                return OwnerReportOperationResultDto.Fail(reference.Message, reference.ErrorCode);

            var upload = await SaveEvidenceAsync(ownerId, evidenceFile, cancellationToken);
            if (!upload.Success)
                return OwnerReportOperationResultDto.Fail(upload.Message, "invalid-evidence");

            var now = DateTime.Now;
            var report = new Report
            {
                ReporterId = ownerId,
                AgainstUserId = reference.CustomerId,
                ReportReason = request.ReportReason.Trim(),
                PlaceId = reference.PlaceId,
                EvidenceImage = upload.RelativePath,
                ReportStatus = ReportConstants.Statuses.Pending,
                AdminComment = null,
                CreatedAt = now,
                ReportType = request.ReportType,
                ReviewId = reference.ReviewId,
                WithdrawnAt = null,
                WithdrawnReason = null,
                OwnerReply = null,
                OwnerReplyDate = null,
                EscalatedAt = null,
                WarningIssuedAt = null,
                ReportNumber = GenerateOwnerReportNumber(),
                RequestKind = ReportConstants.RequestKinds.Report,
                Title = request.Title.Trim(),
                Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category,
                Priority = ReportConstants.Priorities.Normal,
                BookingId = reference.BookingId,
                PromotionId = null,
                AdvertisementId = null,
                AssignedAdminUserId = null,
                ReporterReplyDeadline = null,
                ReportedPartyReplyDeadline = null,
                Resolution = null,
                ResolutionAction = null,
                ResolvedAt = null,
                ResolvedByUserId = null,
                ClosedAt = null,
                UpdatedAt = now,
                ReporterReply = null,
                ReporterReplyDate = null,
                OtherReportTypeDescription = request.ReportType == ReportConstants.Types.Other
                    ? NormalizeOptionalText(request.OtherTypeDescription, 50)
                    : null,
                OtherCategoryDescription = string.Equals(
                    request.Category,
                    ReportConstants.Categories.Other,
                    StringComparison.OrdinalIgnoreCase)
                    ? NormalizeOptionalText(request.OtherCategoryDescription, 50)
                    : null,
                RequiredAction = null
            };

            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                _db.Reports.Add(report);
                await _db.SaveChangesAsync(cancellationToken);

                QueueOwnerNotification(
                    ownerId,
                    report.ReportId,
                    "تم استلام بلاغك",
                    $"تم إرسال البلاغ {report.ReportNumber} إلى إدارة المنصة للمراجعة.",
                    NotificationTypes.ReportCreated);

                await QueueAdminNotificationsAsync(
                    report.ReportId,
                    "بلاغ جديد من مالك ضد عميل",
                    $"وصل بلاغ جديد من مالك ضد عميل برقم {report.ReportNumber} ويحتاج مراجعة.",
                    AdminPermissionKeys.Cases.Manage,
                    cancellationToken);

                // لا يوجد أي إشعار للعميل هنا؛ الإدارة هي التي تحقق وتقرر النتيجة.
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return OwnerReportOperationResultDto.Ok(
                    "تم إرسال البلاغ إلى الإدارة بنجاح.",
                    report.ReportId,
                    report.ReportNumber);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                DeleteUploadedFile(upload.AbsolutePath);

                _logger.LogError(
                    exception,
                    "تعذر إنشاء بلاغ المالك {OwnerId} ضد العميل {CustomerId}.",
                    ownerId,
                    reference.CustomerId);

                return OwnerReportOperationResultDto.Fail(
                    "تعذر حفظ البلاغ الآن. حاول مرة أخرى.",
                    "save-failed");
            }
        }

        public async Task<OwnerReportOperationResultDto> SubmitResponseAsync(
            int ownerId,
            int reportId,
            OwnerReportResponseRequestDto request,
            IFormFile? evidenceFile,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(ownerId);
            if (reportId <= 0)
                return OwnerReportOperationResultDto.Fail("رقم القضية غير صحيح.", "invalid-report-id");

            var activePlaceId = await ResolveActivePlaceIdAsync(ownerId, cancellationToken);
            if (!activePlaceId.HasValue)
                return OwnerReportOperationResultDto.Fail(
                    "لا توجد منشأة نشطة مختارة لهذا الحساب.",
                    "active-place-missing");

            request ??= new OwnerReportResponseRequestDto();
            request.Message = request.Message?.Trim() ?? string.Empty;

            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(
                request,
                new ValidationContext(request),
                validationResults,
                validateAllProperties: true))
            {
                return OwnerReportOperationResultDto.Fail(
                    validationResults.FirstOrDefault()?.ErrorMessage ?? "راجع الرد ثم حاول مرة أخرى.",
                    "validation");
            }

            var responseReportQuery = _db.Reports
                .Include(item => item.Reporter)
                .Include(item => item.AgainstUser)
                .Include(item => item.Place)
                .Include(item => item.Booking)
                    .ThenInclude(booking => booking!.Place)
                .Include(item => item.Review)
                    .ThenInclude(review => review!.Place)
                .AsQueryable();

            responseReportQuery = ApplyPlaceScope(responseReportQuery, activePlaceId.Value);

            var report = await responseReportQuery
                .FirstOrDefaultAsync(item => item.ReportId == reportId, cancellationToken);

            if (report == null)
                return OwnerReportOperationResultDto.Fail("القضية غير موجودة.", "not-found");

            if (!IsRole(report.Reporter, "Customer") || !IsOwnerTarget(report, ownerId))
                return OwnerReportOperationResultDto.Fail("لا تملك صلاحية الرد على هذه القضية.", "forbidden");

            var wasInvited = await _db.Chats
                .AsNoTracking()
                .AnyAsync(chat =>
                    chat.ReportId == reportId &&
                    chat.IsReportMessage &&
                    chat.ReceiverId == ownerId &&
                    chat.Sender.Role == "Admin",
                    cancellationToken);

            if (!wasInvited)
                return OwnerReportOperationResultDto.Fail("لم تطلب الإدارة ردك على هذه القضية.", "response-not-requested");

            if (report.ReportStatus != ReportConstants.Statuses.WaitingForReportedParty ||
                !string.Equals(
                    report.RequiredAction,
                    ReportConstants.RequiredActions.ReportedPartyResponse,
                    StringComparison.OrdinalIgnoreCase))
            {
                return OwnerReportOperationResultDto.Fail(
                    "لا يوجد رد مطلوب منك في هذه القضية حاليًا.",
                    "response-not-requested");
            }

            var adminId = await ResolveAdminReceiverForCaseAsync(
                ownerId,
                reportId,
                cancellationToken);

            if (!adminId.HasValue)
                return OwnerReportOperationResultDto.Fail(
                    "تعذر الوصول إلى حساب إداري لاستقبال الرد.",
                    "admin-unavailable");

            var upload = await SaveEvidenceAsync(ownerId, evidenceFile, cancellationToken);
            if (!upload.Success)
                return OwnerReportOperationResultDto.Fail(upload.Message, "invalid-evidence");

            var now = DateTime.Now;
            var chat = new Chat
            {
                SenderId = ownerId,
                ReceiverId = adminId.Value,
                MessageText = request.Message,
                SendTime = now,
                IsRead = false,
                AttachmentUrl = upload.RelativePath,
                // رسائل القضايا تعتمد على ReportID فقط؛ لا نربطها بـ PlaceID حتى لا نصطدم بقيود الشات العادي.
                PlaceId = null,
                IsDeletedForSender = false,
                IsDeletedForReceiver = false,
                IsEdited = false,
                ReportId = report.ReportId,
                IsReportMessage = true
            };

            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                _db.Chats.Add(chat);

                // إبقاء حقول الرد القديمة متزامنة لدعم تفاصيل الإدارة الحالية.
                report.OwnerReply = request.Message;
                report.OwnerReplyDate = now;
                report.ReportStatus = ReportConstants.Statuses.InReview;
                report.RequiredAction = null;
                report.ReportedPartyReplyDeadline = null;
                report.ReporterReplyDeadline = null;
                report.UpdatedAt = now;

                await QueueAdminNotificationsAsync(
                    report.ReportId,
                    "وصل رد المالك",
                    $"أرسل المالك ردًا جديدًا على القضية {report.ReportNumber}.",
                    AdminPermissionKeys.Cases.Manage,
                    cancellationToken);

                // لا نرسل للعميل شيئًا من هنا؛ الإدارة تراجع الرد أولًا.
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return OwnerReportOperationResultDto.Ok(
                    "تم إرسال ردك إلى الإدارة داخل نفس القضية.",
                    report.ReportId,
                    report.ReportNumber);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                DeleteUploadedFile(upload.AbsolutePath);

                _logger.LogError(
                    exception,
                    "تعذر حفظ رد المالك {OwnerId} على القضية {ReportId}.",
                    ownerId,
                    reportId);

                return OwnerReportOperationResultDto.Fail(
                    "تعذر حفظ الرد الآن. حاول مرة أخرى.",
                    "save-failed");
            }
        }

        /* =====================================================================
           Queries / visibility
           ===================================================================== */

        private IQueryable<Report> BuildIncomingQuery(int ownerId, int activePlaceId)
        {
            var query = _db.Reports.Where(report =>
                report.Reporter.Role == "Customer" &&
                (
                    report.AgainstUserId == ownerId ||
                    (report.Place != null && report.Place.OwnerId == ownerId) ||
                    (report.Booking != null && report.Booking.Place.OwnerId == ownerId) ||
                    (report.Review != null && report.Review.Place.OwnerId == ownerId)
                ) &&
                report.Chats.Any(chat =>
                    chat.IsReportMessage &&
                    (chat.SenderId == ownerId || chat.ReceiverId == ownerId)));

            return ApplyPlaceScope(query, activePlaceId);
        }

        private IQueryable<Report> BuildMineQuery(int ownerId, int activePlaceId)
        {
            var query = _db.Reports.Where(report =>
                report.ReporterId == ownerId &&
                report.Reporter.Role == "Owner" &&
                report.AgainstUser != null &&
                report.AgainstUser.Role == "Customer");

            return ApplyPlaceScope(query, activePlaceId);
        }

        private static IQueryable<Report> ApplyPlaceScope(
            IQueryable<Report> query,
            int activePlaceId)
        {
            return query.Where(report =>
                (report.BookingId.HasValue &&
                 report.Booking != null &&
                 report.Booking.PlaceId == activePlaceId) ||
                (!report.BookingId.HasValue &&
                 report.ReviewId.HasValue &&
                 report.Review != null &&
                 report.Review.PlaceId == activePlaceId) ||
                (!report.BookingId.HasValue &&
                 !report.ReviewId.HasValue &&
                 report.PlaceId == activePlaceId));
        }

        private static IQueryable<Report> ApplyStatusFilter(
            IQueryable<Report> query,
            string statusGroup)
        {
            return statusGroup switch
            {
                OwnerReportValues.StatusNeedsResponse => query.Where(report =>
                    report.ReportStatus == ReportConstants.Statuses.WaitingForReportedParty &&
                    report.RequiredAction == ReportConstants.RequiredActions.ReportedPartyResponse),

                OwnerReportValues.StatusActive => query.Where(report =>
                    report.ReportStatus != ReportConstants.Statuses.Resolved &&
                    report.ReportStatus != ReportConstants.Statuses.Rejected &&
                    report.ReportStatus != ReportConstants.Statuses.Closed &&
                    report.ReportStatus != ReportConstants.Statuses.Withdrawn),

                OwnerReportValues.StatusCompleted => query.Where(report =>
                    report.ReportStatus == ReportConstants.Statuses.Resolved ||
                    report.ReportStatus == ReportConstants.Statuses.Rejected ||
                    report.ReportStatus == ReportConstants.Statuses.Closed ||
                    report.ReportStatus == ReportConstants.Statuses.Withdrawn),

                _ => query
            };
        }

        private static bool IsOwnerTarget(Report report, int ownerId)
        {
            return report.AgainstUserId == ownerId ||
                   report.Place?.OwnerId == ownerId ||
                   report.Booking?.Place?.OwnerId == ownerId ||
                   report.Review?.Place?.OwnerId == ownerId;
        }

        private static OwnerReportListItemDto BuildListItem(
            Report report,
            int ownerId,
            string scope,
            DateTime now)
        {
            var isMine = scope == OwnerReportValues.ScopeMine;
            var customer = isMine ? report.AgainstUser : report.Reporter;
            var status = report.ReportStatus ?? ReportConstants.Statuses.Pending;
            var needsResponse =
                !isMine &&
                status == ReportConstants.Statuses.WaitingForReportedParty &&
                string.Equals(
                    report.RequiredAction,
                    ReportConstants.RequiredActions.ReportedPartyResponse,
                    StringComparison.OrdinalIgnoreCase);

            var deadline = needsResponse ? report.ReportedPartyReplyDeadline : null;

            return new OwnerReportListItemDto
            {
                ReportId = report.ReportId,
                ReportNumber = report.ReportNumber ?? string.Empty,
                Scope = scope,
                Title = string.IsNullOrWhiteSpace(report.Title) ? "قضية" : report.Title.Trim(),
                ReportType = report.ReportType ?? ReportConstants.Types.Other,
                ReportTypeLabel = GetReportTypeLabel(report),
                Category = report.Category,
                CategoryLabel = GetCategoryLabel(report),
                ReportStatus = status,
                StatusLabel = ReportConstants.GetStatusLabel(status),
                StatusTone = ReportConstants.GetStatusTone(status),
                Priority = report.Priority ?? ReportConstants.Priorities.Normal,
                PriorityLabel = GetPriorityLabel(report.Priority),
                NeedsOwnerResponse = needsResponse,
                ReplyDeadline = deadline,
                IsOverdue = deadline.HasValue && deadline.Value < now,
                PlaceId = report.Booking?.PlaceId ?? report.Review?.PlaceId ?? report.PlaceId,
                PlaceName = report.Booking?.Place?.PlaceNameAndDesc ??
                            report.Review?.Place?.PlaceNameAndDesc ??
                            report.Place?.PlaceNameAndDesc,
                BookingId = report.BookingId,
                ReviewId = report.ReviewId,
                ReferenceLabel = BuildReferenceLabel(report),
                CustomerName = customer?.FullName,
                CustomerWarningCount = Math.Max(0, customer?.WarningCount ?? 0),
                CreatedAt = report.CreatedAt ?? report.UpdatedAt,
                UpdatedAt = report.UpdatedAt,
                IsCompleted = ReportConstants.Statuses.IsCompleted(status)
            };
        }

        private static IReadOnlyList<OwnerReportTimelineItemDto> BuildTimeline(
            Report report,
            IReadOnlyList<OwnerMessageProjection> messages,
            int ownerId,
            bool isMine)
        {
            var timeline = new List<OwnerReportTimelineItemDto>();

            if (isMine)
            {
                timeline.Add(new OwnerReportTimelineItemDto
                {
                    Date = report.CreatedAt ?? report.UpdatedAt,
                    Title = "أرسلت البلاغ إلى الإدارة",
                    Description = "تم إنشاء القضية وإرسالها إلى إدارة المنصة للمراجعة.",
                    Actor = "أنت",
                    Icon = "bi-send-check-fill",
                    Tone = "owner",
                    AttachmentUrl = NormalizePublicPath(report.EvidenceImage),
                    IsOwnerMessage = true
                });
            }

            foreach (var message in messages)
            {
                var isOwner = message.SenderId == ownerId;
                var isAdmin = string.Equals(message.SenderRole, "Admin", StringComparison.OrdinalIgnoreCase);

                timeline.Add(new OwnerReportTimelineItemDto
                {
                    Date = message.SendTime ?? report.UpdatedAt,
                    Title = isOwner ? "أرسلت ردًا للإدارة" : "رسالة من الإدارة",
                    Description = message.MessageText,
                    Actor = isOwner ? "أنت" : isAdmin ? "الإدارة" : "النظام",
                    Icon = isOwner ? "bi-chat-right-text-fill" : "bi-shield-check",
                    Tone = isOwner ? "owner" : "admin",
                    AttachmentUrl = NormalizePublicPath(message.AttachmentUrl),
                    IsOwnerMessage = isOwner,
                    IsAdminMessage = isAdmin
                });
            }

            if (report.ResolvedAt.HasValue && !string.IsNullOrWhiteSpace(report.Resolution))
            {
                timeline.Add(new OwnerReportTimelineItemDto
                {
                    Date = report.ResolvedAt.Value,
                    Title = "سجلت الإدارة نتيجة القضية",
                    Description = report.Resolution,
                    Actor = "الإدارة",
                    Icon = "bi-check-circle-fill",
                    Tone = "success",
                    IsAdminMessage = true
                });
            }
            else if (report.ReportStatus == ReportConstants.Statuses.Rejected &&
                     !string.IsNullOrWhiteSpace(report.Resolution))
            {
                timeline.Add(new OwnerReportTimelineItemDto
                {
                    Date = report.UpdatedAt,
                    Title = "تم تسجيل نتيجة القضية",
                    Description = report.Resolution,
                    Actor = "الإدارة",
                    Icon = "bi-x-circle-fill",
                    Tone = "muted",
                    IsAdminMessage = true
                });
            }

            if (report.ClosedAt.HasValue)
            {
                timeline.Add(new OwnerReportTimelineItemDto
                {
                    Date = report.ClosedAt.Value,
                    Title = "تم إغلاق القضية",
                    Description = "أغلقت الإدارة ملف القضية بعد اكتمال المعالجة.",
                    Actor = "الإدارة",
                    Icon = "bi-archive-fill",
                    Tone = "success",
                    IsAdminMessage = true
                });
            }

            return timeline
                .OrderBy(item => item.Date)
                .ToList();
        }

        /* =====================================================================
           Create reference validation
           ===================================================================== */

        private async Task<CreateReferenceResult> ResolveCreateReferenceAsync(
            int ownerId,
            int activePlaceId,
            string referenceType,
            int referenceId,
            CancellationToken cancellationToken)
        {
            if (string.Equals(
                referenceType,
                OwnerReportValues.ReferenceReview,
                StringComparison.OrdinalIgnoreCase))
            {
                var review = await _db.Reviews
                    .AsNoTracking()
                    .Include(item => item.User)
                    .Include(item => item.Place)
                    .FirstOrDefaultAsync(item =>
                        item.ReviewId == referenceId &&
                        item.PlaceId == activePlaceId &&
                        item.Place.OwnerId == ownerId,
                        cancellationToken);

                if (review == null)
                    return CreateReferenceResult.Fail("التقييم غير موجود أو لا يتبع إحدى منشآتك.", "invalid-reference");

                if (!IsRole(review.User, "Customer"))
                    return CreateReferenceResult.Fail("لا يمكن إنشاء بلاغ عميل من هذا التقييم.", "invalid-customer");

                return CreateReferenceResult.Ok(
                    review.UserId,
                    review.PlaceId,
                    bookingId: null,
                    reviewId: review.ReviewId);
            }

            var booking = await _db.Bookings
                .AsNoTracking()
                .Include(item => item.Customer)
                .Include(item => item.Place)
                .FirstOrDefaultAsync(item =>
                    item.BookingId == referenceId &&
                    item.PlaceId == activePlaceId &&
                    item.Place.OwnerId == ownerId,
                    cancellationToken);

            if (booking == null)
                return CreateReferenceResult.Fail("الحجز غير موجود أو لا يتبع إحدى منشآتك.", "invalid-reference");

            if (!IsRole(booking.Customer, "Customer"))
                return CreateReferenceResult.Fail("لا يمكن إنشاء بلاغ عميل من هذا الحجز.", "invalid-customer");

            return CreateReferenceResult.Ok(
                booking.Customer.UserId,
                booking.PlaceId,
                booking.BookingId,
                reviewId: null);
        }

        private async Task<OwnerReportReferenceOptionDto?> ResolveBookingReferenceForDisplayAsync(
            int ownerId,
            int activePlaceId,
            int bookingId,
            CancellationToken cancellationToken)
        {
            return await _db.Bookings
                .AsNoTracking()
                .Where(booking =>
                    booking.BookingId == bookingId &&
                    booking.PlaceId == activePlaceId &&
                    booking.Place.OwnerId == ownerId &&
                    booking.Customer.Role == "Customer")
                .Select(booking => new OwnerReportReferenceOptionDto
                {
                    ReferenceType = OwnerReportValues.ReferenceBooking,
                    ReferenceId = booking.BookingId,
                    Title = $"الحجز #{booking.BookingId}",
                    Subtitle = booking.BookingState,
                    PlaceId = booking.PlaceId,
                    PlaceName = booking.Place.PlaceNameAndDesc,
                    CustomerId = booking.Customer.UserId,
                    CustomerName = booking.Customer.FullName,
                    CustomerWarningCount = booking.Customer.WarningCount,
                    Date = booking.StartDate
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<OwnerReportReferenceOptionDto?> ResolveReviewReferenceForDisplayAsync(
            int ownerId,
            int activePlaceId,
            int reviewId,
            CancellationToken cancellationToken)
        {
            return await _db.Reviews
                .AsNoTracking()
                .Where(review =>
                    review.ReviewId == reviewId &&
                    review.PlaceId == activePlaceId &&
                    review.Place.OwnerId == ownerId &&
                    review.User.Role == "Customer")
                .Select(review => new OwnerReportReferenceOptionDto
                {
                    ReferenceType = OwnerReportValues.ReferenceReview,
                    ReferenceId = review.ReviewId,
                    Title = $"التقييم #{review.ReviewId}",
                    Subtitle = review.ReviewText,
                    PlaceId = review.PlaceId,
                    PlaceName = review.Place.PlaceNameAndDesc,
                    CustomerId = review.UserId,
                    CustomerName = review.User.FullName,
                    CustomerWarningCount = review.User.WarningCount,
                    Date = review.CreatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<int?> ResolveActivePlaceIdAsync(
            int ownerId,
            CancellationToken cancellationToken)
        {
            var cookieName = $"ownerActivePlace_{ownerId}";
            var cookieValue = _httpContextAccessor.HttpContext?.Request.Cookies[cookieName];

            _ = int.TryParse(cookieValue, out var requestedPlaceId);

            return await _db.Places
                .AsNoTracking()
                .Where(place => place.OwnerId == ownerId)
                .OrderByDescending(place =>
                    requestedPlaceId > 0 &&
                    place.PlaceId == requestedPlaceId)
                .ThenByDescending(place =>
                    place.Status == "Approved" ||
                    place.Status == "Active")
                .ThenBy(place => place.PlaceNameAndDesc)
                .Select(place => (int?)place.PlaceId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        /* =====================================================================
           Notifications / admin receiver
           ===================================================================== */

        private async Task<int?> ResolveAdminReceiverForCaseAsync(
            int ownerId,
            int reportId,
            CancellationToken cancellationToken)
        {
            var lastRequesterId = await _db.Chats
                .AsNoTracking()
                .Where(chat =>
                    chat.ReportId == reportId &&
                    chat.IsReportMessage &&
                    chat.ReceiverId == ownerId &&
                    chat.Sender.Role == "Admin")
                .OrderByDescending(chat => chat.SendTime)
                .ThenByDescending(chat => chat.MessageId)
                .Select(chat => (int?)chat.SenderId)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastRequesterId.HasValue)
                return lastRequesterId;

            return await _db.Users
                .AsNoTracking()
                .Where(user =>
                    user.Role == "Admin" &&
                    (user.AccountState == null || user.AccountState == "Active"))
                .OrderBy(user => user.UserId)
                .Select(user => (int?)user.UserId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private Task<int> QueueAdminNotificationsAsync(
            int reportId,
            string title,
            string text,
            string permissionKey,
            CancellationToken cancellationToken)
        {
            return _notifications.QueueForAdminPermissionAsync(
                permissionKey,
                new CreateNotificationDto
                {
                    NotificationTitle = title,
                    NotificationText = text,
                    NotificationType = NotificationTypes.AdminCaseActionRequired,
                    TargetUrl = $"/Admin/Reports/Details/{reportId}",
                    ReferenceType = NotificationReferenceTypes.Report,
                    ReferenceId = reportId
                },
                cancellationToken);
        }

        private void QueueOwnerNotification(
            int ownerId,
            int reportId,
            string title,
            string text,
            string notificationType)
        {
            _notifications.Queue(new CreateNotificationDto
            {
                UserId = ownerId,
                NotificationTitle = title,
                NotificationText = text,
                NotificationType = notificationType,
                TargetUrl = $"/Owner/Reports/OwnerReportDetails/{reportId}",
                ReferenceType = NotificationReferenceTypes.Report,
                ReferenceId = reportId
            });
        }

        /* =====================================================================
           Upload security
           ===================================================================== */

        private async Task<UploadResult> SaveEvidenceAsync(
            int ownerId,
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            if (file == null || file.Length <= 0)
                return UploadResult.Ok();

            if (file.Length > ReportConstants.Limits.MaxEvidenceBytes)
                return UploadResult.Fail("حجم المرفق يجب ألا يتجاوز 5 ميجابايت.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedEvidenceExtensions.Contains(extension))
                return UploadResult.Fail("صيغة المرفق غير مدعومة. استخدم JPG أو PNG أو WEBP أو PDF.");

            var contentType = (file.ContentType ?? string.Empty).Trim().ToLowerInvariant();
            if (!AllowedEvidenceContentTypes.Contains(contentType))
                return UploadResult.Fail("نوع المرفق غير مدعوم.");

            if (!IsExtensionCompatibleWithContentType(extension, contentType))
                return UploadResult.Fail("امتداد المرفق لا يطابق نوع الملف المرسل.");

            if (!await HasValidFileSignatureAsync(file, extension, cancellationToken))
                return UploadResult.Fail("محتوى المرفق لا يطابق الصيغة المسموح بها.");

            var webRoot = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
                webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");

            var relativeFolder = Path.Combine("uploads", "evidence", "owner-reports");
            var absoluteFolder = Path.Combine(webRoot, relativeFolder);
            Directory.CreateDirectory(absoluteFolder);

            var safeFileName =
                $"owner-{ownerId}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}{extension}";
            var absolutePath = Path.Combine(absoluteFolder, safeFileName);

            try
            {
                await using var stream = new FileStream(
                    absolutePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);

                await file.CopyToAsync(stream, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "تعذر حفظ مرفق بلاغ المالك {OwnerId}.", ownerId);
                return UploadResult.Fail("تعذر حفظ المرفق في الخادم.");
            }

            var relativePath = "/" +
                Path.Combine(relativeFolder, safeFileName).Replace("\\", "/");

            return UploadResult.Ok(relativePath, absolutePath);
        }

        private static async Task<bool> HasValidFileSignatureAsync(
            IFormFile file,
            string extension,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[12];
            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);

            if (read < 4) return false;

            if (extension == ".jpg" || extension == ".jpeg")
                return buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF;

            if (extension == ".png")
                return read >= 8 &&
                       buffer[0] == 0x89 && buffer[1] == 0x50 &&
                       buffer[2] == 0x4E && buffer[3] == 0x47 &&
                       buffer[4] == 0x0D && buffer[5] == 0x0A &&
                       buffer[6] == 0x1A && buffer[7] == 0x0A;

            if (extension == ".pdf")
                return buffer[0] == 0x25 && buffer[1] == 0x50 &&
                       buffer[2] == 0x44 && buffer[3] == 0x46;

            if (extension == ".webp")
                return read >= 12 &&
                       buffer[0] == 0x52 && buffer[1] == 0x49 &&
                       buffer[2] == 0x46 && buffer[3] == 0x46 &&
                       buffer[8] == 0x57 && buffer[9] == 0x45 &&
                       buffer[10] == 0x42 && buffer[11] == 0x50;

            return false;
        }

        private static bool IsExtensionCompatibleWithContentType(string extension, string contentType)
        {
            return extension switch
            {
                ".jpg" or ".jpeg" => contentType == "image/jpeg",
                ".png" => contentType == "image/png",
                ".webp" => contentType == "image/webp",
                ".pdf" => contentType == "application/pdf",
                _ => false
            };
        }

        private static void DeleteUploadedFile(string? absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
                return;

            try { File.Delete(absolutePath); }
            catch { /* فشل تنظيف الملف لا يجب أن يكسر العملية الأساسية. */ }
        }

        /* =====================================================================
           Validation / labels
           ===================================================================== */

        private static void NormalizeCreateRequest(OwnerCreateReportRequestDto request)
        {
            request.ReferenceType = string.Equals(
                request.ReferenceType,
                OwnerReportValues.ReferenceReview,
                StringComparison.OrdinalIgnoreCase)
                ? OwnerReportValues.ReferenceReview
                : OwnerReportValues.ReferenceBooking;

            var allowedTypes = new[]
            {
                ReportConstants.Types.Booking,
                ReportConstants.Types.Review,
                ReportConstants.Types.Other
            };

            request.ReportType = allowedTypes.FirstOrDefault(type =>
                string.Equals(type, request.ReportType, StringComparison.OrdinalIgnoreCase))
                ?? ReportConstants.Types.Other;

            request.OtherTypeDescription = NormalizeOptionalText(request.OtherTypeDescription, 50);
            request.Category = string.IsNullOrWhiteSpace(request.Category)
                ? null
                : ReportConstants.NormalizeCategory(request.Category);
            request.OtherCategoryDescription = NormalizeOptionalText(request.OtherCategoryDescription, 50);
            request.Title = request.Title?.Trim() ?? string.Empty;
            request.ReportReason = request.ReportReason?.Trim() ?? string.Empty;
        }

        private static string? ValidateCreateRequest(OwnerCreateReportRequestDto request)
        {
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(
                request,
                new ValidationContext(request),
                results,
                validateAllProperties: true))
            {
                return results.FirstOrDefault()?.ErrorMessage ?? "راجع بيانات البلاغ.";
            }

            if (request.ReportType == ReportConstants.Types.Booking &&
                request.ReferenceType != OwnerReportValues.ReferenceBooking)
                return "بلاغ الحجز يجب أن يرتبط بحجز من حجوزات منشآتك.";

            if (request.ReportType == ReportConstants.Types.Review &&
                request.ReferenceType != OwnerReportValues.ReferenceReview)
                return "بلاغ التقييم يجب أن يرتبط بتقييم على إحدى منشآتك.";

            if (request.ReportType == ReportConstants.Types.Other &&
                string.IsNullOrWhiteSpace(request.OtherTypeDescription))
                return "اكتب وصفًا مختصرًا لنوع البلاغ الآخر.";

            if (string.Equals(
                request.Category,
                ReportConstants.Categories.Other,
                StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(request.OtherCategoryDescription))
                return "اكتب وصفًا مختصرًا للتصنيف الآخر.";

            return null;
        }

        private static IReadOnlyList<OwnerReportOptionDto> BuildReportTypeOptions()
        {
            return new List<OwnerReportOptionDto>
            {
                Option(ReportConstants.Types.Booking, "حجز", "مخالفة مرتبطة بحجز عميل لدى منشأتك.", "bi-calendar2-check-fill"),
                Option(ReportConstants.Types.Review, "تقييم", "تقييم أو محتوى عميل يحتاج مراجعة الإدارة.", "bi-star-fill"),
                Option(ReportConstants.Types.Other, "أخرى", "مخالفة أخرى مرتبطة بحجز أو تقييم يحدد هوية العميل بشكل آمن.", "bi-three-dots")
            };
        }

        private static IReadOnlyList<OwnerReportOptionDto> BuildCategoryOptions()
        {
            return new List<OwnerReportOptionDto>
            {
                Option(ReportConstants.Categories.BookingIssue, "مشكلة في الحجز", null, "bi-calendar-x"),
                Option(ReportConstants.Categories.PaymentIssue, "مشكلة في الدفع", null, "bi-credit-card"),
                Option(ReportConstants.Categories.ServiceIssue, "مشكلة في التعامل", null, "bi-person-exclamation"),
                Option(ReportConstants.Categories.FraudOrScam, "اشتباه احتيال", null, "bi-shield-exclamation"),
                Option(ReportConstants.Categories.AbusiveContent, "إساءة أو محتوى غير لائق", null, "bi-chat-square-dots"),
                Option(ReportConstants.Categories.FakeContent, "محتوى أو تقييم غير حقيقي", null, "bi-eye-slash"),
                Option(ReportConstants.Categories.Other, "أخرى", null, "bi-three-dots")
            };
        }

        private static OwnerReportOptionDto Option(
            string value,
            string label,
            string? description,
            string icon)
        {
            return new OwnerReportOptionDto
            {
                Value = value,
                Label = label,
                Description = description,
                Icon = icon
            };
        }

        private static string GetReportTypeLabel(Report report)
        {
            if (report.ReportType == ReportConstants.Types.Other &&
                !string.IsNullOrWhiteSpace(report.OtherReportTypeDescription))
                return report.OtherReportTypeDescription.Trim();

            return ReportConstants.GetTypeLabel(report.ReportType);
        }

        private static string GetCategoryLabel(Report report)
        {
            if (string.Equals(
                report.Category,
                ReportConstants.Categories.Other,
                StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(report.OtherCategoryDescription))
                return report.OtherCategoryDescription.Trim();

            return ReportConstants.GetCategoryLabel(report.Category);
        }

        private static string GetPriorityLabel(string? priority)
        {
            if (string.Equals(priority, ReportConstants.Priorities.Low, StringComparison.OrdinalIgnoreCase)) return "منخفضة";
            if (string.Equals(priority, ReportConstants.Priorities.High, StringComparison.OrdinalIgnoreCase)) return "عالية";
            if (string.Equals(priority, ReportConstants.Priorities.Urgent, StringComparison.OrdinalIgnoreCase)) return "عاجلة";
            return "عادية";
        }

        private static string? BuildReferenceLabel(Report report)
        {
            if (report.BookingId.HasValue) return $"الحجز #{report.BookingId.Value}";
            if (report.ReviewId.HasValue) return $"التقييم #{report.ReviewId.Value}";
            if (report.PlaceId.HasValue) return report.Place?.PlaceNameAndDesc ?? $"المنشأة #{report.PlaceId.Value}";
            return null;
        }

        private static string GenerateOwnerReportNumber()
        {
            var token = Guid.NewGuid()
                .ToString("N")
                .Substring(0, 10)
                .ToUpperInvariant();

            return $"ORP-{DateTime.Now:yyyyMMdd}-{token}";
        }

        private static string? NormalizeOptionalText(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed.Substring(0, maxLength);
        }

        private static string? NormalizePublicPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var normalized = path.Trim().Replace('\\', '/');

            if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("/", StringComparison.Ordinal))
                return normalized;

            return "/" + normalized.TrimStart('/');
        }

        private static int NormalizePageSize(int pageSize)
        {
            if (pageSize <= 0) return 12;
            return Math.Min(pageSize, 50);
        }

        private static void EnsurePositiveUserId(int userId)
        {
            if (userId <= 0)
                throw new ArgumentOutOfRangeException(nameof(userId));
        }

        private static bool IsRole(User? user, string role)
        {
            return user != null &&
                   string.Equals(user.Role, role, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class OwnerMessageProjection
        {
            public int SenderId { get; set; }
            public string? SenderRole { get; set; }
            public string? MessageText { get; set; }
            public string? AttachmentUrl { get; set; }
            public DateTime? SendTime { get; set; }
        }

        private sealed class CreateReferenceResult
        {
            public bool Success { get; private set; }
            public string Message { get; private set; } = string.Empty;
            public string? ErrorCode { get; private set; }
            public int CustomerId { get; private set; }
            public int PlaceId { get; private set; }
            public int? BookingId { get; private set; }
            public int? ReviewId { get; private set; }

            public static CreateReferenceResult Ok(
                int customerId,
                int placeId,
                int? bookingId,
                int? reviewId)
            {
                return new CreateReferenceResult
                {
                    Success = true,
                    CustomerId = customerId,
                    PlaceId = placeId,
                    BookingId = bookingId,
                    ReviewId = reviewId
                };
            }

            public static CreateReferenceResult Fail(string message, string errorCode)
            {
                return new CreateReferenceResult
                {
                    Success = false,
                    Message = message,
                    ErrorCode = errorCode
                };
            }
        }

        private sealed class UploadResult
        {
            public bool Success { get; private set; }
            public string Message { get; private set; } = string.Empty;
            public string? RelativePath { get; private set; }
            public string? AbsolutePath { get; private set; }

            public static UploadResult Ok(string? relativePath = null, string? absolutePath = null)
            {
                return new UploadResult
                {
                    Success = true,
                    RelativePath = relativePath,
                    AbsolutePath = absolutePath
                };
            }

            public static UploadResult Fail(string message)
            {
                return new UploadResult
                {
                    Success = false,
                    Message = message
                };
            }
        }
    }
}
