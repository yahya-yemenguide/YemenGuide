using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class PlaceAdditionalService
    {
        public PlaceAdditionalService()
        {
            BookingAdditionalServices = new HashSet<BookingAdditionalService>();
            Promotions = new HashSet<Promotion>();
        }

        public int ServiceId { get; set; }
        public int PlaceId { get; set; }
        public string ServiceName { get; set; } = null!;
        public decimal Price { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }

        public virtual Place Place { get; set; } = null!;
        public virtual ICollection<BookingAdditionalService> BookingAdditionalServices { get; set; }
        public virtual ICollection<Promotion> Promotions { get; set; }
    }
}
