using System;
using System.Collections.Generic;
using System.Linq;

namespace YemenGuide.Core.Constants
{
    /// <summary>
    /// المصدر المركزي للقيم النصية الخاصة بنظام الشكاوى والبلاغات.
    ///
    /// ملاحظات مهمة:
    /// - القيم داخل Statuses تطابق CK_Reports_Status في قاعدة البيانات.
    /// - القيم داخل RequestKinds تطابق CK_Reports_RequestKind في قاعدة البيانات.
    /// - القيم داخل Priorities تطابق CK_Reports_Priority في قاعدة البيانات.
    /// - لا تكرر هذه السلاسل النصية داخل Controllers أو Services.
    /// </summary>
    public static class ReportConstants
    {

        // Null is reserved for requests created before phase 1.
        public static class ResponseContentTypes
        {
            public const string Text = "Text";
            public const string Image = "Image";
            public const string TextAndImage = "TextAndImage";

            public static bool IsValid(string? value) =>
                value == Text || value == Image || value == TextAndImage;

            public static string? Validate(string? type, string? message, bool hasFile, bool isImage)
            {
                var hasText = !string.IsNullOrWhiteSpace(message);
                if (message != null && message.Trim().Length > 4000)
                    return "النص يجب ألا يتجاوز 4000 حرف.";
                if (type == null)
                    return hasText || hasFile ? null : "أرسل توضيحًا أو مرفقًا.";
                if (!IsValid(type))
                    return "نوع الاستكمال غير صالح؛ اطلب من الإدارة تجديد الطلب.";
                if (type == Text && (!hasText || hasFile))
                    return "طلبت الإدارة توضيحًا نصيًا فقط، دون مرفق.";
                if (type == Image && (hasText || !hasFile || !isImage))
                    return "طلبت الإدارة صورة إثبات فقط، دون نص أو ملف PDF.";
                if (type == TextAndImage && (!hasText || !hasFile || !isImage))
                    return "طلبت الإدارة توضيحًا نصيًا وصورة إثبات معًا.";
                return null;
            }
        }

        public static class RequestKinds
        {
            public const string Report = "Report";
            public const string Complaint = "Complaint";

            public static readonly IReadOnlyCollection<string> All =
                new[]
                {
                    Report,
                    Complaint
                };

            public static bool IsValid(string? value) =>
                ContainsIgnoreCase(All, value);
        }

        public static class Types
        {
            public const string Booking = "Booking";
            public const string Place = "Place";
            public const string Review = "Review";
            public const string Promotion = "Promotion";
            public const string Advertisement = "Advertisement";
            public const string Platform = "Platform";
            public const string Technical = "Technical";
            public const string Other = "Other";

            public static readonly IReadOnlyCollection<string> All =
                new[]
                {
                    Booking,
                    Place,
                    Review,
                    Promotion,
                    Advertisement,
                    Platform,
                    Technical,
                    Other
                };

            public static bool IsValid(string? value) =>
                ContainsIgnoreCase(All, value);

            public static bool RequiresBooking(string? value) =>
                EqualsState(value, Booking);

            public static bool RequiresPlace(string? value) =>
                EqualsState(value, Place);

            public static bool RequiresReview(string? value) =>
                EqualsState(value, Review);

            public static bool RequiresPromotion(string? value) =>
                EqualsState(value, Promotion);

            public static bool RequiresAdvertisement(string? value) =>
                EqualsState(value, Advertisement);

            public static bool IsPlatformLevel(string? value) =>
                EqualsState(value, Platform) ||
                EqualsState(value, Technical) ||
                EqualsState(value, Other);
        }

        public static class Categories
        {
            public const string BookingIssue = "BookingIssue";
            public const string PaymentIssue = "PaymentIssue";
            public const string RefundIssue = "RefundIssue";
            public const string ServiceIssue = "ServiceIssue";
            public const string DescriptionMismatch = "DescriptionMismatch";
            public const string OwnerConduct = "OwnerConduct";
            public const string FraudOrScam = "FraudOrScam";
            public const string MisleadingContent = "MisleadingContent";
            public const string AbusiveContent = "AbusiveContent";
            public const string FakeContent = "FakeContent";
            public const string TechnicalIssue = "TechnicalIssue";
            public const string PlatformIssue = "PlatformIssue";
            public const string Other = "Other";

