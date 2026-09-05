using System;
using System.Collections.Generic;

namespace YemenGuide.Core.DTOs.Customer
{
    // =========================================================================
    // 1. كلاس لاستقبال التقييم الجديد من العميل وإرساله للسيرفر
    // =========================================================================
    public class ReviewViewModel
    {
        public int PlaceId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }

    // =========================================================================
    // 2. كلاس مساعد لنقل وعرض التعليقات من قاعدة البيانات إلى واجهة المستخدم
    // =========================================================================
    public class ReviewDto
    {
        public int ReviewId { get; set; }
        public int UserId { get; set; }             // لمعرفة هل المستخدم الحالي هو صاحب التعليق
        public string UserName { get; set; } = string.Empty;
        public string? ProfileImage { get; set; }
        public int Stars { get; set; }
        public string? ReviewText { get; set; }
        public DateTime CreatedAt { get; set; }
        public int LikesCount { get; set; }
        public bool IsLikedByMe { get; set; }
        public bool IsEdited { get; set; }
        public List<ReviewDto> Replies { get; set; } = new List<ReviewDto>();
    }

    // =========================================================================
    // 3. كلاس طلب تعديل التقييم أو الرد
    // =========================================================================
    public class EditReviewDto
    {
        public int ReviewId { get; set; }
        public string NewText { get; set; } = string.Empty;
    }

    // =========================================================================
    // 4. كلاس طلب حذف التقييم أو الرد
    // =========================================================================
    public class DeleteReviewDto
    {
        public int ReviewId { get; set; }
    }

    // =========================================================================
    // 5. كلاس طلب الإعجاب
    // =========================================================================
    public class ReviewLikeRequestDto
    {
        public int ReviewId { get; set; }
    }

    // =========================================================================
    // 6. كلاس طلب المفضلة
    // =========================================================================
    public class FavoriteRequestDto
    {
        public int PlaceId { get; set; }
    }

    // =========================================================================
    // 7. 🌟 كلاسات لوحة تحكم العميل (تقييماتي) - التي أضفناها للتو 🌟
    // =========================================================================
    public class CustomerReviewsViewModel
    {
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public Dictionary<int, int> StarDistribution { get; set; } = new Dictionary<int, int>();
        public List<CustomerReviewItemDto> Reviews { get; set; } = new List<CustomerReviewItemDto>();
    }

    public class CustomerReviewItemDto
    {
        public int ReviewId { get; set; }
        public int PlaceId { get; set; }
        public string PlaceName { get; set; } = string.Empty;
        public string PlaceImage { get; set; } = string.Empty;
        public int Stars { get; set; }
        public string ReviewText { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // بيانات رد المالك (إن وُجد)
        public string? OwnerReply { get; set; }
        public DateTime? OwnerReplyDate { get; set; }
    }
}
