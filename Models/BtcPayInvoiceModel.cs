
namespace Nop.Plugin.Payments.BTCPayServer.Models
{
#pragma warning disable IDE1006 // Styles d'affectation de noms

    public struct BtcPayInvoiceMetaData
    {
        public string buyerZip;
        public string buyerName;
        public string buyerEmail;
        public string orderId;
        public string itemDesc;

    }

    public struct BtcPayInvoiceCheckout
    {
        public string defaultLanguage;
        public string redirectURL;
        public bool redirectAutomatically;
        public bool requiresRefundEmail;
    }

    public struct BtcPayInvoiceModel
    {
        public BtcPayInvoiceMetaData metadata;
        public BtcPayInvoiceCheckout checkout;
        public string currency;
        public string amount;
    }
#pragma warning restore IDE1006 // Styles d'affectation de noms
}