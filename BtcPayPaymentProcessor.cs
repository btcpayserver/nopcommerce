using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Autofac.Core;
using BTCPayServer.Client.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.BTCPayServer.Components;
using Nop.Plugin.Payments.BTCPayServer.Models;
using Nop.Plugin.Payments.BTCPayServer.Services;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Stores;

namespace Nop.Plugin.Payments.BTCPayServer
{
    public class BtcPayPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Properties
        public bool SupportCapture => false;

        public bool SupportPartiallyRefund => true;

        public bool SupportRefund => true;

        public bool SupportVoid => true;

        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

        public bool SkipPaymentInfo => false;
        #endregion

        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly ICustomerService _customerService;
        private readonly IStoreService _storeService;
        private readonly CurrencySettings _currencySettings;
        private readonly ICurrencyService _currencyService;
        private readonly ILanguageService _languageService;
        private readonly BtcPaySettings _btcPaySettings;
        private readonly IServiceProvider _serviceProvider;
        private readonly IOrderService _orderService;
        private readonly ILogger _logger;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly BtcPayService _btcPayService;
        private readonly LinkGenerator _linkGenerator;

        #endregion

        #region Ctor

        public BtcPayPaymentProcessor(CurrencySettings currencySettings,
            IOrderTotalCalculationService orderTotalCalculationService,
            ILocalizationService localizationService,
            ILanguageService languageService,
            ISettingService settingService,
            IStoreService storeService,
            ICurrencyService currencyService,
            ICustomerService customerService,
            BtcPaySettings btcPaySettings,
            IServiceProvider serviceProvider,
            LinkGenerator linkGenerator,
            IOrderService orderService,
            IWebHelper webHelper,
            ILogger logger,
            IHttpClientFactory httpClientFactory)
        {
            _localizationService = localizationService;
            _settingService = settingService;
            _currencyService = currencyService;
            _webHelper = webHelper;
            _customerService = customerService;
            _storeService = storeService;
            _currencySettings = currencySettings;
            _languageService = languageService;
            _btcPaySettings = btcPaySettings;
            _linkGenerator = linkGenerator;
            _serviceProvider = serviceProvider;
            _orderService = orderService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _logger = logger;

            _btcPayService = new BtcPayService(_orderService, httpClientFactory);
        }

