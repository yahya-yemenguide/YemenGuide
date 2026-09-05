using System.Collections.Generic;

namespace YemenGuide.Core.Constants
{
    public static class AdminPermissionKeys
    {
        public static class Dashboard
        {
            public const string View = "Dashboard.View";
        }

        public static class Users
        {
            public const string View = "Users.View";
            public const string ViewVerificationDocuments = "Users.ViewVerificationDocuments";
            public const string ChangeState = "Users.ChangeState";
            public const string Delete = "Users.Delete";
        }

        public static class Places
        {
            public const string View = "Places.View";
            public const string Create = "Places.Create";
            public const string Edit = "Places.Edit";
            public const string Review = "Places.Review";
            public const string ChangeStatus = "Places.ChangeStatus";
            public const string Delete = "Places.Delete";
            public const string ReassignOwner = "Places.ReassignOwner";
        }

        public static class MasterData
        {
            public const string View = "MasterData.View";
            public const string ManageCategories = "MasterData.ManageCategories";
            public const string ManageCities = "MasterData.ManageCities";
        }

        public static class Cases
        {
            public const string View = "Cases.View";
            public const string Create = "Cases.Create";
            public const string Assign = "Cases.Assign";
            public const string Manage = "Cases.Manage";
            public const string Escalate = "Cases.Escalate";
            public const string Resolve = "Cases.Resolve";
            public const string Reopen = "Cases.Reopen";
        }

        public static class Finance
        {
            public const string View = "Finance.View";
            public const string ReviewTopUps = "Finance.ReviewTopUps";
            public const string ManageCommissionAccounts = "Finance.ManageCommissionAccounts";
            public const string ManagePlatformWallets = "Finance.ManagePlatformWallets";
            public const string ManageSettings = "Finance.ManageSettings";
            public const string Reports = "Finance.Reports";
        }

        public static class Advertisements
        {
            public const string View = "Advertisements.View";
            public const string Review = "Advertisements.Review";
            public const string ManagePlans = "Advertisements.ManagePlans";
            public const string Control = "Advertisements.Control";
            public const string Reports = "Advertisements.Reports";
        }

        public static class Settings
        {
            public const string View = "Settings.View";
            public const string ManageGeneral = "Settings.ManageGeneral";
            public const string ManagePaymentNetworks = "Settings.ManagePaymentNetworks";
            public const string ManageHeroSlides = "Settings.ManageHeroSlides";
        }

        public static class Employees
        {
            public const string View = "Employees.View";
            public const string Create = "Employees.Create";
            public const string Edit = "Employees.Edit";
            public const string ChangeStatus = "Employees.ChangeStatus";
            // مفتاح قديم للتوافق مع قواعد بيانات/نسخ سابقة فقط.
            // لم يعد يُعرض كصلاحية ولا يُستخدم لإعادة تعيين كلمة مرور الموظف من لوحة الإدارة.
            public const string ResetPassword = "Employees.ResetPassword";
        }

        public static class Roles
        {
            public const string View = "Roles.View";
            public const string Manage = "Roles.Manage";
        }

        public static class AuditLogs
        {
            public const string View = "AuditLogs.View";
        }
    }

    public sealed class AdminPermissionDefinition
    {
        public AdminPermissionDefinition(
            string key,
            string name,
            string group,
            string description,
            bool isSensitive = false)
        {
            Key = key;
            Name = name;
            Group = group;
            Description = description;
            IsSensitive = isSensitive;
        }

        public string Key { get; }
        public string Name { get; }
        public string Group { get; }
        public string Description { get; }
        public bool IsSensitive { get; }
    }

