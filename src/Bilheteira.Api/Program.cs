using System.Text.Json.Serialization;
using Bilheteira.Api.Bookings;
using Bilheteira.Api.Data;
using Bilheteira.Api.Flights;
using Bilheteira.Api.Reservations;
using Bilheteira.Api.Tickets;
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
builder.Services.AddDbContext<BilheteiraDbContext>((services, options) =>
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

app.Run();

// Torna a classe Program visível para o WebApplicationFactory dos testes de integração.
public partial class Program;
