namespace IndexContainment.Data;

public sealed class SimpleRateLimiter
{
    private readonly TimeSpan _delay;
    private DateTime _next = DateTime.MinValue;

    public SimpleRateLimiter(TimeSpan delay) => _delay = delay;

    public async Task WaitAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (now < _next)
        {
            var wait = _next - now;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, ct);
        }
        _next = DateTime.UtcNow + _delay;
    }
}