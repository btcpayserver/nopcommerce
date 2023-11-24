using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.BTCPayServer.Components
{
    public class PaymentBtcPayViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.BTCPayServer/Views/PaymentInfo.cshtml");
        }
    }
}
