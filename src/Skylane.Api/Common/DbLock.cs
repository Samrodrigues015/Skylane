using Skylane.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Skylane.Api.Common;

/// <summary>
/// Lock com nome no SQL Server (sp_getapplock), para serializar uma operação sobre um recurso
/// (ex.: "pagar a reserva K7M2Q9") sem bloquear linhas de tabelas.
///
/// O dono do lock é a sessão (a ligação), e não uma transação, para o código lá dentro
/// poder abrir as suas próprias transações. Por isso a ligação fica aberta até ao Dispose.
/// </summary>
public sealed class DbLock : IAsyncDisposable
{
    private readonly SkylaneDbContext _db;
    private readonly string _resource;

    private DbLock(SkylaneDbContext db, string resource)
    {
        _db = db;
        _resource = resource;
    }

    public static async Task<DbLock> AcquireAsync(SkylaneDbContext db, string resource, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);

        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 30000;
                IF @result < 0 THROW 50000, 'Não foi possível obter o lock.', 1;
                """, ct);
        }
        catch
        {
            await db.Database.CloseConnectionAsync();
            throw;
        }

        return new DbLock(db, resource);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC sp_releaseapplock @Resource = {_resource}, @LockOwner = 'Session'");
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }
}
