using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class PlaceCommissionAccount
    {
        public int PlaceCommissionAccountId { get; set; }
        public int PlaceId { get; set; }
        public decimal AvailableBalance { get; set; }
        public decimal HeldBalance { get; set; }
        public decimal DebtAmount { get; set; }
        public bool? AllowNegativeBalance { get; set; }
        public decimal? DebtLimit { get; set; }
        public decimal? LowBalanceThreshold { get; set; }
        public string AccountStatus { get; set; } = null!;
        public DateTime? LastTopUpAt { get; set; }
        public DateTime? DebtStartedAt { get; set; }
        public DateTime? DebtDueAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = null!;

        public virtual Place Place { get; set; } = null!;
    }
}
