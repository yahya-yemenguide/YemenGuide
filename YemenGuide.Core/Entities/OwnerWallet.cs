using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class OwnerWallet
    {
        public int OwnerWalletId { get; set; }
        public int OwnerId { get; set; }
        public int WalletId { get; set; }
        public string AccountNumber { get; set; } = null!;
        public string BeneficiaryName { get; set; } = null!;
        public bool? IsActive { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public virtual User Owner { get; set; } = null!;
        public virtual SupportedWallet Wallet { get; set; } = null!;
    }
}
