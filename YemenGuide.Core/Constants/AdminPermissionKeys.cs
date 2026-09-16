﻿using System.Collections.Generic;

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

            // مفاتيح قديمة للتوافق مع قواعد البيانات السابقة فقط.
            public const string ManageGeneral = "Settings.ManageGeneral";
            public const string ManagePaymentNetworks = "Settings.ManagePaymentNetworks";

            public const string ViewBranding = "Settings.Branding.View";
            public const string ManageBranding = "Settings.Branding.Manage";

            public const string ViewPolicies = "Settings.Policies.View";
            public const string ManagePolicies = "Settings.Policies.Manage";

            public const string ViewContact = "Settings.Contact.View";
            public const string ManageContact = "Settings.Contact.Manage";

            public const string ViewMaintenance = "Settings.Maintenance.View";
            public const string ManageMaintenance = "Settings.Maintenance.Manage";

            public const string ViewPaymentNetworks = "Settings.PaymentNetworks.View";
            public const string AddPaymentNetwork = "Settings.PaymentNetworks.Add";
            public const string EditPaymentNetwork = "Settings.PaymentNetworks.Edit";
            public const string ChangePaymentNetworkStatus = "Settings.PaymentNetworks.ChangeStatus";
            public const string DeletePaymentNetwork = "Settings.PaymentNetworks.Delete";
            public const string ManageDefaultWalletLogo = "Settings.PaymentNetworks.ManageDefaultLogo";

            public const string ViewHeroSlides = "Settings.HeroSlides.View";
            public const string ManageHeroSlides = "Settings.ManageHeroSlides";

            public const string ViewBackups = "Settings.Backups.View";
            public const string CreateBackup = "Settings.Backups.Create";
            public const string DownloadBackup = "Settings.Backups.Download";
            public const string RestoreBackup = "Settings.Backups.Restore";
            public const string DeleteBackup = "Settings.Backups.Delete";
            public const string ManageBackupSchedule = "Settings.Backups.ManageSchedule";
            public const string ManageBackupCloud = "Settings.Backups.ManageCloud";
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
                    "دخول قسم إعدادات النظام",
                    "إعدادات النظام",
                    "صلاحية أساسية لدخول صفحة إعدادات النظام، وتُفهم تلقائيًا عند منح أي صلاحية فرعية."),

                new(
                    AdminPermissionKeys.Settings.ViewBranding,
                    "مشاهدة الهوية والماركة",
                    "إعدادات النظام",
                    "عرض هوية المنصة والشعارات والثيم الموسمي وإعدادات الترحيب."),
                new(
                    AdminPermissionKeys.Settings.ManageBranding,
                    "تعديل الهوية والماركة",
                    "إعدادات النظام",
                    "تعديل اسم ووصف المنصة والشعارات والثيم الموسمي والبنر.",
                    true),

                new(
                    AdminPermissionKeys.Settings.ViewPolicies,
                    "مشاهدة سياسات التشغيل",
                    "إعدادات النظام",
                    "عرض سياسات التسجيل والموافقة وحدود الصور والإعدادات المالية المسموح بمشاهدتها."),
                new(
                    AdminPermissionKeys.Settings.ManagePolicies,
                    "تعديل سياسات التشغيل",
                    "إعدادات النظام",
                    "تعديل التسجيل والموافقة التلقائية وحدود الصور دون منح إدارة الإعدادات المالية.",
                    true),

                new(
                    AdminPermissionKeys.Settings.ViewContact,
                    "مشاهدة التواصل والروابط",
                    "إعدادات النظام",
                    "عرض البريد والهاتف وروابط التواصل الخاصة بالمنصة."),
                new(
                    AdminPermissionKeys.Settings.ManageContact,
                    "تعديل التواصل والروابط",
                    "إعدادات النظام",
                    "تعديل البريد والهاتف وروابط التواصل والواتساب.",
                    true),

                new(
                    AdminPermissionKeys.Settings.ViewMaintenance,
                    "مشاهدة الصيانة والطوارئ",
                    "إعدادات النظام",
                    "عرض حالة الصيانة وإعداداتها الحالية."),
                new(
                    AdminPermissionKeys.Settings.ManageMaintenance,
                    "إدارة الصيانة والطوارئ",
                    "إعدادات النظام",
                    "تشغيل أو إيقاف الصيانة وتحديد نوعها ونطاقها ومواعيدها والخدمات المتأثرة.",
                    true),

                new(
                    AdminPermissionKeys.Settings.ViewPaymentNetworks,
                    "مشاهدة شبكات الدفع",
                    "إعدادات النظام",
                    "عرض البنوك والمحافظ وشبكات الدفع المدعومة."),
                new(
                    AdminPermissionKeys.Settings.AddPaymentNetwork,
                    "إضافة شبكة دفع",
                    "إعدادات النظام",
                    "إضافة بنك أو محفظة أو شبكة دفع جديدة.",
                    true),
                new(
                    AdminPermissionKeys.Settings.EditPaymentNetwork,
                    "تعديل شبكة دفع",
                    "إعدادات النظام",
                    "تعديل أسماء وبيانات شبكة الدفع.",
                    true),
                new(
                    AdminPermissionKeys.Settings.ChangePaymentNetworkStatus,
                    "تفعيل أو إيقاف شبكة دفع",
                    "إعدادات النظام",
                    "تغيير الحالة التشغيلية لشبكة الدفع فقط.",
                    true),
                new(
                    AdminPermissionKeys.Settings.DeletePaymentNetwork,
                    "حذف شبكة دفع",
                    "إعدادات النظام",
                    "حذف شبكة دفع من النظام نهائيًا.",
                    true),
                new(
                    AdminPermissionKeys.Settings.ManageDefaultWalletLogo,
                    "تغيير الشعار الافتراضي لشبكات الدفع",
                    "إعدادات النظام",
                    "رفع أو تغيير الشعار الافتراضي لشبكات الدفع.",
                    true),

                new(
                    AdminPermissionKeys.Settings.ViewHeroSlides,
                    "مشاهدة شرائح الواجهة الرئيسية",
                    "إعدادات النظام",
                    "عرض شرائح الواجهة الرئيسية وحالتها وترتيبها."),
                new(
                    AdminPermissionKeys.Settings.ManageHeroSlides,
                    "إدارة شرائح الواجهة الرئيسية",
                    "إعدادات النظام",
                    "إضافة وتعديل وترتيب وتفعيل وحذف شرائح الواجهة الرئيسية.",
                    true),

                new(
                    AdminPermissionKeys.Settings.ViewBackups,
                    "مشاهدة النسخ الاحتياطية",
                    "إعدادات النظام",
                    "عرض حالة النسخ الاحتياطية والنسخ المحفوظة."),
                new(
                    AdminPermissionKeys.Settings.CreateBackup,
                    "إنشاء نسخة احتياطية",
                    "إعدادات النظام",
                    "إنشاء نسخة احتياطية كاملة لقاعدة البيانات.",
                    true),
                new(
                    AdminPermissionKeys.Settings.DownloadBackup,
                    "تنزيل نسخة احتياطية",
                    "إعدادات النظام",
                    "تنزيل ملف نسخة احتياطية محفوظة.",
                    true),
                new(
                    AdminPermissionKeys.Settings.RestoreBackup,
                    "استعادة نسخة احتياطية",
                    "إعدادات النظام",
                    "استعادة قاعدة البيانات من نسخة محفوظة أو ملف مرفوع.",
                    true),
                new(
                    AdminPermissionKeys.Settings.DeleteBackup,
                    "حذف نسخة احتياطية",
                    "إعدادات النظام",
                    "حذف نسخة احتياطية محفوظة من النظام.",
                    true),
                new(
                    AdminPermissionKeys.Settings.ManageBackupSchedule,
                    "إدارة النسخ التلقائي",
                    "إعدادات النظام",
                    "تفعيل أو إيقاف النسخ التلقائي وتحديد وقت النسخ اليومي.",
                    true),
                new(
                    AdminPermissionKeys.Settings.ManageBackupCloud,
                    "إدارة Google Drive للنسخ",
                    "إعدادات النظام",
                    "ربط وفصل ومزامنة النسخ الاحتياطية مع Google Drive.",
                    true),

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
