using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BTCPayServer.Client;
using BTCPayServer.Client.Models;
using Newtonsoft.Json.Linq;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.BTCPayServer.Models;
using Nop.Services.Orders;
using Nop.Services.Payments;
using IHttpClientFactory = System.Net.Http.IHttpClientFactory;

namespace Nop.Plugin.Payments.BTCPayServer.Services
{
    public class BtcPayService
    {

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOrderService _orderService;

        public BtcPayService(IOrderService orderService, IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _orderService = orderService;
        }


        public static bool CheckSecretKey(string key, string message, string signature)
        {
            var msgBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(message));
            string hashString = string.Empty;
            foreach (byte x in msgBytes)
            {
                hashString += String.Format("{0:x2}", x);
            }
            return (hashString == signature);
        }

        public async Task<InvoiceData> CreateInvoice(BtcPaySettings settings, PaymentDataModel paymentData)
        {
            var client = GetClient(settings);
            var req = new CreateInvoiceRequest()
            {
                Currency = paymentData.CurrencyCode,
                Amount = paymentData.Amount,
                Checkout = new InvoiceDataBase.CheckoutOptions()
                {
                    DefaultLanguage = paymentData.Lang,
                    RedirectURL = paymentData.RedirectionURL,
                    RedirectAutomatically = true,
                    RequiresRefundEmail = false
                },
                Metadata = JObject.FromObject(new
                {
                    buyerEmail = paymentData.BuyerEmail,
                    buyerName = paymentData.BuyerName,
                    orderId = paymentData.OrderID,
                    orderUrl = paymentData.OrderUrl,
                    itemDesc = paymentData.Description,
                }),
                Receipt = new InvoiceDataBase.ReceiptOptions() { Enabled = true, }
            };

            var invoice = await client.CreateInvoice(settings.BtcPayStoreID, req);
            return invoice;

        }

        public async Task<string> CreateRefund(BtcPaySettings settings, RefundPaymentRequest refundRequest)
        {
            var client = GetClient(settings);
            var invoice = await client.GetInvoicePaymentMethods(settings.BtcPayStoreID,
                refundRequest.Order.AuthorizationTransactionId);
            var pm = (invoice.FirstOrDefault(p => p.Payments.Any()) ?? invoice.First()).PaymentMethod;
            var refundInvoiceRequest = new RefundInvoiceRequest()
            {
                Name = "Refund order " + refundRequest.Order.OrderGuid,
                PaymentMethod = pm,
            };
            if (refundRequest.IsPartialRefund)
            {
                refundInvoiceRequest.Description = "Partial refund";
                refundInvoiceRequest.RefundVariant = RefundVariant.Custom;
                refundInvoiceRequest.CustomAmount = refundRequest.AmountToRefund;
                refundInvoiceRequest.CustomCurrency = refundRequest.Order.CustomerCurrencyCode;
            }
            else
            {
                refundInvoiceRequest.Description = "Full";
                refundInvoiceRequest.PaymentMethod = "BTC";
                refundInvoiceRequest.RefundVariant = RefundVariant.Fiat;
            }


            var refund = await client.RefundInvoice(settings.BtcPayStoreID,
                refundRequest.Order.AuthorizationTransactionId, refundInvoiceRequest);

            return refund.ViewLink;
        }

