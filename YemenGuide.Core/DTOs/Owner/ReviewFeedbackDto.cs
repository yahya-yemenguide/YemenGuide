using System;
using System.Collections.Generic;

namespace YemenGuide.Core.DTOs.Owner
{
    /// <summary>
    /// بيانات لوحة تقييمات المنشأة المحددة في هيدر المالك.
    /// </summary>
    public class ReviewFeedbackDto
    {
        public bool HasActivePlace { get; set; }
        public int ActivePlaceId { get; set; }
        public string? ErrorMessage { get; set; }

        // بيانات المالك اللازمة لواجهة الرد، دون الاستعلام من قاعدة البيانات داخل الـView.
        public string OwnerName { get; set; } = "المالك";
        public string? OwnerProfileImage { get; set; }
        public string OwnerInitial { get; set; } = "م";

        // ملخص التقييمات الخاص بالمنشأة المحددة فقط.
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int RepliedReviews { get; set; }
        public int UnansweredReviews { get; set; }
        public int OwnerRepliesCount { get; set; }
        public double ReplyRate { get; set; }

        public Dictionary<int, int> StarDistribution { get; set; } =
            new Dictionary<int, int>
            {
                [1] = 0,
                [2] = 0,
                [3] = 0,
                [4] = 0,
                [5] = 0
            };

        public OwnerReviewFilterDto Filter { get; set; } = new OwnerReviewFilterDto();

        // معلومات النتائج والصفحات بعد تطبيق الفلاتر.
        public int FilteredReviewsCount { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public List<ReviewItemDto> Reviews { get; set; } = new List<ReviewItemDto>();
    }

    /// <summary>
    /// الفلاتر التي يرسلها JavaScript إلى لوحة التقييمات.
    /// القيم النصية موحدة حتى تبقى الروابط والواجهة واضحة.
    /// </summary>
    public class OwnerReviewFilterDto
    {
        public string? Search { get; set; }

        // null = كل النجوم، والقيم المسموحة من 1 إلى 5.
        public int? Rating { get; set; }

        // all | positive | neutral | negative
        public string Sentiment { get; set; } = "all";

        // all | replied | unanswered
        public string ReplyState { get; set; } = "all";

        // newest | oldest | highest | lowest | most-liked
        public string SortBy { get; set; } = "newest";

        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    /// <summary>
    /// تقييم أو رد واحد داخل شجرة التعليقات.
    /// </summary>
    public class ReviewItemDto
    {
        public int ReviewId { get; set; }
        public int RootReviewId { get; set; }
        public int UserId { get; set; }

        public string UserName { get; set; } = "مستخدم";
        public string? ProfileImage { get; set; }
        public string InitialLetter { get; set; } = "م";

        public int? Stars { get; set; }
        public string ReviewText { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsEdited { get; set; }

        public int? ParentReviewId { get; set; }
        public string? RepliedToName { get; set; }

        public int LikesCount { get; set; }
        public bool IsLikedByMe { get; set; }

        // صلاحيات المالك على العقدة الحالية.
        public bool IsOwnerReply { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }

        // ملخص الردود التابعة للتقييم الرئيسي.
        public bool HasOwnerReply { get; set; }
        public int RepliesCount { get; set; }
        public int OwnerRepliesCount { get; set; }

        public List<ReviewItemDto> Replies { get; set; } = new List<ReviewItemDto>();
    }

    /// <summary>
    /// المودل الحالي للصفحة الجامعة. أبقيناه كما هو حتى لا تتعطل الدردشة
    /// أثناء فصل التقييمات في Controller وCSS وJavaScript مستقلين.
    /// </summary>
    public class OwnerChatPageModel
    {
        public List<ChatContactDto> ChatContacts { get; set; } = new List<ChatContactDto>();
        public ReviewFeedbackDto ReviewData { get; set; } = new ReviewFeedbackDto();
    }

    /// <summary>
    /// أبقينا الكلاس القديم الموجود في الملف حفاظًا على توافق بقية المشروع.
    /// </summary>
    public class EditPlaceDto
    {
        public int PlaceId { get; set; }
        public string PlaceNameAndDesc { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AddressDetail { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
