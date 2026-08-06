namespace PhilSLA.ExamPlatform.Proctor.Tests.Attendance;

internal sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private readonly object _sync = new();
    private readonly List<TestTimer> _timers = [];
    private DateTimeOffset _utcNow = RequireUtc(utcNow);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = RequireUtc(utcNow);

    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        TestTimer[] timers;
        lock (_sync)
        {
            _utcNow += elapsed;
            timers = _timers.ToArray();
        }

        foreach (var timer in timers)
        {
            timer.Advance(elapsed);
        }
    }

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new TestTimer(callback, state, dueTime, period);
        lock (_sync)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    private static DateTimeOffset RequireUtc(DateTimeOffset value) =>
        value.Offset == TimeSpan.Zero
            ? value
            : throw new ArgumentException("Timestamp must use a UTC offset.", nameof(value));

    private sealed class TestTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period) : ITimer
    {
        private readonly object _sync = new();
        private TimeSpan _remaining = dueTime;
        private TimeSpan _period = period;
        private bool _disposed;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return false;
                }

                _remaining = dueTime;
                _period = period;
                return true;
            }
        }

        public void Advance(TimeSpan elapsed)
        {
            var fireCount = 0;
            lock (_sync)
            {
                if (_disposed || _remaining == Timeout.InfiniteTimeSpan)
                {
                    return;
                }

                _remaining -= elapsed;
                while (_remaining <= TimeSpan.Zero && !_disposed)
                {
                    fireCount++;
                    if (_period == Timeout.InfiniteTimeSpan)
                    {
                        _remaining = Timeout.InfiniteTimeSpan;
                        break;
                    }

                    _remaining += _period;
                }
            }

            for (var index = 0; index < fireCount; index++)
            {
                callback(state);
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _disposed = true;
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
