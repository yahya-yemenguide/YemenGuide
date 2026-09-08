using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YemenGuide.Data;

namespace YemenGuide.Web.Filters
{
    /// <summary>
    /// يمنع فتح صفحات/عمليات الحجز والمالية عندما تكون المنشأة النشطة
    /// تابعة لفئة BookingSystemId = 0 (منشأة غير حجزية).
    /// يعتمد على نفس كوكي المنشأة الذي يستخدمه هيدر المالك.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class OwnerBookablePlaceAttribute :
        Attribute,
        IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var httpContext = context.HttpContext;
            var userIdText = httpContext.User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            // التحقق من تسجيل الدخول والصلاحيات يبقى من مسؤولية
            // Authorize / AuthorizeActiveOwner الموجودة أصلًا.
            if (!int.TryParse(userIdText, out var ownerId) ||
                ownerId <= 0)
            {
                await next();
                return;
            }

            var dbContext = httpContext.RequestServices
                .GetRequiredService<AppDbContext>();

            var requestedPlaceId =
                ReadPlaceIdFromActionArguments(context);

            if (!requestedPlaceId.HasValue)
            {
                var cookieName = $"ownerActivePlace_{ownerId}";

                if (int.TryParse(
                        httpContext.Request.Cookies[cookieName],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var cookiePlaceId) &&
                    cookiePlaceId > 0)
                {
                    requestedPlaceId = cookiePlaceId;
                }
            }

            var places = await dbContext.Places
                .AsNoTracking()
                .Where(place => place.OwnerId == ownerId)
                .Select(place => new OwnerPlaceScope
                {
                    PlaceId = place.PlaceId,
                    BookingSystemId = place.Category != null
                        ? place.Category.BookingSystemId
                        : 0,
                    Status = place.Status,
                    IsAvailable = place.IsAvailable,
                    Name = place.PlaceNameAndDesc
                })
                .OrderByDescending(place =>
                    place.Status == "Approved" ||
                    place.Status == "Active")
                .ThenBy(place => place.Name)
                .ToListAsync(httpContext.RequestAborted);

            if (places.Count == 0)
            {
                await next();
                return;
            }

            var activePlace = requestedPlaceId.HasValue
                ? places.FirstOrDefault(place =>
                    place.PlaceId == requestedPlaceId.Value)
                : null;

            activePlace ??= places[0];

            if (activePlace.BookingSystemId > 0)
            {
                await next();
                return;
            }

            const string message =
                "المنشأة المختارة غير حجزية، لذلك لا تتوفر لها الحجوزات أو الشؤون المالية والعمولات.";

            if (context.Controller is Controller controller)
            {
                controller.TempData["OwnerNonBookableInfo"] = message;
            }

            if (ShouldReturnJson(httpContext.Request))
            {
                context.Result = new JsonResult(new
                {
                    success = false,
                    message,
                    nonBookablePlace = true,
                    placeId = activePlace.PlaceId
                })
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };

                return;
            }

            context.Result = new RedirectToActionResult(
                "DashboardOwner",
                "OwnerDashboard",
                new { area = "Owner" });
        }

        private static int? ReadPlaceIdFromActionArguments(
            ActionExecutingContext context)
        {
            string[] keys =
            {
                "placeId",
                "autoPlaceId",
                "requestedPlaceId",
                "activePlaceId"
            };

            foreach (var key in keys)
            {
                if (!context.ActionArguments.TryGetValue(
                        key,
                        out var rawValue) ||
                    rawValue == null)
                {
                    continue;
                }

                if (rawValue is int intValue && intValue > 0)
                {
                    return intValue;
                }


                if (int.TryParse(
                        Convert.ToString(
                            rawValue,
                            CultureInfo.InvariantCulture),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var parsedValue) &&
                    parsedValue > 0)
                {
                    return parsedValue;
                }
            }

            return null;
        }

        private static bool ShouldReturnJson(
            HttpRequest request)
        {
            if (!HttpMethods.IsGet(request.Method))
            {
                return true;
            }

            if (string.Equals(
                    request.Headers["X-Requested-With"].ToString(),
                    "XMLHttpRequest",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return request.Headers.Accept.Any(value =>
                value != null &&
                value.Contains(
                    "application/json",
                    StringComparison.OrdinalIgnoreCase));
        }

        private sealed class OwnerPlaceScope
        {
            public int PlaceId { get; set; }
            public int BookingSystemId { get; set; }
            public string? Status { get; set; }
            public bool? IsAvailable { get; set; }
            public string? Name { get; set; }
        }
    }
}
