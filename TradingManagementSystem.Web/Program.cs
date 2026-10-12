using TradingManagementSystem.Application.Services;
using TradingManagementSystem.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var mongoConnectionString =
    builder.Configuration["MongoDb:ConnectionString"]
    ?? throw new InvalidOperationException(
        "MongoDB connection string is missing.");

var mongoDatabaseName =
    builder.Configuration["MongoDb:DatabaseName"]
    ?? throw new InvalidOperationException(
        "MongoDB database name is missing.");

builder.Services.AddSingleton(
    new MongoDbContext(
        mongoConnectionString,
        mongoDatabaseName));

builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ReceiptService>();
builder.Services.AddScoped<SaleService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();