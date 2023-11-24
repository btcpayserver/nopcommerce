using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;
using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.Payments.BTCPayServer.Models
{
    public record ConfigurationModel : BaseNopModel
    {

        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.BTCPayServer.BtcPayUrl")]
        //[Url]
        [Required]
        public string BtcPayUrl { get; set; }

        [NopResourceDisplayName("Plugins.Payments.BTCPayServer.ApiKey")]
        public string? ApiKey { get; set; }

        [NopResourceDisplayName("Plugins.Payments.BTCPayServer.BtcPayStoreID")]
        public string? BtcPayStoreID { get; set; }

        [NopResourceDisplayName("Plugins.Payments.BTCPayServer.WebHookSecret")]
        public string? WebHookSecret { get; set; }

        [NopResourceDisplayName("Plugins.Payments.BTCPayServer.AdditionalFee")]
        public decimal AdditionalFee { get; set; }

        [NopResourceDisplayName("Plugins.Payments.BTCPayServer.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }

        public bool OverrideForStore { get; set; }


        public bool IsConfigured()
        {
            return
                !string.IsNullOrEmpty(ApiKey) &&
                !string.IsNullOrEmpty(BtcPayStoreID) &&
                !string.IsNullOrEmpty(BtcPayUrl) &&
                !string.IsNullOrEmpty(WebHookSecret);
        }

    }

}