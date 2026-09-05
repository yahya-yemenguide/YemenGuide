using System;

namespace YemenGuide.Core.DTOs.Customer
{
    // 1. الكلاس الرئيسي لعرض بيانات بروفايل العميل
    public class CustomerProfileDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        // ✨ بيانات التواصل وحالات التوثيق
        public string ContactInfo { get; set; } = string.Empty; // يمثل الإيميل
        public bool IsEmailVerified { get; set; } // حالة توثيق الإيميل (كانت ناقصة)

        public string PhoneNumber { get; set; } = string.Empty; // حقل رقم الهاتف المستقل


        // ✨ الحقول الجديدة (الهوية الرقمية)
        public string Bio { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;

        // ✨ الحقل المالي (المحفظة)
        public decimal WalletBalance { get; set; } = 0.00m;
        public string PreferredCurrency { get; set; } = "ريال يمني";

        // ✨ روابط التواصل الاجتماعي
        public string FacebookUrl { get; set; } = string.Empty;
        public string TwitterUrl { get; set; } = string.Empty;
        public string InstagramUrl { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
        public string AccountState { get; set; } = string.Empty;

        public string InitialLetter { get; set; } = "C";
        public string ProfileImage { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        // ✨ تفضيلات الواجهة والنظام
        public bool DarkModeEnabled { get; set; }
        public string Language { get; set; } = "ar";
        public bool ReceiveOffers { get; set; } = true; // الموافقة على العروض الترويجية

        // ✨ التنبيهات المخصصة للعميل
        public bool NewReservationAlert { get; set; } // تحديثات حالة حجوزاتي
        public bool NewMessageAlert { get; set; }     // رسائل من المنشآت
        public bool NewReviewAlert { get; set; }      // الردود على تقييماتي
    }

    // 2. كلاس تحديث البيانات الشخصية للعميل
    public class CustomerPersonalInfoUpdateDto
    {
        public string FullName { get; set; }
        public string Username { get; set; }
        public string ContactInfo { get; set; } // لتحديث البريد الإلكتروني
        public string PhoneNumber { get; set; } // لإتاحة حرية تحديث رقم الهاتف بشكل مستقل

        // ✨ الحقول الجديدة القابلة للتحديث بواسطة العميل
        public string Bio { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; }
        public string City { get; set; }

        public string FacebookUrl { get; set; }
        public string TwitterUrl { get; set; }
        public string InstagramUrl { get; set; }
    }

    // 3. كلاس تحديث كلمة المرور للعميل
    public class CustomerPasswordUpdateDto
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }

    // 4. كلاس حفظ التفضيلات التفاعلية
    public class CustomerPreferenceDto
    {
        public string Key { get; set; }
        public object Value { get; set; }
    }
}
