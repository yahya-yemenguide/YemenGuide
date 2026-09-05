using System;

namespace YemenGuide.Core.DTOs.Owner
{
    // 1. الكلاس الرئيسي لعرض بيانات بروفايل المالك
    public class OwnerProfileDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        // ✨ بيانات التواصل وحالات التوثيق
        public string ContactInfo { get; set; } = string.Empty;
        public bool IsEmailVerified { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;

        // ✨ الحقول الجديدة (الهوية الرقمية - مطابقة للعميل)
        public string Bio { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;

        // ✨ الحقل المالي (المحفظة)
        public decimal WalletBalance { get; set; } = 0.00m;
        public string PreferredCurrency { get; set; } = "ريال يمني";

        // ✨ الروابط الاجتماعية
        public string FacebookUrl { get; set; } = string.Empty;
        public string TwitterUrl { get; set; } = string.Empty;
        public string InstagramUrl { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
        public string AccountState { get; set; } = string.Empty;

        public string InitialLetter { get; set; } = "O";
        public string ProfileImage { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        // ✨ تفضيلات الواجهة (مشتركة مع الإدارة والعميل)
        public bool DarkModeEnabled { get; set; }
        public bool CompactMode { get; set; }
        public string Language { get; set; } = "ar";

        // ✨ التنبيهات المخصصة للمالك
        public bool NewReservationAlert { get; set; }
        public bool NewMessageAlert { get; set; }
        public bool NewReviewAlert { get; set; }
        public bool SoundAlert { get; set; }
    }

    // 2. كلاس تحديث البيانات الشخصية للمالك
    public class OwnerPersonalInfoUpdateDto
    {
        public string FullName { get; set; }
        public string Username { get; set; }
        public string ContactInfo { get; set; }
        public string PhoneNumber { get; set; }

        // ✨ الحقول الجديدة القابلة للتحديث بواسطة المالك
        public string Bio { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; }
        public string City { get; set; }

        public string FacebookUrl { get; set; }
        public string TwitterUrl { get; set; }
        public string InstagramUrl { get; set; }
    }

    // 3. كلاس تحديث كلمة المرور للمالك
    public class OwnerPasswordUpdateDto
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }

    // 4. كلاس حفظ التفضيلات التفاعلية (الوضع الليلي/المدمج)
    public class OwnerPreferenceDto
    {
        public string Key { get; set; }
        public object Value { get; set; }
    }
}
