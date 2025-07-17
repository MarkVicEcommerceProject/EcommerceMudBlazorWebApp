window.paypalIntegration = {
    /**
     * Initialize PayPal buttons.
     * @param {string} createUrl - URL to create an order (e.g. "/api/orders")
     * @param {string} captureUrlTemplate - URL to capture an order (e.g. "/api/orders/{orderID}/capture")
     */
    initButtons: (createUrl, captureUrlTemplate) => {
        if (!window.paypal) {
            console.error("PayPal SDK not loaded.");
            return;
        }

        paypal.Buttons({
            style: {
                shape: "rect",
                layout: "vertical",
                color: "gold",
                label: "paypal",
            },

            /**
             * Create the PayPal order by POSTing to your backend.
             */
            createOrder: async () => {
                try {
                    const response = await fetch(createUrl, {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json"
                        },
                        body: JSON.stringify({
                            cart: [
                                {
                                    id: "YOUR_PRODUCT_ID",    // Replace dynamically in production
                                    quantity: 1
                                }
                            ]
                        })
                    });

                    const orderData = await response.json();
                    console.log(orderData);
                    

                    if (orderData.id) {
                        return orderData.id;
                    }

                    const errorDetail = orderData?.details?.[0];
                    const errorMessage = errorDetail
                        ? `${errorDetail.issue} ${errorDetail.description} (${orderData.debug_id})`
                        : JSON.stringify(orderData);

                    throw new Error(errorMessage);
                } catch (error) {
                    console.error("PayPal createOrder error:", error);
                    paypalIntegration.showMessage(`Could not initiate PayPal Checkout.<br><br>${error}`);
                    throw error;
                }
            },

            /**
             * Capture the order on approval
             */
            onApprove: async (data, actions) => {
                try {
                    const captureUrl = captureUrlTemplate.replace("{orderID}", data.orderID);

                    const response = await fetch(captureUrl, {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json"
                        }
                    });

                    const orderData = await response.json();

                    const errorDetail = orderData?.details?.[0];

                    if (errorDetail?.issue === "INSTRUMENT_DECLINED") {
                        return actions.restart();
                    } else if (errorDetail) {
                        throw new Error(`${errorDetail.description} (${orderData.debug_id})`);
                    } else if (!orderData.purchase_units) {
                        throw new Error("Missing purchase_units in response: " + JSON.stringify(orderData));
                    } else {
                        const transaction =
                            orderData?.purchase_units?.[0]?.payments?.captures?.[0] ||
                            orderData?.purchase_units?.[0]?.payments?.authorizations?.[0];

                        paypalIntegration.showMessage(`Transaction ${transaction.status}: ${transaction.id}<br><br>See console for details`);
                        console.log("Capture result:", orderData);
                    }
                } catch (error) {
                    console.error("PayPal onApprove error:", error);
                    paypalIntegration.showMessage(`Sorry, your transaction could not be processed...<br><br>${error}`);
                }
            },

            /**
             * Handle unexpected errors
             */
            onError: (err) => {
                console.error("PayPal JS SDK error:", err);
                paypalIntegration.showMessage("Unexpected error occurred during payment. Check console for details.");
            }

        }).render("#paypal-button-container");
    },

    /**
     * Show result message (success/failure) on UI
     * @param {string} message
     */
    showMessage: (message) => {
        const container = document.getElementById("result-message");
        if (container) {
            container.innerHTML = message;
        } else {
            alert(message);
        }
    }
};
