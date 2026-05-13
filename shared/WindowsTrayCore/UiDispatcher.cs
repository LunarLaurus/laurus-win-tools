namespace WindowsTrayCore;

/// <summary>
/// STA-bound dispatcher. Must be constructed on the UI thread.
/// Replaces ad-hoc <c>Control.BeginInvoke</c> / <c>SynchronizationContext.Post</c> patterns.
/// </summary>
public sealed class UiDispatcher : IDisposable
{
    private readonly Control _anchor;

    /// <summary>
    /// Binds the dispatcher to the calling thread. Call this from the UI thread
    /// before the message loop starts.
    /// </summary>
    public UiDispatcher()
    {
        _anchor = new Control();
        _ = _anchor.Handle;   // force HWND creation on the current thread
    }

    /// <summary>Fire-and-forget: queues <paramref name="action"/> on the UI thread.</summary>
    public void Post(Action action) => _anchor.BeginInvoke(action);

    /// <summary>Blocking: executes <paramref name="action"/> on the UI thread synchronously.</summary>
    public void Send(Action action) => _anchor.Invoke(action);

    /// <summary>True when called from the thread that created this dispatcher.</summary>
    public bool IsUiThread => !_anchor.InvokeRequired;

    public void Dispose() => _anchor.Dispose();
}
