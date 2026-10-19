using Skylane.Api.Flights;

namespace Skylane.Api.Data;

/// <summary>
/// Dados de referência: aeroportos reais e companhias aéreas fictícias.
/// São inseridos com HasData, por isso existem sempre que a base de dados é criada.
/// </summary>
public static class ReferenceData
{
    public static readonly Airport[] Airports =
    [
        new() { Code = "OPO", Name = "Aeroporto Francisco Sá Carneiro", City = "Porto", Country = "Portugal", TimeZoneId = "Europe/Lisbon" },
        new() { Code = "LIS", Name = "Aeroporto Humberto Delgado", City = "Lisboa", Country = "Portugal", TimeZoneId = "Europe/Lisbon" },
        new() { Code = "FAO", Name = "Aeroporto de Faro", City = "Faro", Country = "Portugal", TimeZoneId = "Europe/Lisbon" },
        new() { Code = "FNC", Name = "Aeroporto da Madeira", City = "Funchal", Country = "Portugal", TimeZoneId = "Atlantic/Madeira" },
        new() { Code = "PDL", Name = "Aeroporto João Paulo II", City = "Ponta Delgada", Country = "Portugal", TimeZoneId = "Atlantic/Azores" },
        new() { Code = "MAD", Name = "Aeropuerto Adolfo Suárez Madrid-Barajas", City = "Madrid", Country = "Espanha", TimeZoneId = "Europe/Madrid" },
        new() { Code = "BCN", Name = "Aeropuerto Josep Tarradellas Barcelona-El Prat", City = "Barcelona", Country = "Espanha", TimeZoneId = "Europe/Madrid" },
        new() { Code = "CDG", Name = "Aéroport Paris-Charles de Gaulle", City = "Paris", Country = "França", TimeZoneId = "Europe/Paris" },
        new() { Code = "LHR", Name = "London Heathrow Airport", City = "Londres", Country = "Reino Unido", TimeZoneId = "Europe/London" },
        new() { Code = "FCO", Name = "Aeroporto di Roma-Fiumicino", City = "Roma", Country = "Itália", TimeZoneId = "Europe/Rome" },
        new() { Code = "AMS", Name = "Amsterdam Airport Schiphol", City = "Amesterdão", Country = "Países Baixos", TimeZoneId = "Europe/Amsterdam" }
    ];

    public static readonly Airline[] Airlines =
    [
        new() { Code = "AT", Name = "Atlântico Air" },
        new() { Code = "LW", Name = "Lusa Wings" }
    ];
}
