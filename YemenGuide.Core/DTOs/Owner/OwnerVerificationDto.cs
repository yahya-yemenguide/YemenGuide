using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace YemenGuide.Core.DTOs.Owner
{
    public class OwnerVerificationDto
    {
        // ==========================================
        // 💡 محدد المسار (سريع أم شامل)
        // ==========================================
        public bool IsFastTrack { get; set; } = true; // True = تسجيل سريع | False = إنشاء المنشأة قبل الدخول


        // ==========================================
        // 1. بيانات المستخدم (مطلوبة دائماً في كلا المسارين)
        // ==========================================

        [Required(ErrorMessage = "يرجى تحديد صفتك الوظيفية.")]
        public string JobTitle { get; set; } = null!;

        [Required(ErrorMessage = "رقم الهاتف الخاص بالإدارة (للطوارئ) مطلوب.")]
        public string AdminPhone { get; set; } = null!;


        // ==========================================
        // 2. بيانات الربط بالمنشأة / الفئة
        // ==========================================

        // ⚠️ تم إزالة [Required] وجعلها (Nullable) لأن المسار الشامل يحفظها مسبقاً عبر الـ Wizard
        public string? FacilityName { get; set; }

        // في التسجيل السريع: الفئة مطلوبة لعرض العمولة الصحيحة في العقد.
        public int? CategoryId { get; set; }

        // يستخدمه المسار الشامل بعد إنشاء المنشأة (يُفعّل بالكامل في المرحلة الثانية).
        public int? PlaceId { get; set; }

        public int? CityId { get; set; }

        public string? PublicPhone { get; set; }

        // الشعار اختياري
        public IFormFile? LogoImage { get; set; }


        // ==========================================
        // 3. بيانات الخطوة الثالثة: الميثاق الإلكتروني
        // ==========================================

        [Range(typeof(bool), "true", "true", ErrorMessage = "يجب الموافقة على ميثاق الشراكة لاستكمال التسجيل.")]
        public bool IsAgreedToTerms { get; set; }


        // ==========================================
        // 4. بيانات الخطوة الرابعة: الوثائق القانونية 
        // ==========================================

        [Required(ErrorMessage = "يرجى رفع صورة أو ملف السجل التجاري")]
        public IFormFile CommercialRegister { get; set; } = null!;

        [Required(ErrorMessage = "يرجى رفع صورة الوجه الأمامي للبطاقة الشخصية")]
        public IFormFile IdentityCardFront { get; set; } = null!;

        [Required(ErrorMessage = "يرجى رفع صورة الوجه الخلفي للبطاقة الشخصية")]
        public IFormFile IdentityCardBack { get; set; } = null!;

        // حقل اختياري في حال كان المسجل مفوضاً من المالك
        public IFormFile? AuthorizationLetter { get; set; }
    }

    /// <summary>
    /// لقطة سياسة المنصة المالية التي تظهر للمالك داخل العقد قبل الموافقة.
    /// القيم تأتي من إعدادات الأدمن، مع تطبيق أي Overrides موجودة لحساب المنشأة إن وُجد.
    /// لا تنفذ هذه الفئة أي عملية شحن أو خصم؛ دورها العرض فقط داخل رحلة التوثيق.
    /// </summary>
    public sealed class OwnerVerificationContractPolicyDto
    {
        public decimal GeneralCommissionPercentage { get; set; }
        public string Currency { get; set; } = "ريال يمني";
        public decimal MinimumCommissionTopUp { get; set; }
        public decimal LowBalanceThreshold { get; set; }
        public bool AllowNegativeBalance { get; set; }
        public decimal DebtLimit { get; set; }
        public int DebtGraceDays { get; set; }
        public bool SuspendBookingWhenBalanceInsufficient { get; set; }
        public bool HidePlaceWhenCommissionSuspended { get; set; }
        public bool UsesPlaceSpecificFinancialPolicy { get; set; }
    }

}
