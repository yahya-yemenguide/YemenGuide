using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class PlaceCalendarException
    {
        public int ExceptionId { get; set; }
        public int PlaceId { get; set; }
        public DateTime ExceptionDate { get; set; }
        public string ExceptionType { get; set; } = null!;
        public string? Reason { get; set; }
        public decimal? CustomPrice { get; set; }
        public DateTime? CreatedAt { get; set; }

        public virtual Place Place { get; set; } = null!;
    }
}
