from pathlib import Path

PATH = Path('YemenGuide.Web/Areas/Owner/Controllers/ReservationController.Calendar.cs')
raw = PATH.read_bytes(); bom = raw.startswith(b'\xef\xbb\xbf'); text = raw.decode('utf-8-sig')

def rep(old, new):
    global text
    if new in text: return
    count = text.count(old)
    if count != 1: raise SystemExit(f'controller1 expected 1 match, found {count}: {old[:100]!r}')
    text = text.replace(old, new, 1)

rep(
'''        private sealed class ManualBookingQuoteInternal
        {
            public OwnerManualBookingQuoteDto PublicQuote { get; set; } = new();
            public Place Place { get; set; } = null!;
            public Room? Room { get; set; }
            public PlacePackage? Package { get; set; }
            public User? Customer { get; set; }
            public List<PlaceAdditionalService> Services { get; set; } = new();''',
'''        private sealed class ManualBookingQuoteInternal
        {
            public OwnerManualBookingQuoteDto PublicQuote { get; set; } = new();
            public Place Place { get; set; } = null!;
            public Room? Room { get; set; }
            public PlacePackage? Package { get; set; }
            public User? Customer { get; set; }
            public OwnerWallet? PaymentWallet { get; set; }
            public List<PlaceAdditionalService> Services { get; set; } = new();''')

rep(
'''        private void ApplyCalendarDayDisplayCurrency(
            OwnerCalendarDayDetailsDto dto,
            OwnerReservationCurrencyContext currency)
        {
            foreach (var booking in dto.Bookings)
            {
                booking.TotalPrice = ToDisplayCurrency(booking.TotalPrice, currency);
                booking.DepositAmount = ToDisplayCurrency(booking.DepositAmount, currency);
                booking.PaidAmount = ToDisplayCurrency(booking.PaidAmount, currency);
                booking.RemainingAmount = ToDisplayCurrency(booking.RemainingAmount, currency);
            }
        }

        // =====================================================================
        // سياق التقويم — المنشأة تأتي من هيدر المالك فقط''',
'''        private void ApplyCalendarDayDisplayCurrency(
            OwnerCalendarDayDetailsDto dto,
            OwnerReservationCurrencyContext currency)
        {
            foreach (var booking in dto.Bookings)
            {
                booking.TotalPrice = ToDisplayCurrency(booking.TotalPrice, currency);
                booking.DepositAmount = ToDisplayCurrency(booking.DepositAmount, currency);
                booking.PaidAmount = ToDisplayCurrency(booking.PaidAmount, currency);
                booking.RemainingAmount = ToDisplayCurrency(booking.RemainingAmount, currency);
            }
        }

        private async Task<List<OwnerManualWalletOptionDto>> GetOwnerManualWalletOptionsAsync(int ownerId)
        {
            return await _context.OwnerWallets
                .AsNoTracking()
                .Where(item =>
                    item.OwnerId == ownerId &&
                    item.IsActive == true &&
                    item.Wallet.IsActive == true)
                .OrderBy(item => item.Wallet.WalletName)
                .ThenBy(item => item.OwnerWalletId)
                .Select(item => new OwnerManualWalletOptionDto
                {
                    OwnerWalletId = item.OwnerWalletId,
                    WalletId = item.WalletId,
                    WalletName = item.Wallet.WalletName,
                    AccountNumber = item.AccountNumber,
                    BeneficiaryName = item.BeneficiaryName
                })
                .ToListAsync(HttpContext.RequestAborted);
        }

        private static string? BuildManualPaymentReference(OwnerWallet? wallet, string? referenceNumber)
        {
            var reference = referenceNumber?.Trim();
            if (wallet == null)
            {
                return string.IsNullOrWhiteSpace(reference)
                    ? null
                    : reference.Length <= 100 ? reference : reference[..100];
            }

            var parts = new List<string>();
            var walletName = wallet.Wallet?.WalletName?.Trim();
            var accountNumber = wallet.AccountNumber?.Trim();
            if (!string.IsNullOrWhiteSpace(walletName)) parts.Add(walletName);
            if (!string.IsNullOrWhiteSpace(accountNumber)) parts.Add(accountNumber);
            if (!string.IsNullOrWhiteSpace(reference)) parts.Add(reference);

            var combined = string.Join(" | ", parts);
            if (string.IsNullOrWhiteSpace(combined)) return null;
            return combined.Length <= 100 ? combined : combined[..100];
        }

        // =====================================================================
        // سياق التقويم — المنشأة تأتي من هيدر المالك فقط''')

rep(
'''            var manualTemplate = ResolveManualBookingTemplate(place, supportsRooms, supportsPackages, totalUnits);

            var context = new OwnerCalendarContextDto''',
'''            var manualTemplate = ResolveManualBookingTemplate(place, supportsRooms, supportsPackages, totalUnits);
            var ownerWallets = await GetOwnerManualWalletOptionsAsync(ownerId);

            var context = new OwnerCalendarContextDto''')

rep(
'''                CheckInTime = place.PlacePolicy?.CheckInTime ?? place.CheckInTime ?? string.Empty,
                CheckOutTime = place.PlacePolicy?.CheckOutTime ?? place.CheckOutTime ?? string.Empty
            };''',
'''                CheckInTime = place.PlacePolicy?.CheckInTime ?? place.CheckInTime ?? string.Empty,
                CheckOutTime = place.PlacePolicy?.CheckOutTime ?? place.CheckOutTime ?? string.Empty,
                Wallets = ownerWallets
            };''')

rep(
'''            foreach (var service in rawServices)
                service.Price = ToDisplayCurrency(service.Price, currency);

            return Json(new''',
'''            foreach (var service in rawServices)
                service.Price = ToDisplayCurrency(service.Price, currency);

            var ownerWallets = await GetOwnerManualWalletOptionsAsync(ownerId);

            return Json(new''')

rep(
'''                    Rooms = roomOptions,
                    Packages = packageOptions,
                    Services = rawServices
                },''',
'''                    Rooms = roomOptions,
                    Packages = packageOptions,
                    Services = rawServices,
                    Wallets = ownerWallets
                },''')

PATH.write_bytes(text.encode('utf-8-sig' if bom else 'utf-8'))
print('controller patch 1 applied')
