from pathlib import Path


def one(path, old, new, label):
    p = Path(path)
    s = p.read_text(encoding="utf-8-sig")
    n = s.count(old)
    if n != 1:
        raise SystemExit(f"{label}: expected 1 match, found {n}")
    p.write_text(s.replace(old, new, 1), encoding="utf-8-sig")


owner = "YemenGuide.Web/Areas/Owner/Controllers/ReservationController.cs"
active = "YemenGuide.Web/Areas/Owner/Views/Reservation/_ActiveReservations.cshtml"
manage = "YemenGuide.Web/Areas/Owner/Views/Reservation/ManageReservations.cshtml"
customer = "YemenGuide.Web/Controllers/BookingsController.cs"

one(owner, "                NoShowGraceMinutes = 60,", "                NoShowGraceMinutes = 0,", "grace")
one(owner,
'''                CanOwnerCancel =
                    IsState(booking.BookingState, "Confirmed") &&
                    (financialRecord?.AmountCollectedAtProperty ?? 0m) <= 0m,''',
'''                CanOwnerCancel =
                    IsState(booking.BookingState, "Confirmed"),''', "owner cancel dto")
one(owner,
'''        private static DateTime GetNoShowAvailableAt(Booking booking)
        {
            // قاعدة تشغيل موحدة: عدم الحضور يصبح متاحًا بعد ساعة واحدة
            // من بداية الفترة الفعلية للحجز.
            return GetOperationalStartAt(booking)
                .AddHours(1);
        }''',
'''        private static DateTime GetNoShowAvailableAt(Booking booking)
        {
            // عدم الحضور يصبح متاحًا من بداية الفترة الفعلية للحجز.
            return GetOperationalStartAt(booking);
        }''', "no show time")
one(owner,
'''                return
                    $"التحصيل يتاح عند بداية الفترة في {startAt:yyyy-MM-dd HH:mm}. " +
                    $"عدم الحضور يتاح بعد ساعة في {noShowAt:yyyy-MM-dd HH:mm}. " +
                    $"الإنهاء يتاح بعد نهاية الفترة في {endAt:yyyy-MM-dd HH:mm}.";''',
'''                return
                    $"التحصيل وعدم الحضور يتاحان عند بداية الفترة في {startAt:yyyy-MM-dd HH:mm}. " +
                    $"الإنهاء يتاح بعد نهاية الفترة في {endAt:yyyy-MM-dd HH:mm}.";''', "prestart note")
one(owner,
'''                    return
                        "تم تسجيل تحصيل عند المنشأة؛ لذلك أصبح عدم الحضور وإلغاء الحجز من جهة المالك غير متاحين. " +
                        $"يمكن استكمال تحصيل أي مبلغ متبقٍ حتى نهاية الفترة في {endAt:yyyy-MM-dd HH:mm}، ثم إنهاء الحجز.";''',
'''                    return
                        "تم تسجيل تحصيل عند المنشأة؛ لذلك أصبح عدم الحضور غير متاح، بينما يظل إلغاء الحجز من جهة المالك متاحًا مع إنشاء استرداد كامل للمبالغ المحصلة. " +
                        $"يمكن استكمال تحصيل أي مبلغ متبقٍ حتى نهاية الفترة في {endAt:yyyy-MM-dd HH:mm}، ثم إنهاء الحجز.";''', "collected note")
one(owner,
'''                if (now < noShowAt)
                {
                    return
                        "التحصيل متاح الآن. " +
                        $"عدم الحضور يتاح بعد ساعة من البداية في {noShowAt:yyyy-MM-dd HH:mm}. " +
                        $"الإنهاء يتاح في {endAt:yyyy-MM-dd HH:mm}.";
                }

                return
                    "التحصيل متاح الآن، وعدم الحضور متاح عند التأكد من غياب العميل. " +
                    $"الإنهاء يتاح بعد نهاية الفترة في {endAt:yyyy-MM-dd HH:mm}.";''',
'''                return
                    "التحصيل متاح الآن، وعدم الحضور متاح عند التأكد من غياب العميل. " +
                    $"الإنهاء يتاح بعد نهاية الفترة في {endAt:yyyy-MM-dd HH:mm}.";''', "active note")