    public static class AdminPermissionCatalog
    {
        public static IReadOnlyList<AdminPermissionDefinition> All { get; } =
            new List<AdminPermissionDefinition>
            {
                new(
                    AdminPermissionKeys.Dashboard.View,
                    "مشاهدة لوحة التحكم",
                    "لوحة التحكم",
                    "عرض مؤشرات وإحصائيات لوحة الإدارة والبحث العام."),

                new(
                    AdminPermissionKeys.Users.View,
                    "مشاهدة المستخدمين",
                    "إدارة المستخدمين",
                    "عرض قائمة العملاء والملاك وبياناتهم الأساسية."),
                new(
                    AdminPermissionKeys.Users.ViewVerificationDocuments,
                    "مراجعة وثائق التحقق",
                    "إدارة المستخدمين",
                    "عرض وثائق التحقق الخاصة بالملاك لمراجعتها.",
                    true),
                new(
                    AdminPermissionKeys.Users.ChangeState,
                    "تغيير حالة المستخدم",
                    "إدارة المستخدمين",
                    "تفعيل أو تعليق أو حظر أو رفض حساب مستخدم.",
                    true),
                new(
                    AdminPermissionKeys.Users.Delete,
                    "الحذف النهائي للمستخدم",
                    "إدارة المستخدمين",
                    "حذف المستخدم المؤهل للحذف النهائي من قاعدة البيانات.",
                    true),

                new(
                    AdminPermissionKeys.Places.View,
                    "مشاهدة المنشآت",
                    "إدارة المنشآت",
                    "عرض قوائم المنشآت وتفاصيلها وحالاتها."),
                new(
                    AdminPermissionKeys.Places.Create,
                    "إضافة منشأة",
                    "إدارة المنشآت",
                    "إنشاء منشأة من لوحة الإدارة وربطها بمالك أو حساب تشغيل الإدارة.",
                    true),
                new(
                    AdminPermissionKeys.Places.Edit,
                    "تعديل منشأة",
                    "إدارة المنشآت",
                    "تعديل بيانات المنشأة من لوحة الإدارة.",
                    true),
                new(
                    AdminPermissionKeys.Places.Review,
                    "اعتماد أو رفض منشأة",
                    "إدارة المنشآت",
                    "مراجعة طلبات المنشآت واعتمادها أو رفضها مع السبب.",
                    true),
                new(
                    AdminPermissionKeys.Places.ChangeStatus,
                    "تعليق أو تنشيط منشأة",
                    "إدارة المنشآت",
                    "تعليق منشأة أو إعادة تنشيطها.",
                    true),
                new(
                    AdminPermissionKeys.Places.Delete,
                    "حذف منشأة",
                    "إدارة المنشآت",
                    "تنفيذ الحذف النهائي للمنشأة وفق قواعد النظام.",
                    true),
                new(
                    AdminPermissionKeys.Places.ReassignOwner,
                    "تغيير مالك المنشأة",
                    "إدارة المنشآت",
                    "نقل ملكية المنشأة من مالك إلى مالك آخر.",
                    true),

                new(
                    AdminPermissionKeys.MasterData.View,
                    "مشاهدة تصنيفات النظام",
                    "تصنيفات النظام",
                    "عرض الفئات والمدن والبيانات الأساسية التابعة لها."),
                new(
                    AdminPermissionKeys.MasterData.ManageCategories,
                    "إدارة الفئات",
                    "تصنيفات النظام",
                    "إضافة وتعديل وحذف الفئات وضبط خصائصها.",
                    true),
                new(
                    AdminPermissionKeys.MasterData.ManageCities,
                    "إدارة المدن",
                    "تصنيفات النظام",
                    "إضافة وتعديل وحذف المدن.",
                    true),

                new(
                    AdminPermissionKeys.Cases.View,
                    "مشاهدة الشكاوى والبلاغات",
                    "الشكاوى والبلاغات",
                    "عرض القضايا والتفاصيل والتبويبات والملخصات."),
                new(
                    AdminPermissionKeys.Cases.Create,
                    "إنشاء قضية إدارية",
                    "الشكاوى والبلاغات",
                    "إنشاء شكوى أو بلاغ إداري من لوحة الإدارة.",
                    true),
                new(
                    AdminPermissionKeys.Cases.Assign,
                    "إسناد القضية لموظف",
                    "الشكاوى والبلاغات",
                    "تعيين موظف إدارة مسؤول عن متابعة القضية.",
                    true),
                new(
                    AdminPermissionKeys.Cases.Manage,
                    "متابعة ومعالجة القضية",
                    "الشكاوى والبلاغات",
                    "بدء المراجعة وطلب الردود وتغيير الأولوية وإضافة الملاحظات الداخلية.",
                    true),
                new(
                    AdminPermissionKeys.Cases.Escalate,
                    "تصعيد القضية",
                    "الشكاوى والبلاغات",
                    "رفع القضية إلى مستوى تصعيد أعلى.",
                    true),
                new(
                    AdminPermissionKeys.Cases.Resolve,
                    "حل القضية",
                    "الشكاوى والبلاغات",
                    "إصدار القرار النهائي وحل القضية.",
                    true),
                new(
                    AdminPermissionKeys.Cases.Reopen,
                    "إعادة فتح القضية",
                    "الشكاوى والبلاغات",
                    "إعادة فتح قضية سبق حلها وفق قواعد النظام.",
                    true),

                new(
                    AdminPermissionKeys.Finance.View,
                    "مشاهدة الإدارة المالية",
                    "الإدارة المالية",
                    "عرض النظرة العامة وعمولات الحجوزات والحركات وحسابات الضمان.",
                    true),
                new(
                    AdminPermissionKeys.Finance.ReviewTopUps,
                    "مراجعة طلبات شحن الضمان",
                    "الإدارة المالية",
                    "عرض إثباتات الشحن واعتماد أو رفض طلبات شحن رصيد الضمان.",
                    true),
                new(
                    AdminPermissionKeys.Finance.ManageCommissionAccounts,
                    "إدارة حسابات ضمان العمولة",
                    "الإدارة المالية",
                    "تعديل سياسة حساب الضمان وتغيير حالة الحساب.",
                    true),
                new(
                    AdminPermissionKeys.Finance.ManagePlatformWallets,
                    "إدارة حسابات المنصة",
                    "الإدارة المالية",
                    "إضافة وتعديل وتفعيل وإيقاف وتعيين الحساب الرئيسي للمنصة.",
                    true),
                new(
                    AdminPermissionKeys.Finance.ManageSettings,
                    "إدارة الإعدادات المالية",
                    "الإدارة المالية",
                    "تعديل إعدادات وسياسات العمولة والرصيد والحدود المالية.",
                    true),
                new(
                    AdminPermissionKeys.Finance.Reports,
                    "التقارير والمستندات المالية",
                    "الإدارة المالية",
                    "عرض وطباعة وتصدير التقارير والمستندات المالية.",
                    true),

                new(
                    AdminPermissionKeys.Advertisements.View,
                    "مشاهدة الإعلانات",
                    "إدارة الإعلانات",
                    "عرض صفحة إدارة الإعلانات وقائمة الإعلانات وتفاصيلها."),
                new(
                    AdminPermissionKeys.Advertisements.Review,
                    "مراجعة طلبات الإعلانات",
                    "إدارة الإعلانات",
                    "مراجعة محتوى الإعلان وإثبات الدفع ثم اعتماد الطلب أو رفضه مع توضيح السبب.",
                    true),
                new(
                    AdminPermissionKeys.Advertisements.ManagePlans,
                    "إدارة باقات الإعلانات",
                    "إدارة الإعلانات",
                    "إنشاء وتعديل وتفعيل وإيقاف باقات الإعلانات وأسعارها وحدودها.",
                    true),
                new(
                    AdminPermissionKeys.Advertisements.Control,
                    "التحكم في الإعلانات",
                    "إدارة الإعلانات",
                    "إيقاف الإعلان أو استئنافه أو إلغاؤه وتسجيل عمليات استرداد المبالغ.",
                    true),
                new(
                    AdminPermissionKeys.Advertisements.Reports,
                    "مشاهدة تقارير الإعلانات",
                    "إدارة الإعلانات",
                    "عرض مؤشرات الأداء والمشاهدات والنقرات والتحويلات والإنفاق الإعلاني."),

                new(
                    AdminPermissionKeys.Settings.View,
                    "مشاهدة إعدادات النظام",
                    "إعدادات النظام",
                    "عرض إعدادات المنصة العامة والمحتوى والمحافظ المدعومة."),
                new(
                    AdminPermissionKeys.Settings.ManageGeneral,
                    "تعديل إعدادات النظام العامة",
                    "إعدادات النظام",
                    "تعديل هوية المنصة والتسجيل والصيانة والتواصل والسياسات العامة.",
                    true),
                new(
                    AdminPermissionKeys.Settings.ManagePaymentNetworks,
                    "إدارة شبكات ومحافظ الدفع",
                    "إعدادات النظام",
                    "إضافة وتفعيل وإيقاف وحذف شبكات ومحافظ الدفع المدعومة.",
                    true),
                new(
                    AdminPermissionKeys.Settings.ManageHeroSlides,
                    "إدارة شرائح الواجهة الرئيسية",
                    "إعدادات النظام",
                    "إضافة وتعديل وترتيب وتفعيل وحذف شرائح الواجهة الرئيسية."),

                new(
                    AdminPermissionKeys.Employees.View,
                    "مشاهدة الموظفين",
                    "الموظفون والصلاحيات",
                    "عرض قائمة موظفي الإدارة وبياناتهم الوظيفية."),
                new(
                    AdminPermissionKeys.Employees.Create,
                    "إضافة موظف",
                    "الموظفون والصلاحيات",
                    "إنشاء حساب موظف إدارة جديد وربطه بدور.",
                    true),
                new(
                    AdminPermissionKeys.Employees.Edit,
                    "تعديل بيانات الموظف",
                    "الموظفون والصلاحيات",
                    "تعديل البيانات الوظيفية والدور المعيّن للموظف.",
                    true),
                new(
                    AdminPermissionKeys.Employees.ChangeStatus,
                    "تغيير حالة الموظف",
                    "الموظفون والصلاحيات",
                    "تعليق أو إعادة تفعيل أو وضع الموظف في إجازة أو إنهاء خدمته.",
                    true),
                new(
                    AdminPermissionKeys.Roles.View,
                    "مشاهدة الأدوار والصلاحيات",
                    "الموظفون والصلاحيات",
                    "عرض الأدوار ومجموعات الصلاحيات المرتبطة بها."),
                new(
                    AdminPermissionKeys.Roles.Manage,
                    "إدارة الأدوار والصلاحيات",
                    "الموظفون والصلاحيات",
                    "إنشاء وتعديل وتعطيل الأدوار وتحديد صلاحيات كل دور.",
                    true),
                new(
                    AdminPermissionKeys.AuditLogs.View,
                    "مشاهدة سجل العمليات",
                    "الموظفون والصلاحيات",
                    "عرض سجل العمليات الإدارية والتغييرات الحساسة.",
                    true)
            };
    }
}

