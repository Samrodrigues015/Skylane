using System.Text.Json.Serialization;
using Skylane.Api.Bookings;
using Skylane.Api.Data;
using Skylane.Api.Flights;
using Skylane.Api.Payments;
using Skylane.Api.Reservations;
using Skylane.Api.Tickets;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// A connection string é lida quando o DbContext é criado (e não aqui, no arranque),
// para os testes a poderem substituir pela do contentor SQL Server temporário.
builder.Services.AddDbContext<SkylaneDbContext>((services, options) =>
    options.UseSqlServer(services.GetRequiredService<IConfiguration>().GetConnectionString("Default")));

builder.Services.Configure<ReservationOptions>(builder.Configuration.GetSection(ReservationOptions.Section));

// Abstração do relógio: em produção é o relógio do sistema; nos testes conseguimos
// "avançar o tempo" para testar expirações sem esperar 10 minutos.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<ReservationService>();
builder.Services.AddScoped<ReservationExpirer>();
builder.Services.AddScoped<TicketService>();

builder.Services.Configure<BookingOptions>(builder.Configuration.GetSection(BookingOptions.Section));
builder.Services.AddScoped<FlightService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<BookingExpirer>();

builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection(PaymentOptions.Section));
builder.Services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
builder.Services.AddScoped<BookingPaymentService>();

builder.Services.AddHostedService<ReservationExpiryJob>();

var app = builder.Build();

await DatabaseSetup.InitializeAsync(app);

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();

// O front-end React é servido a partir do wwwroot (gerado pelo build do Skylane.Web).
// Qualquer rota que não seja da API (ex.: /flights, /booking/K7M2Q9) devolve o index.html
// e o React Router trata dela no browser.
app.MapFallbackToFile("{*path:regex(^(?!api/).*$)}", "index.html");

app.Run();

// Torna a classe Program visível para o WebApplicationFactory dos testes de integração.
public partial class Program;
