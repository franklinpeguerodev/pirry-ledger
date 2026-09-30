// Host: composicion y arranque. Sin logica de negocio (RD-02) y sin controllers:
// los controllers pertenecen a su modulo (RD-01).
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
