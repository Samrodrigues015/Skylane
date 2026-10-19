using Skylane.Api.Data;
using Skylane.Api.Flights;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Events;

/// <summary>
/// Eventos de demonstração em todas as cidades com aeroporto, para os próximos meses.
/// Nomes e salas são fictícios.
/// </summary>
public static class EventCatalog
{
    private sealed record Sample(string Airport, string Name, string Venue, int DaysFromToday, int Hour, int Capacity);

    private static readonly Sample[] Samples =
    [
        new("OPO", "Noite de Fado no Cais", "Armazém do Rio, Porto", 9, 21, 120),
        new("OPO", "Festival de Jazz da Ribeira", "Jardim do Morro, Gaia", 23, 18, 800),
        new("OPO", "Stand-up: Sessão Esgotável", "Sala Pequena, Porto", 4, 22, 8),
        new("OPO", "Concerto de Ano Novo", "Auditório Central, Porto", 80, 17, 450),

        new("LIS", "Fado na Mouraria", "Casa da Guitarra, Lisboa", 6, 21, 90),
        new("LIS", "Festival de Luz do Tejo", "Ribeira das Naus, Lisboa", 19, 20, 2000),
        new("LIS", "Concerto ao Pôr do Sol", "Miradouro da Graça, Lisboa", 41, 18, 300),
        new("LIS", "Lisboa Electrónica", "Armazém 7, Lisboa", 67, 23, 1200),

        new("FAO", "Noites de Música na Marina", "Marina de Faro", 12, 21, 400),
        new("FAO", "Festival de Marisco do Algarve", "Largo da Sé, Faro", 34, 13, 1500),
        new("FAO", "Guitarra ao Luar", "Cidade Velha, Faro", 58, 21, 150),

        new("FNC", "Mostra de Flores do Atlântico", "Praça do Município, Funchal", 15, 11, 3000),
        new("FNC", "Noite de Música na Zona Velha", "Rua de Santa Maria, Funchal", 29, 21, 250),
        new("FNC", "Fogo de Artifício na Baía", "Marina do Funchal", 74, 23, 5000),

        new("PDL", "Concerto nas Portas da Cidade", "Portas da Cidade, Ponta Delgada", 17, 21, 800),
        new("PDL", "Festival de Música dos Açores", "Auditório do Mar, Ponta Delgada", 46, 20, 600),

        new("MAD", "Noche de Flamenco", "Tablao del Centro, Madrid", 8, 22, 120),
        new("MAD", "Jazz en el Parque", "Parque del Retiro, Madrid", 27, 19, 900),
        new("MAD", "Madrid Indie Fest", "Nave 4, Madrid", 53, 20, 1500),

        new("BCN", "Concert a la Platja", "Platja de la Barceloneta, Barcelona", 11, 21, 2500),
        new("BCN", "Nit de Guitarra", "Sala Gòtica, Barcelona", 32, 21, 140),
        new("BCN", "Festival de Música Mediterrània", "Port Vell, Barcelona", 61, 19, 1800),

        new("CDG", "Soirée Jazz à Montmartre", "Cave Saint-Pierre, Paris", 7, 21, 110),
        new("CDG", "Concert sur la Seine", "Quai de la Tournelle, Paris", 25, 20, 700),
        new("CDG", "Nuit Électro", "Hangar 12, Paris", 49, 23, 1500),

        new("LHR", "Late Night Comedy", "Camden Basement, London", 5, 21, 90),
        new("LHR", "Thames Riverside Concert", "South Bank Lawn, London", 22, 19, 1200),
        new("LHR", "Winter Lights Festival", "Canal Side, London", 71, 18, 3000),

        new("FCO", "Notte d'Opera", "Teatro dei Colli, Roma", 13, 20, 500),
        new("FCO", "Jazz a Trastevere", "Piazza del Vicolo, Roma", 36, 21, 300),
        new("FCO", "Cinema sotto le Stelle", "Giardino Aurelio, Roma", 64, 21, 400),

        new("AMS", "Canal Lights Concert", "Prinsengracht, Amsterdam", 10, 20, 600),
        new("AMS", "Jazz in de Jordaan", "Huiskamer Jordaan, Amsterdam", 30, 21, 80),
        new("AMS", "Amsterdam Dance Night", "Westerpark Hal 3, Amsterdam", 55, 23, 2000)
    ];

    public static async Task SeedAsync(SkylaneDbContext db, TimeProvider time, CancellationToken ct = default)
    {
        if (await db.Events.AnyAsync(ct))
        {
            return;
        }

        var timeZones = await db.Airports.ToDictionaryAsync(a => a.Code, a => a.TimeZoneId, ct);
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

        db.Events.AddRange(Samples.Select(s => new Event
        {
            Id = Guid.CreateVersion7(),
            Name = s.Name,
            Venue = s.Venue,
            AirportCode = s.Airport,
            // A hora é a da cidade do evento (21:00 em Roma), guardada em UTC.
            StartsAt = AirportTime.FromLocal(today.AddDays(s.DaysFromToday).ToDateTime(new TimeOnly(s.Hour, 0)), timeZones[s.Airport]),
            Capacity = s.Capacity
        }));

        await db.SaveChangesAsync(ct);
    }
}
