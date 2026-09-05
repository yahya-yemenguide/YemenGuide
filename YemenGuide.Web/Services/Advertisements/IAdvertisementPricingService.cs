using YemenGuide.Core.DTOs.Admin;

namespace YemenGuide.Web.Services.Advertisements
{
    public interface IAdvertisementPricingService
    {
        AdvertisementPriceQuoteDto Calculate(AdvertisementPriceQuoteRequestDto request);
        string? ValidatePlan(AdminAdvertisementPlanUpsertDto request);
    }
}