one(owner,
'''                        message =
                            $"لا يمكن تسجيل عدم حضور قبل مرور ساعة من بداية الفترة، " +
                            $"أي قبل {noShowAvailableAt:yyyy-MM-dd HH:mm}."''',
'''                        message =
                            $"لا يمكن تسجيل عدم حضور قبل بداية فترة الحجز في " +
                            $"{noShowAvailableAt:yyyy-MM-dd HH:mm}."''', "no show server message")
one(owner,
'''                if ((booking.FinancialRecord?.AmountCollectedAtProperty ?? 0m) > 0m)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message =
                            "لا يمكن إلغاء الحجز من جهة المالك بعد تسجيل تحصيل عند المنشأة. أكمل الحجز حتى نهايته ثم استخدم إنهاء الحجز."
                    });
                }

                var now =
                    DateTime.Now;''',
'''                var now =
                    DateTime.Now;''', "owner cancel block")
one(owner,
'''                var originalCommission =
                    Math.Round(''',
'''                var collectedAtProperty =
                    Math.Round(
                        Math.Max(0m, financialRecord?.AmountCollectedAtProperty ?? 0m),
                        2);

                var totalPaid =
                    Math.Round(
                        Math.Min(Math.Max(0m, booking.TotalPrice), paidOnline + collectedAtProperty),
                        2);

                var originalCommission =
                    Math.Round(''', "owner total paid insert")
one(owner,
'''                var refundRequest =
                    paidOnline > 0m
                        ? CreateRefundRequest(
                            booking,
                            paidOnline,
                            paidOnline,''',
'''                var refundRequest =
                    totalPaid > 0m
                        ? CreateRefundRequest(
                            booking,
                            totalPaid,
                            totalPaid,''', "owner refund total")
one(owner, "                if (paidOnline > 0m &&\n                    refundRequest != null)", "                if (totalPaid > 0m &&\n                    refundRequest != null)", "owner refund condition")
one(owner,
'''                    message = paidOnline > 0m
                        ? "تم إلغاء الحجز وإنشاء طلب استرداد كامل للعميل."
                        : "تم إلغاء الحجز وإبلاغ العميل.",
                    nextState = "Cancelled",
                    refundStatus = booking.RefundStatus,
                    refundAmount = paidOnline,''',
'''                    message = totalPaid > 0m
                        ? "تم إلغاء الحجز وإنشاء طلب استرداد كامل لكل المبالغ المدفوعة للعميل."
                        : "تم إلغاء الحجز وإبلاغ العميل.",
                    nextState = "Cancelled",
                    refundStatus = booking.RefundStatus,
                    refundAmount = totalPaid,''', "owner response")

one(active,
'''                                else if (!booking.CanMarkNoShowNow)
                                {
                                    <div class="compact-state-note warning"><i class="bi bi-person-x"></i> عدم الحضور يتاح بعد ساعة: @booking.NoShowAvailableAt.ToString("yyyy-MM-dd HH:mm")</div>
                                }''',
'''                                else if (!booking.CanMarkNoShowNow)
                                {
                                    <div class="compact-state-note warning"><i class="bi bi-person-x"></i> عدم الحضور يتاح عند بداية الحجز: @booking.NoShowAvailableAt.ToString("yyyy-MM-dd HH:mm")</div>
                                }''', "active no show")

one(manage,
'''                const noShowAt = Number.isFinite(configuredNoShowAt)
                    ? configuredNoShowAt
                    : startAt + (60 * 60 * 1000);''',
'''                const noShowAt = Number.isFinite(configuredNoShowAt)
                    ? configuredNoShowAt
                    : startAt;''', "js no show fallback")
