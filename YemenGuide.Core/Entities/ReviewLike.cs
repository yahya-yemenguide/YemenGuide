using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class ReviewLike
    {
        public int LikeId { get; set; }
        public int UserId { get; set; }
        public int ReviewId { get; set; }
        public DateTime? CreatedAt { get; set; }

        public virtual Review Review { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
