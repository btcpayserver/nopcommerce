using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Autofac.Core;
using BTCPayServer.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.BTCPayServer.Models;
using Nop.Plugin.Payments.BTCPayServer.Services;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using NUglify.Helpers;

namespace Nop.Plugin.Payments.BTCPayServer.Controllers
{
    [Area(AreaNames.Admin)]
    [AutoValidateAntiforgeryToken]
    public class PaymentBTCPayServerController : BasePaymentController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly ILogger _logger;
        private readonly LinkGenerator _linkGenerator;
        private readonly BtcPayService _btcPayService;
        private readonly PaymentSettings _paymentSettings;

        #endregion

        #region Ctor

        public PaymentBTCPayServerController(ILocalizationService localizationService,
            LinkGenerator linkGenerator,
            INotificationService notificationService,
            IPermissionService permissionService,
            ISettingService settingService,
            IStoreContext storeContext,
            ILogger logger,
            IOrderService orderService,
            PaymentSettings settings,
            IHttpClientFactory httpClientFactory)
        {
            _linkGenerator = linkGenerator;
            _localizationService = localizationService;
            _notificationService = notificationService;
            _permissionService = permissionService;
            _settingService = settingService;
            _storeContext = storeContext;
            _logger = logger;
            _paymentSettings = settings;

            _btcPayService = new BtcPayService(orderService, httpClientFactory);
        }

        #endregion

        #region Methods


        [AuthorizeAdmin]
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var btcPaySettings = await _settingService.LoadSettingAsync<BtcPaySettings>(storeScope);

            var model = new ConfigurationModel
            {
                BtcPayUrl = btcPaySettings.BtcPayUrl.IfNullOrWhiteSpace(""),
                ApiKey = btcPaySettings.ApiKey.IfNullOrWhiteSpace(""),
                BtcPayStoreID = btcPaySettings.BtcPayStoreID.IfNullOrWhiteSpace(""),
                WebHookSecret = btcPaySettings.WebHookSecret.IfNullOrWhiteSpace(""),
                AdditionalFee = btcPaySettings.AdditionalFee,
                AdditionalFeePercentage = btcPaySettings.AdditionalFeePercentage,
                ActiveStoreScopeConfiguration = storeScope
            };

            var myStore = _storeContext.GetCurrentStore();
            ViewBag.UrlWebHook = new Uri(new Uri(myStore.Url),
                _linkGenerator.GetPathByAction("Process", "WebHookBtcPay"));

