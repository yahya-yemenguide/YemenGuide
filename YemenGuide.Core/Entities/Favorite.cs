using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class Favorite
    {
        public int FavoriteId { get; set; }
        public int UserId { get; set; }
        public int PlaceId { get; set; }
        public DateTime? CreatedAt { get; set; }

        public virtual Place Place { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
