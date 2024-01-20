# BTCPay plugin for NopCommerce - accept Bitcoin payments


## Plugin Overview

This plugin allows you to easily integrate Bitcoin payments into your NopCommerce website using BTCPay Server — a free, self-hosted and open-source payment gateway solution designed to revolutionize Bitcoin payments. Our seamless integration with NopCommerce ensures a hassle-free connection to your self-hosted BTCPay Server. 

Experience the simplicity of accepting Bitcoin payments with just a few straightforward steps. You can configure the plugin either automatically or manually, depending on your preferences and requirements.

## Automatic Configuration

1. Enter Url to your BTCPay Server into "BTCPay Url" field. (e.g. https://mainnet.demo.btcpayserver.org)
2. Click on the "Configure automatically" button to be redirected to the API authorization page on your BTCPay server
3. On BTCPay authorization page: Select the store you want to connect to your NopCommerce (you might need to login first)
4. Click on "Authorize App" button and you will be redirected back to your NopCommerce
3. The "API Key", "BTCPay Store ID" and "Webhook Secret" fields will be automatically filled and a webhook created
4. Click "Save" button at the top right to persist the configuration
5. Congrats, the configuration is now done.

## Manual Configuration

Ensure that the following fields are filled out: "BTCPay Url," "API Key," "BTCPay Store ID," and "WebHook Secret."

To create the BTCPay API key, [read this](https://docs.btcpayserver.org/VirtueMart/#22-create-an-api-key-and-configure-permissions).
- Note: If you want to use the Refund feature, you must also add the "Create non-approved pull payments" permission. After a refund, an order note is created where you can copy the pull payments link and send to your customer (this order note is also visible by the customer). The customer can request the refund on that page.

To create the BTCPay WebHook, [read this](https://docs.btcpayserver.org/VirtueMart/#23-create-a-webhook-on-btcpay-server) and use the default secret code generated by BTCPay.
- Note: Other than in the guide you need to copy the Url shown in field "Webhook Url" from your configuration screen on NopCommerce.

## Support

Feel free to join our support channel over at [Mattermost Chat](https://chat.btcpayserver.org/) if you need help or have any further questions.

If experience a bug please open an issue in our repository [here]
## License

This plugin is released under the [MIT License](LICENSE).

---
Find our latest releases on the [NopCommerce marketplace] or on our [release page]