            return View("~/Plugins/Payments.BTCPayServer/Views/Configure.cshtml", model);
        }

        private string? GetRedirectUri(BtcPaySettings btcPaySettings)
        {
            if (string.IsNullOrEmpty(btcPaySettings?.BtcPayUrl) ||
                !Uri.TryCreate(btcPaySettings?.BtcPayUrl, UriKind.Absolute, out var btcpayUri))
            {
                return null;
            }

            var myStore = _storeContext.GetCurrentStore();
            //var adminUrl = new Uri(new Uri(myStore.Url),
            //    _linkGenerator.GetPathByAction(HttpContext, "GetAutomaticApiKeyConfig", "PaymentBTCPayServer",
            //        new { ssid = myStore.Id, btcpayuri = btcpayUri }));
            var adminUrl = new Uri(new Uri("https://" + Request.Host.Value),
                _linkGenerator.GetPathByAction(HttpContext, "GetAutomaticApiKeyConfig", "PaymentBTCPayServer",
                    new { ssid = myStore.Id, btcpayuri = btcpayUri }));


            var uri = BTCPayServerClient.GenerateAuthorizeUri(btcpayUri,
                new[]
                {
                    Policies.CanCreateInvoice, // create invoices for payment
                    Policies.CanViewInvoices, // fetch created invoices to check status
                    Policies.CanModifyInvoices, // able to mark an invoice invalid in case merchant wants to void the order
                    Policies.CanModifyStoreWebhooks, // able to create the webhook required automatically
                    Policies.CanViewStoreSettings, // able to fetch rates
                    Policies.CanCreateNonApprovedPullPayments // able to create refunds
                },
                true, true, ($"NopCommerce{myStore.Id}", adminUrl));
            return uri + $"&applicationName={HttpUtility.UrlEncode(myStore.Name)}";
        }


        [HttpPost]
        [AuthorizeAdmin]
        public async Task<IActionResult> Configure(ConfigurationModel model, string command = null)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<BtcPaySettings>(storeScope);

            if (command == "delete")
            {
                settings.ApiKey = "";
                settings.BtcPayStoreID = "";
                settings.WebHookSecret = "";
                settings.BtcPayUrl = "";
                await _settingService.SaveSettingAsync<BtcPaySettings>(settings);
                ModelState.Clear();

                _paymentSettings.ActivePaymentMethodSystemNames.Remove("Payments.BTCPayServer");

                _notificationService.SuccessNotification("Settings cleared and payment method deactivated");
                return await Configure();
            }

            if (command == "activate" && model.IsConfigured())
            {
                _paymentSettings.ActivePaymentMethodSystemNames.Add("Payments.BTCPayServer");

                await _settingService.SaveSettingAsync<PaymentSettings>(_paymentSettings);

                _notificationService.SuccessNotification("Payment method activated");
            }

            if (command == "getautomaticapikeyconfig")
            {
                settings.BtcPayUrl = model.BtcPayUrl;
                await _settingService.SaveSettingAsync<BtcPaySettings>(settings);

                string? result = GetRedirectUri(settings);
                if (result != null)
                {
                    return Redirect(result);
                }

                _notificationService.ErrorNotification("Cannot generate automatic configuration URL. Please check your BTCPay URL.");
                return await Configure();
            }

            if (!ModelState.IsValid){
                _notificationService.ErrorNotification("Incorrect data");
                return await Configure();
            }

            //save settings
            settings.BtcPayUrl = model.BtcPayUrl.Trim();
            settings.ApiKey = model.ApiKey?.Trim();
            settings.BtcPayStoreID = model.BtcPayStoreID?.Trim();
            settings.WebHookSecret = model.WebHookSecret?.Trim();
            settings.AdditionalFee = model.AdditionalFee;
            settings.AdditionalFeePercentage = model.AdditionalFeePercentage;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */

            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.BtcPayUrl, model.OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.ApiKey, model.OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.BtcPayStoreID, model.OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.WebHookSecret, model.OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.AdditionalFee, model.OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.AdditionalFeePercentage, model.OverrideForStore, storeScope, false);

            //now clear settings cache
            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GetAutomaticApiKeyConfig()
        {
            var myStore = _storeContext.GetCurrentStore();

            Request.Query.TryGetValue("ssid", out var ssidx);
            var ssid = int.Parse(ssidx.FirstOrDefault() ?? myStore.Id.ToString());
            if (ssid != myStore.Id)
            {
                return NotFound();
            }

            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<BtcPaySettings>(storeScope);

            try
            {
                Request.Form.TryGetValue("apiKey", out var apiKey);
                Request.Form.TryGetValue("permissions[]", out var permissions);
                Permission.TryParse(permissions.FirstOrDefault(), out var permission);
                if (Request.Query.TryGetValue("btcpayuri", out var btcpayUris) &&
                    btcpayUris.FirstOrDefault() is { } stringbtcpayUri)
                {
                    settings.BtcPayUrl = stringbtcpayUri;
                }

                settings.ApiKey = apiKey;
                settings.BtcPayStoreID = permission.Scope;
                try
                {
                    if (permission.Scope is null)
                    {
                        settings.BtcPayStoreID = await _btcPayService.GetStoreId(settings);
                    }

                    if (string.IsNullOrEmpty(settings.WebHookSecret))
                    {
                        var webhookUrl = new Uri(new Uri(myStore.Url),
                            _linkGenerator.GetPathByAction("Process", "WebHookBtcPay"));
                        settings.WebHookSecret = await _btcPayService.CreateWebHook(settings, webhookUrl.ToString());
                    }
                }
                catch
                {
                }

                _paymentSettings.ActivePaymentMethodSystemNames.Add("Payments.BTCPayServer");

                await _settingService.SaveSettingAsync<PaymentSettings>(_paymentSettings);
                await _settingService.SaveSettingAsync<BtcPaySettings>(settings);

                _notificationService.SuccessNotification("Settings automatically configured and payment method activated.");


            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync(ex.Message, ex);

            }
            return RedirectToAction(nameof(Configure));

        }

        #endregion


    }
}