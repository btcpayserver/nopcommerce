using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using BTCPayServer.Client.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.BTCPayServer.Services;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Payments.BTCPayServer.Controllers
{
    public class WebHookBtcPayController : BasePaymentController
    {
        private readonly ISettingService _settingService;
        private readonly IOrderService _orderService;
        private readonly ILogger _logger;
        private readonly BtcPayService _btcPayService;

        public WebHookBtcPayController(IOrderService orderService,
            ISettingService settingService,
            ILogger logger,
            IHttpClientFactory httpClientFactory)
        {
                    _settingService = settingService;
                    _orderService = orderService;
                    _logger = logger;
                    _btcPayService = new BtcPayService(orderService, httpClientFactory);
        }


        [HttpPost]
        public async Task<IActionResult> Process([FromHeader(Name = "BTCPAY-Sig")] string BtcPaySig)
        {
            try
            {
                string jsonStr = await new StreamReader(Request.Body).ReadToEndAsync();
                var webhookEvent = JsonConvert.DeserializeObject<WebhookInvoiceEvent>(jsonStr);
                var BtcPaySecret = BtcPaySig.Split('=')[1];
                if (webhookEvent is null || webhookEvent?.InvoiceId?.StartsWith("__test__") is true || webhookEvent?.Type == WebhookEventType.InvoiceCreated)
                {
                    return Ok();
                }

                if (webhookEvent?.InvoiceId is null || webhookEvent.Metadata?.TryGetValue("orderId", out var orderIdToken) is not true || orderIdToken.ToString() is not { } orderId)
                {
                    await _logger.ErrorAsync("Missing fields in request");
                    return StatusCode(StatusCodes.Status422UnprocessableEntity);
                }


                var order = await _orderService.GetOrderByGuidAsync(new Guid(orderId));
                if (order == null)
                {
                    await _logger.ErrorAsync("Order not found");
                    return StatusCode(StatusCodes.Status422UnprocessableEntity);
                }

                var settings = await _settingService.LoadSettingAsync<BtcPaySettings>(order.StoreId);

                if (settings.WebHookSecret is not null && !BtcPayService.CheckSecretKey(settings.WebHookSecret, jsonStr, BtcPaySecret))
                {
                    await _logger.ErrorAsync("Bad secret key");
                    return StatusCode(StatusCodes.Status400BadRequest);
                }

                var invoice = await _btcPayService.GetInvoice(settings, webhookEvent.InvoiceId);
                await _btcPayService.UpdateOrderWithInvoice(order, invoice, webhookEvent);
                /*if (await _btcPayService.UpdateOrderWithInvoice(order, invoice, webhookEvent))
                {
                    await _orderService.UpdateOrderAsync(order);
                }*/

                return StatusCode(StatusCodes.Status200OK);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync(ex.Message, ex);
                return StatusCode(StatusCodes.Status400BadRequest);
            }
        }

    }
}