        public async Task<bool> UpdateOrderWithInvoice(BtcPaySettings settings, Order order, string invoiceId)
        {
            try
            {
                var invoice = await GetInvoice(settings, invoiceId);
                return await UpdateOrderWithInvoice(order, invoice, null);
            }
            catch (Exception e)
            {
                order.PaymentStatus = PaymentStatus.Voided;
                await _orderService.InsertOrderNoteAsync(new OrderNote
                {
                    OrderId = order.Id,
                    Note = $"BTCPayServer: Error updating order status with invoice {invoiceId} - {e.Message}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                return true;
            }
        }

        public async Task<bool> UpdateOrderWithInvoice(Order order, InvoiceData invoiceData,
            WebhookInvoiceEvent? webhookEvent)
        {
            if (order.PaymentMethodSystemName !=  "Payments.BTCPayServer")
                return false;

            var newPaymentStatus = order.PaymentStatus;
            var newOrderStatus = order.OrderStatus;

            order.AuthorizationTransactionId = invoiceData.Id;
            switch (invoiceData.Status)
            {
                case InvoiceStatus.New:
                    newPaymentStatus = PaymentStatus.Pending;
                    break;
                case InvoiceStatus.Processing:
                    newPaymentStatus = PaymentStatus.Authorized;
                    newOrderStatus = OrderStatus.Pending;
                    break;
                case InvoiceStatus.Expired:
                    newOrderStatus = OrderStatus.Cancelled;
                    newPaymentStatus = PaymentStatus.Voided;
                    break;
                case InvoiceStatus.Invalid:
                    newPaymentStatus = PaymentStatus.Voided;
                    newOrderStatus = OrderStatus.Cancelled;

                    break;
                case InvoiceStatus.Settled:
                    newPaymentStatus = PaymentStatus.Paid;
                    newOrderStatus = OrderStatus.Processing;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var updated = false;
            if (newPaymentStatus != order.PaymentStatus)
            {
                order.PaymentStatus = newPaymentStatus;
                updated = true;
            }

            if (newOrderStatus != order.OrderStatus && order.OrderStatus != OrderStatus.Complete)
            {
                order.OrderStatus = newOrderStatus;
                updated = true;
            }

            var additionalMessage = GetAdditionalMessageFromWebhook(webhookEvent);
            if (updated)
            {
                additionalMessage = string.IsNullOrEmpty(additionalMessage) ? "" : $" - {additionalMessage}";
                await _orderService.InsertOrderNoteAsync(new OrderNote
                {
                    OrderId = order.Id,
                    Note = $"BTCPayServer: Order status updated to {newOrderStatus} and payment status to {newPaymentStatus} by BTCPay with invoice {invoiceData.Id}{additionalMessage}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                if (order.PaymentStatus is PaymentStatus.Authorized or PaymentStatus.Paid &&
                    !string.IsNullOrEmpty(order.CaptureTransactionResult))
                    await _orderService.InsertOrderNoteAsync(new OrderNote
                    {
                        OrderId = order.Id,
                        Note = $"BTCPayServer: Payment {(order.PaymentStatus is PaymentStatus.Authorized ? $"received but waiting to confirm. Click here for more information: {order.AuthorizationTransactionResult}" : $"settled. Click here for more information: {order.CaptureTransactionResult}")}",
                        DisplayToCustomer = true,
                        CreatedOnUtc = DateTime.UtcNow
                    });

                await _orderService.UpdateOrderAsync(order);

                return true;
            }
            if (!string.IsNullOrEmpty(additionalMessage))
            {
                await _orderService.InsertOrderNoteAsync(new OrderNote
                {
                    OrderId = order.Id,
                    Note = $"BTCPayServer: {additionalMessage}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });
            }

            return false;
        }

        public async Task<InvoiceData> GetInvoice(BtcPaySettings settings, string invoiceId)
        {
            var client = GetClient(settings);
            return await client.GetInvoice(settings.BtcPayStoreID, invoiceId);
        }

        public async Task<string> CreateWebHook(BtcPaySettings settings, string webHookUrl)
        {
            var client = GetClient(settings);
            var existing = await client.GetWebhooks(settings.BtcPayStoreID);
            var existingWebHook = existing.Where(x => x.Url == webHookUrl);
            foreach (var webhookData in existingWebHook)
            {
                await client.DeleteWebhook(settings.BtcPayStoreID, webhookData.Id);
            }

            var response = await client.CreateWebhook(settings.BtcPayStoreID,
                new CreateStoreWebhookRequest()
                {
                    Url = webHookUrl,
                    Enabled = true,
                    AuthorizedEvents = new StoreWebhookBaseData.AuthorizedEventsData()
                    {
                        SpecificEvents = new[]
                        {
                            WebhookEventType.InvoiceReceivedPayment, WebhookEventType.InvoiceProcessing,
                            WebhookEventType.InvoiceExpired, WebhookEventType.InvoiceSettled,
                            WebhookEventType.InvoiceInvalid, WebhookEventType.InvoicePaymentSettled,
                        }
                    }
                });
            return response.Secret;
        }


        public BTCPayServerClient GetClient(BtcPaySettings settings)
        {
            return new BTCPayServerClient(new Uri(settings.BtcPayUrl), settings.ApiKey,
                _httpClientFactory.CreateClient("BTCPayServer"));
        }

        public async Task<string> GetStoreId(BtcPaySettings settings)
        {
            return (await GetClient(settings).GetStores()).First().Id;
        }


        private string? GetAdditionalMessageFromWebhook(WebhookInvoiceEvent? webhookEvent)
        {
            switch (webhookEvent?.Type)
            {
                case WebhookEventType.InvoiceReceivedPayment
                    when webhookEvent.ReadAs<WebhookInvoiceReceivedPaymentEvent>() is { } receivedPaymentEvent:
                    return
                        $"Payment detected ({receivedPaymentEvent.PaymentMethod}: {receivedPaymentEvent.Payment.Value})";
                case WebhookEventType.InvoicePaymentSettled
                    when webhookEvent.ReadAs<WebhookInvoicePaymentSettledEvent>() is { } receivedPaymentEvent:
                    return
                        $"Payment settled ({receivedPaymentEvent.PaymentMethod}: {receivedPaymentEvent.Payment.Value})";
                case WebhookEventType.InvoiceProcessing
                    when webhookEvent.ReadAs<WebhookInvoiceProcessingEvent>() is { } receivedPaymentEvent &&
                         receivedPaymentEvent.OverPaid:
                    return $"Invoice was overpaid.";
                case WebhookEventType.InvoiceExpired
                    when webhookEvent.ReadAs<WebhookInvoiceExpiredEvent>() is { } receivedPaymentEvent &&
                         receivedPaymentEvent.PartiallyPaid:
                    return $"Invoice expired but was paid partially.";
                default:
                    return null;
            }
        }

    }
}