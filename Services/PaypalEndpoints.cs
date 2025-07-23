using System.Security.Claims;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Http.Response;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;
using PaypalServerSdk.Standard.Authentication;
using PaypalServerSdk.Standard.Models;
using Order = ECommerceMudblazorWebApp.Models.Order;
using OrderStatus = ECommerceMudblazorWebApp.Models.OrderStatus;
using PaypalServerSdk.Standard.Controllers;
using ECommerceMudblazorWebApp.Models;

namespace ECommerceMudblazorWebApp.Services
{
    internal static class PaypalEndpoints
    {
        // PayPal API endpoints 
        public static IEndpointConventionBuilder MapPaypalEndpoints(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var paypalGroup = endpoints.MapGroup("/api/orders");

            paypalGroup.MapPost("", async (
                HttpContext context,
                IOrderService orderService,
                ICartService cartService,
                IConfiguration config,
                ClaimsPrincipal user
            ) =>
            {
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

                var cartItems = cartService.ShoppingCart.Items;
                if (cartItems == null || cartItems.Count == 0)
                {
                    return Results.BadRequest("Cart is empty.");
                }

                var order = new Order
                {
                    UserId = userId,
                    ShippingAddress = "TEMP",
                    PaymentMethod = "PayPal",
                    OrderItems = cartItems.Select(ci => new OrderItem
                    {
                        ProductId = ci.ProductId,
                        Quantity = ci.Quantity,
                        UnitPrice = ci.UnitPrice
                    }).ToList(),
                    Status = OrderStatus.PENDING,
                };
                var localOrderId = await orderService.PlaceOrderAsync(order);
                PaypalServerSdkClient client = new PaypalServerSdkClient.Builder()
                .Environment(PaypalServerSdk.Standard.Environment.Sandbox)
                .ClientCredentialsAuth(new ClientCredentialsAuthModel.Builder(
                        System.Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID") ?? config["PayPal:ClientId"],
                        System.Environment.GetEnvironmentVariable("PAYPAL_CLIENT_SECRET") ?? config["PayPal:ClientSecret"]
                )
                .Build())
                .Build();

                var ordersController = client.OrdersController;

                var totalAmount = cartItems.Sum(item => item.Product.Price * item.Quantity);
                var totalFormatted = totalAmount.ToString("F2"); // Ensures 2 decimal places
                Console.WriteLine($"Total Amount: {totalFormatted}");

                CheckoutPaymentIntent intent = (CheckoutPaymentIntent)
                        Enum.Parse(typeof(CheckoutPaymentIntent), "CAPTURE", true);

                CreateOrderInput createOrderInput = new CreateOrderInput
                {
                    Body = new OrderRequest
                    {
                        Intent = intent,
                        PurchaseUnits = new List<PurchaseUnitRequest>
                        {
                            new PurchaseUnitRequest
                                {
                                Amount = new AmountWithBreakdown
                                {
                                    CurrencyCode = "USD",
                                    MValue = totalFormatted,
                                    Breakdown = new AmountBreakdown
                                    {
                                        ItemTotal = new Money
                                        {
                                            CurrencyCode = "USD",
                                            MValue = totalFormatted
                                        }
                                    }
                                },
                                Items = cartItems.Select(ci => new Item
                                {
                                    Name = ci.Product?.Name,
                                    UnitAmount = new Money
                                    {
                                        CurrencyCode = "USD",
                                        MValue = ci.UnitPrice.ToString("F2")
                                    },
                                    Quantity = ci.Quantity.ToString()
                                }).ToList()
                            }
                        },
                    }
                };
                var paypalOrder = await ordersController.CreateOrderAsync(createOrderInput);
                return Results.Ok(new
                {
                    id = paypalOrder.Data.Id,
                    localOrderId
                });
            });

            paypalGroup.MapPost("/{orderID}/capture", async (
                string orderID,
                IOrderService orderService,
                IConfiguration config
            ) =>
            {
                PaypalServerSdkClient paypalClient = new PaypalServerSdkClient.Builder()
                    .Environment(PaypalServerSdk.Standard.Environment.Sandbox)
                    .ClientCredentialsAuth(new ClientCredentialsAuthModel.Builder(
                        System.Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID") ?? config["PayPal:ClientId"],
                        System.Environment.GetEnvironmentVariable("PAYPAL_CLIENT_SECRET") ?? config["PayPal:ClientSecret"]
                    ).Build())
                    .Build();

                OrdersController ordersController = paypalClient.OrdersController;

                CaptureOrderInput captureInput = new CaptureOrderInput { Id = orderID };
                ApiResponse<PaypalServerSdk.Standard.Models.Order> captureResult = await ordersController.CaptureOrderAsync(captureInput);

                if (captureResult.Data.Status.ToString() == "COMPLETED")
                {
                    // Optional: update local order status to Confirmed/Paid
                    // await orderService.ConfirmOrder(localOrderId);
                    return Results.Ok(captureResult.Data);
                }

                return Results.StatusCode(500);
            });

            return paypalGroup;
        }
    }
}