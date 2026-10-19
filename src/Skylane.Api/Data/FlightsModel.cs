using Skylane.Api.Bookings;
using Skylane.Api.Flights;
using Skylane.Api.Payments;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Data;

/// <summary>
/// Configuração das tabelas dos voos e das reservas de voo.
/// Está separada do DbContext para não ficar tudo num ficheiro enorme.
/// </summary>
public static class FlightsModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Airport>(a =>
        {
            a.HasKey(x => x.Code);
            a.Property(x => x.Code).HasMaxLength(3);
            a.Property(x => x.Name).HasMaxLength(100);
            a.Property(x => x.City).HasMaxLength(60);
            a.Property(x => x.Country).HasMaxLength(60);
            a.Property(x => x.TimeZoneId).HasMaxLength(40);

            // Dados de referência: existem sempre, também nos testes.
            a.HasData(ReferenceData.Airports);
        });

        modelBuilder.Entity<Airline>(a =>
        {
            a.HasKey(x => x.Code);
            a.Property(x => x.Code).HasMaxLength(2);
            a.Property(x => x.Name).HasMaxLength(60);
            a.HasData(ReferenceData.Airlines);
        });

        modelBuilder.Entity<Flight>(f =>
        {
            f.ToTable("Flights", t =>
            {
                t.HasCheckConstraint("CK_Flights_DifferentAirports", "[OriginCode] <> [DestinationCode]");
                t.HasCheckConstraint("CK_Flights_ArrivesAfterDeparture", "[ArrivesAt] > [DepartsAt]");
            });

            f.Property(x => x.Number).HasMaxLength(8);
            f.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);

            // Restrict em todas: apagar um aeroporto ou uma companhia não pode apagar voos em cascata.
            // (E o SQL Server nem deixaria: duas cascatas para a mesma tabela dão erro.)
            f.HasOne(x => x.Airline).WithMany().HasForeignKey(x => x.AirlineCode).OnDelete(DeleteBehavior.Restrict);
            f.HasOne(x => x.Origin).WithMany().HasForeignKey(x => x.OriginCode).OnDelete(DeleteBehavior.Restrict);
            f.HasOne(x => x.Destination).WithMany().HasForeignKey(x => x.DestinationCode).OnDelete(DeleteBehavior.Restrict);

            f.HasMany(x => x.Fares).WithOne().HasForeignKey(x => x.FlightId);
            f.HasMany(x => x.Seats).WithOne().HasForeignKey(x => x.FlightId);

            // A pesquisa é sempre "de X para Y, entre estas horas": o índice segue essa ordem.
            f.HasIndex(x => new { x.OriginCode, x.DestinationCode, x.DepartsAt });
        });

        modelBuilder.Entity<FlightFare>(f =>
        {
            f.ToTable("FlightFares", t => t.HasCheckConstraint("CK_FlightFares_PricePositive", "[Price] > 0"));
            f.HasKey(x => new { x.FlightId, x.Class });
            f.Property(x => x.Class).HasConversion<string>().HasMaxLength(16);

            // Dinheiro: sempre decimal com precisão definida, nunca float/double.
            f.Property(x => x.Price).HasPrecision(10, 2);
        });

        modelBuilder.Entity<FlightSeat>(s =>
        {
            // Um lugar livre não tem dono; um lugar preso ou vendido tem sempre reserva e passageiro.
            s.ToTable("FlightSeats", t => t.HasCheckConstraint(
                "CK_FlightSeats_OwnerMatchesStatus",
                "([Status] = 'Available' AND [BookingId] IS NULL AND [PassengerId] IS NULL) OR " +
                "([Status] <> 'Available' AND [BookingId] IS NOT NULL AND [PassengerId] IS NOT NULL)"));

            s.HasKey(x => new { x.FlightId, x.SeatNumber });
            s.Property(x => x.SeatNumber).HasMaxLength(4);
            s.Property(x => x.Letter).HasMaxLength(1);
            s.Property(x => x.Class).HasConversion<string>().HasMaxLength(16);
            s.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);

            s.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.NoAction);
            s.HasOne(x => x.Passenger).WithMany().HasForeignKey(x => x.PassengerId).OnDelete(DeleteBehavior.NoAction);

            // Para libertar os lugares de uma reserva expirada: "WHERE BookingId = @id".
            s.HasIndex(x => x.BookingId);

            // Um passageiro não pode ter dois lugares no mesmo voo.
            // Índice filtrado: só se aplica às linhas com passageiro (as livres têm todas NULL).
            s.HasIndex(x => new { x.FlightId, x.PassengerId })
                .IsUnique()
                .HasFilter("[PassengerId] IS NOT NULL");
        });

        modelBuilder.Entity<Booking>(b =>
        {
            b.Property(x => x.Reference).HasMaxLength(6);
            b.HasIndex(x => x.Reference).IsUnique();

            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.ContactEmail).HasMaxLength(254);
            b.Property(x => x.Total).HasPrecision(10, 2);
            b.Property(x => x.Currency).HasMaxLength(3);

            b.HasOne(x => x.OutboundFlight).WithMany().HasForeignKey(x => x.OutboundFlightId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ReturnFlight).WithMany().HasForeignKey(x => x.ReturnFlightId).OnDelete(DeleteBehavior.Restrict);

            b.HasMany(x => x.Passengers).WithOne().HasForeignKey(x => x.BookingId);

            // O job de expiração procura "pendentes com prazo ultrapassado".
            b.HasIndex(x => new { x.Status, x.ExpiresAt });
        });

        modelBuilder.Entity<Payment>(p =>
        {
            p.ToTable("Payments", t => t.HasCheckConstraint("CK_Payments_AmountPositive", "[Amount] > 0"));
            p.Property(x => x.Amount).HasPrecision(10, 2);
            p.Property(x => x.Currency).HasMaxLength(3);
            p.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            p.Property(x => x.CardLast4).HasMaxLength(4);
            p.Property(x => x.ProviderReference).HasMaxLength(40);
            p.Property(x => x.DeclineCode).HasMaxLength(32);

            p.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
            p.HasIndex(x => x.BookingId);
        });

        modelBuilder.Entity<Passenger>(p =>
        {
            p.Property(x => x.FirstName).HasMaxLength(60);
            p.Property(x => x.LastName).HasMaxLength(60);
            p.Property(x => x.DocumentNumber).HasMaxLength(20);
        });
    }
}
