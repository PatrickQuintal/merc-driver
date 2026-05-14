namespace Merc.Mapper;

public sealed class MapperRuntime : IDisposable
{
    private readonly Action<string> _log;
    private readonly object _sync = new();
    private MercInputMapper? _mapper;
    private CancellationTokenSource? _cancellation;
    private MapperOptions _options = new();
    private bool _disposed;

    public MapperRuntime(Action<string> log)
    {
        _log = log;
    }

    public event Action<string?>? Stopped;

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _mapper?.IsRunning == true;
            }
        }
    }

    public MapperOptions Options
    {
        get
        {
            lock (_sync)
            {
                return _options;
            }
        }
    }

    public void Start(MapperOptions options)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_mapper is not null)
            {
                if (_mapper.IsRunning)
                {
                    return;
                }

                _mapper.Dispose();
                _cancellation?.Dispose();
                _mapper = null;
                _cancellation = null;
            }

            _options = options;
            var cancellation = new CancellationTokenSource();
            var mapper = new MercInputMapper(_log, options);
            mapper.Stopped += reason => OnMapperStopped(mapper, reason);

            try
            {
                mapper.Start(cancellation.Token);
            }
            catch
            {
                cancellation.Cancel();
                mapper.Dispose();
                cancellation.Dispose();
                throw;
            }

            _cancellation = cancellation;
            _mapper = mapper;
        }
    }

    public void Stop()
    {
        MercInputMapper? mapper;
        CancellationTokenSource? cancellation;

        lock (_sync)
        {
            mapper = _mapper;
            cancellation = _cancellation;
            _mapper = null;
            _cancellation = null;
        }

        cancellation?.Cancel();
        mapper?.Dispose();
        cancellation?.Dispose();
    }

    public void Restart(MapperOptions options)
    {
        Stop();
        Start(options);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MapperRuntime));
        }
    }

    private void OnMapperStopped(MercInputMapper mapper, string? reason)
    {
        CancellationTokenSource? cancellation = null;
        lock (_sync)
        {
            if (!ReferenceEquals(_mapper, mapper))
            {
                return;
            }

            _mapper = null;
            cancellation = _cancellation;
            _cancellation = null;
        }

        cancellation?.Dispose();
        Stopped?.Invoke(reason);
    }
}
