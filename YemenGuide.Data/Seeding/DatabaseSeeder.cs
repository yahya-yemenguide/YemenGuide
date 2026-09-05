using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YemenGuide.Core.Constants;
using YemenGuide.Core.Entities;
using BCrypt.Net;

namespace YemenGuide.Data.Seeding
{
    public static class DatabaseSeeder
    {
        private const string SuperAdminRoleName = "Super Admin";

        // =============================================================
        // بيانات الأدمن الافتراضي الأصلية كما كانت في المشروع
        // =============================================================
        private const string DefaultAdminFullName = "مدير النظام العام";
        private const string DefaultAdminUsername = "manar";
        private const string DefaultAdminEmail = "manar@gmail.com";
        private const string DefaultAdminPassword = "m0000000";

        public static void SeedAdminUser(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            using var transaction = context.Database.BeginTransaction();

            try
            {
                // 1) تأكيد وجود الأدمن الرئيسي.
                // إذا لم يوجد أدمن رئيسي، يُعاد إنشاء الأدمن الافتراضي الأصلي.
                var primaryAdmin = EnsurePrimaryAdmin(context);

                // 2) تأكيد وجود دور Super Admin النظامي.
                var superAdminRole = EnsureSuperAdminRole(
                    context,
                    primaryAdmin.UserId);

                // 3) زرع/تحديث قائمة الصلاحيات.
                SeedPermissions(context);

                // كلمة مرور الموظف بعد أول دخول أصبحت مسؤولية الموظف نفسه.
                // لذلك نعطّل صلاحية إعادة التعيين القديمة ونفك أي ربط سابق لها،
                // مع إبقاء سجل الصلاحية في قاعدة البيانات للتوافق والتاريخ فقط.
                DisableLegacyEmployeePasswordResetPermission(context);

                context.SaveChanges();

                // 4) منح Super Admin جميع الصلاحيات الحالية.
                GrantAllPermissionsToSuperAdmin(
                    context,
                    superAdminRole.AdminRoleId,
                    primaryAdmin.UserId);

                // 5) ربط الأدمن الرئيسي بدور Super Admin.
                EnsureMainAdminAssignment(
                    context,
                    superAdminRole.AdminRoleId,
                    primaryAdmin.UserId);

                context.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static User EnsurePrimaryAdmin(AppDbContext context)
        {
            // أ) إذا كان هناك أدمن مربوط مسبقًا بدور نظامي، فهو الأدمن الرئيسي.
            var systemAdmin = context.Users
                .Include(user => user.AdminRole)
                .Where(user =>
                    user.Role == "Admin" &&
                    user.AdminRole != null &&
                    user.AdminRole.IsSystemRole)
                .OrderBy(user => user.UserId)
                .FirstOrDefault();

            if (systemAdmin != null)
            {
                NormalizePrimaryAdmin(systemAdmin);
                return systemAdmin;
            }

            // ب) محاولة العثور على حساب الأدمن الافتراضي الأصلي إن كان موجودًا
            // من قاعدة سابقة قبل إضافة نظام الأدوار والصلاحيات.
            var defaultAdmin = context.Users
                .FirstOrDefault(user =>
                    user.Role == "Admin" &&
                    (user.Username == DefaultAdminUsername ||
                     user.ContactInfo == DefaultAdminEmail));

            if (defaultAdmin != null)
            {
                NormalizePrimaryAdmin(defaultAdmin);
                return defaultAdmin;
            }

            // ج) لا يوجد أدمن رئيسي معروف ولا الحساب الافتراضي الأصلي:
            // ننشئ الأدمن الافتراضي الأصلي حتى تبقى هناك دائمًا نقطة دخول
            // يمكن منها إنشاء الموظفين والأدوار والصلاحيات لاحقًا.
            var adminUser = new User
            {
                FullName = DefaultAdminFullName,
                Username = DefaultAdminUsername,
                ContactInfo = DefaultAdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                    DefaultAdminPassword,
                    workFactor: 11),
                Role = "Admin",
                AccountState = "Active",
                IsEmailVerified = true,
                IsDarkMode = false,
                PreferredLanguage = "ar",
                PreferredCurrency = "ريال يمني (جديد)",
                CreatedAt = DateTime.Now,

                // حقول نظام الموظفين الجديدة حتى يكون الحساب الرئيسي صالحًا فورًا.
                EmploymentStatus = "Active",
                HireDate = DateTime.Now,
                MustChangePassword = false
            };

            context.Users.Add(adminUser);
            context.SaveChanges();

            return adminUser;
        }

        private static void NormalizePrimaryAdmin(User admin)
        {
            admin.Role = "Admin";
            admin.AccountState = "Active";
            admin.IsEmailVerified = true;
            admin.EmploymentStatus = "Active";
            admin.HireDate ??= admin.CreatedAt ?? DateTime.Now;
            admin.MustChangePassword = false;
        }

        private static AdminRole EnsureSuperAdminRole(
            AppDbContext context,
            int createdByUserId)
        {
            // الأفضل الاعتماد على IsSystemRole وليس على رقم ثابت أو اسم مستخدم.
            var systemRole = context.AdminRoles
                .OrderBy(role => role.AdminRoleId)
                .FirstOrDefault(role => role.IsSystemRole);

            if (systemRole == null)
            {
                // إذا وُجد دور بنفس الاسم من تشغيل سابق لكنه ليس System Role،
                // نعيد استخدامه بدل إنشاء اسم مكرر بسبب Unique Index.
                systemRole = context.AdminRoles
                    .FirstOrDefault(role => role.RoleName == SuperAdminRoleName);
            }

            if (systemRole == null)
            {
                systemRole = new AdminRole
                {
                    RoleName = SuperAdminRoleName,
                    Description =
                        "الدور النظامي المحمي للمدير الرئيسي، وله كامل صلاحيات لوحة الإدارة.",
                    IsActive = true,
                    IsSystemRole = true,
                    CreatedAt = DateTime.Now,
                    CreatedByUserId = createdByUserId
                };

                context.AdminRoles.Add(systemRole);
                context.SaveChanges();
            }
            else
            {
                systemRole.RoleName = SuperAdminRoleName;
                systemRole.Description =
                    "الدور النظامي المحمي للمدير الرئيسي، وله كامل صلاحيات لوحة الإدارة.";
                systemRole.IsActive = true;
                systemRole.IsSystemRole = true;
            }

            return systemRole;
        }

        private static void SeedPermissions(AppDbContext context)
        {
            var existingPermissions = context.AdminPermissions
                .ToDictionary(
                    permission => permission.PermissionKey,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var definition in AdminPermissionCatalog.All)
            {
                if (existingPermissions.TryGetValue(
                        definition.Key,
                        out var permission))
                {
                    permission.PermissionName = definition.Name;
                    permission.PermissionGroup = definition.Group;
                    permission.Description = definition.Description;
                    permission.IsSensitive = definition.IsSensitive;
                    permission.IsActive = true;
                    continue;
                }

                var newPermission = new AdminPermission
                {
                    PermissionKey = definition.Key,
                    PermissionName = definition.Name,
                    PermissionGroup = definition.Group,
                    Description = definition.Description,
                    IsSensitive = definition.IsSensitive,
                    IsActive = true
                };

                context.AdminPermissions.Add(newPermission);
                existingPermissions[definition.Key] = newPermission;
            }
        }

        private static void DisableLegacyEmployeePasswordResetPermission(
            AppDbContext context)
        {
            const string legacyPermissionKey = "Employees.ResetPassword";

            var legacyPermission = context.AdminPermissions
                .FirstOrDefault(permission =>
                    permission.PermissionKey == legacyPermissionKey);

            if (legacyPermission == null)
            {
                return;
            }

            legacyPermission.IsActive = false;

            var legacyLinks = context.AdminRolePermissions
                .Where(link =>
                    link.AdminPermissionId == legacyPermission.AdminPermissionId)
                .ToList();

            if (legacyLinks.Count > 0)
            {
                context.AdminRolePermissions.RemoveRange(legacyLinks);
            }
        }

        private static void GrantAllPermissionsToSuperAdmin(
            AppDbContext context,
            int superAdminRoleId,
            int grantedByUserId)
        {
            var permissionIds = context.AdminPermissions
                .Where(permission => permission.IsActive == true)
                .Select(permission => permission.AdminPermissionId)
                .ToList();

            var existingLinks = context.AdminRolePermissions
                .Where(link => link.AdminRoleId == superAdminRoleId)
                .Select(link => link.AdminPermissionId)
                .ToHashSet();

            foreach (var permissionId in permissionIds)
            {
                if (existingLinks.Contains(permissionId))
                {
                    continue;
                }

                context.AdminRolePermissions.Add(
                    new AdminRolePermission
                    {
                        AdminRoleId = superAdminRoleId,
                        AdminPermissionId = permissionId,
                        GrantedAt = DateTime.Now,
                        GrantedByUserId = grantedByUserId
                    });
            }
        }

        private static void EnsureMainAdminAssignment(
            AppDbContext context,
            int superAdminRoleId,
            int primaryAdminUserId)
        {
            var primaryAdmin = context.Users
                .First(user => user.UserId == primaryAdminUserId);

            primaryAdmin.AdminRoleId = superAdminRoleId;
            NormalizePrimaryAdmin(primaryAdmin);
        }
    }
}
