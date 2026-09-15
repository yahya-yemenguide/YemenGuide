from pathlib import Path

PATH = Path('YemenGuide.Web/Areas/Owner/Controllers/ReservationController.Calendar.cs')
raw = PATH.read_bytes(); bom = raw.startswith(b'\xef\xbb\xbf'); text = raw.decode('utf-8-sig')

def rep(old, new):
    global text
    if new in text: return
    count = text.count(old)
    if count != 1: raise SystemExit(f'controller2 expected 1 match, found {count}: {old[:100]!r}')
    text = text.replace(old, new, 1)

rep(
'''            var place = await LoadManualBookingPlaceAsync(placeId.Value, ownerId);
            if (place == null) { failed.PublicQuote.Message = "المنشأة المختارة غير متاحة."; return failed; }
            var displayCurrency = await GetOwnerCurrencyContextAsync(place.PlaceId);''',
'''            var place = await LoadManualBookingPlaceAsync(placeId.Value, ownerId);
            if (place == null) { failed.PublicQuote.Message = "المنشأة المختارة غير متاحة."; return failed; }

            OwnerWallet? paymentWallet = null;
            if (IsState(request.PaymentMode, "Collected") && IsState(request.PaymentMethod, "Wallet"))
            {
                if (!request.OwnerWalletId.HasValue || request.OwnerWalletId.Value <= 0)
                {
                    failed.PublicQuote.Message = "حدد نوع المحفظة التي تم استلام العربون عليها.";
                    return failed;
                }

                paymentWallet = await _context.OwnerWallets
                    .AsNoTracking()
                    .Include(item => item.Wallet)
                    .FirstOrDefaultAsync(item =>
                        item.OwnerWalletId == request.OwnerWalletId.Value &&
                        item.OwnerId == ownerId &&
                        item.IsActive == true &&
                        item.Wallet.IsActive == true);

                if (paymentWallet == null)
                {
                    failed.PublicQuote.Message = "المحفظة المختارة غير مفعلة أو لا تتبع حساب المالك.";
                    return failed;
                }
            }

            var displayCurrency = await GetOwnerCurrencyContextAsync(place.PlaceId);''')

rep(
'''            if (IsState(request.PaymentMode, "Collected") && totalPrice > 0m && request.PaidAmount <= 0m)
            { failed.PublicQuote.Message = "أدخل المبلغ المستلم فعليًا."; return failed; }

            var collected = totalPrice > 0m && IsState(request.PaymentMode, "Collected") && request.PaidAmount > 0m;
            var normalizedPaidAmount = collected ? Math.Round(Math.Max(0m, request.PaidAmount), 2) : 0m;
            if (normalizedPaidAmount > totalPrice)
            { failed.PublicQuote.Message = "المبلغ المستلم لا يمكن أن يتجاوز إجمالي الحجز."; return failed; }
            if (collected && depositAmount > 0m && normalizedPaidAmount < depositAmount)
            {
                failed.PublicQuote.Message = $"المبلغ المستلم أقل من العربون المطلوب ({DisplayMoney(depositAmount, displayCurrency)}).";
                return failed;
            }''',
'''            var wantsCollected = totalPrice > 0m && IsState(request.PaymentMode, "Collected");
            var normalizedPaidAmount = wantsCollected
                ? Math.Round(Math.Max(0m, request.PaidAmount), 2)
                : 0m;

            // العربون للحجز اليدوي مصدره سياسة المنشأة وليس إدخالًا حرًا.
            if (wantsCollected && depositAmount > 0m && normalizedPaidAmount <= 0m)
            {
                normalizedPaidAmount = depositAmount;
            }

            var collected = wantsCollected && normalizedPaidAmount > 0m;
            if (normalizedPaidAmount > totalPrice)
            { failed.PublicQuote.Message = "المبلغ المستلم لا يمكن أن يتجاوز إجمالي الحجز."; return failed; }
            if (collected && depositAmount > 0m && Math.Abs(normalizedPaidAmount - depositAmount) > 0.01m)
            {
                failed.PublicQuote.Message = $"العربون المستلم يجب أن يساوي العربون المحدد في سياسة المنشأة ({DisplayMoney(depositAmount, displayCurrency)}). ويمكن تحصيل المتبقي لاحقًا من الحجز النشط.";
                return failed;
            }''')

rep(
'''            var defaultCommission = await _context.SystemSettings.AsNoTracking()
                .OrderByDescending(item => item.Id)
                .Select(item => (decimal?)item.CommissionPercentage)
                .FirstOrDefaultAsync() ?? 0m;
            var commissionPercentage = Math.Clamp(place.Category?.CommissionPercentage ?? defaultCommission, 0m, 100m);
            var platformCommission = totalPrice <= 0m ? 0m : Math.Round(totalPrice * commissionPercentage / 100m, 2);''',
'''            // الحجز اليدوي أنشأه المالك خارج مسار اكتساب الحجز في المنصة؛
            // لذلك لا تُحسب عليه عمولة ولا يُحجز من رصيد ضمان العمولات.
            const decimal commissionPercentage = 0m;
            const decimal platformCommission = 0m;''')