            public static readonly IReadOnlyCollection<string> All =
                new[]
                {
                    BookingIssue,
                    PaymentIssue,
                    RefundIssue,
                    ServiceIssue,
                    DescriptionMismatch,
                    OwnerConduct,
                    FraudOrScam,
                    MisleadingContent,
                    AbusiveContent,
                    FakeContent,
                    TechnicalIssue,
                    PlatformIssue,
                    Other
                };

            public static bool IsValid(string? value) =>
                string.IsNullOrWhiteSpace(value) ||
                ContainsIgnoreCase(All, value);
        }

        public static class Statuses
        {
            public const string Pending = "Pending";
            public const string InReview = "InReview";
            public const string WaitingForReporter = "WaitingForReporter";
            public const string WaitingForReportedParty = "WaitingForReportedParty";
            public const string Escalated = "Escalated";
            public const string Resolved = "Resolved";
            public const string Rejected = "Rejected";
            public const string Closed = "Closed";
            public const string Withdrawn = "Withdrawn";

            public static readonly IReadOnlyCollection<string> All =
                new[]
                {
                    Pending,
                    InReview,
                    WaitingForReporter,
                    WaitingForReportedParty,
                    Escalated,
                    Resolved,
                    Rejected,
                    Closed,
                    Withdrawn
                };

            public static readonly IReadOnlyCollection<string> Terminal =
                new[]
                {
                    Rejected,
                    Closed,
                    Withdrawn
                };

            public static readonly IReadOnlyCollection<string> Completed =
                new[]
                {
                    Resolved,
                    Rejected,
                    Closed,
                    Withdrawn
                };

            public static bool IsValid(string? value) =>
                ContainsIgnoreCase(All, value);

            public static bool IsTerminal(string? value) =>
                ContainsIgnoreCase(Terminal, value);

            public static bool IsCompleted(string? value) =>
                ContainsIgnoreCase(Completed, value);

            public static bool NeedsReporterReply(string? value) =>
                EqualsState(value, WaitingForReporter);

            public static bool IsActive(string? value) =>
                !IsCompleted(value);
        }

        public static class RequiredActions
        {
            public const string Clarification = "Clarification";

            public const string ResolutionConfirmation =
                "ResolutionConfirmation";

            public const string ReportedPartyResponse =
                "ReportedPartyResponse";

            public static readonly IReadOnlyCollection<string> All =
                new[]
                {
            Clarification,
            ResolutionConfirmation,
            ReportedPartyResponse
                };

            public static bool IsValid(string? value) =>
                string.IsNullOrWhiteSpace(value) ||
                ContainsIgnoreCase(All, value);
        }
        public static class Priorities
        {
            public const string Low = "Low";
            public const string Normal = "Normal";
            public const string High = "High";
            public const string Urgent = "Urgent";

            public static readonly IReadOnlyCollection<string> All =
                new[]
                {
                    Low,
                    Normal,
                    High,
                    Urgent
                };

            public static bool IsValid(string? value) =>
                ContainsIgnoreCase(All, value);
        }

        public static class StatusGroups
        {
            public const string All = "all";
            public const string NeedsResponse = "needs-response";
            public const string Active = "active";
            public const string Completed = "completed";

            public static readonly IReadOnlyCollection<string> Values =
                new[]
                {
                    All,
                    NeedsResponse,
                    Active,
                    Completed
                };

            public static bool IsValid(string? value) =>
                ContainsIgnoreCase(Values, value);
        }

        public static class ReferenceTypes
        {
            public const string Report = "Report";
            public const string Booking = "Booking";
            public const string Place = "Place";
            public const string Review = "Review";
            public const string Promotion = "Promotion";
            public const string Advertisement = "Advertisement";
        }

        public static class NotificationTypes
        {
            public const string Report = "Report";
        }

        public static class Limits
        {
            public const int TitleMaxLength = 200;
            public const int CategoryMaxLength = 50;
            public const int OtherDescriptionMaxLength = 50;
            public const int ReasonMaxLength = 4000;
            public const int ClarificationMaxLength = 4000;
            public const int WithdrawReasonMaxLength = 500;
            public const int DefaultPageSize = 12;
            public const int MaxPageSize = 50;
            public const int RecentBookingsTake = 20;
            public const long MaxEvidenceBytes = 5L * 1024L * 1024L;
        }

        public static string NormalizeRequestKind(string? value)
        {
            return RequestKinds.IsValid(value)
                ? FindCanonical(RequestKinds.All, value) ?? RequestKinds.Report
                : RequestKinds.Report;
        }

        public static string NormalizeType(string? value)
        {
            return Types.IsValid(value)
                ? FindCanonical(Types.All, value) ?? Types.Other
                : Types.Other;
        }

