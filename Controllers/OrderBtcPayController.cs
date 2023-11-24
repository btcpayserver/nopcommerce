using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Services.Orders;
using Nop.Web.Framework.Controllers;


namespace Nop.Plugin.Payments.BtcPayServer.Controllers
{
    [Route("btcpayserver/order")]
    public class OrderBtcPayController : BaseController
    {
        private readonly IOrderService _orderService;

        [HttpGet("{id}")]
        public async Task<IActionResult> Index(Guid id)
        {
            var order = await _orderService.GetOrderByGuidAsync(id);
            if (order is null)
            {
                return NotFound();
            }

            return RedirectToAction("Details", "Order", new { id = order.Id });
        } 
    }


}
