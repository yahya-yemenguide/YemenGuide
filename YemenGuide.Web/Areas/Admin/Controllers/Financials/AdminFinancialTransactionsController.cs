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
    [Route("Admin/Financials/Transactions")]
    public sealed class AdminFinancialTransactionsController : Controller
    {
        private const string PartialViewPath =
            "~/Areas/Admin/Views/AdminFinancials/_AdminFinancialTransactions.cshtml";

        private readonly IAdminFinancialService _adminFinancialService;
        private readonly ILogger<AdminFinancialTransactionsController> _logger;

        public AdminFinancialTransactionsController(
            IAdminFinancialService adminFinancialService,
            ILogger<AdminFinancialTransactionsController> logger)
        {
            _adminFinancialService = adminFinancialService ??
                throw new ArgumentNullException(nameof(adminFinancialService));

            _logger = logger ??
                throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(
            [FromQuery] AdminFinancialTransactionFilterDto? filter,
            CancellationToken cancellationToken = default)
        {
            filter ??= new AdminFinancialTransactionFilterDto();

            try
            {
                DisableResponseCaching();

                var model =
                    await _adminFinancialService.GetTransactionsAsync(
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
                    "حدث خطأ أثناء تحميل كشف الحركات المالية.");

                return StatusCode(
                    500,
                    "تعذر تحميل كشف الحركات المالية. أعد المحاولة.");
            }
        }

        [HttpGet("Details/{commissionTransactionId:int}")]
        [HttpGet("Details")]
        public async Task<IActionResult> Details(
            int commissionTransactionId,
            CancellationToken cancellationToken = default)
        {
            if (commissionTransactionId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "رقم الحركة المالية غير صحيح."
                });
            }

            try
            {
                DisableResponseCaching();

                var details =
                    await _adminFinancialService.GetTransactionDetailsAsync(
                        commissionTransactionId,
                        cancellationToken);

                if (details == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "الحركة المالية غير موجودة."
                    });
                }

                return Json(new
                {
                    success = true,
                    data = details
                });
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
                    "حدث خطأ أثناء تحميل تفاصيل الحركة المالية {TransactionId}.",
                    commissionTransactionId);

                return StatusCode(500, new
                {
                    success = false,
                    message = "تعذر تحميل تفاصيل الحركة المالية."
                });
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
