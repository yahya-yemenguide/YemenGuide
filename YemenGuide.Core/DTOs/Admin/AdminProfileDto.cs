using System;

namespace YemenGuide.Core.DTOs.Admin
{
    // 1. الكلاس الرئيسي لعرض بيانات بروفايل المدير
    public class AdminProfileDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        // ✨ بيانات التواصل وحالات التوثيق (مطابق للمالك)
        public string ContactInfo { get; set; } = string.Empty;
        public bool IsEmailVerified { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;

        // ✨ الحقول الجديدة (الهوية الرقمية - مطابق للمالك)
        public string Bio { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;

        // ✨ الروابط الاجتماعية
        public string FacebookUrl { get; set; } = string.Empty;
        public string TwitterUrl { get; set; } = string.Empty;
        public string InstagramUrl { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
        public string AccountState { get; set; } = string.Empty;

        public string InitialLetter { get; set; } = "A";
        public string ProfileImage { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        // ✨ تفضيلات الواجهة
        public bool DarkModeEnabled { get; set; }
        public bool CompactMode { get; set; }
        public string Language { get; set; } = "ar";
        public string PreferredCurrency { get; set; } = "ريال يمني";
    }

    // 2. كلاس تحديث البيانات الشخصية للمدير
    public class AdminPersonalInfoUpdateDto
    {
        public string FullName { get; set; }
        public string Username { get; set; }
        public string ContactInfo { get; set; }
        public string PhoneNumber { get; set; }

        public string Bio { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; }
        public string City { get; set; }

        public string FacebookUrl { get; set; }
        public string TwitterUrl { get; set; }
        public string InstagramUrl { get; set; }
    }

    // 3. كلاس تحديث كلمة المرور للمدير
    public class AdminPasswordUpdateDto
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }

    // 4. كلاس حفظ التفضيلات التفاعلية (الوضع الليلي/المدمج)
    public class AdminPreferenceDto
    {
        public string Key { get; set; }
        public object Value { get; set; }
    }

    // 5. كلاس طلب تغيير الإيميل (نظام التوثيق المحمي)
    public class AdminEmailChangeRequestDto
    {
        public string NewEmail { get; set; }
    }
}
