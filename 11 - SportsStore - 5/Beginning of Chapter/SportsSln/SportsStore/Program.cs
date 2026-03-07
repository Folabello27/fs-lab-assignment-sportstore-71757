using Microsoft.EntityFrameworkCore;
using Serilog;
using SportsStore.Models;
using SportsStore.Models.Payments;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<StoreDbContext>(opts => {
    opts.UseSqlite(
        builder.Configuration.GetConnectionString("SportsStoreConnection")
        ?? throw new InvalidOperationException("Connection string 'SportsStoreConnection' was not found."));
});

builder.Services.AddScoped<IStoreRepository, EFStoreRepository>();
builder.Services.AddScoped<IOrderRepository, EFOrderRepository>();
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection("Stripe"));
builder.Services.AddScoped<IPaymentService, StripePaymentService>();

builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
builder.Services.AddScoped<Cart>(sp => SessionCart.GetCart(sp));
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddServerSideBlazor();

try {
    Log.Information("Starting SportsStore application");

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.Use(async (context, next) => {
        try {
            await next();
        } catch (Exception ex) {
            Log.ForContext("RequestPath", context.Request.Path.Value)
                .Error(ex, "Unhandled exception while processing request");
            throw;
        }
    });

    app.UseStaticFiles();
    app.UseSession();

    app.MapControllerRoute("catpage",
        "{category}/Page{productPage:int}",
        new { Controller = "Home", action = "Index" });

    app.MapControllerRoute("page", "Page{productPage:int}",
        new { Controller = "Home", action = "Index", productPage = 1 });

    app.MapControllerRoute("category", "{category}",
        new { Controller = "Home", action = "Index", productPage = 1 });

    app.MapControllerRoute("pagination",
        "Products/Page{productPage}",
        new { Controller = "Home", action = "Index", productPage = 1 });

    app.MapDefaultControllerRoute();
    app.MapRazorPages();
    app.MapBlazorHub();
    app.MapFallbackToPage("/admin/{*catchall}", "/Admin/Index");

    SeedData.EnsurePopulated(app);

    Log.Information("SportsStore configured and ready");
    app.Run();
} catch (Exception ex) {
    Log.Fatal(ex, "SportsStore terminated unexpectedly during startup");
} finally {
    Log.CloseAndFlush();
}
