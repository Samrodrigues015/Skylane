namespace Skylane.Tests.Infrastructure;

/// <summary>
/// Relógio controlado pelos testes: permite "avançar o tempo" sem esperar.
/// </summary>
public sealed class TestClock : TimeProvider
{
    private readonly Lock _lock = new();
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return _now;
        }
    }

    public void Advance(TimeSpan by)
    {
        lock (_lock)
        {
            _now = _now.Add(by);
        }
    }
}
