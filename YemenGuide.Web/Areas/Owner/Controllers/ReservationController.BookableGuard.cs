using YemenGuide.Web.Filters;

namespace YemenGuide.Web.Areas.Owner.Controllers
{
    /// <summary>
    /// الحماية مضافة في partial مستقل حتى لا نلمس منطق ReservationController الحالي.
    /// السمات الموضوعة على أي جزء من partial class تطبق على النوع كاملًا.
    /// </summary>
    [OwnerBookablePlace]
    public partial class ReservationController
    {
    }
}
