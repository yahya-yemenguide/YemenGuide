using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YemenGuide.Core.Constants;
using YemenGuide.Core.Entities;
using YemenGuide.Web.Localization;

namespace YemenGuide.Web.Infrastructure.Localization
{
    /// <summary>
    /// يجعل الحساب الذي ينشئه الزائر يرث لغة الواجهة التي كان
    /// يستخدمها قبل التسجيل، بدون تعديل AccountController الضخم.
    ///
    /// يعمل فقط عند إضافة User جديد من مساري التسجيل الذاتي:
    /// /Account/Register
    /// /Account/GoogleResponse
    /// </summary>
    public sealed class PreferredLanguageSaveChangesInterceptor
        : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor
            _httpContextAccessor;

        public PreferredLanguageSaveChangesInterceptor(
            IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor =
                httpContextAccessor;
        }

        private void ApplyPreferredLanguage(
            DbContext? dbContext)
        {
            if (dbContext == null)
            {
                return;
            }

            var httpContext =
                _httpContextAccessor.HttpContext;

            if (httpContext == null)
            {
                return;
            }

            var requestPath =
                httpContext.Request.Path.Value ??
                string.Empty;

            var isSelfRegistration =
                requestPath.Equals(
                    "/Account/Register",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                requestPath.Equals(
                    "/Account/GoogleResponse",
                    StringComparison.OrdinalIgnoreCase
                );

            if (!isSelfRegistration)
            {
                return;
            }

            var requestCulture =
                CustomerLocalization
                    .NormalizeOrDefault(
                        CultureInfo
                            .CurrentUICulture
                            .Name
                    );

            var newUsers =
                dbContext
                    .ChangeTracker
                    .Entries<User>()
                    .Where(
                        entry =>
                            entry.State ==
                            EntityState.Added
                    );

            foreach (var entry in newUsers)
            {
                var role =
                    entry.Entity.Role ??
                    string.Empty;

                var isCustomerOrOwner =
                    string.Equals(
                        role,
                        SystemRoles.Customer,
                        StringComparison.OrdinalIgnoreCase
                    ) ||
                    string.Equals(
                        role,
                        SystemRoles.Owner,
                        StringComparison.OrdinalIgnoreCase
                    );

                if (!isCustomerOrOwner)
                {
                    continue;
                }

                entry.Entity.PreferredLanguage =
                    requestCulture;
            }
        }

        public override InterceptionResult<int>
            SavingChanges(
                DbContextEventData eventData,
                InterceptionResult<int> result)
        {
            ApplyPreferredLanguage(
                eventData.Context);

            return base.SavingChanges(
                eventData,
                result);
        }

        public override ValueTask<
            InterceptionResult<int>>
            SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken =
                    default)
        {
            ApplyPreferredLanguage(
                eventData.Context);

            return base.SavingChangesAsync(
                eventData,
                result,
                cancellationToken);
        }
    }
}
