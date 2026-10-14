using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MsSql;

namespace Bilheteira.Tests.Infrastructure;

/// <summary>
/// Arranca a API em memória contra um SQL Server real, num contentor Docker
/// criado só para os testes e destruído no fim.
///
/// Porquê um SQL Server real e não o provider InMemory do EF Core?
/// Porque o que estamos a testar (UPDATEs atómicos, bloqueios, check constraints)
/// é comportamento da base de dados — o InMemory não o reproduz.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public TestClock Clock { get; } = new();

    public Task InitializeAsync() => _sqlServer.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _sqlServer.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = new SqlConnectionStringBuilder(_sqlServer.GetConnectionString())
        {
            InitialCatalog = "BilheteiraTests"
        }.ConnectionString;

        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("SeedDemoData", "false");

        // Os testes chamam o ReservationExpirer diretamente, para controlar quando corre.
        builder.UseSetting("Reservations:ExpiryJobEnabled", "false");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
