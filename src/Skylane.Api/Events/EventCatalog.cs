using Skylane.Api.Data;
using Skylane.Api.Flights;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Events;

/// <summary>
/// Eventos de demonstração em todas as cidades com aeroporto, que se repetem ao longo do ano.
/// Nomes e salas são fictícios.
/// </summary>
public static class EventCatalog
{
    /// <param name="FirstDay">Dias depois de <see cref="Anchor"/> em que acontece pela primeira vez.</param>
    /// <param name="RepeatDays">De quantos em quantos dias se repete.</param>
    private sealed record Sample(
        string Airport, string Name, string Venue, int FirstDay, int Hour, int Capacity, decimal Price, int RepeatDays,
        EventCategory Category, string Pt, string En);

    /// <summary>
    /// Data fixa a partir da qual os eventos se repetem. Por ser fixa, o calendário é sempre o mesmo,
    /// seja qual for o dia em que a aplicação arranca.
    /// </summary>
    private static readonly DateOnly Anchor = new(2026, 1, 5);

    /// <summary>Até quantos dias à frente há sempre eventos.</summary>
    public const int DaysAhead = 120;

    private static readonly Sample[] Samples =
    [
        // ---------- Porto ----------
        new("OPO", "Noite de Fado no Cais", "Armazém do Rio, Porto", 9, 21, 120, 22m, 21, EventCategory.Fado,
            "Um armazém junto ao Douro transformado em casa de fado por uma noite: duas fadistas, guitarra portuguesa e viola, com as luzes de Gaia do outro lado do rio. Lugares sentados, com vinho do Porto à venda no bar.",
            "A riverside warehouse on the Douro turned into a fado house for one night: two fado singers, a Portuguese guitar and a classical guitar, with the lights of Gaia across the river. Seated, with port wine at the bar."),
        new("OPO", "Festival de Jazz da Ribeira", "Jardim do Morro, Gaia", 23, 18, 800, 35m, 28, EventCategory.Jazz,
            "Quatro grupos de jazz ao ar livre, do swing ao jazz contemporâneo, num jardim com vista para a Ribeira e para a ponte D. Luís I. Começa ao fim da tarde e acaba já de noite.",
            "Four open-air jazz acts, from swing to contemporary jazz, in a garden overlooking the Ribeira and the D. Luís I bridge. Starts in the late afternoon and runs into the night."),
        new("OPO", "Stand-up: Sessão Esgotável", "Sala Pequena, Porto", 4, 22, 8, 12m, 7, EventCategory.Comedy,
            "Comédia de stand-up numa sala minúscula na Baixa: três humoristas a testar material novo para um público de apenas oito pessoas. Esgota quase todas as semanas.",
            "Stand-up comedy in a tiny room downtown: three comedians trying out new material for an audience of just eight. Sells out almost every week."),
        new("OPO", "Concerto de Gala", "Auditório Central, Porto", 80, 17, 450, 45m, 35, EventCategory.Classical,
            "Orquestra de câmara com um programa de Mozart e Beethoven, num auditório com acústica pensada para música clássica. Sessão ao fim da tarde, ideal antes de jantar.",
            "A chamber orchestra playing Mozart and Beethoven in a hall designed for classical music. Early-evening performance, perfect before dinner."),

        // ---------- Lisboa ----------
        new("LIS", "Fado na Mouraria", "Casa da Guitarra, Lisboa", 6, 21, 90, 25m, 21, EventCategory.Fado,
            "A Mouraria é um dos bairros onde o fado nasceu. Nesta casa pequena canta-se sem microfone, à luz das velas, e entre fados conta-se a história de cada canção.",
            "Mouraria is one of the neighbourhoods where fado was born. In this small house the singers perform without microphones, by candlelight, telling the story behind each song."),
        new("LIS", "Festival de Luz do Tejo", "Ribeira das Naus, Lisboa", 19, 20, 2000, 15m, 28, EventCategory.LightFestival,
            "Instalações de luz e projeções ao longo da margem do Tejo, entre o Cais do Sodré e a Praça do Comércio. Percurso a pé de cerca de uma hora, com música ao vivo pelo caminho.",
            "Light installations and projections along the Tagus riverfront, between Cais do Sodré and Praça do Comércio. About an hour's walk, with live music along the way."),
        new("LIS", "Concerto ao Pôr do Sol", "Miradouro da Graça, Lisboa", 41, 18, 300, 18m, 14, EventCategory.Concert,
            "Concerto acústico num dos miradouros mais altos de Lisboa, marcado para a hora do pôr do sol, com vista sobre o castelo e o rio.",
            "An acoustic concert at one of Lisbon's highest viewpoints, timed for sunset, overlooking the castle and the river."),
        new("LIS", "Lisboa Electrónica", "Armazém 7, Lisboa", 67, 23, 1200, 30m, 35, EventCategory.Electronic,
            "Noite de música eletrónica num antigo armazém do porto, com DJs portugueses e convidados internacionais até de manhã.",
            "A night of electronic music in an old harbour warehouse, with Portuguese DJs and international guests until sunrise."),

        // ---------- Faro ----------
        new("FAO", "Noites de Música na Marina", "Marina de Faro", 12, 21, 400, 15m, 21, EventCategory.Concert,
            "Concertos ao ar livre junto à marina, com bandas do Algarve e esplanadas à volta. Uma boa maneira de acabar um dia de praia.",
            "Open-air concerts by the marina with bands from the Algarve and terraces all around. A good way to end a day at the beach."),
        new("FAO", "Festival de Marisco do Algarve", "Largo da Sé, Faro", 34, 13, 1500, 20m, 28, EventCategory.Food,
            "Bancas de marisco da Ria Formosa, cataplanas e petiscos algarvios no largo da Sé. O bilhete inclui uma prova de três pratos e um copo de vinho.",
            "Seafood stalls from the Ria Formosa, cataplanas and Algarve snacks in the cathedral square. The ticket includes a tasting of three dishes and a glass of wine."),
        new("FAO", "Guitarra ao Luar", "Cidade Velha, Faro", 58, 21, 150, 12m, 14, EventCategory.Guitar,
            "Recital de guitarra clássica dentro das muralhas da Cidade Velha, com repertório espanhol e português.",
            "A classical guitar recital inside the walls of Faro's old town, with Spanish and Portuguese repertoire."),

        // ---------- Funchal ----------
        new("FNC", "Mostra de Flores do Atlântico", "Praça do Município, Funchal", 15, 11, 3000, 8m, 21, EventCategory.Flowers,
            "A Madeira é conhecida pelas flores, que o clima ameno faz florir quase todo o ano. Nesta mostra, produtores da ilha enchem a praça de estrelícias, orquídeas e tapetes de flores.",
            "Madeira is known for its flowers, which bloom almost all year thanks to the mild climate. At this show, local growers fill the square with birds of paradise, orchids and flower carpets."),
        new("FNC", "Noite de Música na Zona Velha", "Rua de Santa Maria, Funchal", 29, 21, 250, 15m, 28, EventCategory.Concert,
            "Música tradicional madeirense e bandas locais na rua mais antiga do Funchal, conhecida pelas portas pintadas por artistas.",
            "Traditional Madeiran music and local bands on Funchal's oldest street, known for its doors painted by artists."),
        new("FNC", "Fogo de Artifício na Baía", "Marina do Funchal", 74, 23, 5000, 10m, 14, EventCategory.Fireworks,
            "O fogo de artifício faz parte da tradição do Funchal. Este espetáculo de quinze minutos é lançado de vários pontos da baía, e o bilhete dá acesso à zona reservada da marina.",
            "Fireworks are part of Funchal's tradition. This fifteen-minute show is launched from several points around the bay, and the ticket gives access to the reserved area at the marina."),

        // ---------- Ponta Delgada ----------
        new("PDL", "Concerto nas Portas da Cidade", "Portas da Cidade, Ponta Delgada", 17, 21, 800, 18m, 21, EventCategory.Concert,
            "Concerto em frente às Portas da Cidade, os três arcos que são o postal de Ponta Delgada, com músicos das várias ilhas dos Açores.",
            "A concert in front of the Portas da Cidade, the three arches that are Ponta Delgada's postcard, with musicians from across the Azores."),
        new("PDL", "Festival de Música dos Açores", "Auditório do Mar, Ponta Delgada", 46, 20, 600, 28m, 28, EventCategory.Festival,
            "Duas noites de música com artistas açorianos e convidados do continente, num auditório virado para o Atlântico.",
            "Two nights of music with Azorean artists and guests from the mainland, in an auditorium facing the Atlantic."),

        // ---------- Madrid ----------
        new("MAD", "Noche de Flamenco", "Tablao del Centro, Madrid", 8, 22, 120, 35m, 21, EventCategory.Flamenco,
            "Espetáculo num tablao, o palco pequeno onde o flamenco se vê de perto: cante, guitarra e baile, com o público a poucos metros dos bailarinos.",
            "A show at a tablao, the small stage where flamenco is seen up close: singing, guitar and dance, with the audience just a few metres from the dancers."),
        new("MAD", "Jazz en el Parque", "Parque del Retiro, Madrid", 27, 19, 900, 25m, 28, EventCategory.Jazz,
            "Jazz ao fim da tarde num dos maiores parques de Madrid, com piquenique permitido e bancas de comida.",
            "Early-evening jazz in one of Madrid's largest parks, with picnics allowed and food stalls."),
        new("MAD", "Madrid Indie Fest", "Nave 4, Madrid", 53, 20, 1500, 30m, 14, EventCategory.Festival,
            "Festival de música independente numa antiga fábrica, com bandas espanholas em dois palcos.",
            "An indie music festival in a former factory, with Spanish bands on two stages."),

        // ---------- Barcelona ----------
        new("BCN", "Concert a la Platja", "Platja de la Barceloneta, Barcelona", 11, 21, 2500, 20m, 21, EventCategory.Concert,
            "Concerto na areia da Barceloneta, a praia mais conhecida da cidade, com pop catalão e uma banda convidada.",
            "A concert on the sand at Barceloneta, the city's best-known beach, with Catalan pop and a guest band."),
        new("BCN", "Nit de Guitarra", "Sala Gòtica, Barcelona", 32, 21, 140, 28m, 28, EventCategory.Guitar,
            "Guitarra espanhola numa sala do Bairro Gótico, com obras de Albéniz, Granados e Tárrega.",
            "Spanish guitar in a hall in the Gothic Quarter, with works by Albéniz, Granados and Tárrega."),
        new("BCN", "Festival de Música Mediterrània", "Port Vell, Barcelona", 61, 19, 1800, 32m, 14, EventCategory.Festival,
            "Músicos de vários países do Mediterrâneo no porto antigo de Barcelona: rumba catalã, música grega, árabe e italiana.",
            "Musicians from around the Mediterranean in Barcelona's old port: Catalan rumba, Greek, Arabic and Italian music."),

        // ---------- Paris ----------
        new("CDG", "Soirée Jazz à Montmartre", "Cave Saint-Pierre, Paris", 7, 21, 110, 30m, 21, EventCategory.Jazz,
            "Jazz numa cave abobadada em Montmartre, o bairro dos artistas no alto de Paris. Trio de piano, contrabaixo e bateria.",
            "Jazz in a vaulted cellar in Montmartre, the artists' quarter on the hill above Paris. Piano, double bass and drums."),
        new("CDG", "Concert sur la Seine", "Quai de la Tournelle, Paris", 25, 20, 700, 45m, 28, EventCategory.Classical,
            "Concerto de música clássica num cais do Sena, com vista para a Île de la Cité.",
            "A classical concert on a quay of the Seine, overlooking the Île de la Cité."),
        new("CDG", "Nuit Électro", "Hangar 12, Paris", 49, 23, 1500, 35m, 14, EventCategory.Electronic,
            "Noite de house e techno francês num hangar industrial nos arredores de Paris.",
            "A night of French house and techno in an industrial hangar on the outskirts of Paris."),

        // ---------- Londres ----------
        new("LHR", "Late Night Comedy", "Camden Basement, London", 5, 21, 90, 18m, 21, EventCategory.Comedy,
            "Londres tem uma das cenas de stand-up mais fortes do mundo. Nesta cave em Camden atuam humoristas em início de carreira e, às vezes, nomes conhecidos a testar material. Espetáculo em inglês.",
            "London has one of the strongest stand-up scenes in the world. In this Camden basement, up-and-coming comedians perform and well-known names sometimes drop in to test new material. In English."),
        new("LHR", "Thames Riverside Concert", "South Bank Lawn, London", 22, 19, 1200, 40m, 28, EventCategory.Concert,
            "Concerto ao ar livre na margem sul do Tamisa, com vista para o centro de Londres.",
            "An open-air concert on the South Bank of the Thames, looking across to central London."),
        new("LHR", "Winter Lights Festival", "Canal Side, London", 71, 18, 3000, 25m, 14, EventCategory.LightFestival,
            "Percurso de instalações de luz ao longo de um canal no leste de Londres, pensado para os fins de tarde que escurecem cedo.",
            "A trail of light installations along a canal in East London, designed for the early-dark evenings."),

        // ---------- Roma ----------
        new("FCO", "Notte d'Opera", "Teatro dei Colli, Roma", 13, 20, 500, 55m, 21, EventCategory.Opera,
            "Uma seleção das árias mais conhecidas de Verdi e Puccini, cantadas por solistas e coro com orquestra. Legendas em inglês.",
            "A selection of the best-known arias by Verdi and Puccini, sung by soloists and chorus with orchestra. English surtitles."),
        new("FCO", "Jazz a Trastevere", "Piazza del Vicolo, Roma", 36, 21, 300, 25m, 28, EventCategory.Jazz,
            "Jazz numa praça de Trastevere, o bairro de ruas estreitas do outro lado do Tibre, cheio de trattorias.",
            "Jazz in a square in Trastevere, the neighbourhood of narrow streets across the Tiber, full of trattorias."),
        new("FCO", "Cinema sotto le Stelle", "Giardino Aurelio, Roma", 64, 21, 400, 12m, 14, EventCategory.Cinema,
            "O cinema ao ar livre é uma tradição dos verões italianos. Clássicos do cinema italiano num jardim, com legendas em inglês e cadeiras de lona.",
            "Open-air cinema is an Italian summer tradition. Classic Italian films in a garden, with English subtitles and deckchairs."),

        // ---------- Amesterdão ----------
        new("AMS", "Canal Lights Concert", "Prinsengracht, Amsterdam", 10, 20, 600, 35m, 21, EventCategory.Classical,
            "Concerto de música de câmara num barco no Prinsengracht, um dos canais do centro histórico, Património Mundial da UNESCO.",
            "Chamber music on a boat on the Prinsengracht, one of the canals of the historic centre, a UNESCO World Heritage Site."),
        new("AMS", "Jazz in de Jordaan", "Huiskamer Jordaan, Amsterdam", 30, 21, 80, 22m, 28, EventCategory.Jazz,
            "Jazz numa sala pequena no Jordaan, o bairro de casas estreitas e canais tranquilos ao lado do centro.",
            "Jazz in a small room in the Jordaan, the neighbourhood of narrow houses and quiet canals next to the centre."),
        new("AMS", "Amsterdam Dance Night", "Westerpark Hal 3, Amsterdam", 55, 23, 2000, 38m, 14, EventCategory.Electronic,
            "Os Países Baixos são uma referência da música eletrónica. Noite de DJs holandeses num antigo complexo industrial no Westerpark.",
            "The Netherlands is a leading name in electronic music. A night of Dutch DJs in a former industrial complex in Westerpark.")
    ];

