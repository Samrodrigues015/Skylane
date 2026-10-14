using Bilheteira.Api.Events;
using Bilheteira.Api.Reservations;
using Bilheteira.Api.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Bilheteira.Api.Data;

public sealed class BilheteiraDbContext(DbContextOptions<BilheteiraDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(e =>
        {
            // Última linha de defesa: mesmo que o código tenha um bug,
            // a base de dados recusa vender acima da lotação.
            e.ToTable("Events", t => t.HasCheckConstraint(
                "CK_Events_Reserved_WithinCapacity",
                "[Reserved] >= 0 AND [Reserved] <= [Capacity]"));

            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Venue).HasMaxLength(120);
            e.Ignore(x => x.Available);
            e.HasIndex(x => x.StartsAt);
        });

        modelBuilder.Entity<Reservation>(r =>
        {
            r.ToTable("Reservations", t => t.HasCheckConstraint(
                "CK_Reservations_Quantity_Positive",
                "[Quantity] > 0"));

            r.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);

            r.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict);

            r.HasMany(x => x.Tickets)
                .WithOne(t => t.Reservation)
                .HasForeignKey(t => t.ReservationId);

            // O job de expiração procura "pendentes com prazo ultrapassado".
            r.HasIndex(x => new { x.Status, x.ExpiresAt });
        });

        modelBuilder.Entity<Ticket>(t =>
        {
            t.Property(x => x.Code).HasMaxLength(16);
            t.HasIndex(x => x.Code).IsUnique();
        });
    }
}
