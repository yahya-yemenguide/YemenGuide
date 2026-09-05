using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

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

            // تسجيل فلتر Partial على MVC كامل مرة واحدة.
            // لا حاجة لوضع Attribute على Controllers العميل/المالك.
            services.Configure<MvcOptions>(
                options =>
                {
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