    /// <summary>
    /// Garante que cada evento tem as suas sessões dos próximos <see cref="DaysAhead"/> dias.
    /// Corre no arranque e uma vez por dia (<see cref="Data.DemoDataRefresher"/>), por isso a demo
    /// tem sempre eventos à venda, seja qual for o dia em que alguém a abre.
    /// </summary>
    public static async Task EnsureUpcomingAsync(SkylaneDbContext db, TimeProvider time, CancellationToken ct = default)
    {
        var timeZones = await db.Airports.ToDictionaryAsync(a => a.Code, a => a.TimeZoneId, ct);
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var first = today.AddDays(1);
        var last = today.AddDays(DaysAhead);

        var wanted = Samples
            .SelectMany(s => Occurrences(s, first, last).Select(day => new
            {
                Sample = s,
                // A hora é a da cidade do evento (21:00 em Roma), guardada em UTC.
                StartsAt = AirportTime.FromLocal(day.ToDateTime(new TimeOnly(s.Hour, 0)), timeZones[s.Airport])
            }))
            .ToList();

        var from = AirportTime.FromLocal(first.ToDateTime(TimeOnly.MinValue), "UTC").AddDays(-1);
        var existing = (await db.Events
                .Where(e => e.StartsAt >= from)
                .Select(e => new { e.Name, e.StartsAt })
                .ToListAsync(ct))
            .Select(e => (e.Name, e.StartsAt.UtcDateTime))
            .ToHashSet();

        var missing = wanted
            .Where(w => !existing.Contains((w.Sample.Name, w.StartsAt.UtcDateTime)))
            .Select(w => new Event
            {
                Id = Guid.CreateVersion7(),
                Name = w.Sample.Name,
                Venue = w.Sample.Venue,
                AirportCode = w.Sample.Airport,
                StartsAt = w.StartsAt,
                Capacity = w.Sample.Capacity,
                Price = w.Sample.Price,
                Category = w.Sample.Category,
                Description = w.Sample.Pt,
                DescriptionEn = w.Sample.En
            })
            .ToList();

        if (missing.Count > 0)
        {
            db.Events.AddRange(missing);
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Dias entre <paramref name="first"/> e <paramref name="last"/> em que o evento acontece.</summary>
    private static IEnumerable<DateOnly> Occurrences(Sample sample, DateOnly first, DateOnly last)
    {
        var day = Anchor.AddDays(sample.FirstDay);
        if (day < first)
        {
            var periods = (first.DayNumber - day.DayNumber + sample.RepeatDays - 1) / sample.RepeatDays;
            day = day.AddDays(periods * sample.RepeatDays);
        }

        for (; day <= last; day = day.AddDays(sample.RepeatDays))
        {
            yield return day;
        }
    }
}
