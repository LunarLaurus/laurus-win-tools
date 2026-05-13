namespace WindowsAppCore;

/// <summary>
/// Enforces single-instance startup and fires ActivationRequested when a second
/// invocation attempts to run. Works across UAC integrity levels via
/// CrossIntegrityMutex and ActivationSignal.
/// </summary>
public sealed class SingleInstanceActivation : IDisposable
{
    private readonly CrossIntegrityMutex _mutex;
    private readonly ActivationSignal _signal;
    private readonly Action<Action>? _dispatchToUi;
    private volatile bool _running = true;

    private SingleInstanceActivation(
        CrossIntegrityMutex mutex,
        ActivationSignal signal,
        Action<Action>? dispatchToUi)
    {
        _mutex = mutex;
        _signal = signal;
        _dispatchToUi = dispatchToUi;

        var thread = new Thread(ListenLoop)
        {
            IsBackground = true,
            Name = "SingleInstanceActivation.Listener",
        };
        thread.Start();
    }

    /// <summary>
    /// Attempts to claim the single-instance slot for <paramref name="appName"/>.
    /// Returns true and an owned handle when this is the first instance.
    /// Returns false (and signals the existing instance) when another is running.
    /// </summary>
    /// <param name="appName">Unique per-app identifier (no backslashes).</param>
    /// <param name="dispatchToUi">
    /// Delegate used to marshal ActivationRequested onto the UI thread.
    /// Pass e.g. <c>action => control.BeginInvoke(action)</c>.
    /// If null the event fires on the background listener thread.
    /// </param>
    public static bool TryClaim(
        string appName,
        Action<Action>? dispatchToUi,
        out SingleInstanceActivation? handle)
    {
        var mutexName  = $@"Local\{appName}.SingleInstance";
        var signalName = $@"Local\{appName}.Activate";

        var mutex  = CrossIntegrityMutex.CreateOrOpen(mutexName);
        var signal = ActivationSignal.CreateOrOpen(signalName);

        if (!mutex.CreatedNew)
        {
            signal.TrySignal();
            mutex.Dispose();
            signal.Dispose();
            handle = null;
            return false;
        }

        handle = new SingleInstanceActivation(mutex, signal, dispatchToUi);
        return true;
    }

    /// <summary>
    /// Fires on the UI thread (or the listener thread if no dispatcher was provided)
    /// each time a second instance signals this one to come to the foreground.
    /// </summary>
    public event EventHandler? ActivationRequested;

    private void ListenLoop()
    {
        while (_running)
        {
            if (_signal.WaitOne(500))
            {
                if (!_running) return;
                void Fire() => ActivationRequested?.Invoke(this, EventArgs.Empty);
                if (_dispatchToUi is not null)
                    _dispatchToUi(Fire);
                else
                    Fire();
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        // Dispose the signal first so WaitOne returns and the loop can exit.
        _signal.Dispose();
        _mutex.Dispose();
    }
}
