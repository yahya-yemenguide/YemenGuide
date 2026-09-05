using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Core.DTOs.Notifications;
using YemenGuide.Core.Entities;
using YemenGuide.Data;
using YemenGuide.Web.Services.Notifications;

namespace YemenGuide.Web.Services.Reports
{
    /// <summary>
    /// محرك بلاغات الإدارة - النسخة المبسطة.
    ///
    /// القواعد الأساسية:
    /// 1) لا يوجد إسناد موظفين داخل البلاغات. لوحة الموظفين والصلاحيات مستقلة تمامًا.
    /// 2) بلاغ العميل ضد المالك: الإدارة تستطيع طلب توضيح من العميل أو رد من المالك.
    /// 3) بلاغ المالك ضد العميل: لا يفتح للعميل مسار رد داخل القضية؛ الإدارة تثبت/تنفي المخالفة إداريًا.
    /// 4) عند ثبوت مخالفة العميل يزيد WarningCount ويصل للعميل تنبيه فقط، دون فتح القضية له.
    /// 5) كل الردود الرسمية تحفظ داخل نفس ReportID في Chats، وكل القرارات تسجل في AdminAuditLogs.
    /// </summary>
    public sealed class AdminReportsService : IAdminReportsService
    {
        private const string ReportEntityType = "Report";

        private const string ReviewStartedAction = "ReportReviewStarted";
        private const string ClarificationRequestedAction = "ReportClarificationRequested";
        private const string OwnerResponseRequestedAction = "OwnerResponseRequested";
        private const string ResolutionConfirmationRequestedAction = "ReportResolutionConfirmationRequested";
        private const string PriorityChangedAction = "ReportPriorityChanged";
        private const string EscalatedAction = "ReportEscalated";
        private const string InternalNoteAction = "ReportInternalNote";
        private const string ResolvedAction = "ReportResolved";
        private const string RejectedAction = "ReportRejected";
        private const string CustomerViolationConfirmedAction = "CustomerViolationConfirmed";
        private const string CustomerViolationNotConfirmedAction = "CustomerViolationNotConfirmed";
        private const string ClosedAction = "ReportClosed";
        private const string ReopenedAction = "ReportReopened";

        private const string CustomerViolationConfirmedResolutionAction = "CustomerViolationConfirmed";
        private const string CustomerViolationNotConfirmedResolutionAction = "CustomerViolationNotConfirmed";

        private const int MaximumAuditDescriptionLength = 1000;
        private const int MaximumAdminCommentLength = 500;

        private readonly AppDbContext _db;
        private readonly INotificationService _notifications;
        private readonly ILogger<AdminReportsService> _logger;

        public AdminReportsService(
            AppDbContext db,
            INotificationService notifications,
            ILogger<AdminReportsService> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /* =====================================================================
           مركز البلاغات
           ===================================================================== */

        public async Task<AdminReportsCenterDto> GetReportsCenterAsync(
            string? activeTab,
            AdminReportFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var tab = AdminReportValues.NormalizeTab(activeTab);
            filter ??= new AdminReportFilterDto();

            var summary = await GetSummaryAsync(cancellationToken);
            var list = await GetReportsListAsync(tab, filter, cancellationToken);

            return new AdminReportsCenterDto
            {
                ActiveTab = tab,
                Summary = summary,
                Tabs = BuildTabs(summary),
                Reports = list
            };
        }

        public async Task<AdminReportSummaryDto> GetSummaryAsync(
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.Now;

            var summary = await _db.Reports
                .AsNoTracking()
                .GroupBy(_ => 1)
                .Select(group => new AdminReportSummaryDto
                {
                    TotalCount = group.Count(),
                    NewCount = group.Count(report =>
                        report.ReportStatus == ReportConstants.Statuses.Pending),
                    ActionCount = group.Count(report =>
                        report.ReportStatus == ReportConstants.Statuses.InReview),
                    WaitingCount = group.Count(report =>
                        report.ReportStatus == ReportConstants.Statuses.WaitingForReporter ||
                        report.ReportStatus == ReportConstants.Statuses.WaitingForReportedParty),
                    EscalatedCount = group.Count(report =>
                        report.ReportStatus == ReportConstants.Statuses.Escalated),
                    CompletedCount = group.Count(report =>
                        report.ReportStatus == ReportConstants.Statuses.Resolved ||
                        report.ReportStatus == ReportConstants.Statuses.Rejected ||
                        report.ReportStatus == ReportConstants.Statuses.Closed ||
                        report.ReportStatus == ReportConstants.Statuses.Withdrawn),
                    UrgentOpenCount = group.Count(report =>
                        report.Priority == ReportConstants.Priorities.Urgent &&
                        report.ReportStatus != ReportConstants.Statuses.Resolved &&
                        report.ReportStatus != ReportConstants.Statuses.Rejected &&
                        report.ReportStatus != ReportConstants.Statuses.Closed &&
                        report.ReportStatus != ReportConstants.Statuses.Withdrawn),
                    OverdueCount = group.Count(report =>
                        (report.ReportStatus == ReportConstants.Statuses.WaitingForReporter &&
                         report.ReporterReplyDeadline.HasValue &&
                         report.ReporterReplyDeadline.Value < now) ||
                        (report.ReportStatus == ReportConstants.Statuses.WaitingForReportedParty &&
                         report.ReportedPartyReplyDeadline.HasValue &&
                         report.ReportedPartyReplyDeadline.Value < now))
                })
                .FirstOrDefaultAsync(cancellationToken);

            summary ??= new AdminReportSummaryDto();

            // عدادات المسارات الرئيسية مستقلة عن تبويبات الحالة.
            var scopeQuery = _db.Reports.AsNoTracking();

            summary.CustomerAgainstOwnerCount = await ApplyScope(
                    scopeQuery,
                    AdminReportValues.ScopeCustomerAgainstOwner)
                .CountAsync(cancellationToken);

            summary.OwnerAgainstCustomerCount = await ApplyScope(
                    scopeQuery,
                    AdminReportValues.ScopeOwnerAgainstCustomer)
                .CountAsync(cancellationToken);

            summary.PlatformCount = await ApplyScope(
                    scopeQuery,
                    AdminReportValues.ScopePlatform)
                .CountAsync(cancellationToken);

            return summary;
        }

        public async Task<AdminReportsListPageDto> GetReportsListAsync(
            string? tab,
            AdminReportFilterDto filter,
            CancellationToken cancellationToken = default)
        {
            var normalizedTab = AdminReportValues.NormalizeTab(tab);
            filter ??= new AdminReportFilterDto();
            NormalizeFilter(filter);

            var query = _db.Reports
                .AsNoTracking()
                .Include(report => report.Reporter)
                .Include(report => report.AgainstUser)
                .Include(report => report.Place)
                    .ThenInclude(place => place!.Owner)
                .Include(report => report.Booking)
                    .ThenInclude(booking => booking!.Place)
                        .ThenInclude(place => place!.Owner)
                .AsSplitQuery()
                .AsQueryable();

            query = ApplyScope(query, filter.Scope);
            query = ApplyTab(query, normalizedTab);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                query = query.Where(report =>
                    report.ReportNumber.Contains(search) ||
                    (report.Title != null && report.Title.Contains(search)) ||
                    report.Reporter.FullName.Contains(search) ||
                    (report.AgainstUser != null && report.AgainstUser.FullName.Contains(search)) ||
                    (report.Place != null && report.Place.PlaceNameAndDesc.Contains(search)) ||
                    (report.Booking != null && report.Booking.Place != null &&
                     report.Booking.Place.PlaceNameAndDesc.Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(filter.Status) &&
                ReportConstants.Statuses.IsValid(filter.Status))
            {
                var status = FindCanonical(ReportConstants.Statuses.All, filter.Status)!;
                query = query.Where(report => report.ReportStatus == status);
            }

            if (!string.IsNullOrWhiteSpace(filter.ReportType) &&
                ReportConstants.Types.IsValid(filter.ReportType))
            {
                var reportType = FindCanonical(ReportConstants.Types.All, filter.ReportType)!;
                query = query.Where(report => report.ReportType == reportType);
            }

            if (!string.IsNullOrWhiteSpace(filter.Priority) &&
                ReportConstants.Priorities.IsValid(filter.Priority))
            {
                var priority = FindCanonical(ReportConstants.Priorities.All, filter.Priority)!;
                query = query.Where(report => report.Priority == priority);
            }

            if (filter.DateFrom.HasValue)
            {
                var from = filter.DateFrom.Value.Date;
                query = query.Where(report =>
                    (report.CreatedAt ?? report.UpdatedAt) >= from);
            }

            if (filter.DateTo.HasValue)
            {
                var toExclusive = filter.DateTo.Value.Date.AddDays(1);
                query = query.Where(report =>
                    (report.CreatedAt ?? report.UpdatedAt) < toExclusive);
            }

            if (filter.IsOverdueOnly)
            {
                var now = DateTime.Now;
                query = query.Where(report =>
                    (report.ReportStatus == ReportConstants.Statuses.WaitingForReporter &&
                     report.ReporterReplyDeadline.HasValue &&
                     report.ReporterReplyDeadline.Value < now) ||
                    (report.ReportStatus == ReportConstants.Statuses.WaitingForReportedParty &&
                     report.ReportedPartyReplyDeadline.HasValue &&
                     report.ReportedPartyReplyDeadline.Value < now));
            }

            var filteredCount = await query.CountAsync(cancellationToken);
            var totalPages = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)filter.PageSize));
            if (filter.Page > totalPages) filter.Page = totalPages;

            var rows = await query
                .OrderByDescending(report => report.ReportStatus == ReportConstants.Statuses.Escalated)
                .ThenByDescending(report => report.Priority == ReportConstants.Priorities.Urgent)
                .ThenByDescending(report => report.UpdatedAt)
                .ThenByDescending(report => report.ReportId)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync(cancellationToken);

            var nowForCards = DateTime.Now;
            var items = rows.Select(report => BuildListItem(report, nowForCards)).ToList();

            // نوحّد عرض سجل "المبلّغ ضده" في كروت المركز للمالك والعميل معًا.
            // نحسبه دفعة واحدة لتجنب N+1 queries.
            await PopulateAgainstHistoryForListAsync(
                rows,
                items,
                cancellationToken);

            return new AdminReportsListPageDto
            {
                Tab = normalizedTab,
                Filter = filter,
                Items = items,
                FilteredCount = filteredCount,
                CurrentPage = filter.Page,
                PageSize = filter.PageSize,
                TotalPages = totalPages
            };
        }

        private static IQueryable<Report> ApplyScope(
            IQueryable<Report> query,
            string? scope)
        {
            return AdminReportValues.NormalizeScope(scope) switch
            {
                AdminReportValues.ScopeCustomerAgainstOwner => query.Where(report =>
                    report.Reporter.Role == "Customer" &&
                    report.ReportType != ReportConstants.Types.Platform &&
                    report.ReportType != ReportConstants.Types.Technical &&
                    (
                        (report.AgainstUser != null && report.AgainstUser.Role == "Owner") ||
                        (
                            report.AgainstUserId == null &&
                            (
                                (report.Place != null && report.Place.Owner != null) ||
                                (report.Booking != null &&
                                 report.Booking.Place != null &&
                                 report.Booking.Place.Owner != null)
                            )
                        )
                    )),

                AdminReportValues.ScopeOwnerAgainstCustomer => query.Where(report =>
                    report.Reporter.Role == "Owner" &&
                    report.AgainstUser != null &&
                    report.AgainstUser.Role == "Customer"),

                AdminReportValues.ScopePlatform => query.Where(report =>
                    report.ReportType == ReportConstants.Types.Platform ||
                    report.ReportType == ReportConstants.Types.Technical ||
                    (
                        report.AgainstUserId == null &&
                        report.PlaceId == null &&
                        report.BookingId == null
                    )),

                _ => query
            };
        }

