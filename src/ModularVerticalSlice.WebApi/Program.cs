using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Catalog;
using ModularVerticalSlice.Application.Shared.Modules;
using ModularVerticalSlice.Application.Modules.Notifications;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.Persistence;
using Wolverine;
using Wolverine.Postgresql;

var builder = WebApplication.CreateBuilder(args);
var connectionString =
    builder.Configuration.GetConnectionString("Database") ??
    "Host=localhost;Port=5432;Database=modularverticalslice;Username=postgres;Password=postgres";

IModule[] modules =
[
    new CatalogModule(),
    new BookingsModule(),
    new PaymentsModule(),
    new NotificationsModule()
];

builder.Host.UseWolverine(options =>
{
    options.Policies.AutoApplyTransactions();
    options
        .PersistMessagesWithPostgresql(connectionString, "messaging")
        .EnableMessageTransport(_ => { });
});

builder.Services.AddApplicationModules(builder.Configuration, modules);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapApplicationModules(modules);
app.MapGet("/", () => Results.Text("ModularVerticalSlice.WebApi"));

app.Run();
