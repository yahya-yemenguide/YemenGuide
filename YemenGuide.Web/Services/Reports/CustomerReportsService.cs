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
using YemenGuide.Core.DTOs.Customer;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.Entities;
using YemenGuide.Data;
using YemenGuide.Web.Services.Notifications;

namespace YemenGuide.Web.Services.Reports
{
    /// <summary>
    /// المحرك المركزي لشكاوى وبلاغات العميل.
    ///
    /// القاعدة الأساسية للنظام:
    /// - العميل يرسل القضية إلى الإدارة أولًا.
    /// - لا يوجد تحويل مباشر إلى المالك من هذه الخدمة.
    /// - الإدارة هي التي تقرر لاحقًا هل تطلب رد المالك أم تعالج القضية بنفسها.
    /// - جميع التوضيحات والأدلة اللاحقة تحفظ داخل نفس القضية عبر Chats + ReportID.
    /// - العميل لا يغلق القضية رسميًا؛ الإغلاق النهائي من الإدارة.
    /// </summary>
    public sealed class CustomerReportsService : ICustomerReportsService
    {
        private static readonly string[] AllowedEvidenceExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp",
            ".pdf"
        };

        private static readonly string[] AllowedEvidenceContentTypes =
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "application/pdf"
        };

        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _environment;
        private readonly INotificationService _notifications;
        private readonly ILogger<CustomerReportsService> _logger;

        public CustomerReportsService(
            AppDbContext db,
            IWebHostEnvironment environment,
            INotificationService notifications,
            ILogger<CustomerReportsService> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CustomerReportsPageDto> GetMyReportsAsync(
            int customerId,
            CustomerReportsFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(customerId);

            filter ??= new CustomerReportsFilterDto();

            var normalizedFilter = new CustomerReportsFilterDto
            {
                StatusGroup = ReportConstants.NormalizeStatusGroup(filter.StatusGroup),
                Search = NormalizeOptionalText(filter.Search, 150),
                Page = filter.Page <= 0 ? 1 : filter.Page,
                PageSize = NormalizePageSize(filter.PageSize)
            };

            var ownedQuery = _db.Reports
                .AsNoTracking()
                .Where(report => report.ReporterId == customerId);

            var totalCount = await ownedQuery.CountAsync(cancellationToken);

            var needsResponseCount = await ownedQuery
                .CountAsync(
                    report => report.ReportStatus == ReportConstants.Statuses.WaitingForReporter,
                    cancellationToken);

            var completedCount = await ownedQuery
                .CountAsync(
                    report =>
                        report.ReportStatus == ReportConstants.Statuses.Resolved ||
                        report.ReportStatus == ReportConstants.Statuses.Rejected ||
                        report.ReportStatus == ReportConstants.Statuses.Closed ||
                        report.ReportStatus == ReportConstants.Statuses.Withdrawn,
                    cancellationToken);

            var query = ownedQuery;

            if (normalizedFilter.StatusGroup == ReportConstants.StatusGroups.NeedsResponse)
            {
                query = query.Where(report =>
                    report.ReportStatus == ReportConstants.Statuses.WaitingForReporter);
            }
            else if (normalizedFilter.StatusGroup == ReportConstants.StatusGroups.Active)
            {
                query = query.Where(report =>
                    report.ReportStatus != ReportConstants.Statuses.WaitingForReporter &&
                    report.ReportStatus != ReportConstants.Statuses.Resolved &&
                    report.ReportStatus != ReportConstants.Statuses.Rejected &&
                    report.ReportStatus != ReportConstants.Statuses.Closed &&
                    report.ReportStatus != ReportConstants.Statuses.Withdrawn);
            }
            else if (normalizedFilter.StatusGroup == ReportConstants.StatusGroups.Completed)
            {
                query = query.Where(report =>
                    report.ReportStatus == ReportConstants.Statuses.Resolved ||
                    report.ReportStatus == ReportConstants.Statuses.Rejected ||
                    report.ReportStatus == ReportConstants.Statuses.Closed ||
                    report.ReportStatus == ReportConstants.Statuses.Withdrawn);
            }

            if (!string.IsNullOrWhiteSpace(normalizedFilter.Search))
            {
                var search = normalizedFilter.Search;

                query = query.Where(report =>
                    report.ReportNumber.Contains(search) ||
                    (report.Title != null && report.Title.Contains(search)) ||
                    report.ReportReason.Contains(search) ||
                    (report.OtherReportTypeDescription != null &&
                     report.OtherReportTypeDescription.Contains(search)) ||
                    (report.OtherCategoryDescription != null &&
                     report.OtherCategoryDescription.Contains(search)) ||
                    (report.Place != null && report.Place.PlaceNameAndDesc.Contains(search)));
            }

            var filteredCount = await query.CountAsync(cancellationToken);

            var totalPages = filteredCount == 0
                ? 0
                : (int)Math.Ceiling(filteredCount / (double)normalizedFilter.PageSize);

            if (totalPages > 0 && normalizedFilter.Page > totalPages)
            {
                normalizedFilter.Page = totalPages;
            }

            var skip = (normalizedFilter.Page - 1) * normalizedFilter.PageSize;

            var rows = await query
                .OrderByDescending(report => report.UpdatedAt)
                .ThenByDescending(report => report.CreatedAt)
                .Skip(skip)
                .Take(normalizedFilter.PageSize)
                .Select(report => new ListProjection
                {
                    ReportId = report.ReportId,
                    ReportNumber = report.ReportNumber,
                    RequestKind = report.RequestKind,
                    ReportType = report.ReportType,
                    OtherReportTypeDescription = report.OtherReportTypeDescription,
                    Category = report.Category,
                    OtherCategoryDescription = report.OtherCategoryDescription,
                    RequiredAction = report.RequiredAction,
                    ResponseContentType = report.ResponseContentType,
                    Title = report.Title,
                    ReportStatus = report.ReportStatus,
                    Priority = report.Priority,
                    PlaceId = report.PlaceId,
                    PlaceName = report.Place != null
                        ? report.Place.PlaceNameAndDesc
                        : null,
                    BookingId = report.BookingId,
                    ReviewId = report.ReviewId,
                    PromotionId = report.PromotionId,
                    AdvertisementId = report.AdvertisementId,
                    CreatedAt = report.CreatedAt,
                    UpdatedAt = report.UpdatedAt,
                    ReporterReplyDeadline = report.ReporterReplyDeadline
                })
                .ToListAsync(cancellationToken);

            var items = rows
                .Select(BuildListItem)
                .ToList();

            return new CustomerReportsPageDto
            {
                Filter = normalizedFilter,
                Summary = new CustomerReportSummaryDto
                {
                    TotalCount = totalCount,
                    NeedsResponseCount = needsResponseCount,
                    ActiveCount = Math.Max(0, totalCount - completedCount - needsResponseCount),
                    CompletedCount = completedCount
                },
                Items = items,
                FilteredCount = filteredCount,
                CurrentPage = normalizedFilter.Page,
                PageSize = normalizedFilter.PageSize,
                TotalPages = totalPages
            };
        }

        public async Task<CustomerCreateReportPageDto> GetCreatePageAsync(
            int customerId,
            CustomerCreateReportRequestDto? form = null,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(customerId);

            form ??= new CustomerCreateReportRequestDto();

            var bookingRows = await _db.Bookings
                .AsNoTracking()
                .Where(booking => booking.CustomerId == customerId)
                .OrderByDescending(booking => booking.CreatedAt)
                .ThenByDescending(booking => booking.BookingId)
                .Take(ReportConstants.Limits.RecentBookingsTake)
                .Select(booking => new BookingReferenceProjection
                {
                    BookingId = booking.BookingId,
                    PlaceId = booking.PlaceId,
                    PlaceName = booking.Place.PlaceNameAndDesc,
                    StartDate = booking.StartDate,
                    EndDate = booking.EndDate,
                    BookingState = booking.BookingState
                })
                .ToListAsync(cancellationToken);

            var recentBookings = bookingRows
                .Select(row => new CustomerReportReferenceOptionDto
                {
                    ReferenceType = ReportConstants.ReferenceTypes.Booking,
                    ReferenceId = row.BookingId,
                    Title = $"الحجز #{row.BookingId}",
                    Subtitle = BuildBookingSubtitle(row.BookingState),
                    PlaceId = row.PlaceId,
                    PlaceName = CompactPlaceName(row.PlaceName),
                    Date = row.StartDate
                })
                .ToList();

            var relatedPlaces = bookingRows
                .GroupBy(row => row.PlaceId)
                .Select(group => group.First())
                .Select(row => new CustomerReportReferenceOptionDto
                {
                    ReferenceType = ReportConstants.ReferenceTypes.Place,
                    ReferenceId = row.PlaceId,
                    Title = CompactPlaceName(row.PlaceName),
                    Subtitle = "منشأة مرتبطة بأحد حجوزاتك",
                    PlaceId = row.PlaceId,
                    PlaceName = CompactPlaceName(row.PlaceName)
                })
                .OrderBy(item => item.Title)
                .ToList();

            var selectedReference = await ResolveSelectedReferenceForDisplayAsync(
                customerId,
                form,
                cancellationToken);

            if (selectedReference != null &&
                selectedReference.ReferenceType == ReportConstants.ReferenceTypes.Place &&
                relatedPlaces.All(item => item.ReferenceId != selectedReference.ReferenceId))
            {
                relatedPlaces.Insert(0, selectedReference);
            }

            return new CustomerCreateReportPageDto
            {
                Form = form,
                RequestKinds = BuildRequestKindOptions(),
                ReportTypes = BuildReportTypeOptions(),
                Categories = BuildCategoryOptions(),
                RecentBookings = recentBookings,
                RelatedPlaces = relatedPlaces,
                SelectedReference = selectedReference
            };
        }

        public async Task<IReadOnlyList<CustomerReportReferenceOptionDto>> SearchBookingsAsync(
            int customerId,
            string? search,
            int take = 10,
            int skip = 0,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(customerId);

            var normalizedSearch = (search ?? string.Empty).Trim();
            var normalizedTake = Math.Clamp(take, 1, 20);
            var normalizedSkip = Math.Max(0, skip);

            var query = _db.Bookings
                .AsNoTracking()
                .Where(booking => booking.CustomerId == customerId);

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                if (int.TryParse(normalizedSearch.TrimStart('#'), out var bookingNumber) &&
                    bookingNumber > 0)
                {
                    query = query.Where(booking =>
                        booking.BookingId == bookingNumber ||
                        booking.Place.PlaceNameAndDesc.Contains(normalizedSearch));
                }
                else
                {
                    query = query.Where(booking =>
                        booking.Place.PlaceNameAndDesc.Contains(normalizedSearch));
                }
            }

            var rows = await query
                .OrderByDescending(booking => booking.CreatedAt)
                .ThenByDescending(booking => booking.BookingId)
                .Skip(normalizedSkip)
                .Take(normalizedTake)
                .Select(booking => new BookingReferenceProjection
                {
                    BookingId = booking.BookingId,
                    PlaceId = booking.PlaceId,
                    PlaceName = booking.Place.PlaceNameAndDesc,
                    StartDate = booking.StartDate,
                    EndDate = booking.EndDate,
                    BookingState = booking.BookingState
                })
                .ToListAsync(cancellationToken);

            return rows
                .Select(row => new CustomerReportReferenceOptionDto
                {
                    ReferenceType = ReportConstants.ReferenceTypes.Booking,
                    ReferenceId = row.BookingId,
                    Title = $"الحجز #{row.BookingId}",
                    Subtitle = BuildBookingSubtitle(row.BookingState),
                    PlaceId = row.PlaceId,
                    PlaceName = CompactPlaceName(row.PlaceName),
                    Date = row.StartDate
                })
                .ToList();
        }

        public async Task<IReadOnlyList<CustomerReportReferenceOptionDto>> SearchPlacesAsync(
            int customerId,
            string? search,
            int take = 12,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(customerId);

            var normalizedSearch = (search ?? string.Empty).Trim();
            var normalizedTake = Math.Clamp(take, 1, 20);

            var query = _db.Places
                .AsNoTracking()
                .Where(place => place.Status == "Active");

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(place =>
                    place.PlaceNameAndDesc.Contains(normalizedSearch));
            }

            var rows = await query
                .OrderBy(place => place.PlaceNameAndDesc)
                .Take(normalizedTake)
                .Select(place => new CustomerReportReferenceOptionDto
                {
                    ReferenceType = ReportConstants.ReferenceTypes.Place,
                    ReferenceId = place.PlaceId,
                    Title = place.PlaceNameAndDesc,
                    Subtitle = "منشأة على منصة دليلك اليمني",
                    PlaceId = place.PlaceId,
                    PlaceName = place.PlaceNameAndDesc
                })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                row.Title = CompactPlaceName(row.Title);
                row.PlaceName = CompactPlaceName(row.PlaceName);
            }

            return rows;
        }

        public async Task<CustomerReportDetailsDto?> GetDetailsAsync(
            int customerId,
            int reportId,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(customerId);

            if (reportId <= 0)
            {
                return null;
            }

            var row = await _db.Reports
                .AsNoTracking()
                .Where(report =>
                    report.ReportId == reportId &&
                    report.ReporterId == customerId)
                .Select(report => new DetailsProjection
                {
                    ReportId = report.ReportId,
                    ReportNumber = report.ReportNumber,
                    RequestKind = report.RequestKind,
                    ReportType = report.ReportType,
                    OtherReportTypeDescription = report.OtherReportTypeDescription,
                    Category = report.Category,
                    OtherCategoryDescription = report.OtherCategoryDescription,
                    RequiredAction = report.RequiredAction,
                    ResponseContentType = report.ResponseContentType,
                    Title = report.Title,
                    ReportReason = report.ReportReason,
                    ReportStatus = report.ReportStatus,
                    Priority = report.Priority,
                    EvidenceImage = report.EvidenceImage,
                    AdminComment = report.AdminComment,
                    Resolution = report.Resolution,
                    ResolutionAction = report.ResolutionAction,
                    WithdrawnReason = report.WithdrawnReason,
                    OwnerReply = report.OwnerReply,
                    OwnerReplyDate = report.OwnerReplyDate,
                    PlaceId = report.PlaceId,
                    PlaceName = report.Place != null
                        ? report.Place.PlaceNameAndDesc
                        : null,
                    BookingId = report.BookingId,
                    BookingStartDate = report.Booking != null
                        ? report.Booking.StartDate
                        : null,
                    BookingEndDate = report.Booking != null
                        ? report.Booking.EndDate
                        : null,
                    BookingState = report.Booking != null
                        ? report.Booking.BookingState
                        : null,
                    ReviewId = report.ReviewId,
                    PromotionId = report.PromotionId,
                    AdvertisementId = report.AdvertisementId,
                    CreatedAt = report.CreatedAt,
                    UpdatedAt = report.UpdatedAt,
                    ReporterReplyDeadline = report.ReporterReplyDeadline,
                    ReportedPartyReplyDeadline = report.ReportedPartyReplyDeadline,
                    EscalatedAt = report.EscalatedAt,
                    ResolvedAt = report.ResolvedAt,
                    ClosedAt = report.ClosedAt,
                    WithdrawnAt = report.WithdrawnAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row == null)
            {
                return null;
            }

            var messageRows = await _db.Chats
                .AsNoTracking()
                .Where(chat =>
                    chat.ReportId == reportId &&
                    chat.IsReportMessage &&
                    (chat.SenderId == customerId || chat.ReceiverId == customerId))
                .OrderBy(chat => chat.SendTime)
                .ThenBy(chat => chat.MessageId)
                .Select(chat => new MessageProjection
                {
                    MessageId = chat.MessageId,
                    SenderId = chat.SenderId,
                    SenderName = chat.Sender.FullName,
                    SenderRole = chat.Sender.Role,
                    MessageText = chat.MessageText,
                    AttachmentUrl = chat.AttachmentUrl,
                    SendTime = chat.SendTime
                })
                .ToListAsync(cancellationToken);

            var timeline = BuildTimeline(
                customerId,
                row,
                messageRows);

            var status = row.ReportStatus ?? ReportConstants.Statuses.Pending;

            return new CustomerReportDetailsDto
            {
                ReportId = row.ReportId,
                ReportNumber = row.ReportNumber ?? string.Empty,
                RequestKind = row.RequestKind ?? ReportConstants.RequestKinds.Report,
                RequestKindLabel = ReportConstants.GetRequestKindLabel(row.RequestKind),
                ReportType = row.ReportType ?? ReportConstants.Types.Other,
                OtherReportTypeDescription = row.OtherReportTypeDescription,
                ReportTypeLabel = GetReportTypeDisplayLabel(
                    row.ReportType,
                    row.OtherReportTypeDescription),
                Category = row.Category,
                OtherCategoryDescription = row.OtherCategoryDescription,
                CategoryLabel = GetCategoryDisplayLabel(
                    row.Category,
                    row.OtherCategoryDescription),
                RequiredAction = row.RequiredAction,
                ResponseContentType = row.ResponseContentType,
                Title = string.IsNullOrWhiteSpace(row.Title)
                    ? "شكوى أو بلاغ"
                    : row.Title.Trim(),
                ReportReason = row.ReportReason ?? string.Empty,
                ReportStatus = status,
                StatusLabel = ReportConstants.GetStatusLabel(status),
                StatusTone = ReportConstants.GetStatusTone(status),
                Priority = row.Priority ?? ReportConstants.Priorities.Normal,
                EvidenceImage = row.EvidenceImage,
                AdminComment = null,
                Resolution = row.Resolution,
                ResolutionAction = row.ResolutionAction,
                WithdrawnReason = row.WithdrawnReason,
                PlaceId = row.PlaceId,
                PlaceName = string.IsNullOrWhiteSpace(row.PlaceName)
                    ? null
                    : CompactPlaceName(row.PlaceName),
                BookingId = row.BookingId,
                BookingStartDate = row.BookingStartDate,
                BookingEndDate = row.BookingEndDate,
                BookingState = row.BookingState,
                ReviewId = row.ReviewId,
                PromotionId = row.PromotionId,
                AdvertisementId = row.AdvertisementId,
                CreatedAt = row.CreatedAt,
                UpdatedAt = row.UpdatedAt,
                ReporterReplyDeadline = row.ReporterReplyDeadline,
                ReportedPartyReplyDeadline = row.ReportedPartyReplyDeadline,
                ResolvedAt = row.ResolvedAt,
                ClosedAt = row.ClosedAt,
                WithdrawnAt = row.WithdrawnAt,
                NeedsCustomerAction = status == ReportConstants.Statuses.WaitingForReporter,
                CanAddClarification = CanAddClarification(status, row.RequiredAction),
                CanConfirmResolution =
                    status == ReportConstants.Statuses.WaitingForReporter &&
                    string.Equals(
                        row.RequiredAction,
                        ReportConstants.RequiredActions.ResolutionConfirmation,
                        StringComparison.OrdinalIgnoreCase),
                CanRequestReopen = CanRequestReopen(status),
                CanWithdraw = CanWithdraw(status),
                Timeline = timeline
            };
        }

        public async Task<CustomerReportOperationResultDto> CreateAsync(
            int customerId,
            CustomerCreateReportRequestDto request,
            IFormFile? evidenceFile,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(customerId);

            if (request == null)
            {
                return CustomerReportOperationResultDto.Fail(
                    "بيانات الطلب غير مكتملة.",
                    "invalid-request");
            }

            NormalizeCreateRequest(request);

            var validationError = ValidateCreateRequest(request);
            if (validationError != null)
            {
                return CustomerReportOperationResultDto.Fail(
                    validationError,
                    "validation");
            }

            var reference = await ResolveReferenceAsync(
                customerId,
                request,
                cancellationToken);

            if (!reference.Success)
            {
                return CustomerReportOperationResultDto.Fail(
                    reference.Message,
                    "invalid-reference");
            }

            var upload = await SaveEvidenceAsync(
                customerId,
                evidenceFile,
                cancellationToken);

            if (!upload.Success)
            {
                return CustomerReportOperationResultDto.Fail(
                    upload.Message,
                    "invalid-evidence");
            }

            var now = DateTime.Now;
            var requestKind = ReportConstants.RequestKinds.Report;
            var reportType = ReportConstants.NormalizeType(request.ReportType);
            var category = NormalizeOptionalText(
                request.Category,
                ReportConstants.Limits.CategoryMaxLength);

            var otherReportTypeDescription =
                reportType == ReportConstants.Types.Other
                    ? NormalizeOptionalText(
                        request.OtherTypeDescription,
                        ReportConstants.Limits.OtherDescriptionMaxLength)
                    : null;

            var otherCategoryDescription =
                string.Equals(
                    category,
                    ReportConstants.Categories.Other,
                    StringComparison.OrdinalIgnoreCase)
                    ? NormalizeOptionalText(
                        request.OtherCategoryDescription,
                        ReportConstants.Limits.OtherDescriptionMaxLength)
                    : null;

            var report = new Report
            {
                ReporterId = customerId,
                AgainstUserId = reference.AgainstUserId,
                ReportReason = request.ReportReason.Trim(),
                PlaceId = reference.PlaceId,
                EvidenceImage = upload.RelativePath,
                ReportStatus = ReportConstants.Statuses.Pending,
                CreatedAt = now,
                ReportType = reportType,
                OtherReportTypeDescription = otherReportTypeDescription,
                OtherCategoryDescription = otherCategoryDescription,
                ReviewId = reference.ReviewId,
                ReportNumber = GenerateReportNumber(requestKind),
                RequestKind = requestKind,
                Title = request.Title.Trim(),
                Category = string.IsNullOrWhiteSpace(category)
                    ? null
                    : category,
                Priority = ReportConstants.Priorities.Normal,
                BookingId = reference.BookingId,
                PromotionId = reference.PromotionId,
                AdvertisementId = reference.AdvertisementId,
                AssignedAdminUserId = null,
                ReporterReplyDeadline = null,
                ReportedPartyReplyDeadline = null,
                RequiredAction = null,
                UpdatedAt = now
            };

            await using var transaction = await _db.Database
                .BeginTransactionAsync(cancellationToken);

            try
            {
                _db.Reports.Add(report);
                await _db.SaveChangesAsync(cancellationToken);

                QueueCustomerNotification(
                    customerId,
                    report.ReportId,
                    "تم استلام طلبك",
                    $"تم استلام الطلب {report.ReportNumber} وسيتم مراجعته من إدارة المنصة.");

                await QueueAdminNotificationsAsync(
                    report.ReportId,
                    "قضية جديدة تحتاج مراجعة",
                    $"وصل {ReportConstants.GetRequestKindLabel(report.RequestKind)} جديد من عميل برقم {report.ReportNumber}.",
                    AdminPermissionKeys.Cases.Manage,
                    cancellationToken);

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return CustomerReportOperationResultDto.Ok(
                    "تم إرسال الطلب إلى إدارة المنصة بنجاح.",
                    report.ReportId,
                    report.ReportNumber);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                DeleteUploadedFile(upload.AbsolutePath);

                _logger.LogError(
                    exception,
                    "تعذر إنشاء شكوى/بلاغ للعميل {CustomerId}.",
                    customerId);

                return CustomerReportOperationResultDto.Fail(
                    "تعذر حفظ الطلب الآن. حاول مرة أخرى.",
                    "save-failed");
            }
        }

        public async Task<CustomerReportOperationResultDto> AddClarificationAsync(
            int customerId,
            int reportId,
            CustomerReportClarificationRequestDto request,
            IFormFile? evidenceFile,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(customerId);

            request ??= new CustomerReportClarificationRequestDto();
            var message = request.Message?.Trim();

            if (!string.IsNullOrWhiteSpace(message) &&
                message.Length > ReportConstants.Limits.ClarificationMaxLength)
            {
                return CustomerReportOperationResultDto.Fail(
                    "التوضيح يجب ألا يتجاوز 4000 حرف.",
                    "validation");
            }

            if (string.IsNullOrWhiteSpace(message) &&
                (evidenceFile == null || evidenceFile.Length <= 0))
            {
                return CustomerReportOperationResultDto.Fail(
                    "اكتب توضيحًا أو أرفق دليلًا جديدًا.",
                    "empty-clarification");
            }

            var report = await _db.Reports
                .FirstOrDefaultAsync(
                    item =>
                        item.ReportId == reportId &&
                        item.ReporterId == customerId,
                    cancellationToken);

            if (report == null)
            {
                return CustomerReportOperationResultDto.Fail(
                    "القضية غير موجودة.",
                    "not-found");
            }

            if (!CanAddClarification(report.ReportStatus, report.RequiredAction))
            {
                return CustomerReportOperationResultDto.Fail(
                    "هذه القضية منتهية. استخدم طلب إعادة المراجعة إذا ظهر لديك دليل جديد.",
                    "case-completed");
            }

            if (report.ReportStatus == ReportConstants.Statuses.WaitingForReporter &&
                report.RequiredAction == ReportConstants.RequiredActions.Clarification)
            {
                var extension = evidenceFile == null ? "" : Path.GetExtension(evidenceFile.FileName).ToLowerInvariant();
                var contentError = ReportConstants.ResponseContentTypes.Validate(
                    report.ResponseContentType, message, evidenceFile != null && evidenceFile.Length > 0,
                    extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".webp");
                if (contentError != null)
                    return CustomerReportOperationResultDto.Fail(contentError, "requested-content-only");
            }

            var adminId = await ResolveAdminReceiverAsync(
                cancellationToken);

            if (!adminId.HasValue)
            {
                return CustomerReportOperationResultDto.Fail(
                    "تعذر الوصول إلى حساب إداري لاستقبال التوضيح.",
                    "admin-unavailable");
            }

            var upload = await SaveEvidenceAsync(
                customerId,
                evidenceFile,
                cancellationToken);

            if (!upload.Success)
            {
                return CustomerReportOperationResultDto.Fail(
                    upload.Message,
                    "invalid-evidence");
            }

            var now = DateTime.Now;
            var chat = BuildReportChat(
                customerId,
                adminId.Value,
                report,
                string.IsNullOrWhiteSpace(message)
                    ? "أرفق العميل دليلًا جديدًا للقضية."
                    : message,
                upload.RelativePath,
                now);

            await using var transaction = await _db.Database
                .BeginTransactionAsync(cancellationToken);

            try
            {
                _db.Chats.Add(chat);

                report.ReporterReply = string.IsNullOrWhiteSpace(message)
                    ? "أرفق العميل دليلًا جديدًا للقضية."
                    : message;
                report.ReporterReplyDate = now;

                if (report.ReportStatus == ReportConstants.Statuses.WaitingForReporter &&
                    string.Equals(
                        report.RequiredAction,
                        ReportConstants.RequiredActions.Clarification,
                        StringComparison.OrdinalIgnoreCase))
                {
                    report.ReportStatus = ReportConstants.Statuses.InReview;
                    report.RequiredAction = null;
                    report.ResponseContentType = null;
                    report.ReporterReplyDeadline = null;
                }

                report.UpdatedAt = now;

                await QueueAdminNotificationsAsync(
                    report.ReportId,
                    "وصل توضيح جديد من العميل",
                    $"أضاف العميل توضيحًا أو دليلًا جديدًا للقضية {report.ReportNumber}.",
                    AdminPermissionKeys.Cases.Manage,
                    cancellationToken);

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return CustomerReportOperationResultDto.Ok(
                    "تمت إضافة التوضيح إلى نفس القضية بنجاح.",
                    report.ReportId,
                    report.ReportNumber);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                DeleteUploadedFile(upload.AbsolutePath);

                _logger.LogError(
                    exception,
                    "تعذر إضافة توضيح للقضية {ReportId} من العميل {CustomerId}.",
                    reportId,
                    customerId);

                return CustomerReportOperationResultDto.Fail(
                    "تعذر حفظ التوضيح الآن.",
                    "save-failed");
            }
        }

        public async Task<CustomerReportOperationResultDto> ConfirmResolutionAsync(
            int customerId,
            int reportId,
            CustomerReportResolutionConfirmationDto request,
            IFormFile? evidenceFile,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(customerId);

            if (request == null)
            {
                return CustomerReportOperationResultDto.Fail(
                    "حدد هل تم حل المشكلة أم لا.",
                    "invalid-request");
            }

            var report = await _db.Reports
                .FirstOrDefaultAsync(
                    item =>
                        item.ReportId == reportId &&
                        item.ReporterId == customerId,
                    cancellationToken);

            if (report == null)
            {
                return CustomerReportOperationResultDto.Fail(
                    "القضية غير موجودة.",
                    "not-found");
            }

            if (report.ReportStatus != ReportConstants.Statuses.WaitingForReporter ||
                !string.Equals(
                    report.RequiredAction,
                    ReportConstants.RequiredActions.ResolutionConfirmation,
                    StringComparison.OrdinalIgnoreCase))
            {
                return CustomerReportOperationResultDto.Fail(
                    "لا يوجد تأكيد حل مطلوب منك في هذه القضية حاليًا.",
                    "confirmation-not-requested");
            }

            var note = request.Note?.Trim();

            if (!request.IsResolved && string.IsNullOrWhiteSpace(note))
            {
                return CustomerReportOperationResultDto.Fail(
                    "اشرح ما الذي ما زال غير محلول حتى تتمكن الإدارة من المتابعة.",
                    "validation");
            }

            if (!string.IsNullOrWhiteSpace(note) &&
                note.Length > ReportConstants.Limits.ClarificationMaxLength)
            {
                return CustomerReportOperationResultDto.Fail(
                    "التوضيح يجب ألا يتجاوز 4000 حرف.",
                    "validation");
            }

            var adminId = await ResolveAdminReceiverAsync(
                cancellationToken);

            if (!adminId.HasValue)
            {
                return CustomerReportOperationResultDto.Fail(
                    "تعذر الوصول إلى حساب إداري لاستقبال تحديث القضية.",
                    "admin-unavailable");
            }

            var upload = await SaveEvidenceAsync(
                customerId,
                evidenceFile,
                cancellationToken);

            if (!upload.Success)
            {
                return CustomerReportOperationResultDto.Fail(
                    upload.Message,
                    "invalid-evidence");
            }

            var message = request.IsResolved
                ? "تأكيد العميل: نعم، تم حل المشكلة."
                : "تأكيد العميل: لا، المشكلة ما زالت مستمرة.";

            if (!string.IsNullOrWhiteSpace(note))
            {
                message += "\n" + note;
            }

            var now = DateTime.Now;

            _db.Chats.Add(BuildReportChat(
                customerId,
                adminId.Value,
                report,
                message,
                upload.RelativePath,
                now));

            report.ReporterReply = message;
            report.ReporterReplyDate = now;

            // العميل لا يغلق القضية بنفسه؛ يعيدها للإدارة لاتخاذ الإجراء النهائي.
            report.ReportStatus = ReportConstants.Statuses.InReview;
            report.RequiredAction = null;
            report.ResponseContentType = null;
            report.ReporterReplyDeadline = null;
            report.UpdatedAt = now;

            await using var transaction = await _db.Database
                .BeginTransactionAsync(cancellationToken);

            try
            {
                await QueueAdminNotificationsAsync(
                    report.ReportId,
                    request.IsResolved
                        ? "العميل أكد حل المشكلة"
                        : "العميل أفاد أن المشكلة مستمرة",
                    request.IsResolved
                        ? $"أكد العميل أن القضية {report.ReportNumber} تم حلها وتحتاج الإغلاق الإداري."
                        : $"أفاد العميل أن القضية {report.ReportNumber} لم تُحل بعد وتحتاج متابعة.",
                    request.IsResolved
                        ? AdminPermissionKeys.Cases.Resolve
                        : AdminPermissionKeys.Cases.Manage,
                    cancellationToken);

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return CustomerReportOperationResultDto.Ok(
                    request.IsResolved
                        ? "تم إرسال تأكيدك للإدارة، وستقوم الإدارة بإغلاق القضية رسميًا."
                        : "تم إبلاغ الإدارة أن المشكلة ما زالت مستمرة.",
                    report.ReportId,
                    report.ReportNumber);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                DeleteUploadedFile(upload.AbsolutePath);

                _logger.LogError(
                    exception,
                    "تعذر حفظ تأكيد العميل للقضية {ReportId}.",
                    reportId);

                return CustomerReportOperationResultDto.Fail(
                    "تعذر حفظ ردك الآن.",
                    "save-failed");
            }
        }

        public async Task<CustomerReportOperationResultDto> RequestReopenAsync(
            int customerId,
            int reportId,
            CustomerReportReopenRequestDto request,
            IFormFile? evidenceFile,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(customerId);

            if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            {
                return CustomerReportOperationResultDto.Fail(
                    "اكتب سبب طلب إعادة المراجعة.",
                    "validation");
            }

            var reason = request.Reason.Trim();
            if (reason.Length < 10 || reason.Length > ReportConstants.Limits.ClarificationMaxLength)
            {
                return CustomerReportOperationResultDto.Fail(
                    "سبب إعادة المراجعة يجب أن يكون بين 10 و4000 حرف.",
                    "validation");
            }

            var report = await _db.Reports
                .FirstOrDefaultAsync(
                    item =>
                        item.ReportId == reportId &&
                        item.ReporterId == customerId,
                    cancellationToken);

            if (report == null)
            {
                return CustomerReportOperationResultDto.Fail(
                    "القضية غير موجودة.",
                    "not-found");
            }

            if (!CanRequestReopen(report.ReportStatus))
            {
                return CustomerReportOperationResultDto.Fail(
                    "القضية ما زالت مفتوحة ولا تحتاج طلب إعادة مراجعة.",
                    "case-active");
            }

            var adminId = await ResolveAdminReceiverAsync(
                cancellationToken);

            if (!adminId.HasValue)
            {
                return CustomerReportOperationResultDto.Fail(
                    "تعذر الوصول إلى حساب إداري لاستقبال طلب إعادة المراجعة.",
                    "admin-unavailable");
            }

            var upload = await SaveEvidenceAsync(
                customerId,
                evidenceFile,
                cancellationToken);

            if (!upload.Success)
            {
                return CustomerReportOperationResultDto.Fail(
                    upload.Message,
                    "invalid-evidence");
            }

            var now = DateTime.Now;

            _db.Chats.Add(BuildReportChat(
                customerId,
                adminId.Value,
                report,
                "طلب إعادة مراجعة من العميل:\n" + reason,
                upload.RelativePath,
                now));

            // الطلب لا ينشئ قضية جديدة. Escalated يجعل القضية القديمة تعود إلى قائمة المراجعة.
            report.ReportStatus = ReportConstants.Statuses.Escalated;
            report.EscalatedAt = now;
            report.ReporterReply = "طلب إعادة مراجعة من العميل:\n" + reason;
            report.ReporterReplyDate = now;
            report.RequiredAction = null;
            report.ResponseContentType = null;
            report.ReporterReplyDeadline = null;
            report.ReportedPartyReplyDeadline = null;
            report.UpdatedAt = now;

            await using var transaction = await _db.Database
                .BeginTransactionAsync(cancellationToken);

            try
            {
                await QueueAdminNotificationsAsync(
                    report.ReportId,
                    "طلب إعادة مراجعة",
                    $"أرسل العميل دليلًا أو سببًا جديدًا لإعادة مراجعة القضية {report.ReportNumber}.",
                    AdminPermissionKeys.Cases.Manage,
                    cancellationToken);

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return CustomerReportOperationResultDto.Ok(
                    "تم إرسال طلب إعادة المراجعة داخل نفس القضية.",
                    report.ReportId,
                    report.ReportNumber);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                DeleteUploadedFile(upload.AbsolutePath);

                _logger.LogError(
                    exception,
                    "تعذر إرسال طلب إعادة مراجعة القضية {ReportId}.",
                    reportId);

                return CustomerReportOperationResultDto.Fail(
                    "تعذر إرسال طلب إعادة المراجعة الآن.",
                    "save-failed");
            }
        }

        public async Task<CustomerReportOperationResultDto> WithdrawAsync(
            int customerId,
            int reportId,
            CustomerReportWithdrawRequestDto request,
            CancellationToken cancellationToken = default)
        {
            EnsurePositiveUserId(customerId);

            if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            {
                return CustomerReportOperationResultDto.Fail(
                    "اكتب سبب سحب القضية.",
                    "validation");
            }

            var report = await _db.Reports
                .FirstOrDefaultAsync(
                    item =>
                        item.ReportId == reportId &&
                        item.ReporterId == customerId,
                    cancellationToken);

            if (report == null)
            {
                return CustomerReportOperationResultDto.Fail(
                    "القضية غير موجودة.",
                    "not-found");
            }

            if (!CanWithdraw(report.ReportStatus))
            {
                return CustomerReportOperationResultDto.Fail(
                    "لا يمكن سحب هذه القضية في حالتها الحالية.",
                    "withdraw-not-allowed");
            }

            var reason = request.Reason.Trim();
            if (reason.Length > ReportConstants.Limits.WithdrawReasonMaxLength)
            {
                return CustomerReportOperationResultDto.Fail(
                    "سبب السحب يجب ألا يتجاوز 500 حرف.",
                    "validation");
            }

            var now = DateTime.Now;
            report.ReportStatus = ReportConstants.Statuses.Withdrawn;
            report.WithdrawnAt = now;
            report.WithdrawnReason = reason;
            report.RequiredAction = null;
            report.ResponseContentType = null;
            report.ReporterReplyDeadline = null;
            report.ReportedPartyReplyDeadline = null;
            report.UpdatedAt = now;

            var adminId = await ResolveAdminReceiverAsync(
                cancellationToken);

            if (adminId.HasValue)
            {
                _db.Chats.Add(BuildReportChat(
                    customerId,
                    adminId.Value,
                    report,
                    string.IsNullOrWhiteSpace(reason)
                        ? "قام العميل بسحب القضية."
                        : "قام العميل بسحب القضية. السبب:\n" + reason,
                    attachmentUrl: null,
                    now: now));
            }

            try
            {
                await QueueAdminNotificationsAsync(
                    report.ReportId,
                    "تم سحب قضية",
                    $"سحب العميل القضية {report.ReportNumber}.",
                    AdminPermissionKeys.Cases.Manage,
                    cancellationToken);

                await _db.SaveChangesAsync(cancellationToken);

                return CustomerReportOperationResultDto.Ok(
                    "تم سحب القضية بنجاح.",
                    report.ReportId,
                    report.ReportNumber);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "تعذر سحب القضية {ReportId} للعميل {CustomerId}.",
                    reportId,
                    customerId);

                return CustomerReportOperationResultDto.Fail(
                    "تعذر سحب القضية الآن.",
                    "save-failed");
            }
        }

        private async Task<ReferenceResolution> ResolveReferenceAsync(
            int customerId,
            CustomerCreateReportRequestDto request,
            CancellationToken cancellationToken)
        {
            var type = ReportConstants.NormalizeType(request.ReportType);

            if (type == ReportConstants.Types.Booking)
            {
                if (!request.BookingId.HasValue || request.BookingId.Value <= 0)
                {
                    return ReferenceResolution.Fail("اختر الحجز المرتبط بالمشكلة.");
                }

                var row = await _db.Bookings
                    .AsNoTracking()
                    .Where(booking =>
                        booking.BookingId == request.BookingId.Value &&
                        booking.CustomerId == customerId)
                    .Select(booking => new
                    {
                        booking.BookingId,
                        booking.PlaceId,
                        OwnerId = booking.Place.OwnerId
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (row == null)
                {
                    return ReferenceResolution.Fail("الحجز المحدد لا يتبع حسابك.");
                }

                return ReferenceResolution.Ok(
                    placeId: row.PlaceId,
                    againstUserId: row.OwnerId,
                    bookingId: row.BookingId);
            }

            if (type == ReportConstants.Types.Place)
            {
                if (!request.PlaceId.HasValue || request.PlaceId.Value <= 0)
                {
                    return ReferenceResolution.Fail("اختر المنشأة المرتبطة بالمشكلة.");
                }

                var row = await _db.Places
                    .AsNoTracking()
                    .Where(place => place.PlaceId == request.PlaceId.Value)
                    .Select(place => new
                    {
                        place.PlaceId,
                        place.OwnerId
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (row == null)
                {
                    return ReferenceResolution.Fail("المنشأة المحددة غير موجودة.");
                }

                return ReferenceResolution.Ok(
                    placeId: row.PlaceId,
                    againstUserId: row.OwnerId);
            }

            if (type == ReportConstants.Types.Review)
            {
                if (!request.ReviewId.HasValue || request.ReviewId.Value <= 0)
                {
                    return ReferenceResolution.Fail("حدد التقييم الذي تريد الإبلاغ عنه.");
                }

                var row = await _db.Reviews
                    .AsNoTracking()
                    .Where(review => review.ReviewId == request.ReviewId.Value)
                    .Select(review => new
                    {
                        review.ReviewId,
                        review.UserId,
                        review.PlaceId
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (row == null)
                {
                    return ReferenceResolution.Fail("التقييم المحدد غير موجود.");
                }

                if (row.UserId == customerId)
                {
                    return ReferenceResolution.Fail("لا يمكنك إنشاء بلاغ على تقييمك أنت.");
                }

                return ReferenceResolution.Ok(
                    placeId: row.PlaceId,
                    againstUserId: row.UserId,
                    reviewId: row.ReviewId);
            }

            if (type == ReportConstants.Types.Promotion)
            {
                if (!request.PromotionId.HasValue || request.PromotionId.Value <= 0)
                {
                    return ReferenceResolution.Fail("حدد العرض الذي تريد الإبلاغ عنه.");
                }

                var row = await _db.Promotions
                    .AsNoTracking()
                    .Where(promotion => promotion.PromotionId == request.PromotionId.Value)
                    .Select(promotion => new
                    {
                        promotion.PromotionId,
                        promotion.PlaceId,
                        promotion.OwnerId
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (row == null)
                {
                    return ReferenceResolution.Fail("العرض المحدد غير موجود.");
                }

                return ReferenceResolution.Ok(
                    placeId: row.PlaceId,
                    againstUserId: row.OwnerId,
                    promotionId: row.PromotionId);
            }

            if (type == ReportConstants.Types.Advertisement)
            {
                if (!request.AdvertisementId.HasValue || request.AdvertisementId.Value <= 0)
                {
                    return ReferenceResolution.Fail("حدد الإعلان الذي تريد الإبلاغ عنه.");
                }

                var row = await _db.Advertisements
                    .AsNoTracking()
                    .Where(advertisement => advertisement.AdvertisementId == request.AdvertisementId.Value)
                    .Select(advertisement => new
                    {
                        advertisement.AdvertisementId,
                        advertisement.PlaceId,
                        advertisement.OwnerId
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (row == null)
                {
                    return ReferenceResolution.Fail("الإعلان المحدد غير موجود.");
                }

                return ReferenceResolution.Ok(
                    placeId: row.PlaceId,
                    againstUserId: row.OwnerId,
                    advertisementId: row.AdvertisementId);
            }

            // بلاغات المنصة والمشكلات التقنية و"أخرى" تصل للإدارة دون طرف مقابل.
            return ReferenceResolution.Ok();
        }

        private async Task<CustomerReportReferenceOptionDto?> ResolveSelectedReferenceForDisplayAsync(
            int customerId,
            CustomerCreateReportRequestDto form,
            CancellationToken cancellationToken)
        {
            var type = ReportConstants.NormalizeType(form.ReportType);

            if (type == ReportConstants.Types.Booking && form.BookingId.HasValue)
            {
                return await _db.Bookings
                    .AsNoTracking()
                    .Where(booking =>
                        booking.BookingId == form.BookingId.Value &&
                        booking.CustomerId == customerId)
                    .Select(booking => new CustomerReportReferenceOptionDto
                    {
                        ReferenceType = ReportConstants.ReferenceTypes.Booking,
                        ReferenceId = booking.BookingId,
                        Title = "الحجز #" + booking.BookingId,
                        Subtitle = booking.Place.PlaceNameAndDesc,
                        PlaceId = booking.PlaceId,
                        PlaceName = booking.Place.PlaceNameAndDesc,
                        Date = booking.StartDate
                    })
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (type == ReportConstants.Types.Place && form.PlaceId.HasValue)
            {
                var selected = await _db.Places
                    .AsNoTracking()
                    .Where(place => place.PlaceId == form.PlaceId.Value)
                    .Select(place => new CustomerReportReferenceOptionDto
                    {
                        ReferenceType = ReportConstants.ReferenceTypes.Place,
                        ReferenceId = place.PlaceId,
                        Title = place.PlaceNameAndDesc,
                        PlaceId = place.PlaceId,
                        PlaceName = place.PlaceNameAndDesc
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (selected != null)
                {
                    selected.Title = CompactPlaceName(selected.Title);
                    selected.PlaceName = CompactPlaceName(selected.PlaceName);
                }

                return selected;
            }

            return null;
        }

        private async Task<UploadResult> SaveEvidenceAsync(
            int customerId,
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            if (file == null || file.Length <= 0)
            {
                return UploadResult.Ok();
            }

            if (file.Length > ReportConstants.Limits.MaxEvidenceBytes)
            {
                return UploadResult.Fail("حجم المرفق يجب ألا يتجاوز 5 ميجابايت.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedEvidenceExtensions.Contains(extension))
            {
                return UploadResult.Fail("صيغة المرفق غير مدعومة. استخدم JPG أو PNG أو WEBP أو PDF.");
            }

            var contentType = (file.ContentType ?? string.Empty).Trim().ToLowerInvariant();
            if (!AllowedEvidenceContentTypes.Contains(contentType))
            {
                return UploadResult.Fail("نوع المرفق غير مدعوم.");
            }

            if (!IsExtensionCompatibleWithContentType(extension, contentType))
            {
                return UploadResult.Fail("امتداد المرفق لا يطابق نوع الملف المرسل من المتصفح.");
            }

            if (!await HasValidFileSignatureAsync(file, extension, cancellationToken))
            {
                return UploadResult.Fail("محتوى المرفق لا يطابق الصيغة المسموح بها.");
            }

            var webRoot = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");
            }

            var relativeFolder = Path.Combine(
                "uploads",
                "evidence",
                "customer-reports");

            var absoluteFolder = Path.Combine(webRoot, relativeFolder);
            Directory.CreateDirectory(absoluteFolder);

            var safeFileName =
                $"customer-{customerId}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}{extension}";

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
                _logger.LogError(
                    exception,
                    "تعذر حفظ مرفق قضية العميل {CustomerId}.",
                    customerId);

                return UploadResult.Fail("تعذر حفظ المرفق في الخادم.");
            }

            var relativePath = "/" +
                Path.Combine(relativeFolder, safeFileName)
                    .Replace("\\", "/");

            return UploadResult.Ok(relativePath, absolutePath);
        }

        private static async Task<bool> HasValidFileSignatureAsync(
            IFormFile file,
            string extension,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[12];

            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAsync(
                buffer.AsMemory(0, buffer.Length),
                cancellationToken);

            if (extension == ".jpg" || extension == ".jpeg")
            {
                return read >= 3 &&
                       buffer[0] == 0xFF &&
                       buffer[1] == 0xD8 &&
                       buffer[2] == 0xFF;
            }

            if (extension == ".png")
            {
                byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
                return read >= signature.Length &&
                       signature.SequenceEqual(buffer.Take(signature.Length));
            }

            if (extension == ".webp")
            {
                return read >= 12 &&
                       buffer[0] == (byte)'R' &&
                       buffer[1] == (byte)'I' &&
                       buffer[2] == (byte)'F' &&
                       buffer[3] == (byte)'F' &&
                       buffer[8] == (byte)'W' &&
                       buffer[9] == (byte)'E' &&
                       buffer[10] == (byte)'B' &&
                       buffer[11] == (byte)'P';
            }

            if (extension == ".pdf")
            {
                return read >= 4 &&
                       buffer[0] == (byte)'%' &&
                       buffer[1] == (byte)'P' &&
                       buffer[2] == (byte)'D' &&
                       buffer[3] == (byte)'F';
            }

            return false;
        }

        private static bool IsExtensionCompatibleWithContentType(
            string extension,
            string contentType)
        {
            if (extension == ".jpg" || extension == ".jpeg")
            {
                return contentType == "image/jpeg";
            }

            if (extension == ".png")
            {
                return contentType == "image/png";
            }

            if (extension == ".webp")
            {
                return contentType == "image/webp";
            }

            if (extension == ".pdf")
            {
                return contentType == "application/pdf";
            }

            return false;
        }

        private static void DeleteUploadedFile(string? absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) ||
                !File.Exists(absolutePath))
            {
                return;
            }

            try
            {
                File.Delete(absolutePath);
            }
            catch
            {
                // فشل تنظيف ملف لا يجب أن يكسر عملية المستخدم.
            }
        }

        private async Task<int?> ResolveAdminReceiverAsync(
            CancellationToken cancellationToken)
        {
            // هذا الحساب مجرد مستقبل تقني لرسالة القضية داخل Chats،
            // وليس إسنادًا للقضية إلى موظف معيّن.
            return await _db.Users
                .AsNoTracking()
                .Where(user =>
                    user.Role == "Admin" &&
                    user.AccountState != "Suspended" &&
                    user.AccountState != "Blocked")
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

        private void QueueCustomerNotification(
            int customerId,
            int reportId,
            string title,
            string text)
        {
            _notifications.Queue(new CreateNotificationDto
            {
                UserId = customerId,
                NotificationTitle = title,
                NotificationText = text,
                NotificationType = ReportConstants.NotificationTypes.Report,
                TargetUrl = $"/Reports/ReportDetails/{reportId}",
                ReferenceType = ReportConstants.ReferenceTypes.Report,
                ReferenceId = reportId
            });
        }

        private static Chat BuildReportChat(
            int senderId,
            int receiverId,
            Report report,
            string message,
            string? attachmentUrl,
            DateTime now)
        {
            return new Chat
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                MessageText = message,
                SendTime = now,
                IsRead = false,
                AttachmentUrl = attachmentUrl,
                PlaceId = report.PlaceId,
                IsDeletedForSender = false,
                IsDeletedForReceiver = false,
                IsEdited = false,
                ReportId = report.ReportId,
                IsReportMessage = true
            };
        }

        private static IReadOnlyList<CustomerReportTimelineItemDto> BuildTimeline(
            int customerId,
            DetailsProjection row,
            IReadOnlyList<MessageProjection> messages)
        {
            var timeline = new List<CustomerReportTimelineItemDto>();

            if (row.CreatedAt.HasValue)
            {
                timeline.Add(new CustomerReportTimelineItemDto
                {
                    Date = row.CreatedAt.Value,
                    Title = "تم إنشاء القضية",
                    Description = "تم إرسال الطلب إلى إدارة المنصة للمراجعة.",
                    Actor = "أنت",
                    Icon = "bi-send-check-fill",
                    Tone = "new",
                    AttachmentUrl = row.EvidenceImage,
                    IsCustomerMessage = true
                });
            }

            foreach (var message in messages)
            {
                var senderRole = message.SenderRole ?? string.Empty;
                var isCustomer = message.SenderId == customerId;
                var isAdmin = string.Equals(senderRole, "Admin", StringComparison.OrdinalIgnoreCase);
                var isOwner = string.Equals(senderRole, "Owner", StringComparison.OrdinalIgnoreCase);

                timeline.Add(new CustomerReportTimelineItemDto
                {
                    Date = message.SendTime ?? row.UpdatedAt,
                    Title = isCustomer
                        ? "أضفت توضيحًا"
                        : isAdmin
                            ? "تحديث من الإدارة"
                            : isOwner
                                ? "رد من المالك"
                                : "تحديث على القضية",
                    Description = message.MessageText,
                    Actor = isCustomer
                        ? "أنت"
                        : isAdmin
                            ? "الإدارة"
                            : isOwner
                                ? "المالك"
                                : (string.IsNullOrWhiteSpace(message.SenderName)
                                    ? "النظام"
                                    : message.SenderName),
                    Icon = isCustomer
                        ? "bi-chat-right-text-fill"
                        : isAdmin
                            ? "bi-shield-check"
                            : isOwner
                                ? "bi-building-check"
                                : "bi-chat-dots-fill",
                    Tone = isCustomer
                        ? "customer"
                        : isAdmin
                            ? "admin"
                            : isOwner
                                ? "owner"
                                : "info",
                    AttachmentUrl = message.AttachmentUrl,
                    IsCustomerMessage = isCustomer,
                    IsOwnerMessage = isOwner,
                    IsAdminMessage = isAdmin
                });
            }

            if (row.EscalatedAt.HasValue)
            {
                timeline.Add(new CustomerReportTimelineItemDto
                {
                    Date = row.EscalatedAt.Value,
                    Title = "تم تصعيد القضية للمراجعة",
                    Description = "القضية تحتاج مراجعة إدارية إضافية.",
                    Actor = "الإدارة",
                    Icon = "bi-arrow-up-right-circle-fill",
                    Tone = "danger",
                    IsAdminMessage = true
                });
            }

            if (row.ResolvedAt.HasValue)
            {
                timeline.Add(new CustomerReportTimelineItemDto
                {
                    Date = row.ResolvedAt.Value,
                    Title = "تم تسجيل حل للقضية",
                    Description = string.IsNullOrWhiteSpace(row.Resolution)
                        ? "سجلت الإدارة نتيجة المعالجة."
                        : row.Resolution,
                    Actor = "الإدارة",
                    Icon = "bi-check-circle-fill",
                    Tone = "success",
                    IsAdminMessage = true
                });
            }

            if (row.WithdrawnAt.HasValue)
            {
                timeline.Add(new CustomerReportTimelineItemDto
                {
                    Date = row.WithdrawnAt.Value,
                    Title = "تم سحب القضية",
                    Description = row.WithdrawnReason,
                    Actor = "أنت",
                    Icon = "bi-arrow-counterclockwise",
                    Tone = "muted",
                    IsCustomerMessage = true
                });
            }

            if (row.ClosedAt.HasValue)
            {
                timeline.Add(new CustomerReportTimelineItemDto
                {
                    Date = row.ClosedAt.Value,
                    Title = "تم إغلاق القضية",
                    Description = string.IsNullOrWhiteSpace(row.ResolutionAction)
                        ? "تم إغلاق ملف القضية من الإدارة."
                        : row.ResolutionAction,
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

        private static CustomerReportListItemDto BuildListItem(ListProjection row)
        {
            var status = string.IsNullOrWhiteSpace(row.ReportStatus)
                ? ReportConstants.Statuses.Pending
                : row.ReportStatus;

            return new CustomerReportListItemDto
            {
                ReportId = row.ReportId,
                ReportNumber = row.ReportNumber ?? string.Empty,
                RequestKind = row.RequestKind ?? ReportConstants.RequestKinds.Report,
                RequestKindLabel = ReportConstants.GetRequestKindLabel(row.RequestKind),
                ReportType = row.ReportType ?? ReportConstants.Types.Other,
                OtherReportTypeDescription = row.OtherReportTypeDescription,
                ReportTypeLabel = GetReportTypeDisplayLabel(
                    row.ReportType,
                    row.OtherReportTypeDescription),
                Category = row.Category,
                OtherCategoryDescription = row.OtherCategoryDescription,
                CategoryLabel = GetCategoryDisplayLabel(
                    row.Category,
                    row.OtherCategoryDescription),
                RequiredAction = row.RequiredAction,
                ResponseContentType = row.ResponseContentType,
                Title = string.IsNullOrWhiteSpace(row.Title)
                    ? "شكوى أو بلاغ"
                    : row.Title.Trim(),
                ReportStatus = status,
                StatusLabel = ReportConstants.GetStatusLabel(status),
                StatusTone = ReportConstants.GetStatusTone(status),
                Priority = row.Priority ?? ReportConstants.Priorities.Normal,
                PlaceId = row.PlaceId,
                PlaceName = string.IsNullOrWhiteSpace(row.PlaceName)
                    ? null
                    : CompactPlaceName(row.PlaceName),
                BookingId = row.BookingId,
                ReviewId = row.ReviewId,
                PromotionId = row.PromotionId,
                AdvertisementId = row.AdvertisementId,
                CreatedAt = row.CreatedAt,
                UpdatedAt = row.UpdatedAt,
                ReplyDeadline = row.ReporterReplyDeadline,
                NeedsCustomerAction = status == ReportConstants.Statuses.WaitingForReporter,
                IsCompleted = ReportConstants.Statuses.IsCompleted(status)
            };
        }

        private static string GetReportTypeDisplayLabel(
            string? reportType,
            string? otherDescription)
        {
            if (string.Equals(
                    reportType,
                    ReportConstants.Types.Other,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(otherDescription))
            {
                return otherDescription.Trim();
            }

            return ReportConstants.GetTypeLabel(reportType);
        }

        private static string? GetCategoryDisplayLabel(
            string? category,
            string? otherDescription)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return null;
            }

            if (string.Equals(
                    category,
                    ReportConstants.Categories.Other,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(otherDescription))
            {
                return otherDescription.Trim();
            }

            return ReportConstants.GetCategoryLabel(category);
        }

        private static IReadOnlyList<CustomerReportOptionDto> BuildRequestKindOptions()
        {
            return new List<CustomerReportOptionDto>
            {
                new CustomerReportOptionDto
                {
                    Value = ReportConstants.RequestKinds.Complaint,
                    Label = "شكوى",
                    Description = "مشكلة تحتاج حلًا أو معالجة من إدارة المنصة.",
                    Icon = "bi-chat-square-heart-fill"
                },
                new CustomerReportOptionDto
                {
                    Value = ReportConstants.RequestKinds.Report,
                    Label = "بلاغ",
                    Description = "مخالفة أو محتوى يحتاج مراجعة الإدارة.",
                    Icon = "bi-shield-exclamation"
                }
            };
        }

        private static IReadOnlyList<CustomerReportOptionDto> BuildReportTypeOptions()
        {
            return new List<CustomerReportOptionDto>
            {
                Option(ReportConstants.Types.Booking, "حجز", "مشكلة مرتبطة بأحد حجوزاتك.", "bi-calendar2-check-fill"),
                Option(ReportConstants.Types.Place, "منشأة", "مشكلة أو مخالفة مرتبطة بمنشأة.", "bi-building-fill"),
                Option(ReportConstants.Types.Review, "تقييم", "الإبلاغ عن تقييم أو محتوى غير مناسب.", "bi-star-fill"),
                Option(ReportConstants.Types.Promotion, "عرض", "الإبلاغ عن عرض غير صحيح أو مضلل.", "bi-tags-fill"),
                Option(ReportConstants.Types.Advertisement, "إعلان", "الإبلاغ عن إعلان يحتاج مراجعة.", "bi-megaphone-fill"),
                Option(ReportConstants.Types.Platform, "المنصة", "ملاحظة أو مشكلة تتعلق بالمنصة نفسها.", "bi-headset"),
                Option(ReportConstants.Types.Technical, "مشكلة تقنية", "خلل تقني يمنعك من استخدام خدمة بشكل صحيح.", "bi-tools"),
                Option(ReportConstants.Types.Other, "أخرى", "موضوع لا يندرج ضمن الخيارات السابقة.", "bi-three-dots")
            };
        }

        private static IReadOnlyList<CustomerReportOptionDto> BuildCategoryOptions()
        {
            return new List<CustomerReportOptionDto>
            {
                Option(ReportConstants.Categories.BookingIssue, "مشكلة في الحجز", null, "bi-calendar-x"),
                Option(ReportConstants.Categories.PaymentIssue, "مشكلة في الدفع", null, "bi-credit-card"),
                Option(ReportConstants.Categories.RefundIssue, "مشكلة في الاسترداد", null, "bi-arrow-repeat"),
                Option(ReportConstants.Categories.ServiceIssue, "مشكلة في الخدمة", null, "bi-person-workspace"),
                Option(ReportConstants.Categories.DescriptionMismatch, "لا يطابق الوصف", null, "bi-card-checklist"),
                Option(ReportConstants.Categories.OwnerConduct, "تصرف غير مناسب من المالك", null, "bi-person-exclamation"),
                Option(ReportConstants.Categories.FraudOrScam, "اشتباه احتيال", null, "bi-shield-exclamation"),
                Option(ReportConstants.Categories.MisleadingContent, "محتوى مضلل", null, "bi-exclamation-diamond"),
                Option(ReportConstants.Categories.AbusiveContent, "إساءة أو محتوى غير لائق", null, "bi-chat-square-dots"),
                Option(ReportConstants.Categories.FakeContent, "محتوى أو تقييم غير حقيقي", null, "bi-eye-slash"),
                Option(ReportConstants.Categories.TechnicalIssue, "مشكلة تقنية", null, "bi-tools"),
                Option(ReportConstants.Categories.PlatformIssue, "مشكلة في المنصة", null, "bi-window-stack"),
                Option(ReportConstants.Categories.Other, "أخرى", null, "bi-three-dots")
            };
        }

        private static CustomerReportOptionDto Option(
            string value,
            string label,
            string? description,
            string icon)
        {
            return new CustomerReportOptionDto
            {
                Value = value,
                Label = label,
                Description = description,
                Icon = icon
            };
        }

        private static void NormalizeCreateRequest(CustomerCreateReportRequestDto request)
        {
            // واجهة العميل تستخدم مسارًا واحدًا واضحًا: «بلاغ».
            request.RequestKind = ReportConstants.RequestKinds.Report;
            request.ReportType = ReportConstants.NormalizeType(request.ReportType);

            request.OtherTypeDescription = NormalizeOptionalText(
                request.OtherTypeDescription,
                ReportConstants.Limits.OtherDescriptionMaxLength);

            request.OtherCategoryDescription = NormalizeOptionalText(
                request.OtherCategoryDescription,
                ReportConstants.Limits.OtherDescriptionMaxLength);

            var selectedCategory = request.Category?.Trim();

            // نحافظ على القيمة القياسية Category = Other،
            // ونحفظ الوصف المخصص في OtherCategoryDescription داخل Reports.
            request.Category = request.ReportType == ReportConstants.Types.Other
                ? null
                : ReportConstants.NormalizeCategory(selectedCategory);

            request.Title = request.Title?.Trim() ?? string.Empty;
            request.ReportReason = request.ReportReason?.Trim() ?? string.Empty;
        }

        private static string? ValidateCreateRequest(CustomerCreateReportRequestDto request)
        {
            var validationResults = new List<ValidationResult>();
            var context = new ValidationContext(request);

            if (!Validator.TryValidateObject(request, context, validationResults, validateAllProperties: true))
            {
                return validationResults.FirstOrDefault()?.ErrorMessage ?? "راجع البيانات المطلوبة.";
            }

            if (!ReportConstants.RequestKinds.IsValid(request.RequestKind))
            {
                return "نوع الطلب غير صحيح.";
            }

            if (!ReportConstants.Types.IsValid(request.ReportType))
            {
                return "موضوع الطلب غير صحيح.";
            }

            if (request.ReportType == ReportConstants.Types.Other &&
                string.IsNullOrWhiteSpace(request.OtherTypeDescription))
            {
                return "اكتب باختصار ما نوع المشكلة عند اختيار «أخرى».";
            }

            if (request.ReportType == ReportConstants.Types.Other &&
                request.OtherTypeDescription!.Trim().Length < 3)
            {
                return "وصف نوع المشكلة يجب أن يكون 3 أحرف على الأقل.";
            }

            if (string.Equals(
                    request.Category,
                    ReportConstants.Categories.Other,
                    StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(request.OtherCategoryDescription))
            {
                return "اكتب التصنيف عند اختيار «أخرى».";
            }

            if (string.Equals(
                    request.Category,
                    ReportConstants.Categories.Other,
                    StringComparison.OrdinalIgnoreCase) &&
                request.OtherCategoryDescription!.Trim().Length < 3)
            {
                return "وصف التصنيف يجب أن يكون 3 أحرف على الأقل.";
            }

            if (!string.IsNullOrWhiteSpace(request.Category) &&
                request.Category.Length > ReportConstants.Limits.CategoryMaxLength)
            {
                return "تصنيف المشكلة طويل جدًا.";
            }

            return null;
        }

        private static bool CanAddClarification(
            string? status,
            string? requiredAction)
        {
            if (ReportConstants.Statuses.IsCompleted(status))
            {
                return false;
            }

            // عند انتظار تأكيد الحل نظهر إجراء التأكيد الواضح بدل نموذج توضيح عام.
            return !(status == ReportConstants.Statuses.WaitingForReporter &&
                     string.Equals(
                         requiredAction,
                         ReportConstants.RequiredActions.ResolutionConfirmation,
                         StringComparison.OrdinalIgnoreCase));
        }

        private static bool CanRequestReopen(string? status)
        {
            return status == ReportConstants.Statuses.Rejected ||
                   status == ReportConstants.Statuses.Closed ||
                   status == ReportConstants.Statuses.Resolved;
        }

        private static bool CanWithdraw(string? status)
        {
            return status == ReportConstants.Statuses.Pending ||
                   status == ReportConstants.Statuses.InReview ||
                   status == ReportConstants.Statuses.WaitingForReporter ||
                   status == ReportConstants.Statuses.WaitingForReportedParty ||
                   status == ReportConstants.Statuses.Escalated;
        }

        private static string GenerateReportNumber(string? requestKind)
        {
            var prefix = requestKind == ReportConstants.RequestKinds.Complaint
                ? "CCM"
                : "CRP";

            var token = Guid.NewGuid()
                .ToString("N")
                .Substring(0, 10)
                .ToUpperInvariant();

            return $"{prefix}-{DateTime.Now:yyyyMMdd}-{token}";
        }

        private static int NormalizePageSize(int pageSize)
        {
            if (pageSize <= 0)
            {
                return ReportConstants.Limits.DefaultPageSize;
            }

            return Math.Min(pageSize, ReportConstants.Limits.MaxPageSize);
        }

        private static string? NormalizeOptionalText(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim();
            return normalized.Length <= maxLength
                ? normalized
                : normalized.Substring(0, maxLength);
        }

        private static string BuildBookingSubtitle(string? bookingState)
        {
            var stateLabel = GetBookingStateLabel(bookingState);

            return string.IsNullOrWhiteSpace(stateLabel)
                ? "حجز مرتبط بحسابك"
                : stateLabel;
        }

        private static string GetBookingStateLabel(string? state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return string.Empty;
            }

            var value = state.Trim();

            if (value.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return "بانتظار المراجعة";
            if (value.Equals("AwaitingPayment", StringComparison.OrdinalIgnoreCase)) return "بانتظار الدفع";
            if (value.Equals("PaymentReview", StringComparison.OrdinalIgnoreCase)) return "الدفع قيد المراجعة";
            if (value.Equals("Confirmed", StringComparison.OrdinalIgnoreCase)) return "مؤكد";
            if (value.Equals("Completed", StringComparison.OrdinalIgnoreCase)) return "مكتمل";
            if (value.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)) return "ملغي";
            if (value.Equals("Rejected", StringComparison.OrdinalIgnoreCase)) return "مرفوض";
            if (value.Equals("Expired", StringComparison.OrdinalIgnoreCase)) return "منتهي";
            if (value.Equals("NoShow", StringComparison.OrdinalIgnoreCase)) return "لم يحضر";
            if (value.Equals("ReadyToClose", StringComparison.OrdinalIgnoreCase)) return "جاهز للإنهاء";

            return value;
        }

        private static string CompactPlaceName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "منشأة";
            }

            var normalized = value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();

            return normalized.Length <= 120
                ? normalized
                : normalized.Substring(0, 117) + "...";
        }

        private static void EnsurePositiveUserId(int userId)
        {
            if (userId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(userId),
                    "معرف المستخدم غير صحيح.");
            }
        }

        private sealed class ListProjection
        {
            public int ReportId { get; set; }
            public string? ReportNumber { get; set; }
            public string? RequestKind { get; set; }
            public string? ReportType { get; set; }
            public string? OtherReportTypeDescription { get; set; }
            public string? Category { get; set; }
            public string? OtherCategoryDescription { get; set; }
            public string? RequiredAction { get; set; }
            public string? ResponseContentType { get; set; }
            public string? Title { get; set; }
            public string? ReportStatus { get; set; }
            public string? Priority { get; set; }
            public int? PlaceId { get; set; }
            public string? PlaceName { get; set; }
            public int? BookingId { get; set; }
            public int? ReviewId { get; set; }
            public int? PromotionId { get; set; }
            public int? AdvertisementId { get; set; }
            public DateTime? CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public DateTime? ReporterReplyDeadline { get; set; }
        }

        private sealed class DetailsProjection
        {
            public int ReportId { get; set; }
            public string? ReportNumber { get; set; }
            public string? RequestKind { get; set; }
            public string? ReportType { get; set; }
            public string? OtherReportTypeDescription { get; set; }
            public string? Category { get; set; }
            public string? OtherCategoryDescription { get; set; }
            public string? RequiredAction { get; set; }
            public string? ResponseContentType { get; set; }
            public string? Title { get; set; }
            public string? ReportReason { get; set; }
            public string? ReportStatus { get; set; }
            public string? Priority { get; set; }
            public string? EvidenceImage { get; set; }
            public string? AdminComment { get; set; }
            public string? Resolution { get; set; }
            public string? ResolutionAction { get; set; }
            public string? WithdrawnReason { get; set; }
            public string? OwnerReply { get; set; }
            public DateTime? OwnerReplyDate { get; set; }
            public int? PlaceId { get; set; }
            public string? PlaceName { get; set; }
            public int? BookingId { get; set; }
            public DateTime? BookingStartDate { get; set; }
            public DateTime? BookingEndDate { get; set; }
            public string? BookingState { get; set; }
            public int? ReviewId { get; set; }
            public int? PromotionId { get; set; }
            public int? AdvertisementId { get; set; }
            public DateTime? CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public DateTime? ReporterReplyDeadline { get; set; }
            public DateTime? ReportedPartyReplyDeadline { get; set; }
            public DateTime? EscalatedAt { get; set; }
            public DateTime? ResolvedAt { get; set; }
            public DateTime? ClosedAt { get; set; }
            public DateTime? WithdrawnAt { get; set; }
        }

        private sealed class MessageProjection
        {
            public int MessageId { get; set; }
            public int SenderId { get; set; }
            public string? SenderName { get; set; }
            public string? SenderRole { get; set; }
            public string? MessageText { get; set; }
            public string? AttachmentUrl { get; set; }
            public DateTime? SendTime { get; set; }
        }

        private sealed class BookingReferenceProjection
        {
            public int BookingId { get; set; }
            public int PlaceId { get; set; }
            public string? PlaceName { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime? EndDate { get; set; }
            public string? BookingState { get; set; }
        }

        private sealed class ReferenceResolution
        {
            public bool Success { get; private set; }
            public string Message { get; private set; } = string.Empty;
            public int? AgainstUserId { get; private set; }
            public int? PlaceId { get; private set; }
            public int? BookingId { get; private set; }
            public int? ReviewId { get; private set; }
            public int? PromotionId { get; private set; }
            public int? AdvertisementId { get; private set; }

            public static ReferenceResolution Ok(
                int? placeId = null,
                int? againstUserId = null,
                int? bookingId = null,
                int? reviewId = null,
                int? promotionId = null,
                int? advertisementId = null)
            {
                return new ReferenceResolution
                {
                    Success = true,
                    PlaceId = placeId,
                    AgainstUserId = againstUserId,
                    BookingId = bookingId,
                    ReviewId = reviewId,
                    PromotionId = promotionId,
                    AdvertisementId = advertisementId
                };
            }

            public static ReferenceResolution Fail(string message)
            {
                return new ReferenceResolution
                {
                    Success = false,
                    Message = message
                };
            }
        }

        private sealed class UploadResult
        {
            public bool Success { get; private set; }
            public string Message { get; private set; } = string.Empty;
            public string? RelativePath { get; private set; }
            public string? AbsolutePath { get; private set; }

            public static UploadResult Ok(
                string? relativePath = null,
                string? absolutePath = null)
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