rep(
'''                Package = package,
                Customer = customer,
                Services = services,''',
'''                Package = package,
                Customer = customer,
                PaymentWallet = paymentWallet,
                Services = services,''')

rep(
'''                var paidAmount = isFreeBooking ? 0m
                    : isCollectedMode ? Math.Round(Math.Max(0m, request.PaidAmount), 2) : 0m;''',
'''                var paidAmount = isFreeBooking
                    ? 0m
                    : isCollectedMode
                        ? Math.Round(
                            request.PaidAmount > 0m
                                ? request.PaidAmount
                                : quote.PublicQuote.DepositAmount,
                            2)
                        : 0m;''')

rep(
'''                    TransferReferenceNumber = isCollected && !string.IsNullOrWhiteSpace(request.PaymentReferenceNumber)
                        ? request.PaymentReferenceNumber.Trim() : null,
                    AmountPaidOnline = 0m,
                    AmountToPayAtProperty = remaining,
                    AmountCollectedAtProperty = paidAmount,
                    UpdatedAt = now''',
'''                    TransferReferenceNumber = isCollected
                        ? BuildManualPaymentReference(quote.PaymentWallet, request.PaymentReferenceNumber)
                        : null,
                    AmountPaidOnline = 0m,
                    AmountToPayAtProperty = remaining,
                    AmountCollectedAtProperty = paidAmount,
                    CollectedAt = isCollected ? now : null,
                    UpdatedAt = now''')

rep(
'''                await _context.SaveChangesAsync();
                if (bookingState == "Confirmed" && quote.PublicQuote.PlatformCommission > 0m)
                {
                    var commissionResult = await _commissionFinanceService.HoldBookingCommissionAsync(
                        booking.BookingId, ownerId, HttpContext.RequestAborted);
                    if (!commissionResult.Success)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { success = false, message = commissionResult.Message });
                    }
                }

                await transaction.CommitAsync();''',
'''                await _context.SaveChangesAsync();

                // OwnerManual معفى من دورة العمولة بالكامل.
                await transaction.CommitAsync();''')

rep(
'''                var currency = await GetOwnerCurrencyContextAsync(booking.PlaceId);
                var inputPaidAmountYer = Math.Round(Math.Max(0m, ToYer(input.PaidAmount, currency)), 2);
                var isInitialPayment = IsState(booking.BookingState, "AwaitingPayment");''',
'''                var currency = await GetOwnerCurrencyContextAsync(booking.PlaceId);
                var inputPaidAmountYer = Math.Round(Math.Max(0m, ToYer(input.PaidAmount, currency)), 2);

                OwnerWallet? paymentWallet = null;
                if (IsState(input.PaymentMethod, "Wallet"))
                {
                    if (!input.OwnerWalletId.HasValue || input.OwnerWalletId.Value <= 0)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { success = false, message = "حدد نوع المحفظة المستخدمة في التحصيل." });
                    }

                    paymentWallet = await _context.OwnerWallets
                        .AsNoTracking()
                        .Include(item => item.Wallet)
                        .FirstOrDefaultAsync(item =>
                            item.OwnerWalletId == input.OwnerWalletId.Value &&
                            item.OwnerId == ownerId &&
                            item.IsActive == true &&
                            item.Wallet.IsActive == true);

                    if (paymentWallet == null)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { success = false, message = "المحفظة المختارة غير مفعلة أو لا تتبع حساب المالك." });
                    }
                }

                var isInitialPayment = IsState(booking.BookingState, "AwaitingPayment");''')

rep(
'''                financial.AmountToPayAtProperty = remainingAmount;
                if (!string.IsNullOrWhiteSpace(input.PaymentReferenceNumber))
                {
                    financial.TransferReferenceNumber = input.PaymentReferenceNumber.Trim();
                }
                financial.CollectedAt = now;''',
'''                financial.AmountToPayAtProperty = remainingAmount;
                financial.TransferReferenceNumber = BuildManualPaymentReference(
                    paymentWallet,
                    input.PaymentReferenceNumber);
                financial.CollectedAt = now;''')

rep(
'''                await _context.SaveChangesAsync();
                if (isInitialPayment && financial.PlatformCommission > 0m)
                {
                    var commissionResult = await _commissionFinanceService.HoldBookingCommissionAsync(
                        booking.BookingId, ownerId, HttpContext.RequestAborted);
                    if (!commissionResult.Success)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { success = false, message = commissionResult.Message });
                    }
                }

                await transaction.CommitAsync();''',
'''                await _context.SaveChangesAsync();

                // الدفعات اللاحقة للحجز اليدوي لا تنشئ أي حركة عمولة.
                await transaction.CommitAsync();''')

PATH.write_bytes(text.encode('utf-8-sig' if bom else 'utf-8'))
print('controller patch 2 applied')