one(manage, '                        "عدم الحضور يتاح بعد ساعة من بداية فترة الحجز."', '                        "عدم الحضور يتاح عند بداية فترة الحجز."', "js no show message")
one(manage,
'''                if (collected > 0) {
                    setReservationActionDisabled(
                        ownerCancelAction,
                        true,
                        "تم تسجيل تحصيل عند المنشأة؛ لذلك لا يمكن إلغاء الحجز من جهة المالك."
                    );
                }''',
'''                setReservationActionDisabled(
                    ownerCancelAction,
                    false
                );''', "js owner cancel")
one(manage, '                                <span>يمكن التحصيل على دفعات حتى سداد المتبقي. أول تحصيل فعلي يمنع تسجيل عدم حضور وإلغاء الحجز من جهة المالك، ولا ينهي الحجز.</span>', '                                <span>يمكن التحصيل على دفعات حتى سداد المتبقي. أول تحصيل فعلي يمنع تسجيل عدم حضور فقط، ولا يمنع إلغاء المالك ولا ينهي الحجز.</span>', "collection note")
one(manage, '                        const raw = currentButton.dataset.onlineRaw;', '                        const raw = currentButton.dataset.totalCollectedRaw || currentButton.dataset.onlineRaw;', "cancel dialog amount")

one(customer,
'''            /*
             * المصدر المالي الوحيد للاسترداد هو المبلغ الإلكتروني
             * الذي تم اعتماده فعليًا في FinancialRecord.AmountPaidOnline.
             * لا نستخدم DepositAmount ولا صورة الإثبات ولا TotalPrice
             * كمبلغ مدفوع.
             */
            var depositPaid =
                isConfirmedPayment
                    ? Math.Max(
                        0m,
                        financialRecord?
                            .AmountPaidOnline ??
                        0m)
                    : 0m;''',
'''            /*
             * مصدر حقيقة الاسترداد هو كل مبلغ تم تحصيله فعليًا للحجز:
             * المدفوع الإلكتروني المعتمد + ما سجله المالك كمحصّل عند المنشأة.
             */
            var depositPaid =
                isConfirmedPayment
                    ? Math.Max(0m, financialRecord?.AmountPaidOnline ?? 0m) +
                      Math.Max(0m, financialRecord?.AmountCollectedAtProperty ?? 0m)
                    : 0m;''', "customer paid source")
one(customer,
'''                var verifiedPaidOnline =
                    Math.Round(
                        Math.Min(
                            Math.Max(
                                0m,
                                booking.FinancialRecord?
                                    .AmountPaidOnline ??
                                0m),
                            Math.Max(
                                0m,
                                booking.TotalPrice)),
                        2);

                if (quote.RefundAmount >
                    verifiedPaidOnline + 0.01m)''',
'''                var verifiedPaidAmount =
                    Math.Round(
                        Math.Min(
                            Math.Max(0m, booking.TotalPrice),
                            Math.Max(0m, booking.FinancialRecord?.AmountPaidOnline ?? 0m) +
                            Math.Max(0m, booking.FinancialRecord?.AmountCollectedAtProperty ?? 0m)),
                        2);

                if (quote.RefundAmount >
                    verifiedPaidAmount + 0.01m)''', "customer verified paid")
one(customer, '                            "تعذر إنشاء الاسترداد لأن مبلغ الاسترداد يتجاوز المبلغ الإلكتروني المعتمد فعليًا للحجز."', '                            "تعذر إنشاء الاسترداد لأن مبلغ الاسترداد يتجاوز إجمالي المبلغ المحصل فعليًا للحجز."', "guard message")
one(customer, "                            DepositPaid =\n                                verifiedPaidOnline,", "                            DepositPaid =\n                                verifiedPaidAmount,", "refund snapshot")
one(customer, "                                        verifiedPaidOnline,", "                                        verifiedPaidAmount,", "fee cap")
one(customer, "                                        verifiedPaidOnline,", "                                        verifiedPaidAmount,", "refund cap")
