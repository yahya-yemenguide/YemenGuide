from pathlib import Path

PATH = Path('YemenGuide.Core/DTOs/Owner/OwnerCalendarDtos.cs')
raw = PATH.read_bytes()
bom = raw.startswith(b'\xef\xbb\xbf')
text = raw.decode('utf-8-sig')

def rep(old, new):
    global text
    if new in text:
        return
    count = text.count(old)
    if count != 1:
        raise SystemExit(f'OwnerCalendarDtos.cs: expected 1 match, found {count}: {old[:90]!r}')
    text = text.replace(old, new, 1)

rep(
'''        public string CheckInTime { get; set; } = string.Empty;
        public string CheckOutTime { get; set; } = string.Empty;
    }''',
'''        public string CheckInTime { get; set; } = string.Empty;
        public string CheckOutTime { get; set; } = string.Empty;
        public List<OwnerManualWalletOptionDto> Wallets { get; set; } = new();
    }''')

rep(
'''        public List<OwnerManualPackageOptionDto> Packages { get; set; } = new();
        public List<OwnerManualServiceOptionDto> Services { get; set; } = new();
    }

    public sealed class OwnerManualRoomOptionDto''',
'''        public List<OwnerManualPackageOptionDto> Packages { get; set; } = new();
        public List<OwnerManualServiceOptionDto> Services { get; set; } = new();
        public List<OwnerManualWalletOptionDto> Wallets { get; set; } = new();
    }

    public sealed class OwnerManualWalletOptionDto
    {
        public int OwnerWalletId { get; set; }
        public int WalletId { get; set; }
        public string WalletName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string BeneficiaryName { get; set; } = string.Empty;
        public string DisplayText => string.IsNullOrWhiteSpace(AccountNumber)
            ? WalletName
            : $"{WalletName} · {AccountNumber}";
    }

    public sealed class OwnerManualRoomOptionDto''')

rep(
'''        [StringLength(30)]
        public string? PaymentMethod { get; set; }

        [StringLength(100)]
        public string? PaymentReferenceNumber { get; set; }''',
'''        [StringLength(30)]
        public string? PaymentMethod { get; set; }

        public int? OwnerWalletId { get; set; }

        [StringLength(100)]
        public string? PaymentReferenceNumber { get; set; }''')

rep(
'''        [Required]
        [StringLength(30)]
        public string PaymentMethod { get; set; } = "Cash";

        [StringLength(100)]
        public string? PaymentReferenceNumber { get; set; }''',
'''        [Required]
        [StringLength(30)]
        public string PaymentMethod { get; set; } = "Cash";

        public int? OwnerWalletId { get; set; }

        [StringLength(100)]
        public string? PaymentReferenceNumber { get; set; }''')

PATH.write_bytes(text.encode('utf-8-sig' if bom else 'utf-8'))
print('DTO patch applied')
