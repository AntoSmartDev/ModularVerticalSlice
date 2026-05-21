using ModularVerticalSlice.Modules.Bookings;
using ModularVerticalSlice.Modules.Catalog;
using ModularVerticalSlice.Modules.Common.Modules;
using ModularVerticalSlice.Modules.Notifications;
using ModularVerticalSlice.Modules.Payments;

var builder = WebApplication.CreateBuilder(args);

IModule[] modules =
[
    new CatalogModule(),
    new BookingsModule(),
    new PaymentsModule(),
    new NotificationsModule()
];

builder.Services.AddApplicationModules(builder.Configuration, modules);
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapApplicationModules(modules);
app.MapGet("/", () => Results.Text("ModularVerticalSlice.WebApi"));

app.Run();
