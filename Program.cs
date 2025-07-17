using ECommerceMudblazorWebApp.Components;
using ECommerceMudblazorWebApp.Components.Account;
using ECommerceMudblazorWebApp.Data;
using ECommerceMudblazorWebApp.Data.Models;
using ECommerceMudblazorWebApp.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using System.Security.Claims;
using PaypalServerSdk.Standard;
using PaypalServerSdk.Standard.Http.Response;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;
using PaypalServerSdk.Standard.Authentication;
using PaypalServerSdk.Standard.Models;
using Order = ECommerceMudblazorWebApp.Data.Models.Order;
using OrderStatus = ECommerceMudblazorWebApp.Data.Models.OrderStatus;
using PaypalServerSdk.Standard.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.AddSingleton<ICartService, CartService>();
builder.Services.AddSingleton<IOrderService,OrderService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

//Minimal Api for Paypal Integration
app.MapPost("/api/orders", async (
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

app.MapPost("/api/orders/{orderID}/capture", async (
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

app.Run();
