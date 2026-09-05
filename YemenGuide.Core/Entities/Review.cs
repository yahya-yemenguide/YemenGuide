using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class Review
    {
        public Review()
        {
            InverseParentReview = new HashSet<Review>();
            Reports = new HashSet<Report>();
            ReviewLikes = new HashSet<ReviewLike>();
        }

        public int ReviewId { get; set; }
        public int UserId { get; set; }
        public int PlaceId { get; set; }
        public int? Stars { get; set; }
        public string? ReviewText { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? OwnerReply { get; set; }
        public DateTime? ReplyCreatedAt { get; set; }
        public bool IsEdited { get; set; }
        public int? ParentReviewId { get; set; }

        public virtual Review? ParentReview { get; set; }
        public virtual Place Place { get; set; } = null!;
        public virtual User User { get; set; } = null!;
        public virtual ICollection<Review> InverseParentReview { get; set; }
        public virtual ICollection<Report> Reports { get; set; }
        public virtual ICollection<ReviewLike> ReviewLikes { get; set; }
    }
}