        #endregion
        public override async Task InstallAsync()
        {
            //settings
            await _settingService.SaveSettingAsync(new BtcPaySettings
            {
                BtcPayUrl = "",
                ApiKey = "",
                BtcPayStoreID = "",
                WebHookSecret = ""
            });
            await AddBTCCurrency();

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Payments.BTCPayServer.AdditionalFee"] = "Additional fee",
                ["Plugins.Payments.BTCPayServer.AdditionalFee.Hint"] = "The additional fee.",
                ["Plugins.Payments.BTCPayServer.AdditionalFeePercentage"] = "Additional fee. Use percentage",
                ["Plugins.Payments.BTCPayServer.AdditionalFeePercentage.Hint"] = "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.",
                ["Plugins.Payments.BTCPayServer.Instructions"] = "<div class=\"mb-1\"><b>BTCPay Plugin for NopCommerce</b></div>" +
                        "<div class=\"mb-1\">The plugin configuration can be done automatically or manually.</div>" +
                        "<div class=\"mb-1\"><br/><b>Automatic Configuration:</b></div>" +
                        "<ul>" +
                        "    <li>Enter the \"BTCPay Url\" parameter.</li>" +
                        "    <li>Click on the \"Configure automatically\" button to be redirected to the key creation page on your BTCPay Server.</li>" +
                        "    <li>The \"API Key\", \"BTCPay Store ID\" and \"WebHook Secret\" parameters will be automatically filled. Make sure to click Save.</li>" +
                        "</ul>" +
                        "<div class=\"mb-1\"><br/><b>Manual Configuration:</b></div>" +
                        "<ul>" +
                        "    <li>The \"BTCPay Url,\" \"API Key,\" \"BTCPay Store ID,\" and \"WebHook Secret\" fields must be filled out, then save.</li>" +
                        "    <li>Read detailed step by step <a href =\"https://github.com/btcpayserver/nopcommerce?tab=readme-ov-file#manual-configuration\" target=\"_blank\"> instructions on how to manually configure</a>. </li>" +
                        "</ul>",

                ["Plugins.Payments.BTCPayServer.WebHookInfo"] = "Here is the URL to set for the WebHook creation in BTCPay : ",

                ["Plugins.Payments.BTCPayServer.PaymentMethodDescription"] = "Pay your order in bitcoins",
                ["Plugins.Payments.BTCPayServer.PaymentMethodDescription2"] = "After completing the order you will be redirected to the merchant BTCPay instance, where you can make the Bitcoin payment for your order.",
                ["Plugins.Payments.BTCPayServer.PaymentError"] = "Error processing the payment. Please try again and contact us if the problem persists.",
                ["Plugins.Payments.BTCPayServer.BtcPayUrl"] = "BTCPay Url",
                ["Plugins.Payments.BTCPayServer.BtcPayUrl.Hint"] = "The url of your BTCPay instance",
                ["Plugins.Payments.BTCPayServer.CreateApiKey"] = "Create API key automatically",
                ["Plugins.Payments.BTCPayServer.ApiKey"] = "Api Key",
                ["Plugins.Payments.BTCPayServer.ApiKey.Hint"] = "The API Key value generated in your BTCPay instance",
                ["Plugins.Payments.BTCPayServer.BtcPayStoreID"] = "BTCPay Store ID",
                ["Plugins.Payments.BTCPayServer.BtcPayStoreID.Hint"] = "The BTCPay Store ID",
                ["Plugins.Payments.BTCPayServer.CreateWebhook"] = "Create webhook automatically",
                ["Plugins.Payments.BTCPayServer.WebHookUrl"] = "WebHook Url",
                ["Plugins.Payments.BTCPayServer.WebHookSecret"] = "WebHook Secret",
                ["Plugins.Payments.BTCPayServer.WebHookSecret.Hint"] = "The WebHook Secret value generated in your BTCPay instance",

                ["Plugins.Payments.BTCPayServer.NoteRefund"] = "A refund has been made. Please visit the following link to claim your refund: ",
            });

            await base.InstallAsync();
        }

        private async Task AddBTCCurrency()
        {
            try
            {
                await _currencyService.InsertCurrencyAsync(new Currency
                {
                    DisplayLocale = "en-US",
                    Name = "Bitcoin",
                    CurrencyCode = "BTC",
                    CustomFormatting = "{0} ₿",
                    Published = true,

                    DisplayOrder = 1,
                });

            }
            catch
            {
                // ignored
            }
        }


        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            await _settingService.DeleteSettingAsync<BtcPaySettings>();
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.BTCPayServer");

            await base.UninstallAsync();
        }

        Task<CancelRecurringPaymentResult> IPaymentMethod.CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            //always success
            return Task.FromResult(new CancelRecurringPaymentResult());
        }

        Task<bool> IPaymentMethod.CanRePostProcessPaymentAsync(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            if (order.PaymentStatus == PaymentStatus.Pending)
            {
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        Task<CapturePaymentResult> IPaymentMethod.CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            return Task.FromResult(new CapturePaymentResult { Errors = new[] { "Capture method not supported" } });
        }


        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the additional handling fee
        /// </returns>
        public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {
            return await _orderTotalCalculationService.CalculatePaymentAdditionalFeeAsync(cart,
                _btcPaySettings.AdditionalFee, _btcPaySettings.AdditionalFeePercentage);
        }


        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentBTCPayServer/Configure";
        }

        public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            return Task.FromResult(new ProcessPaymentRequest());
        }
        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            return await _localizationService.GetResourceAsync("Plugins.Payments.BTCPayServer.PaymentMethodDescription");
        }

        public Type GetPublicViewComponent()
        {
            return typeof(PaymentBtcPayViewComponent);
        }

        public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return Task.FromResult(false);
        }

        public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            if (postProcessPaymentRequest.Order.PaymentStatus == PaymentStatus.Pending)
            {

                var accessor = _serviceProvider.GetService<IHttpContextAccessor>();
                var sUrlRedirect = postProcessPaymentRequest.Order.AuthorizationTransactionResult;

                if (accessor.HttpContext is null)
                    return;

                if (_btcPayService.UpdateOrderWithInvoice(_btcPaySettings, postProcessPaymentRequest.Order,
                        postProcessPaymentRequest.Order.AuthorizationTransactionId).Result)
                {
                    await _orderService.UpdateOrderAsync(postProcessPaymentRequest.Order);

                    if (postProcessPaymentRequest.Order.PaymentStatus != PaymentStatus.Pending)
                    {

                        sUrlRedirect = _linkGenerator.GetUriByAction(accessor.HttpContext, "Details", "Order",
                            new { orderId = postProcessPaymentRequest.Order.Id });

                    }
                }

                accessor.HttpContext.Response.Redirect(sUrlRedirect);
            }

        }

        public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = PaymentStatus.Pending;

            // implement process payment
            try
            {

                var myStore = await _storeService.GetStoreByIdAsync(processPaymentRequest.StoreId);
                var currency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
                var lang = await _languageService.GetLanguageByIdAsync(myStore.DefaultLanguageId);
                var langCulture = (lang == null) ? "en-US" : lang.LanguageCulture;

                string? sEmail;
                Customer? myCustomer = await _customerService.GetCustomerByIdAsync(processPaymentRequest.CustomerId) ?? throw new Exception("Customer not found");
                var billingAddress = await _customerService.GetCustomerBillingAddressAsync(myCustomer);
                var sFullName = await _customerService.GetCustomerFullNameAsync(myCustomer);
                if (billingAddress == null)
                {
                    sEmail = myCustomer.Email;
                }
                else
                {
                    sEmail = billingAddress.Email;
                }


                var invoice = await _btcPayService.CreateInvoice(_btcPaySettings, new PaymentDataModel()
                {
                    CurrencyCode = currency.CurrencyCode,
                    Amount = processPaymentRequest.OrderTotal,
                    BuyerEmail = myCustomer.Email ?? billingAddress.Email,
                    BuyerName = sFullName,
                    OrderID = processPaymentRequest.OrderGuid.ToString(),
                    StoreID = myStore.Id,
                    CustomerID = processPaymentRequest.CustomerId,
                    Description = "From " + myStore.Name,
                    RedirectionURL = myStore.Url + "checkout/completed",
                    Lang = langCulture,
                    OrderUrl = new Uri(new Uri(myStore.Url),
                            _linkGenerator.GetPathByAction("Index", "OrderBtcPay",
                                new { id = processPaymentRequest.OrderGuid })).ToString()
                });

                result.AuthorizationTransactionResult = invoice.CheckoutLink;
                result.AuthorizationTransactionId = invoice.Id;
                result.CaptureTransactionResult =
                    invoice.Receipt?.Enabled is true ? invoice.CheckoutLink + "/receipt" : null;

            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync(ex.Message, ex);
                result.AddError(ex.Message);
            }

            return await Task.FromResult(result);
        }
        Task<ProcessPaymentResult> IPaymentMethod.ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            throw new NotImplementedException();
        }

        public async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            try
            {
                var sUrl = await _btcPayService.CreateRefund(_btcPaySettings, refundPaymentRequest);
                result.NewPaymentStatus = refundPaymentRequest.IsPartialRefund ? PaymentStatus.PartiallyRefunded : PaymentStatus.Refunded;
                await _orderService.InsertOrderNoteAsync(new OrderNote
                {
                    OrderId = refundPaymentRequest.Order.Id,
                    Note = await _localizationService.GetResourceAsync("Plugins.Payments.BTCPayServer.NoteRefund") + sUrl,
                    DisplayToCustomer = true,
                    CreatedOnUtc = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync(ex.Message, ex);
                result.AddError(ex.Message);
            }

            return await Task.FromResult(result);
        }

        Task<IList<string>> IPaymentMethod.ValidatePaymentFormAsync(IFormCollection form)
        {
            var warnings = new List<string>();

            return Task.FromResult<IList<string>>(warnings);
        }

        public async Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            try
            {
                /*var client = _btcPayService.GetClient(_btcPaySettings);
                var invoice = await client.MarkInvoiceStatus(_btcPaySettings.BtcPayStoreID,
                    voidPaymentRequest.Order.AuthorizationTransactionId,
                    new MarkInvoiceStatusRequest() { Status = InvoiceStatus.Invalid });
                */
                return new VoidPaymentResult()
                {
                    NewPaymentStatus = /*invoice.Status == InvoiceStatus.Invalid
                        ? PaymentStatus.Voided
                        : */ voidPaymentRequest.Order.PaymentStatus
                };

            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync(ex.Message, ex);
                return new VoidPaymentResult() { NewPaymentStatus = voidPaymentRequest.Order.PaymentStatus };
            }
        }

    }
}