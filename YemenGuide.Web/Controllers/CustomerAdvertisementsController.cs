using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using YemenGuide.Core.DTOs.Customer;
using YemenGuide.Web.Services.Advertisements;

namespace YemenGuide.Web.Controllers
{
    [Route("CustomerAdvertisements")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public sealed class CustomerAdvertisementsController : Controller
    {
        private readonly ICustomerAdvertisementService _advertisements;

        public CustomerAdvertisementsController(
            ICustomerAdvertisementService advertisements)
        {
            _advertisements = advertisements;
        }

        [HttpPost("Impression")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Impression(
            [FromBody] CustomerAdvertisementTrackRequestDto request,
            CancellationToken cancellationToken)
        {
            var result = await _advertisements.RecordImpressionAsync(
                request,
                cancellationToken);

            return Json(new
            {
                success = result.Success,
                alreadyRecorded = result.AlreadyRecorded,
                message = result.Message
            });
        }

        [HttpPost("BookingStarted")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookingStarted(
            [FromBody] CustomerAdvertisementBookingStartRequestDto request,
            CancellationToken cancellationToken)
        {
            var result = await _advertisements.RecordBookingStartedAsync(
                request,
                cancellationToken);

            return Json(new
            {
                success = result.Success,
                alreadyRecorded = result.AlreadyRecorded,
                message = result.Message
            });
        }

        [HttpPost("Click")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Click(
            [FromBody] CustomerAdvertisementTrackRequestDto request,
            CancellationToken cancellationToken)
        {
            var result = await _advertisements.RecordClickAsync(
                request,
                cancellationToken);

            return Json(new
            {
                success = result.Success,
                alreadyRecorded = result.AlreadyRecorded,
                message = result.Message,
                redirectUrl = result.RedirectUrl
            });
        }
    }
}