        public static string NormalizeCategory(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Categories.IsValid(value)
                ? FindCanonical(Categories.All, value) ?? Categories.Other
                : Categories.Other;
        }

        public static string NormalizeStatusGroup(string? value)
        {
            return StatusGroups.IsValid(value)
                ? FindCanonical(StatusGroups.Values, value) ?? StatusGroups.All
                : StatusGroups.All;
        }

        public static string GetRequestKindLabel(string? value)
        {
            return EqualsState(value, RequestKinds.Complaint)
                ? "شكوى"
                : "بلاغ";
        }

        public static string GetTypeLabel(string? value)
        {
            if (EqualsState(value, Types.Booking)) return "حجز";
            if (EqualsState(value, Types.Place)) return "منشأة";
            if (EqualsState(value, Types.Review)) return "تقييم";
            if (EqualsState(value, Types.Promotion)) return "عرض";
            if (EqualsState(value, Types.Advertisement)) return "إعلان";
            if (EqualsState(value, Types.Platform)) return "المنصة";
            if (EqualsState(value, Types.Technical)) return "مشكلة تقنية";
            return "أخرى";
        }

        public static string GetCategoryLabel(string? value)
        {
            if (EqualsState(value, Categories.BookingIssue)) return "مشكلة في الحجز";
            if (EqualsState(value, Categories.PaymentIssue)) return "مشكلة في الدفع";
            if (EqualsState(value, Categories.RefundIssue)) return "مشكلة في الاسترداد";
            if (EqualsState(value, Categories.ServiceIssue)) return "مشكلة في الخدمة";
            if (EqualsState(value, Categories.DescriptionMismatch)) return "الخدمة أو المنشأة لا تطابق الوصف";
            if (EqualsState(value, Categories.OwnerConduct)) return "تصرف غير مناسب من المالك";
            if (EqualsState(value, Categories.FraudOrScam)) return "اشتباه احتيال";
            if (EqualsState(value, Categories.MisleadingContent)) return "محتوى مضلل";
            if (EqualsState(value, Categories.AbusiveContent)) return "إساءة أو محتوى غير لائق";
            if (EqualsState(value, Categories.FakeContent)) return "محتوى أو تقييم غير حقيقي";
            if (EqualsState(value, Categories.TechnicalIssue)) return "مشكلة تقنية";
            if (EqualsState(value, Categories.PlatformIssue)) return "مشكلة في المنصة";
            if (EqualsState(value, Categories.Other)) return "أخرى";

            // القيم غير القياسية تستخدم لوصف خيار «أخرى» الذي كتبه العميل.
            return string.IsNullOrWhiteSpace(value)
                ? "أخرى"
                : value.Trim();
        }

        public static string GetStatusLabel(string? value)
        {
            if (EqualsState(value, Statuses.Pending)) return "جديد";
            if (EqualsState(value, Statuses.InReview)) return "قيد المراجعة";
            if (EqualsState(value, Statuses.WaitingForReporter)) return "بانتظار ردك";
            if (EqualsState(value, Statuses.WaitingForReportedParty)) return "بانتظار رد الطرف الآخر";
            if (EqualsState(value, Statuses.Escalated)) return "مصعّد للمراجعة";
            if (EqualsState(value, Statuses.Resolved)) return "تم الحل";
            if (EqualsState(value, Statuses.Rejected)) return "مرفوض";
            if (EqualsState(value, Statuses.Closed)) return "مغلق";
            if (EqualsState(value, Statuses.Withdrawn)) return "مسحوب";
            return "قيد المتابعة";
        }

        public static string GetStatusTone(string? value)
        {
            if (EqualsState(value, Statuses.WaitingForReporter)) return "warning";
            if (EqualsState(value, Statuses.Escalated)) return "danger";
            if (EqualsState(value, Statuses.Resolved) || EqualsState(value, Statuses.Closed)) return "success";
            if (EqualsState(value, Statuses.Rejected) || EqualsState(value, Statuses.Withdrawn)) return "muted";
            if (EqualsState(value, Statuses.Pending)) return "new";
            return "info";
        }

        public static bool EqualsState(string? left, string? right)
        {
            return string.Equals(
                left?.Trim(),
                right?.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsIgnoreCase(
            IEnumerable<string> values,
            string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            return values.Any(value =>
                EqualsState(value, candidate));
        }

        private static string? FindCanonical(
            IEnumerable<string> values,
            string? candidate)
        {
            return values.FirstOrDefault(value =>
                EqualsState(value, candidate));
        }
    }
}