        private static IQueryable<Report> ApplyTab(IQueryable<Report> query, string tab)
        {
            return tab switch
            {
                AdminReportValues.TabNew => query.Where(report =>
                    report.ReportStatus == ReportConstants.Statuses.Pending),

                AdminReportValues.TabAction => query.Where(report =>
                    report.ReportStatus == ReportConstants.Statuses.InReview),

                AdminReportValues.TabWaiting => query.Where(report =>
                    report.ReportStatus == ReportConstants.Statuses.WaitingForReporter ||
                    report.ReportStatus == ReportConstants.Statuses.WaitingForReportedParty),

                AdminReportValues.TabEscalated => query.Where(report =>
                    report.ReportStatus == ReportConstants.Statuses.Escalated),

                AdminReportValues.TabCompleted => query.Where(report =>
                    report.ReportStatus == ReportConstants.Statuses.Resolved ||
                    report.ReportStatus == ReportConstants.Statuses.Rejected ||
                    report.ReportStatus == ReportConstants.Statuses.Closed ||
                    report.ReportStatus == ReportConstants.Statuses.Withdrawn),

                _ => query
            };
        }

        private static void NormalizeFilter(AdminReportFilterDto filter)
        {
            filter.Search = NormalizeOptional(filter.Search);
            filter.Scope = AdminReportValues.NormalizeScope(filter.Scope);
            filter.Status = NormalizeOptional(filter.Status);
            filter.ReportType = NormalizeOptional(filter.ReportType);
            filter.Priority = NormalizeOptional(filter.Priority);
            filter.Page = Math.Max(1, filter.Page);
            filter.PageSize = Math.Clamp(filter.PageSize, 5, 50);

            if (filter.DateFrom.HasValue &&
                filter.DateTo.HasValue &&
                filter.DateFrom.Value.Date > filter.DateTo.Value.Date)
            {
                (filter.DateFrom, filter.DateTo) = (filter.DateTo, filter.DateFrom);
            }
        }

        /* =====================================================================
           تفاصيل القضية
           ===================================================================== */

        public async Task<AdminReportDetailsDto?> GetReportDetailsAsync(
            int reportId,
            CancellationToken cancellationToken = default)
        {
            if (reportId <= 0) return null;

            var report = await _db.Reports
                .AsNoTracking()
                .Include(item => item.Reporter)
                .Include(item => item.AgainstUser)
                .Include(item => item.ResolvedByUser)
                .Include(item => item.Place)
                    .ThenInclude(place => place!.Owner)
                .Include(item => item.Booking)
                    .ThenInclude(booking => booking!.Place)
                        .ThenInclude(place => place!.Owner)
                .Include(item => item.Booking)
                    .ThenInclude(booking => booking!.Customer)
                .Include(item => item.Review)
                    .ThenInclude(review => review!.Place)
                .Include(item => item.Review)
                    .ThenInclude(review => review!.User)
                .Include(item => item.Promotion)
                    .ThenInclude(promotion => promotion!.Place)
                .Include(item => item.Advertisement)
                    .ThenInclude(advertisement => advertisement!.Place)
                .AsSplitQuery()
                .FirstOrDefaultAsync(item => item.ReportId == reportId, cancellationToken);

            if (report == null) return null;

            var chats = await _db.Chats
                .AsNoTracking()
                .Where(chat => chat.ReportId == reportId && chat.IsReportMessage)
                .OrderBy(chat => chat.SendTime)
                .ThenBy(chat => chat.MessageId)
                .Select(chat => new ReportChatProjection
                {
                    MessageId = chat.MessageId,
                    SenderId = chat.SenderId,
                    SenderName = chat.Sender.FullName,
                    SenderRole = chat.Sender.Role,
                    ReceiverId = chat.ReceiverId,
                    ReceiverName = chat.Receiver.FullName,
                    ReceiverRole = chat.Receiver.Role,
                    MessageText = chat.MessageText,
                    AttachmentUrl = chat.AttachmentUrl,
                    SendTime = chat.SendTime
                })
                .ToListAsync(cancellationToken);

            var auditLogs = await _db.AdminAuditLogs
                .AsNoTracking()
                .Include(log => log.User)
                .Where(log =>
                    log.EntityType == ReportEntityType &&
                    log.EntityId == reportId.ToString())
                .OrderBy(log => log.OccurredAt)
                .ThenBy(log => log.AdminAuditLogId)
                .ToListAsync(cancellationToken);

            var effectiveAgainstUser = GetEffectiveAgainstUser(report);
            var reporterParty = await BuildPartyAsync(
                report.Reporter,
                report.ReportId,
                cancellationToken);

            var reportedParty = effectiveAgainstUser == null
                ? EmptyReportedParty()
                : await BuildPartyAsync(
                    effectiveAgainstUser,
                    report.ReportId,
                    cancellationToken);

            var scenario = GetScenario(report, effectiveAgainstUser);
            var now = DateTime.Now;
            var createdAt = report.CreatedAt ?? report.UpdatedAt;
            var replyDeadline = GetReplyDeadline(report);
            var isCompleted = IsCompleted(report.ReportStatus);
            var confirmationState = GetCustomerResolutionConfirmationState(report.ReporterReply);

            var isCustomerAgainstOwner = scenario == AdminReportValues.ScenarioCustomerAgainstOwner;
            var isOwnerAgainstCustomer = scenario == AdminReportValues.ScenarioOwnerAgainstCustomer;
            var isPlatformCase = scenario == AdminReportValues.ScenarioPlatform;

            return new AdminReportDetailsDto
            {
                ReportId = report.ReportId,
                ReportNumber = report.ReportNumber,
                RequestKind = report.RequestKind,
                RequestKindLabel = ReportConstants.GetRequestKindLabel(report.RequestKind),
                ReportType = report.ReportType,
                ReportTypeLabel = AdminReportValues.GetReportTypeLabel(
                    report.ReportType,
                    report.OtherReportTypeDescription),
                TypeIcon = AdminReportValues.GetTypeIcon(report.ReportType),
                Category = report.Category,
                CategoryLabel = AdminReportValues.GetCategoryLabel(
                    report.Category,
                    report.OtherCategoryDescription),
                Title = BuildTitle(report),
                ReportReason = report.ReportReason,
                Status = report.ReportStatus,
                StatusLabel = AdminReportValues.GetAdminStatusLabel(report.ReportStatus),
                Priority = report.Priority,
                PriorityLabel = AdminReportValues.GetPriorityLabel(report.Priority),
                RequiredAction = report.RequiredAction,
                RequiredActionLabel = AdminReportValues.GetRequiredActionLabel(report.RequiredAction),
                Scenario = scenario,
                ScenarioLabel = AdminReportValues.GetScenarioLabel(scenario),
                DirectionLabel = BuildDirectionLabel(report.Reporter, effectiveAgainstUser, scenario),
                IsCustomerAgainstOwner = isCustomerAgainstOwner,
                IsOwnerAgainstCustomer = isOwnerAgainstCustomer,
                IsPlatformCase = isPlatformCase,
                CurrentStepLabel = GetCurrentStepLabel(report, scenario),
                CurrentStepDescription = GetCurrentStepDescription(report, scenario),
                EvidenceImageUrl = NormalizePublicPath(report.EvidenceImage),
                CreatedAt = createdAt,
                UpdatedAt = report.UpdatedAt,
                ReplyDeadline = replyDeadline,
                IsOverdue = replyDeadline.HasValue && replyDeadline.Value < now && !isCompleted,
                IsUrgent =
                    ReportConstants.EqualsState(report.Priority, ReportConstants.Priorities.Urgent) ||
                    ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Escalated),
                AdminComment = report.AdminComment,
                ReporterReply = report.ReporterReply,
                ReporterReplyDate = report.ReporterReplyDate,
                OwnerReply = report.OwnerReply,
                OwnerReplyDate = report.OwnerReplyDate,
                EscalatedAt = report.EscalatedAt,
                ResolvedAt = report.ResolvedAt,
                Resolution = report.Resolution,
                ResolutionAction = report.ResolutionAction,
                ResolvedByAdminName = report.ResolvedByUser?.FullName,
                ClosedAt = report.ClosedAt,
                WithdrawnAt = report.WithdrawnAt,
                WithdrawnReason = report.WithdrawnReason,
                WarningIssuedAt = report.WarningIssuedAt,
                CustomerResolutionConfirmationState = confirmationState,
                Reporter = reporterParty,
                ReportedParty = reportedParty,
                Reference = BuildReference(report),
                Timeline = BuildTimeline(report, chats, auditLogs),
                InternalNotes = BuildInternalNotes(auditLogs),

                CanStartReview = ReportConstants.EqualsState(
                    report.ReportStatus,
                    ReportConstants.Statuses.Pending),

                CanRequestReporterClarification = CanRequestExternalInput(report.ReportStatus),

                CanRequestOwnerResponse =
                    isCustomerAgainstOwner &&
                    CanRequestExternalInput(report.ReportStatus) &&
                    effectiveAgainstUser != null &&
                    IsRole(effectiveAgainstUser, "Owner"),

                CanRequestResolutionConfirmation =
                    !isOwnerAgainstCustomer &&
                    IsRole(report.Reporter, "Customer") &&
                    ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Resolved) &&
                    !string.IsNullOrWhiteSpace(report.Resolution) &&
                    report.ResolvedAt.HasValue,

