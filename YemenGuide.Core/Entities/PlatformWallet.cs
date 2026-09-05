using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class PlatformWallet
    {
        public PlatformWallet()
        {
            Advertisements = new HashSet<Advertisement>();
            CommissionTransactions = new HashSet<CommissionTransaction>();
        }

        public int PlatformWalletId { get; set; }
        public int WalletId { get; set; }
        public string AccountNumber { get; set; } = null!;
        public string BeneficiaryName { get; set; } = null!;
        public bool? IsActive { get; set; }
        public bool IsPrimary { get; set; }
        public string? Instructions { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public virtual SupportedWallet Wallet { get; set; } = null!;
        public virtual ICollection<Advertisement> Advertisements { get; set; }
        public virtual ICollection<CommissionTransaction> CommissionTransactions { get; set; }
    }
}
