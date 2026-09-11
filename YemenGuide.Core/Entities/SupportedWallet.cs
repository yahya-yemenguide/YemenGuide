using System;
using System.Collections.Generic;

namespace YemenGuide.Core.Entities
{
    public partial class SupportedWallet
    {
        public SupportedWallet()
        {
            Advertisements = new HashSet<Advertisement>();
            OwnerWallets = new HashSet<OwnerWallet>();
            RefundRequests = new HashSet<RefundRequest>();
        }

        public int WalletId { get; set; }
        public string WalletName { get; set; } = null!;
        public string? WalletNameEn { get; set; }
        public string? LogoPath { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }

        public virtual PlatformWallet? PlatformWallet { get; set; }
        public virtual ICollection<Advertisement> Advertisements { get; set; }
        public virtual ICollection<OwnerWallet> OwnerWallets { get; set; }
        public virtual ICollection<RefundRequest> RefundRequests { get; set; }
    }
}
