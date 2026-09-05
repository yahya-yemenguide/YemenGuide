using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YemenGuide.Core.Constants;
using YemenGuide.Core.DTOs.Admin;
using YemenGuide.Web.Filters;
using YemenGuide.Web.Services.Finance;

namespace YemenGuide.Web.Areas.Admin.Controllers.Financials
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    [RequireAdminPermission(AdminPermissionKeys.Finance.View)]
    [Route("Admin/Financials/Overview")]
    public sealed class AdminFinancialOverviewController : Controller
    {
        private const string PartialViewPath =
            "~/Areas/Admin/Views/AdminFinancials/_AdminFinancialOverview.cshtml";

        private readonly IAdminFinancialService _adminFinancialService;
        private readonly ILogger<AdminFinancialOverviewController> _logger;

        public AdminFinancialOverviewController(
            IAdminFinancialService adminFinancialService,
            ILogger<AdminFinancialOverviewController> logger)
        {
            _adminFinancialService = adminFinancialService ??
                throw new ArgumentNullException(nameof(adminFinancialService));

            _logger = logger ??
                throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(
            [FromQuery] AdminFinancialOverviewFilterDto? filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminFinancialOverviewFilterDto();

            try
            {
                DisableResponseCaching();

                var model =
                    await _adminFinancialService.GetOverviewAsync(
                        filter,
                        cancellationToken);

                return PartialView(
                    PartialViewPath,
                    model);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return new EmptyResult();
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "حدث خطأ أثناء تحميل النظرة العامة للإدارة المالية.");

                return StatusCode(
                    500,
                    "تعذر تحميل النظرة العامة للإدارة المالية. أعد المحاولة.");
            }
        }

        private void DisableResponseCaching()
        {
            Response.Headers.CacheControl =
                "no-store, no-cache, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
        }
    }
}
