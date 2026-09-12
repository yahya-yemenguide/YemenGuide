using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using YemenGuide.Web.Filters;

namespace YemenGuide.Web.Maintenance
{
    public static class MaintenanceExtensions
    {
        public static IServiceCollection
            AddYemenGuideMaintenance(
                this IServiceCollection services)
        {
            services.AddScoped<
                IMaintenanceService,
                MaintenanceService>();

            services.AddScoped<
                MaintenanceOperationPolicy>();

            services.AddScoped<
                MaintenancePartialFilter>();

            // تطبيق حالات الحساب (Blocked / Suspended) على كل MVC من مكان واحد.
            services.AddScoped<
                AccountStateEnforcementFilter>();

            // تسجيل الفلاتر المركزية على MVC كامل مرة واحدة.
            // لا حاجة لوضع Attribute على Controllers العميل/المالك.
            services.Configure<MvcOptions>(
                options =>
                {
                    options.Filters.Add(
                        new ServiceFilterAttribute(
                            typeof(
                                AccountStateEnforcementFilter))
                        {
                            // يطبق حالة الحساب قبل فلاتر الصيانة الجزئية.
                            Order = -11000
                        });

                    options.Filters.Add(
                        new ServiceFilterAttribute(
                            typeof(
                                MaintenancePartialFilter))
                        {
                            Order = -10000
                        });
                });

            return services;
        }

        public static IApplicationBuilder
            UseYemenGuideMaintenance(
                this IApplicationBuilder app)
        {
            return app.UseMiddleware<
                MaintenanceMiddleware>();
        }
    }
}
