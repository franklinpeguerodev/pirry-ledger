// Host: composicion y arranque. Sin logica de negocio (RD-02) y sin controllers:
// cada pieza expone los suyos (RD-01).
using PirryLedger.Core.AccessControl.Api;
using PirryLedger.Core.AccessControl.Infrastructure;
using PirryLedger.Core.Notifications.Api;
using PirryLedger.Core.Notifications.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddAccessControl();
builder.Services.AddNotifications();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapAccessControl();
app.MapNotifications();

app.Run();