                CanChangePriority = !isCompleted,
                CanEscalate =
                    !isCompleted &&
                    !ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Escalated),
                CanAddInternalNote = true,
                CanResolve = !isOwnerAgainstCustomer && CanMakeDecision(report.ReportStatus),
                CanReject =
                    !isOwnerAgainstCustomer &&
                    !isCompleted &&
                    !ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Pending),
                CanConfirmCustomerViolation =
                    isOwnerAgainstCustomer && CanMakeDecision(report.ReportStatus),
                CanDismissCustomerViolation =
                    isOwnerAgainstCustomer && CanMakeDecision(report.ReportStatus),
                CanClose = CanCloseReport(report, confirmationState),
                CanReopen = CanReopen(report.ReportStatus)
            };
        }

        private async Task<AdminReportPartyDto> BuildPartyAsync(
            User user,
            int currentReportId,
            CancellationToken cancellationToken)
        {
            var isOwner = IsRole(user, "Owner");

            var filedQuery = _db.Reports
                .AsNoTracking()
                .Where(report =>
                    report.ReportId != currentReportId &&
                    report.ReporterId == user.UserId);

            // "ضده" تعني AgainstUserID، ومع المالك نغطي كذلك البلاغات القديمة
            // التي كانت مرتبطة بالمنشأة فقط قبل تثبيت AgainstUserID.
            var againstQuery = _db.Reports
                .AsNoTracking()
                .Where(report =>
                    report.ReportId != currentReportId &&
                    (
                        report.AgainstUserId == user.UserId ||
                        (
                            isOwner &&
                            report.AgainstUserId == null &&
                            (
                                (report.Place != null &&
                                 report.Place.Owner != null &&
                                 report.Place.Owner.UserId == user.UserId) ||
                                (report.Booking != null &&
                                 report.Booking.Place != null &&
                                 report.Booking.Place.Owner != null &&
                                 report.Booking.Place.Owner.UserId == user.UserId)
                            )
                        )
                    ));

            var reportsFiledCount = await filedQuery.CountAsync(
                report => report.RequestKind == ReportConstants.RequestKinds.Report,
                cancellationToken);

            var complaintsFiledCount = await filedQuery.CountAsync(
                report => report.RequestKind == ReportConstants.RequestKinds.Complaint,
                cancellationToken);

            var reportsAgainstCount = await againstQuery.CountAsync(
                report => report.RequestKind == ReportConstants.RequestKinds.Report,
                cancellationToken);

            var complaintsAgainstCount = await againstQuery.CountAsync(
                report => report.RequestKind == ReportConstants.RequestKinds.Complaint,
                cancellationToken);

            var confirmedViolationsCount = await againstQuery.CountAsync(
                report => report.WarningIssuedAt.HasValue,
                cancellationToken);

            var recentRows = await againstQuery
                .OrderByDescending(report => report.CreatedAt ?? report.UpdatedAt)
                .ThenByDescending(report => report.ReportId)
                .Take(5)
                .ToListAsync(cancellationToken);

            var recentCases = recentRows
                .Select(item => new AdminReportPartyHistoryItemDto
                {
                    ReportId = item.ReportId,
                    ReportNumber = item.ReportNumber,
                    RequestKindLabel = ReportConstants.GetRequestKindLabel(item.RequestKind),
                    Title = BuildTitle(item),
                    StatusLabel = AdminReportValues.GetAdminStatusLabel(item.ReportStatus),
                    CreatedAt = item.CreatedAt ?? item.UpdatedAt,
                    HasWarning = item.WarningIssuedAt.HasValue
                })
                .ToList();

            return new AdminReportPartyDto
            {
                UserId = user.UserId,
                FullName = GetDisplayName(user, "مستخدم"),
                Role = user.Role,
                RoleLabel = AdminReportValues.GetRoleLabel(user.Role),
                ProfileImage = user.ProfileImage,
                AccountState = user.AccountState ?? string.Empty,
                AccountStateLabel = GetAccountStateLabel(user.AccountState),
                WarningCount = Math.Max(0, user.WarningCount),
                PreviousReportsCount =
                    reportsFiledCount +
                    complaintsFiledCount +
                    reportsAgainstCount +
                    complaintsAgainstCount,
                ReportsFiledCount = reportsFiledCount,
                ComplaintsFiledCount = complaintsFiledCount,
                ReportsAgainstCount = reportsAgainstCount,
                ComplaintsAgainstCount = complaintsAgainstCount,
                ConfirmedViolationsCount = confirmedViolationsCount,
                RecentCasesAgainst = recentCases
            };
        }

        private static AdminReportPartyDto EmptyReportedParty() => new()
        {
            UserId = null,
            FullName = "لا يوجد طرف مبلّغ ضده",
            Role = string.Empty,
            RoleLabel = "—",
            AccountStateLabel = "—"
        };

        private static AdminReportReferenceDto BuildReference(Report report)
        {
            if (report.BookingId.HasValue)
            {
                return new AdminReportReferenceDto
                {
                    ReferenceType = ReportConstants.ReferenceTypes.Booking,
                    PrimaryLabel = $"الحجز #{report.BookingId.Value}",
                    SecondaryLabel = report.Booking?.Place?.PlaceNameAndDesc,
                    ReferenceId = report.BookingId,
                    PlaceId = report.Booking?.PlaceId ?? report.PlaceId,
                    PlaceName = report.Booking?.Place?.PlaceNameAndDesc ?? report.Place?.PlaceNameAndDesc,
                    ExtraInfo = report.Booking == null
                        ? null
                        : $"من {report.Booking.StartDate:yyyy/MM/dd}" +
                          (report.Booking.EndDate.HasValue
                              ? $" إلى {report.Booking.EndDate.Value:yyyy/MM/dd}"
                              : string.Empty)
                };
            }

            if (report.ReviewId.HasValue)
            {
                return new AdminReportReferenceDto
                {
                    ReferenceType = ReportConstants.ReferenceTypes.Review,
                    PrimaryLabel = $"التقييم #{report.ReviewId.Value}",
                    SecondaryLabel = report.Review?.Place?.PlaceNameAndDesc,
                    ReferenceId = report.ReviewId,
                    PlaceId = report.Review?.PlaceId ?? report.PlaceId,
                    PlaceName = report.Review?.Place?.PlaceNameAndDesc ?? report.Place?.PlaceNameAndDesc,
                    ExtraInfo = report.Review?.Stars.HasValue == true
                        ? $"التقييم: {report.Review.Stars.Value}/5"
                        : null
                };
            }

            if (report.PromotionId.HasValue)
            {
                return new AdminReportReferenceDto
                {
                    ReferenceType = ReportConstants.ReferenceTypes.Promotion,
                    PrimaryLabel = $"العرض #{report.PromotionId.Value}",
                    SecondaryLabel = report.Promotion?.Place?.PlaceNameAndDesc,
                    ReferenceId = report.PromotionId,
                    PlaceId = report.Promotion?.PlaceId ?? report.PlaceId,
                    PlaceName = report.Promotion?.Place?.PlaceNameAndDesc ?? report.Place?.PlaceNameAndDesc
                };
            }

            if (report.AdvertisementId.HasValue)
            {
                return new AdminReportReferenceDto
                {
                    ReferenceType = ReportConstants.ReferenceTypes.Advertisement,
                    PrimaryLabel = $"الإعلان #{report.AdvertisementId.Value}",
                    SecondaryLabel = report.Advertisement?.Place?.PlaceNameAndDesc,
                    ReferenceId = report.AdvertisementId,
                    PlaceId = report.Advertisement?.PlaceId ?? report.PlaceId,
                    PlaceName = report.Advertisement?.Place?.PlaceNameAndDesc ?? report.Place?.PlaceNameAndDesc
                };
            }

            if (report.PlaceId.HasValue)
            {
                return new AdminReportReferenceDto
                {
                    ReferenceType = ReportConstants.ReferenceTypes.Place,
                    PrimaryLabel = report.Place?.PlaceNameAndDesc ?? $"المنشأة #{report.PlaceId.Value}",
                    ReferenceId = report.PlaceId,
                    PlaceId = report.PlaceId,
                    PlaceName = report.Place?.PlaceNameAndDesc
                };
            }

            return new AdminReportReferenceDto();
        }

        /* =====================================================================
           إجراءات الإدارة
           ===================================================================== */

        public Task<AdminReportOperationResultDto> StartReviewAsync(
            AdminReportSimpleActionDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default)
        {
            return ExecuteMutationAsync(
                input.ReportId,
                currentAdminUserId,
                requestContext,
                report =>
                {
                    if (!ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Pending))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "يمكن بدء المراجعة للقضايا الجديدة فقط.",
                            "invalid-status"));
                    }

                    var oldStatus = report.ReportStatus;
                    var now = DateTime.Now;

                    report.ReportStatus = ReportConstants.Statuses.InReview;
                    ClearWaitingState(report);
                    report.UpdatedAt = now;

                    AddAuditLog(
                        currentAdminUserId,
                        ReviewStartedAction,
                        report.ReportId,
                        "تم بدء المراجعة الإدارية للقضية.",
                        new { Status = oldStatus },
                        new { Status = report.ReportStatus },
                        requestContext);

                    return Task.FromResult(AdminReportOperationResultDto.Ok(
                        "تم بدء مراجعة القضية.",
                        report.ReportId));
                },
                cancellationToken);
        }

        public Task<AdminReportOperationResultDto> RequestClarificationAsync(
            AdminReportReplyRequestDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default)
        {
            input.Message = NormalizeRequired(input.Message);
            var deadlineError = ValidateDeadline(input.Deadline);
            if (deadlineError != null)
                return Task.FromResult(AdminReportOperationResultDto.Fail(deadlineError, "invalid-deadline"));

            return ExecuteMutationAsync(
                input.ReportId,
                currentAdminUserId,
                requestContext,
                report =>
                {
                    if (!CanRequestExternalInput(report.ReportStatus))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "ابدأ مراجعة القضية أولًا أو أنهِ الإجراء الحالي قبل طلب توضيح جديد.",
                            "invalid-status"));
                    }

                    var now = DateTime.Now;
                    var oldStatus = report.ReportStatus;
                    var oldAction = report.RequiredAction;

                    AddReportChat(currentAdminUserId, report.ReporterId, report, input.Message, now);

                    report.ReportStatus = ReportConstants.Statuses.WaitingForReporter;
                    report.RequiredAction = ReportConstants.RequiredActions.Clarification;
                    report.ReporterReplyDeadline = input.Deadline;
                    report.ReportedPartyReplyDeadline = null;
                    report.AdminComment = Truncate(input.Message, MaximumAdminCommentLength, string.Empty);
                    report.UpdatedAt = now;

                    AddAuditLog(
                        currentAdminUserId,
                        ClarificationRequestedAction,
                        report.ReportId,
                        $"تم طلب توضيح من المبلّغ حتى {input.Deadline!.Value:yyyy/MM/dd hh:mm tt}.",
                        new { Status = oldStatus, RequiredAction = oldAction },
                        new { Status = report.ReportStatus, report.RequiredAction, Deadline = input.Deadline },
                        requestContext);

                    QueueNotification(
                        report.ReporterId,
                        "القضية تحتاج توضيحك",
                        $"تحتاج القضية {report.ReportNumber} إلى توضيح منك قبل {input.Deadline.Value:yyyy/MM/dd hh:mm tt}.",
                        NotificationTypes.ReportReply,
                        ReporterDetailsUrl(report.ReportId, report.Reporter.Role),
                        report.ReportId);

                    return Task.FromResult(AdminReportOperationResultDto.Ok(
                        "تم إرسال طلب التوضيح إلى المبلّغ.",
                        report.ReportId));
                },
                cancellationToken);
        }

        public Task<AdminReportOperationResultDto> RequestOwnerResponseAsync(
            AdminReportReplyRequestDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default)
        {
            input.Message = NormalizeRequired(input.Message);
            var deadlineError = ValidateDeadline(input.Deadline);
            if (deadlineError != null)
                return Task.FromResult(AdminReportOperationResultDto.Fail(deadlineError, "invalid-deadline"));

            return ExecuteMutationAsync(
                input.ReportId,
                currentAdminUserId,
                requestContext,
                report =>
                {
                    if (!CanRequestExternalInput(report.ReportStatus))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "القضية ليست في حالة تسمح بطلب رد المالك الآن.",
                            "invalid-status"));
                    }

                    if (!IsRole(report.Reporter, "Customer"))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "طلب رد المالك متاح فقط عندما يكون المبلّغ عميلًا.",
                            "not-customer-report"));
                    }

                    var owner = GetEffectiveAgainstUser(report);
                    if (owner == null || !IsRole(owner, "Owner"))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "لا يوجد مالك محدد يمكن طلب الرد منه في هذه القضية.",
                            "owner-missing"));
                    }

                    var now = DateTime.Now;
                    var oldStatus = report.ReportStatus;
                    var oldAction = report.RequiredAction;

                    // لو البلاغ كان مربوطًا بالمنشأة فقط دون AgainstUser، نثبّت المالك رسميًا كالمبلّغ ضده.
                    if (!report.AgainstUserId.HasValue)
                    {
                        report.AgainstUserId = owner.UserId;
                    }

                    AddReportChat(currentAdminUserId, owner.UserId, report, input.Message, now);

                    report.ReportStatus = ReportConstants.Statuses.WaitingForReportedParty;
                    report.RequiredAction = ReportConstants.RequiredActions.ReportedPartyResponse;
                    report.ReportedPartyReplyDeadline = input.Deadline;
                    report.ReporterReplyDeadline = null;
                    report.AdminComment = Truncate(input.Message, MaximumAdminCommentLength, string.Empty);
                    report.UpdatedAt = now;

                    AddAuditLog(
                        currentAdminUserId,
                        OwnerResponseRequestedAction,
                        report.ReportId,
                        $"تم طلب رد من المالك حتى {input.Deadline!.Value:yyyy/MM/dd hh:mm tt}.",
                        new { Status = oldStatus, RequiredAction = oldAction },
                        new { Status = report.ReportStatus, report.RequiredAction, OwnerUserId = owner.UserId, Deadline = input.Deadline },
                        requestContext);

                    QueueNotification(
                        owner.UserId,
                        "لديك قضية تحتاج ردك",
                        $"طلبت الإدارة ردك على القضية {report.ReportNumber} قبل {input.Deadline.Value:yyyy/MM/dd hh:mm tt}.",
                        NotificationTypes.ReportReply,
                        ReporterDetailsUrl(report.ReportId, owner.Role),
                        report.ReportId);

                    return Task.FromResult(AdminReportOperationResultDto.Ok(
                        "تم إرسال طلب الرد إلى المالك.",
                        report.ReportId));
                },
                cancellationToken);
        }

        public Task<AdminReportOperationResultDto> RequestResolutionConfirmationAsync(
            AdminReportResolutionConfirmationRequestDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default)
        {
            input.Message = NormalizeOptional(input.Message);
            var deadlineError = ValidateDeadline(input.Deadline);
            if (deadlineError != null)
                return Task.FromResult(AdminReportOperationResultDto.Fail(deadlineError, "invalid-deadline"));

            return ExecuteMutationAsync(
                input.ReportId,
                currentAdminUserId,
                requestContext,
                report =>
                {
                    if (!IsRole(report.Reporter, "Customer"))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "تأكيد الحل يستخدم مع العميل المبلّغ فقط.",
                            "customer-confirmation-only"));
                    }

                    if (!ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Resolved) ||
                        string.IsNullOrWhiteSpace(report.Resolution) ||
                        !report.ResolvedAt.HasValue)
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "سجّل حل القضية أولًا ثم اطلب من العميل تأكيد الحل.",
                            "resolution-required"));
                    }

                    var now = DateTime.Now;
                    var message = string.IsNullOrWhiteSpace(input.Message)
                        ? $"سجلت الإدارة حلًا للقضية {report.ReportNumber}. هل تم حل المشكلة؟ اختر نعم أو لا من صفحة القضية."
                        : input.Message!;

                    AddReportChat(currentAdminUserId, report.ReporterId, report, message, now);

                    report.ReportStatus = ReportConstants.Statuses.WaitingForReporter;
                    report.RequiredAction = ReportConstants.RequiredActions.ResolutionConfirmation;
                    report.ReporterReplyDeadline = input.Deadline;
                    report.ReportedPartyReplyDeadline = null;
                    report.AdminComment = Truncate(message, MaximumAdminCommentLength, string.Empty);
                    report.UpdatedAt = now;

                    AddAuditLog(
                        currentAdminUserId,
                        ResolutionConfirmationRequestedAction,
                        report.ReportId,
                        $"تم طلب تأكيد الحل من العميل حتى {input.Deadline!.Value:yyyy/MM/dd hh:mm tt}.",
                        null,
                        new { Status = report.ReportStatus, report.RequiredAction, Deadline = input.Deadline },
                        requestContext);

                    QueueNotification(
                        report.ReporterId,
                        "هل تم حل المشكلة؟",
                        $"سجلت الإدارة حلًا للقضية {report.ReportNumber}. نحتاج تأكيدك من صفحة القضية.",
                        NotificationTypes.ReportReply,
                        ReporterDetailsUrl(report.ReportId, report.Reporter.Role),
                        report.ReportId);

                    return Task.FromResult(AdminReportOperationResultDto.Ok(
                        "تم إرسال طلب تأكيد الحل إلى العميل.",
                        report.ReportId));
                },
                cancellationToken);
        }

        public Task<AdminReportOperationResultDto> ChangePriorityAsync(
            AdminReportPriorityDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default)
        {
            input.Priority = NormalizeRequired(input.Priority);
            input.Reason = NormalizeRequired(input.Reason);

            if (!ReportConstants.Priorities.IsValid(input.Priority))
            {
                return Task.FromResult(AdminReportOperationResultDto.Fail(
                    "الأولوية المحددة غير صحيحة.",
                    "invalid-priority"));
            }

            return ExecuteMutationAsync(
                input.ReportId,
                currentAdminUserId,
                requestContext,
                report =>
                {
                    if (IsCompleted(report.ReportStatus))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "لا يمكن تغيير أولوية قضية منتهية.",
                            "case-completed"));
                    }

                    var oldPriority = report.Priority;
                    report.Priority = FindCanonical(ReportConstants.Priorities.All, input.Priority)
                                      ?? ReportConstants.Priorities.Normal;
                    report.UpdatedAt = DateTime.Now;

                    AddAuditLog(
                        currentAdminUserId,
                        PriorityChangedAction,
                        report.ReportId,
                        $"تم تغيير الأولوية إلى {AdminReportValues.GetPriorityLabel(report.Priority)}. السبب: {input.Reason}",
                        new { Priority = oldPriority },
                        new { report.Priority, input.Reason },
                        requestContext);

                    return Task.FromResult(AdminReportOperationResultDto.Ok(
                        "تم تحديث أولوية القضية.",
                        report.ReportId));
                },
                cancellationToken);
        }

        public Task<AdminReportOperationResultDto> EscalateAsync(
            AdminReportEscalationDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default)
        {
            input.Reason = NormalizeRequired(input.Reason);

            return ExecuteMutationAsync(
                input.ReportId,
                currentAdminUserId,
                requestContext,
                report =>
                {
                    if (IsCompleted(report.ReportStatus))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "لا يمكن تصعيد قضية منتهية.",
                            "case-completed"));
                    }

                    if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Escalated))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "القضية مصعّدة مسبقًا.",
                            "already-escalated"));
                    }

                    var oldStatus = report.ReportStatus;
                    var oldPriority = report.Priority;
                    var now = DateTime.Now;

                    report.ReportStatus = ReportConstants.Statuses.Escalated;
                    report.Priority = ReportConstants.Priorities.Urgent;
                    report.EscalatedAt = now;
                    ClearWaitingState(report);
                    report.UpdatedAt = now;

                    AddAuditLog(
                        currentAdminUserId,
                        EscalatedAction,
                        report.ReportId,
                        $"تم تصعيد القضية. السبب: {input.Reason}",
                        new { Status = oldStatus, Priority = oldPriority },
                        new { Status = report.ReportStatus, Priority = report.Priority, report.EscalatedAt, input.Reason },
                        requestContext);

                    return Task.FromResult(AdminReportOperationResultDto.Ok(
                        "تم تصعيد القضية ورفع أولويتها إلى عاجلة.",
                        report.ReportId));
                },
                cancellationToken);
        }

        public Task<AdminReportOperationResultDto> AddInternalNoteAsync(
            AdminReportInternalNoteRequestDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default)
        {
            input.NoteText = NormalizeRequired(input.NoteText);

            return ExecuteMutationAsync(
                input.ReportId,
                currentAdminUserId,
                requestContext,
                report =>
                {
                    AddAuditLog(
                        currentAdminUserId,
                        InternalNoteAction,
                        report.ReportId,
                        input.NoteText,
                        null,
                        new { input.NoteText },
                        requestContext);

                    // لا نغير UpdatedAt لأن الملاحظة داخلية ولا يجب أن تبدو كتحديث خارجي للقضية.
                    return Task.FromResult(AdminReportOperationResultDto.Ok(
                        "تم حفظ الملاحظة الداخلية.",
                        report.ReportId));
                },
                cancellationToken);
        }

        public Task<AdminReportOperationResultDto> ResolveAsync(
            AdminReportResolutionDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default)
        {
            input.Resolution = NormalizeRequired(input.Resolution);
            input.ResolutionAction = NormalizeRequired(input.ResolutionAction);

            return ExecuteMutationAsync(
                input.ReportId,
                currentAdminUserId,
                requestContext,
                report =>
                {
                    if (IsOwnerAgainstCustomer(report))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "بلاغ المالك ضد العميل له قرار مبسط: المخالفة مثبتة أو غير مثبتة.",
                            "use-customer-violation-decision"));
                    }

                    if (!CanMakeDecision(report.ReportStatus))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "القضية ليست في حالة تسمح بتسجيل حل الآن.",
                            "invalid-status"));
                    }

                    var now = DateTime.Now;
                    var oldStatus = report.ReportStatus;
                    var actionParts = new List<string> { input.ResolutionAction };
                    if (input.ReferToFinance) actionParts.Add("إحالة للمالية");
                    if (input.ReferToTechnicalSupport) actionParts.Add("إحالة للدعم التقني");

                    report.ReportStatus = ReportConstants.Statuses.Resolved;
                    report.Resolution = input.Resolution;
                    report.ResolutionAction = Truncate(
                        string.Join(" | ", actionParts),
                        100,
                        input.ResolutionAction);
                    report.ResolvedAt = now;
                    report.ResolvedByUserId = currentAdminUserId;
                    report.ClosedAt = null;
                    ClearWaitingState(report);
                    report.UpdatedAt = now;

                    AddAuditLog(
                        currentAdminUserId,
                        ResolvedAction,
                        report.ReportId,
                        "تم تسجيل حل القضية.",
                        new { Status = oldStatus },
                        new { Status = report.ReportStatus, report.Resolution, report.ResolutionAction, report.ResolvedAt },
                        requestContext);

                    QueueNotification(
                        report.ReporterId,
                        "تم تسجيل حل للقضية",
                        $"سجلت الإدارة حلًا للقضية {report.ReportNumber}.",
                        NotificationTypes.ReportResolved,
                        ReporterDetailsUrl(report.ReportId, report.Reporter.Role),
                        report.ReportId);

                    var owner = GetEffectiveAgainstUser(report);
                    if (owner != null && IsRole(owner, "Owner") && owner.UserId != report.ReporterId)
                    {
                        QueueNotification(
                            owner.UserId,
                            "تم تحديث نتيجة القضية",
                            $"سجلت الإدارة نتيجة للقضية {report.ReportNumber}.",
                            NotificationTypes.ReportResolved,
                            ReporterDetailsUrl(report.ReportId, owner.Role),
                            report.ReportId);
                    }

                    return Task.FromResult(AdminReportOperationResultDto.Ok(
                        "تم تسجيل حل القضية بنجاح.",
                        report.ReportId));
                },
                cancellationToken);
        }

        public Task<AdminReportOperationResultDto> RejectAsync(
            AdminReportRejectionDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default)
        {
            input.Reason = NormalizeRequired(input.Reason);

            return ExecuteMutationAsync(
                input.ReportId,
                currentAdminUserId,
                requestContext,
                report =>
                {
                    if (IsOwnerAgainstCustomer(report))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "بلاغ المالك ضد العميل يستخدم قرار «المخالفة غير مثبتة» بدل الرفض العام.",
                            "use-customer-violation-decision"));
                    }

                    if (IsCompleted(report.ReportStatus))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "القضية منتهية مسبقًا.",
                            "case-completed"));
                    }

                    if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Pending))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "ابدأ مراجعة القضية قبل إصدار قرار الرفض.",
                            "review-required"));
                    }

                    var oldStatus = report.ReportStatus;
                    var now = DateTime.Now;

                    report.ReportStatus = ReportConstants.Statuses.Rejected;
                    report.Resolution = input.Reason;
                    report.ResolutionAction = "Rejected";
                    report.ResolvedAt = null;
                    report.ResolvedByUserId = currentAdminUserId;
                    report.ClosedAt = null;
                    ClearWaitingState(report);
                    report.UpdatedAt = now;

                    AddAuditLog(
                        currentAdminUserId,
                        RejectedAction,
                        report.ReportId,
                        $"تم رفض القضية. السبب: {input.Reason}",
                        new { Status = oldStatus },
                        new { Status = report.ReportStatus, report.Resolution },
                        requestContext);

                    QueueNotification(
                        report.ReporterId,
                        "تم رفض القضية",
                        $"تم رفض القضية {report.ReportNumber}. راجع التفاصيل لمعرفة السبب.",
                        NotificationTypes.ReportRejected,
                        ReporterDetailsUrl(report.ReportId, report.Reporter.Role),
                        report.ReportId);

                    return Task.FromResult(AdminReportOperationResultDto.Ok(
                        "تم رفض القضية وتسجيل السبب.",
                        report.ReportId));
                },
                cancellationToken);
        }

        public async Task<AdminReportOperationResultDto> ConfirmCustomerViolationAsync(
            AdminCustomerViolationDecisionDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default)
        {
            input.Reason = NormalizeRequired(input.Reason);

            return await ExecuteMutationAsync(
                input.ReportId,
                currentAdminUserId,
                requestContext,
                async report =>
                {
                    if (!IsOwnerAgainstCustomer(report))
                    {
                        return AdminReportOperationResultDto.Fail(
                            "هذا الإجراء مخصص لبلاغ المالك ضد العميل فقط.",
                            "invalid-scenario");
                    }

                    if (!CanMakeDecision(report.ReportStatus))
                    {
                        return AdminReportOperationResultDto.Fail(
                            "ابدأ مراجعة القضية أولًا قبل تثبيت المخالفة.",
                            "invalid-status");
                    }

                    if (!report.AgainstUserId.HasValue)
                    {
                        return AdminReportOperationResultDto.Fail(
                            "لا يوجد عميل محدد في هذه القضية.",
                            "customer-missing");
                    }

                    var customer = await _db.Users
                        .FirstOrDefaultAsync(
                            user => user.UserId == report.AgainstUserId.Value,
                            cancellationToken);

                    if (customer == null || !IsRole(customer, "Customer"))
                    {
                        return AdminReportOperationResultDto.Fail(
                            "تعذر العثور على العميل المبلّغ ضده.",
                            "customer-missing");
                    }

                    var now = DateTime.Now;
                    var oldStatus = report.ReportStatus;
                    var oldWarningCount = customer.WarningCount;

                    customer.WarningCount = Math.Max(0, customer.WarningCount) + 1;
                    customer.LastWarningDate = now;

                    report.ReportStatus = ReportConstants.Statuses.Resolved;
                    report.Resolution = input.Reason;
                    report.ResolutionAction = CustomerViolationConfirmedResolutionAction;
                    report.ResolvedAt = now;
                    report.ResolvedByUserId = currentAdminUserId;
                    report.WarningIssuedAt = now;
                    report.ClosedAt = null;
                    ClearWaitingState(report);
                    report.UpdatedAt = now;

                    AddAuditLog(
                        currentAdminUserId,
                        CustomerViolationConfirmedAction,
                        report.ReportId,
                        $"تم إثبات مخالفة العميل وإصدار تنبيه. السبب: {input.Reason}",
                        new { Status = oldStatus, WarningCount = oldWarningCount },
                        new { Status = report.ReportStatus, WarningCount = customer.WarningCount, report.WarningIssuedAt },
                        requestContext);

                    // المالك المبلّغ يعرف نتيجة بلاغه.
                    QueueNotification(
                        report.ReporterId,
                        "تم إثبات المخالفة",
                        $"أكدت الإدارة المخالفة في القضية {report.ReportNumber} وتم تسجيل تنبيه على حساب العميل.",
                        NotificationTypes.ReportResolved,
                        ReporterDetailsUrl(report.ReportId, report.Reporter.Role),
                        report.ReportId);

                    // الاتفاق النهائي: العميل لا يدخل القضية ولا يطلب منه رد؛ يستلم تنبيهًا فقط.
                    QueueNotification(
                        customer.UserId,
                        "تنبيه على حسابك",
                        $"تم تسجيل مخالفة على حسابك. عدد التنبيهات الحالية: {customer.WarningCount}. في حال تكرار المخالفات قد يتم اتخاذ إجراءات على الحساب بما في ذلك الحظر.",
                        NotificationTypes.ReportResolved,
                        targetUrl: null,
                        report.ReportId);

                    return AdminReportOperationResultDto.Ok(
                        "تم إثبات المخالفة وإرسال التنبيه للعميل.",
                        report.ReportId);
                },
                cancellationToken);
        }

        public Task<AdminReportOperationResultDto> DismissCustomerViolationAsync(
            AdminCustomerViolationDecisionDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default)
        {
            input.Reason = NormalizeRequired(input.Reason);

            return ExecuteMutationAsync(
                input.ReportId,
                currentAdminUserId,
                requestContext,
                report =>
                {
                    if (!IsOwnerAgainstCustomer(report))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "هذا الإجراء مخصص لبلاغ المالك ضد العميل فقط.",
                            "invalid-scenario"));
                    }

                    if (!CanMakeDecision(report.ReportStatus))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "ابدأ مراجعة القضية أولًا قبل إصدار القرار.",
                            "invalid-status"));
                    }

                    var oldStatus = report.ReportStatus;
                    var now = DateTime.Now;

                    report.ReportStatus = ReportConstants.Statuses.Rejected;
                    report.Resolution = input.Reason;
                    report.ResolutionAction = CustomerViolationNotConfirmedResolutionAction;
                    report.ResolvedAt = null;
                    report.ResolvedByUserId = currentAdminUserId;
                    report.WarningIssuedAt = null;
                    report.ClosedAt = null;
                    ClearWaitingState(report);
                    report.UpdatedAt = now;

                    AddAuditLog(
                        currentAdminUserId,
                        CustomerViolationNotConfirmedAction,
                        report.ReportId,
                        $"لم تثبت مخالفة العميل. السبب: {input.Reason}",
                        new { Status = oldStatus },
                        new { Status = report.ReportStatus, report.Resolution },
                        requestContext);

                    // لا نرسل للعميل شيئًا لأن المخالفة لم تثبت.
                    QueueNotification(
                        report.ReporterId,
                        "لم تثبت المخالفة",
                        $"لم تثبت المخالفة في القضية {report.ReportNumber}. راجع تفاصيل القرار.",
                        NotificationTypes.ReportRejected,
                        ReporterDetailsUrl(report.ReportId, report.Reporter.Role),
                        report.ReportId);

                    return Task.FromResult(AdminReportOperationResultDto.Ok(
                        "تم تسجيل أن المخالفة غير مثبتة.",
                        report.ReportId));
                },
                cancellationToken);
        }

        public Task<AdminReportOperationResultDto> CloseAsync(
            AdminReportCloseDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default)
        {
            input.Reason = NormalizeRequired(input.Reason);

            return ExecuteMutationAsync(
                input.ReportId,
                currentAdminUserId,
                requestContext,
                report =>
                {
                    var confirmationState = GetCustomerResolutionConfirmationState(report.ReporterReply);
                    if (!CanCloseReport(report, confirmationState))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "لا يمكن إغلاق القضية الآن. يجب وجود نتيجة معالجة مسجلة، وإذا طُلب تأكيد العميل فيجب أن يكون قد أكد الحل.",
                            "close-not-allowed"));
                    }

                    if (string.IsNullOrWhiteSpace(report.Resolution))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "لا يمكن إغلاق القضية دون نتيجة معالجة مسجلة.",
                            "resolution-required"));
                    }

                    var oldStatus = report.ReportStatus;
                    var now = DateTime.Now;

                    report.ReportStatus = ReportConstants.Statuses.Closed;
                    report.ClosedAt = now;
                    ClearWaitingState(report);
                    report.UpdatedAt = now;

                    AddAuditLog(
                        currentAdminUserId,
                        ClosedAction,
                        report.ReportId,
                        $"تم إغلاق القضية إداريًا. الملخص: {input.Reason}",
                        new { Status = oldStatus },
                        new { Status = report.ReportStatus, report.ClosedAt, input.Reason },
                        requestContext);

                    QueueNotification(
                        report.ReporterId,
                        "تم إغلاق القضية",
                        $"أغلقت الإدارة القضية {report.ReportNumber} رسميًا.",
                        NotificationTypes.ReportResolved,
                        ReporterDetailsUrl(report.ReportId, report.Reporter.Role),
                        report.ReportId);

                    // في بلاغ المالك ضد العميل: العميل لا يتابع القضية، لذلك لا نرسل له إشعار إغلاق.
                    if (!IsOwnerAgainstCustomer(report))
                    {
                        var owner = GetEffectiveAgainstUser(report);
                        if (owner != null && IsRole(owner, "Owner") && owner.UserId != report.ReporterId)
                        {
                            QueueNotification(
                                owner.UserId,
                                "تم إغلاق القضية",
                                $"أغلقت الإدارة القضية {report.ReportNumber} رسميًا.",
                                NotificationTypes.ReportResolved,
                                ReporterDetailsUrl(report.ReportId, owner.Role),
                                report.ReportId);
                        }
                    }

                    return Task.FromResult(AdminReportOperationResultDto.Ok(
                        "تم إغلاق القضية رسميًا.",
                        report.ReportId));
                },
                cancellationToken);
        }

        public Task<AdminReportOperationResultDto> ReopenAsync(
            AdminReportReopenDto input,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            CancellationToken cancellationToken = default)
        {
            input.Reason = NormalizeRequired(input.Reason);

            return ExecuteMutationAsync(
                input.ReportId,
                currentAdminUserId,
                requestContext,
                report =>
                {
                    if (!CanReopen(report.ReportStatus))
                    {
                        return Task.FromResult(AdminReportOperationResultDto.Fail(
                            "يمكن إعادة فتح القضايا المحلولة أو المرفوضة أو المغلقة فقط.",
                            "reopen-not-allowed"));
                    }

                    var oldStatus = report.ReportStatus;
                    var now = DateTime.Now;

                    report.ReportStatus = ReportConstants.Statuses.InReview;
                    report.ClosedAt = null;
                    ClearWaitingState(report);
                    report.UpdatedAt = now;

                    AddAuditLog(
                        currentAdminUserId,
                        ReopenedAction,
                        report.ReportId,
                        $"تمت إعادة فتح القضية. السبب: {input.Reason}",
                        new { Status = oldStatus },
                        new { Status = report.ReportStatus, input.Reason },
                        requestContext);

                    QueueNotification(
                        report.ReporterId,
                        "تمت إعادة فتح القضية",
                        $"أعادت الإدارة فتح القضية {report.ReportNumber} للمراجعة.",
                        NotificationTypes.ReportReply,
                        ReporterDetailsUrl(report.ReportId, report.Reporter.Role),
                        report.ReportId);

                    return Task.FromResult(AdminReportOperationResultDto.Ok(
                        "تمت إعادة فتح القضية وإعادتها للمراجعة.",
                        report.ReportId));
                },
                cancellationToken);
        }

        /* =====================================================================
           Mutation / persistence helpers
           ===================================================================== */

        private async Task<AdminReportOperationResultDto> ExecuteMutationAsync(
            int reportId,
            int currentAdminUserId,
            AdminReportRequestContextDto requestContext,
            Func<Report, Task<AdminReportOperationResultDto>> mutation,
            CancellationToken cancellationToken)
        {
            if (reportId <= 0)
                return AdminReportOperationResultDto.Fail("رقم القضية غير صحيح.", "invalid-report-id");

            if (currentAdminUserId <= 0)
                return AdminReportOperationResultDto.Fail("جلسة الإدارة غير صالحة.", "invalid-admin");

            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            try
            {
                var report = await _db.Reports
                    .Include(item => item.Reporter)
                    .Include(item => item.AgainstUser)
                    .Include(item => item.Place)
                        .ThenInclude(place => place!.Owner)
                    .Include(item => item.Booking)
                        .ThenInclude(booking => booking!.Place)
                            .ThenInclude(place => place!.Owner)
                    .FirstOrDefaultAsync(item => item.ReportId == reportId, cancellationToken);

                if (report == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return AdminReportOperationResultDto.Fail("القضية غير موجودة.", "not-found");
                }

                var result = await mutation(report);
                if (!result.Success)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return result;
                }

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateConcurrencyException exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning(
                    exception,
                    "تعارض تزامن أثناء تحديث القضية {ReportId} بواسطة الإدارة {AdminId}.",
                    reportId,
                    currentAdminUserId);

                return AdminReportOperationResultDto.Fail(
                    "تم تعديل القضية من جهة أخرى. أعد تحميل الصفحة ثم حاول مجددًا.",
                    "concurrency");
            }
            catch (DbUpdateException exception)
            {
                await transaction.RollbackAsync(cancellationToken);

                var databaseMessage = exception.GetBaseException().Message ?? string.Empty;
                _logger.LogError(
                    exception,
                    "تعذر حفظ تحديث القضية {ReportId} بواسطة الإدارة {AdminId}. السبب: {DatabaseMessage}",
                    reportId,
                    currentAdminUserId,
                    databaseMessage);

                var friendlyMessage =
                    databaseMessage.Contains("FK_Chats_Places", StringComparison.OrdinalIgnoreCase)
                        ? "تعذر حفظ رسالة القضية بسبب مرجع منشأة قديم. تم تعديل رسائل القضايا لتعمل بالـ ReportID فقط؛ أعد المحاولة بعد تحديث الملف."
                        : databaseMessage.Contains("RequiredAction", StringComparison.OrdinalIgnoreCase)
                            ? "تعذر حفظ الإجراء لأن إعداد RequiredAction في قاعدة البيانات غير مطابق للقيم المعتمدة."
                            : databaseMessage.Contains("CK_Reports", StringComparison.OrdinalIgnoreCase)
                                ? "قاعدة البيانات رفضت انتقال حالة القضية. راجع قيود جدول Reports."
                                : "تعذر حفظ الإجراء في قاعدة البيانات. راجع نافذة Output في Visual Studio إذا استمر الخطأ.";

                return AdminReportOperationResultDto.Fail(
                    friendlyMessage,
                    "db-update-failed");
            }
            catch (OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(
                    exception,
                    "تعذر تنفيذ إجراء إداري على القضية {ReportId} بواسطة الإدارة {AdminId}.",
                    reportId,
                    currentAdminUserId);

                return AdminReportOperationResultDto.Fail(
                    "حدث خطأ أثناء تحديث القضية.",
                    "save-failed");
            }
        }

        private void AddReportChat(
            int senderId,
            int receiverId,
            Report report,
            string message,
            DateTime now)
        {
            _db.Chats.Add(new Chat
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                MessageText = message,
                SendTime = now,
                IsRead = false,
                AttachmentUrl = null,

                // رسائل القضية تعتمد على ReportID فقط.
                // ترك PlaceID فارغًا يمنع أي تعارض FK غير ضروري مع المنشآت القديمة/المحذوفة.
                PlaceId = null,

                IsDeletedForSender = false,
                IsDeletedForReceiver = false,
                IsEdited = false,
                ReportId = report.ReportId,
                IsReportMessage = true
            });
        }

        private void QueueNotification(
            int userId,
            string title,
            string text,
            string notificationType,
            string? targetUrl,
            int reportId)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(text)) return;

            _notifications.Queue(new CreateNotificationDto
            {
                UserId = userId,
                NotificationTitle = title,
                NotificationText = text.Trim(),
                NotificationType = notificationType,
                TargetUrl = targetUrl,
                ReferenceType = NotificationReferenceTypes.Report,
                ReferenceId = reportId
            });
        }

        private void AddAuditLog(
            int adminUserId,
            string actionType,
            int reportId,
            string? description,
            object? oldValues,
            object? newValues,
            AdminReportRequestContextDto requestContext)
        {
            _db.AdminAuditLogs.Add(new AdminAuditLog
            {
                UserId = adminUserId > 0 ? adminUserId : null,
                ActionType = Truncate(actionType, 100, "ReportAction"),
                EntityType = ReportEntityType,
                EntityId = reportId.ToString(),
                Description = Truncate(description, MaximumAuditDescriptionLength, string.Empty),
                OldValuesJson = SerializeValues(oldValues),
                NewValuesJson = SerializeValues(newValues),
                Ipaddress = Truncate(requestContext.IpAddress, 50, string.Empty),
                UserAgent = Truncate(requestContext.UserAgent, 500, string.Empty),
                OccurredAt = DateTime.Now,
                IsSuccessful = true,
                FailureReason = null
            });
        }

        /* =====================================================================
           Scenario / state helpers
           ===================================================================== */

        private static User? GetEffectiveAgainstUser(Report report)
        {
            if (report.AgainstUser != null) return report.AgainstUser;

            if (IsRole(report.Reporter, "Customer"))
            {
                if (report.Place?.Owner != null)
                    return report.Place.Owner;

                if (report.Booking?.Place?.Owner != null)
                    return report.Booking.Place.Owner;
            }

            return null;
        }

        private static string GetScenario(Report report, User? effectiveAgainstUser = null)
        {
            effectiveAgainstUser ??= GetEffectiveAgainstUser(report);

            if (IsRole(report.Reporter, "Owner") && IsRole(effectiveAgainstUser, "Customer"))
                return AdminReportValues.ScenarioOwnerAgainstCustomer;

            if (IsRole(report.Reporter, "Customer") && IsRole(effectiveAgainstUser, "Owner"))
                return AdminReportValues.ScenarioCustomerAgainstOwner;

            if (ReportConstants.EqualsState(report.ReportType, ReportConstants.Types.Platform) ||
                ReportConstants.EqualsState(report.ReportType, ReportConstants.Types.Technical) ||
                effectiveAgainstUser == null)
            {
                return AdminReportValues.ScenarioPlatform;
            }

            return AdminReportValues.ScenarioGeneral;
        }

        private static bool IsOwnerAgainstCustomer(Report report) =>
            GetScenario(report) == AdminReportValues.ScenarioOwnerAgainstCustomer;

        private static bool IsRole(User? user, string role) =>
            user != null && string.Equals(user.Role, role, StringComparison.OrdinalIgnoreCase);

        private static bool IsCompleted(string? status) =>
            ReportConstants.EqualsState(status, ReportConstants.Statuses.Resolved) ||
            ReportConstants.EqualsState(status, ReportConstants.Statuses.Rejected) ||
            ReportConstants.EqualsState(status, ReportConstants.Statuses.Closed) ||
            ReportConstants.EqualsState(status, ReportConstants.Statuses.Withdrawn);

        private static bool CanRequestExternalInput(string? status) =>
            ReportConstants.EqualsState(status, ReportConstants.Statuses.InReview) ||
            ReportConstants.EqualsState(status, ReportConstants.Statuses.Escalated);

        private static bool CanMakeDecision(string? status) =>
            ReportConstants.EqualsState(status, ReportConstants.Statuses.InReview) ||
            ReportConstants.EqualsState(status, ReportConstants.Statuses.Escalated) ||
            ReportConstants.EqualsState(status, ReportConstants.Statuses.WaitingForReporter) ||
            ReportConstants.EqualsState(status, ReportConstants.Statuses.WaitingForReportedParty);

        private static bool CanReopen(string? status) =>
            ReportConstants.EqualsState(status, ReportConstants.Statuses.Resolved) ||
            ReportConstants.EqualsState(status, ReportConstants.Statuses.Rejected) ||
            ReportConstants.EqualsState(status, ReportConstants.Statuses.Closed);

        private static bool CanCloseReport(Report report, string confirmationState)
        {
            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Resolved) &&
                !string.IsNullOrWhiteSpace(report.Resolution))
            {
                return true;
            }

            return ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.InReview) &&
                   string.Equals(confirmationState, "Confirmed", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(report.Resolution);
        }

        private static void ClearWaitingState(Report report)
        {
            report.RequiredAction = null;
            report.ReporterReplyDeadline = null;
            report.ReportedPartyReplyDeadline = null;
            report.AdminComment = null;
        }

        private static DateTime? GetReplyDeadline(Report report)
        {
            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.WaitingForReporter))
                return report.ReporterReplyDeadline;

            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.WaitingForReportedParty))
                return report.ReportedPartyReplyDeadline;

            return null;
        }

        private static string GetCustomerResolutionConfirmationState(string? reporterReply)
        {
            var value = reporterReply?.Trim() ?? string.Empty;
            if (value.StartsWith("تأكيد العميل: نعم، تم حل المشكلة.", StringComparison.OrdinalIgnoreCase))
                return "Confirmed";
            if (value.StartsWith("تأكيد العميل: لا، المشكلة ما زالت مستمرة.", StringComparison.OrdinalIgnoreCase))
                return "Rejected";
            return "None";
        }

        private static string GetCurrentStepLabel(Report report, string scenario)
        {
            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Pending)) return "قضية جديدة";
            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.InReview)) return "قيد المتابعة";
            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Escalated)) return "تحتاج متابعة عاجلة";
            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Resolved)) return "تم تسجيل نتيجة";
            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Rejected)) return "تم رفض القضية";
            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Closed)) return "القضية مغلقة";
            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Withdrawn)) return "القضية مسحوبة";

            if (string.Equals(report.RequiredAction, ReportConstants.RequiredActions.Clarification, StringComparison.OrdinalIgnoreCase))
                return "بانتظار توضيح المبلّغ";
            if (string.Equals(report.RequiredAction, ReportConstants.RequiredActions.ResolutionConfirmation, StringComparison.OrdinalIgnoreCase))
                return "بانتظار تأكيد العميل";
            if (string.Equals(report.RequiredAction, ReportConstants.RequiredActions.ReportedPartyResponse, StringComparison.OrdinalIgnoreCase))
                return "بانتظار رد المالك";

            return scenario == AdminReportValues.ScenarioOwnerAgainstCustomer
                ? "مراجعة مخالفة العميل"
                : "بانتظار المتابعة";
        }

        private static string GetCurrentStepDescription(Report report, string scenario)
        {
            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Pending))
                return "ابدأ مراجعة القضية، ثم سيظهر فقط الإجراء المناسب لنوع البلاغ.";

            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.InReview))
            {
                if (scenario == AdminReportValues.ScenarioOwnerAgainstCustomer)
                    return "راجع أدلة المالك وبيانات النظام ثم حدد: المخالفة مثبتة أو غير مثبتة. لا يُطلب من العميل الرد داخل القضية.";
                if (scenario == AdminReportValues.ScenarioCustomerAgainstOwner)
                    return "يمكن طلب توضيح من العميل، أو طلب رد من المالك، أو اتخاذ قرار.";
                if (scenario == AdminReportValues.ScenarioPlatform)
                    return "راجع المشكلة، واطلب توضيحًا من المبلّغ إذا احتجت، ثم سجّل الحل أو ارفض البلاغ.";
                return "راجع التفاصيل والأدلة وحدد الإجراء التالي.";
            }

            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Escalated))
                return "القضية مصعّدة وتحتاج قرارًا أو متابعة عاجلة من الإدارة.";

            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Resolved))
            {
                if (scenario == AdminReportValues.ScenarioOwnerAgainstCustomer)
                    return "تم تسجيل نتيجة مخالفة العميل ويمكن للإدارة إغلاق القضية رسميًا.";
                if (IsRole(report.Reporter, "Customer"))
                    return "تم تسجيل الحل. يمكن طلب تأكيد العميل قبل الإغلاق أو إغلاق القضية إذا لم تكن بحاجة لتأكيد إضافي.";
                return "تم تسجيل النتيجة ويمكن إغلاق القضية.";
            }

            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Rejected))
                return "صدر قرار برفض القضية، ويمكن إعادة فتح نفس القضية فقط عند وجود سبب جديد.";

            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Closed))
                return "أغلقت القضية رسميًا ولا تقبل إجراءات عادية.";

            if (ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Withdrawn))
                return "سحب المبلّغ القضية ولا يعاد فتحها تلقائيًا.";

            if (string.Equals(report.RequiredAction, ReportConstants.RequiredActions.Clarification, StringComparison.OrdinalIgnoreCase))
                return "الإدارة طلبت توضيحًا من المبلّغ وتنتظر الرد داخل نفس القضية.";

            if (string.Equals(report.RequiredAction, ReportConstants.RequiredActions.ResolutionConfirmation, StringComparison.OrdinalIgnoreCase))
                return "تم إرسال نتيجة الحل للعميل وتنتظر الإدارة تأكيده: نعم أو لا.";

            if (string.Equals(report.RequiredAction, ReportConstants.RequiredActions.ReportedPartyResponse, StringComparison.OrdinalIgnoreCase))
                return "الإدارة طلبت ردًا من المالك وتنتظر رده الرسمي داخل نفس القضية.";

            return "القضية بانتظار الرد قبل استكمال المعالجة.";
        }

        /* =====================================================================
           List / presentation helpers
           ===================================================================== */

        private async Task PopulateAgainstHistoryForListAsync(
            IReadOnlyCollection<Report> rows,
            IReadOnlyCollection<AdminReportListItemDto> items,
            CancellationToken cancellationToken)
        {
            if (rows.Count == 0 || items.Count == 0)
            {
                return;
            }

            var identities = rows
                .Select(report => new
                {
                    report.ReportId,
                    User = GetEffectiveAgainstUser(report)
                })
                .Where(item => item.User != null)
                .Select(item => new
                {
                    item.ReportId,
                    UserId = item.User!.UserId,
                    IsOwner = IsRole(item.User, "Owner")
                })
                .ToList();

            if (identities.Count == 0)
            {
                return;
            }

            var userIds = identities
                .Select(item => item.UserId)
                .Distinct()
                .ToList();

            var ownerIds = identities
                .Where(item => item.IsOwner)
                .Select(item => item.UserId)
                .Distinct()
                .ToList();

            var historyRows = await _db.Reports
                .AsNoTracking()
                .Where(report =>
                    (report.AgainstUserId.HasValue &&
                     userIds.Contains(report.AgainstUserId.Value)) ||
                    (report.AgainstUserId == null &&
                     ownerIds.Count > 0 &&
                     (
                         (report.Place != null &&
                          report.Place.OwnerId.HasValue &&
                          ownerIds.Contains(report.Place.OwnerId.Value)) ||
                         (report.Booking != null &&
                          report.Booking.Place != null &&
                          report.Booking.Place.OwnerId.HasValue &&
                          ownerIds.Contains(report.Booking.Place.OwnerId.Value))
                     )))
                .Select(report => new AdminAgainstHistoryProjection
                {
                    ReportId = report.ReportId,
                    DirectAgainstUserId = report.AgainstUserId,
                    PlaceOwnerId =
                        report.AgainstUserId == null && report.Place != null
                            ? report.Place.OwnerId
                            : null,
                    BookingOwnerId =
                        report.AgainstUserId == null &&
                        report.Booking != null &&
                        report.Booking.Place != null
                            ? report.Booking.Place.OwnerId
                            : null,
                    HasWarning = report.WarningIssuedAt.HasValue
                })
                .ToListAsync(cancellationToken);

            var historyByUser = new Dictionary<int, List<AdminAgainstHistoryProjection>>();

            static void AddHistory(
                Dictionary<int, List<AdminAgainstHistoryProjection>> target,
                int userId,
                AdminAgainstHistoryProjection row)
            {
                if (!target.TryGetValue(userId, out var list))
                {
                    list = new List<AdminAgainstHistoryProjection>();
                    target[userId] = list;
                }

                // نفس القضية قد تُلتقط من مرجع المنشأة والحجز معًا؛ لا نكررها.
                if (list.All(item => item.ReportId != row.ReportId))
                {
                    list.Add(row);
                }
            }

            foreach (var history in historyRows)
            {
                if (history.DirectAgainstUserId.HasValue &&
                    userIds.Contains(history.DirectAgainstUserId.Value))
                {
                    AddHistory(
                        historyByUser,
                        history.DirectAgainstUserId.Value,
                        history);

                    continue;
                }

                if (history.PlaceOwnerId.HasValue &&
                    ownerIds.Contains(history.PlaceOwnerId.Value))
                {
                    AddHistory(
                        historyByUser,
                        history.PlaceOwnerId.Value,
                        history);
                }

                if (history.BookingOwnerId.HasValue &&
                    ownerIds.Contains(history.BookingOwnerId.Value))
                {
                    AddHistory(
                        historyByUser,
                        history.BookingOwnerId.Value,
                        history);
                }
            }

            var identityByReportId = identities
                .ToDictionary(item => item.ReportId);

            foreach (var item in items)
            {
                if (!identityByReportId.TryGetValue(item.ReportId, out var identity) ||
                    !historyByUser.TryGetValue(identity.UserId, out var history))
                {
                    item.AgainstUserPreviousCasesCount = 0;
                    item.AgainstUserConfirmedViolationsCount = 0;
                    continue;
                }

                var previous = history
                    .Where(row => row.ReportId != item.ReportId)
                    .ToList();

                item.AgainstUserPreviousCasesCount = previous.Count;
                item.AgainstUserConfirmedViolationsCount =
                    previous.Count(row => row.HasWarning);
            }
        }

        private static AdminReportListItemDto BuildListItem(Report report, DateTime now)
        {
            var effectiveAgainstUser = GetEffectiveAgainstUser(report);
            var scenario = GetScenario(report, effectiveAgainstUser);
            var deadline = GetReplyDeadline(report);
            var completed = IsCompleted(report.ReportStatus);
            var createdAt = report.CreatedAt ?? report.UpdatedAt;

            return new AdminReportListItemDto
            {
                ReportId = report.ReportId,
                ReportNumber = report.ReportNumber,
                RequestKind = report.RequestKind,
                RequestKindLabel = ReportConstants.GetRequestKindLabel(report.RequestKind),
                ReportType = report.ReportType,
                ReportTypeLabel = AdminReportValues.GetReportTypeLabel(report.ReportType, report.OtherReportTypeDescription),
                TypeIcon = AdminReportValues.GetTypeIcon(report.ReportType),
                Category = report.Category,
                CategoryLabel = AdminReportValues.GetCategoryLabel(report.Category, report.OtherCategoryDescription),
                Title = BuildTitle(report),
                Status = report.ReportStatus,
                StatusLabel = AdminReportValues.GetAdminStatusLabel(report.ReportStatus),
                Priority = report.Priority,
                PriorityLabel = AdminReportValues.GetPriorityLabel(report.Priority),
                RequiredAction = report.RequiredAction,
                RequiredActionLabel = AdminReportValues.GetRequiredActionLabel(report.RequiredAction),
                Scenario = scenario,
                ScenarioLabel = AdminReportValues.GetScenarioLabel(scenario),
                DirectionLabel = BuildDirectionLabel(report.Reporter, effectiveAgainstUser, scenario),
                ReporterUserId = report.ReporterId,
                ReporterName = GetDisplayName(report.Reporter, "مستخدم"),
                ReporterRole = report.Reporter.Role,
                ReporterRoleLabel = AdminReportValues.GetRoleLabel(report.Reporter.Role),
                AgainstUserId = effectiveAgainstUser?.UserId,
                AgainstUserName = effectiveAgainstUser == null
                    ? "لا يوجد طرف مبلّغ ضده"
                    : GetDisplayName(effectiveAgainstUser, "مستخدم"),
                AgainstUserRole = effectiveAgainstUser?.Role ?? string.Empty,
                AgainstUserRoleLabel = effectiveAgainstUser == null
                    ? "—"
                    : AdminReportValues.GetRoleLabel(effectiveAgainstUser.Role),
                AgainstUserWarningCount = Math.Max(0, effectiveAgainstUser?.WarningCount ?? 0),
                PlaceName = report.Place?.PlaceNameAndDesc ?? report.Booking?.Place?.PlaceNameAndDesc,
                ReferenceLabel = BuildReferenceLabel(report),
                CreatedAt = createdAt,
                UpdatedAt = report.UpdatedAt,
                ReplyDeadline = deadline,
                HasEvidence = !string.IsNullOrWhiteSpace(report.EvidenceImage),
                IsOverdue = deadline.HasValue && deadline.Value < now && !completed,
                IsUrgent =
                    ReportConstants.EqualsState(report.Priority, ReportConstants.Priorities.Urgent) ||
                    ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Escalated),
                NeedsAdminAction =
                    ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Pending) ||
                    ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.InReview) ||
                    ReportConstants.EqualsState(report.ReportStatus, ReportConstants.Statuses.Escalated)
            };
        }

        private static string BuildDirectionLabel(User reporter, User? againstUser, string scenario)
        {
            if (scenario == AdminReportValues.ScenarioPlatform)
                return $"{AdminReportValues.GetRoleLabel(reporter.Role)} يبلّغ عن مشكلة في المنصة";

            if (againstUser == null)
                return $"المبلّغ: {AdminReportValues.GetRoleLabel(reporter.Role)} — لا يوجد طرف مبلّغ ضده";

            return $"{AdminReportValues.GetRoleLabel(reporter.Role)} ضد {AdminReportValues.GetRoleLabel(againstUser.Role)}";
        }

        private static string BuildReferenceLabel(Report report)
        {
            if (report.BookingId.HasValue) return $"حجز #{report.BookingId.Value}";
            if (report.ReviewId.HasValue) return $"تقييم #{report.ReviewId.Value}";
            if (report.PromotionId.HasValue) return $"عرض #{report.PromotionId.Value}";
            if (report.AdvertisementId.HasValue) return $"إعلان #{report.AdvertisementId.Value}";
            if (report.PlaceId.HasValue) return report.Place?.PlaceNameAndDesc ?? $"منشأة #{report.PlaceId.Value}";
            return "بدون مرجع";
        }

        private static string BuildTitle(Report report)
        {
            if (!string.IsNullOrWhiteSpace(report.Title)) return report.Title.Trim();

            return $"{ReportConstants.GetRequestKindLabel(report.RequestKind)} — " +
                   AdminReportValues.GetReportTypeLabel(report.ReportType, report.OtherReportTypeDescription);
        }

        private static IReadOnlyList<AdminReportTabDto> BuildTabs(AdminReportSummaryDto summary) =>
            new List<AdminReportTabDto>
            {
                new() { Key = AdminReportValues.TabAll, Label = "الكل", Icon = "bi-collection-fill", Count = summary.TotalCount },
                new() { Key = AdminReportValues.TabNew, Label = "جديدة", Icon = "bi-inbox-fill", Count = summary.NewCount },
                new() { Key = AdminReportValues.TabAction, Label = "قيد المتابعة", Icon = "bi-arrow-repeat", Count = summary.ActionCount },
                new() { Key = AdminReportValues.TabWaiting, Label = "بانتظار رد", Icon = "bi-hourglass-split", Count = summary.WaitingCount },
                new() { Key = AdminReportValues.TabEscalated, Label = "مصعّدة", Icon = "bi-arrow-up-right-circle-fill", Count = summary.EscalatedCount },
                new() { Key = AdminReportValues.TabCompleted, Label = "منتهية", Icon = "bi-archive-fill", Count = summary.CompletedCount }
            };

        /* =====================================================================
           Timeline / internal notes
           ===================================================================== */

        private static IReadOnlyList<AdminReportTimelineItemDto> BuildTimeline(
            Report report,
            IReadOnlyCollection<ReportChatProjection> chats,
            IReadOnlyCollection<AdminAuditLog> auditLogs)
        {
            var items = new List<AdminReportTimelineItemDto>
            {
                new()
                {
                    EventType = "Created",
                    Title = "تم إنشاء القضية",
                    Description = report.ReportReason,
                    OccurredAt = report.CreatedAt ?? report.UpdatedAt,
                    ActorName = GetDisplayName(report.Reporter, "المبلّغ"),
                    ActorRoleLabel = AdminReportValues.GetRoleLabel(report.Reporter.Role),
                    Icon = "bi-file-earmark-plus-fill",
                    Tone = "info",
                    AttachmentUrl = NormalizePublicPath(report.EvidenceImage)
                }
            };

            foreach (var chat in chats)
            {
                items.Add(new AdminReportTimelineItemDto
                {
                    EventType = "Message",
                    Title = GetMessageTitle(chat.SenderRole),
                    Description = chat.MessageText,
                    OccurredAt = chat.SendTime ?? report.CreatedAt ?? report.UpdatedAt,
                    ActorName = string.IsNullOrWhiteSpace(chat.SenderName) ? "مستخدم" : chat.SenderName,
                    ActorRoleLabel = AdminReportValues.GetRoleLabel(chat.SenderRole),
                    Icon = "bi-chat-left-text-fill",
                    Tone = GetMessageTone(chat.SenderRole),
                    AttachmentUrl = NormalizePublicPath(chat.AttachmentUrl),
                    IsMessage = true
                });
            }

            foreach (var log in auditLogs.Where(log =>
                         !string.Equals(log.ActionType, InternalNoteAction, StringComparison.OrdinalIgnoreCase) &&
                         !IsLegacyAssignmentAudit(log)))
            {
                var presentation = GetAuditPresentation(log.ActionType);
                items.Add(new AdminReportTimelineItemDto
                {
                    EventType = log.ActionType,
                    Title = presentation.Title,
                    Description = log.Description,
                    OccurredAt = log.OccurredAt,
                    ActorName = GetDisplayName(log.User, "الإدارة"),
                    ActorRoleLabel = "الإدارة",
                    Icon = presentation.Icon,
                    Tone = presentation.Tone
                });
            }

            if (report.WithdrawnAt.HasValue)
            {
                items.Add(new AdminReportTimelineItemDto
                {
                    EventType = "Withdrawn",
                    Title = "تم سحب القضية",
                    Description = report.WithdrawnReason,
                    OccurredAt = report.WithdrawnAt.Value,
                    ActorName = GetDisplayName(report.Reporter, "المبلّغ"),
                    ActorRoleLabel = AdminReportValues.GetRoleLabel(report.Reporter.Role),
                    Icon = "bi-arrow-return-left",
                    Tone = "muted"
                });
            }

            return items
                .OrderBy(item => item.OccurredAt)
                .ThenBy(item => item.EventType)
                .ToList();
        }

        private static bool IsLegacyAssignmentAudit(AdminAuditLog log)
        {
            if (log == null) return false;

            if (!string.IsNullOrWhiteSpace(log.ActionType) &&
                log.ActionType.Contains("Assign", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(log.Description) &&
                   (
                       log.Description.Contains("تعيين الموظف", StringComparison.OrdinalIgnoreCase) ||
                       log.Description.Contains("الموظف المسؤول", StringComparison.OrdinalIgnoreCase) ||
                       log.Description.Contains("إسناد القضية", StringComparison.OrdinalIgnoreCase)
                   );
        }

        private static IReadOnlyList<AdminReportInternalNoteDto> BuildInternalNotes(
            IReadOnlyCollection<AdminAuditLog> auditLogs)
        {
            return auditLogs
                .Where(log => string.Equals(
                    log.ActionType,
                    InternalNoteAction,
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(log => log.OccurredAt)
                .Select(log => new AdminReportInternalNoteDto
                {
                    AuditLogId = log.AdminAuditLogId,
                    AdminName = GetDisplayName(log.User, "الإدارة"),
                    NoteText = log.Description ?? string.Empty,
                    CreatedAt = log.OccurredAt
                })
                .ToList();
        }

        private static string GetMessageTitle(string? senderRole)
        {
            if (string.Equals(senderRole, "Admin", StringComparison.OrdinalIgnoreCase)) return "رسالة من الإدارة";
            if (string.Equals(senderRole, "Owner", StringComparison.OrdinalIgnoreCase)) return "رد المالك";
            if (string.Equals(senderRole, "Customer", StringComparison.OrdinalIgnoreCase)) return "رد العميل";
            return "رسالة داخل القضية";
        }

        private static string GetMessageTone(string? senderRole)
        {
            if (string.Equals(senderRole, "Owner", StringComparison.OrdinalIgnoreCase)) return "owner";
            if (string.Equals(senderRole, "Customer", StringComparison.OrdinalIgnoreCase)) return "customer";
            return "admin";
        }

        private static (string Title, string Icon, string Tone) GetAuditPresentation(string? action)
        {
            return action switch
            {
                ReviewStartedAction => ("بدأت الإدارة المراجعة", "bi-search", "info"),
                ClarificationRequestedAction => ("طلبت الإدارة توضيحًا", "bi-question-circle-fill", "info"),
                OwnerResponseRequestedAction => ("طلبت الإدارة رد المالك", "bi-building-check", "owner"),
                ResolutionConfirmationRequestedAction => ("طلبت الإدارة تأكيد الحل", "bi-patch-question-fill", "info"),
                PriorityChangedAction => ("تم تغيير الأولوية", "bi-exclamation-diamond-fill", "warning"),
                EscalatedAction => ("تم تصعيد القضية", "bi-arrow-up-right-circle-fill", "danger"),
                ResolvedAction => ("سجلت الإدارة الحل", "bi-check-circle-fill", "success"),
                RejectedAction => ("تم رفض القضية", "bi-x-circle-fill", "danger"),
                CustomerViolationConfirmedAction => ("تم إثبات مخالفة العميل", "bi-shield-fill-exclamation", "danger"),
                CustomerViolationNotConfirmedAction => ("المخالفة غير مثبتة", "bi-shield-check", "success"),
                ClosedAction => ("تم إغلاق القضية", "bi-lock-fill", "muted"),
                ReopenedAction => ("تمت إعادة فتح القضية", "bi-arrow-counterclockwise", "warning"),
                _ => ("تحديث إداري", "bi-activity", "info")
            };
        }

        /* =====================================================================
           General helpers
           ===================================================================== */

        private static string GetAccountStateLabel(string? value)
        {
            if (string.Equals(value, "Active", StringComparison.OrdinalIgnoreCase)) return "نشط";
            if (string.Equals(value, "Suspended", StringComparison.OrdinalIgnoreCase)) return "موقوف";
            if (string.Equals(value, "Pending", StringComparison.OrdinalIgnoreCase)) return "قيد المراجعة";
            if (string.Equals(value, "Rejected", StringComparison.OrdinalIgnoreCase)) return "مرفوض";
            return string.IsNullOrWhiteSpace(value) ? "غير محدد" : value.Trim();
        }

        private static string ReporterDetailsUrl(int reportId, string? role)
        {
            if (string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase))
                return $"/Reports/ReportDetails/{reportId}";

            if (string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase))
                return $"/Owner/Reports/OwnerReportDetails/{reportId}";

            return $"/Admin/Reports/Details/{reportId}";
        }

        private static string? NormalizePublicPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var normalized = path.Trim().Replace('\\', '/');
            if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("/", StringComparison.Ordinal))
            {
                return normalized;
            }

            return "/" + normalized.TrimStart('/');
        }

        private static string GetDisplayName(User? user, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(user?.FullName)) return user.FullName.Trim();
            if (!string.IsNullOrWhiteSpace(user?.Username)) return user.Username.Trim();
            return fallback;
        }

        private static string NormalizeRequired(string? value) =>
            value?.Trim() ?? string.Empty;

        private static string? NormalizeOptional(string? value)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private static string? ValidateDeadline(DateTime? deadline)
        {
            if (!deadline.HasValue) return "حدد مهلة الرد.";
            if (deadline.Value <= DateTime.Now.AddMinutes(5)) return "مهلة الرد يجب أن تكون في المستقبل.";
            return null;
        }

        private static string Truncate(string? value, int maxLength, string fallback)
        {
            var normalized = value?.Trim();
            if (string.IsNullOrEmpty(normalized)) return fallback;
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
        }

        private static string? SerializeValues(object? values)
        {
            if (values == null) return null;
            try
            {
                return JsonSerializer.Serialize(values);
            }
            catch
            {
                return null;
            }
        }

        private static string? FindCanonical(IEnumerable<string> values, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return values.FirstOrDefault(item =>
                string.Equals(item, value.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private sealed class AdminAgainstHistoryProjection
        {
            public int ReportId { get; set; }
            public int? DirectAgainstUserId { get; set; }
            public int? PlaceOwnerId { get; set; }
            public int? BookingOwnerId { get; set; }
            public bool HasWarning { get; set; }
        }

        private sealed class ReportChatProjection
        {
            public int MessageId { get; set; }
            public int SenderId { get; set; }
            public string SenderName { get; set; } = string.Empty;
            public string SenderRole { get; set; } = string.Empty;
            public int ReceiverId { get; set; }
            public string ReceiverName { get; set; } = string.Empty;
            public string ReceiverRole { get; set; } = string.Empty;
            public string MessageText { get; set; } = string.Empty;
            public string? AttachmentUrl { get; set; }
            public DateTime? SendTime { get; set; }
        }
    }
}
