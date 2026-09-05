namespace YemenGuide.Core.Constants
{
    /// <summary>
    /// الأنواع الموحدة لكل إشعارات النظام.
    /// تستخدم للعميل والمالك والأدمن حتى لا تنتشر النصوص السحرية داخل Controllers.
    /// </summary>
    public static class NotificationTypes
    {
        public const string General = "General";

        public const string BookingCreated = "BookingCreated";
        public const string BookingAccepted = "BookingAccepted";
        public const string BookingConfirmed = "BookingConfirmed";
        public const string BookingRejected = "BookingRejected";
        public const string BookingCancelled = "BookingCancelled";
        public const string BookingExpired = "BookingExpired";

        public const string PaymentRequired = "PaymentRequired";
        public const string PaymentReceived = "PaymentReceived";
        public const string PaymentApproved = "PaymentApproved";
        public const string PaymentRejected = "PaymentRejected";
        /// <summary>
        /// تم إنشاء استرداد مالي للعميل ويحتاج متابعة/تحديد وجهة الاستلام.
        /// </summary>
        public const string RefundRequested = "RefundRequested";

        /// <summary>
        /// العميل حدد حساب الاستلام وأصبح الطلب جاهزًا لتحويل المالك.
        /// </summary>
        public const string RefundReadyForTransfer = "RefundReadyForTransfer";

        /// <summary>
        /// المالك سجل تحويل الاسترداد، والطلب ينتظر تأكيد العميل.
        /// </summary>
        public const string RefundTransferred = "RefundTransferred";

        /// <summary>
        /// أكد العميل الاستلام واكتملت دورة الاسترداد.
        /// </summary>
        public const string RefundCompleted = "RefundCompleted";

        public const string ReviewCreated = "ReviewCreated";
        public const string ReviewReply = "ReviewReply";

        public const string Message = "Message";

        public const string ReportCreated = "ReportCreated";
        public const string ReportReply = "ReportReply";
        public const string ReportEscalated = "ReportEscalated";
        public const string ReportWithdrawn = "ReportWithdrawn";
        public const string ReportResolved = "ReportResolved";
        public const string ReportRejected = "ReportRejected";

        public const string Promotion = "Promotion";
        public const string Advertisement = "Advertisement";
        public const string Account = "Account";
        public const string Security = "Security";

        // إشعارات إدارية عامة قابلة لإعادة الاستخدام في المرحلة الثانية.
        // الحدث نفسه يحدد المستلمين بواسطة AdminPermissionKeys المناسبة.
        public const string AdminCaseActionRequired = "AdminCaseActionRequired";
        public const string AdminFinanceActionRequired = "AdminFinanceActionRequired";
        public const string AdminAdvertisementActionRequired = "AdminAdvertisementActionRequired";
        public const string AdminPlaceActionRequired = "AdminPlaceActionRequired";
        public const string AdminAccountActionRequired = "AdminAccountActionRequired";
        public const string AdminSystemAlert = "AdminSystemAlert";
    }

    /// <summary>
    /// أنواع العناصر التي يمكن أن يشير إليها الإشعار.
    /// ReferenceId متعدد الاستخدامات، لذلك لا يوجد FK ثابت له في قاعدة البيانات.
    /// </summary>
    public static class NotificationReferenceTypes
    {
        public const string Booking = "Booking";
        public const string Review = "Review";
        public const string Chat = "Chat";
        public const string Report = "Report";
        public const string Promotion = "Promotion";
        public const string Advertisement = "Advertisement";
        public const string User = "User";
        public const string Place = "Place";
        public const string CommissionTransaction = "CommissionTransaction";
    }
}
