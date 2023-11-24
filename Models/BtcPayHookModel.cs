

namespace Nop.Plugin.Payments.BTCPayServer.Models
{
    public struct BtcPayHookModel
    {
        public bool enabled = true;
        public bool automaticRedelivery = true;
        public string url;
        public BtcPayHookAuthorizedEvents authorizedEvents = new BtcPayHookAuthorizedEvents();
        public string secret;

        public BtcPayHookModel()
        {
        }
    }

    public struct BtcPayHookAuthorizedEvents
    {
        public bool everything = true;

        public BtcPayHookAuthorizedEvents()
        {
        }
    }
}
