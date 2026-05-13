using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class UiDispatcherTests
{
    [WindowsFact]
    public void IsUiThread_FalseOnBackgroundThread()
    {
        // UiDispatcher must be created on a UI thread (STA with a handle).
        // We create one on a dedicated STA thread and then check IsUiThread from
        // a background ThreadPool thread — it must return false.
        UiDispatcher? dispatcher = null;
        var ready = new ManualResetEventSlim(false);

        var sta = new Thread(() =>
        {
            dispatcher = new UiDispatcher();
            ready.Set();
            // Keep the STA thread alive until the test finishes
            Thread.Sleep(500);
            dispatcher.Dispose();
        })
        { IsBackground = true };
        sta.SetApartmentState(ApartmentState.STA);
        sta.Start();

        ready.Wait(TimeSpan.FromSeconds(5));
        dispatcher.Should().NotBeNull();
        dispatcher!.IsUiThread.Should().BeFalse();
    }

    [WindowsFact]
    public void Post_ExecutesActionOnCreatingThread()
    {
        int? capturedId = null;
        int? staId = null;
        var done = new ManualResetEventSlim(false);
        UiDispatcher? dispatcher = null;
        var ready = new ManualResetEventSlim(false);

        var sta = new Thread(() =>
        {
            staId = Environment.CurrentManagedThreadId;
            dispatcher = new UiDispatcher();
            ready.Set();
            // Run message loop briefly so BeginInvoke can execute
            var end = DateTime.UtcNow.AddSeconds(3);
            while (!done.IsSet && DateTime.UtcNow < end)
                Application.DoEvents();
        })
        { IsBackground = true };
        sta.SetApartmentState(ApartmentState.STA);
        sta.Start();

        ready.Wait(TimeSpan.FromSeconds(5));
        dispatcher!.Post(() =>
        {
            capturedId = Environment.CurrentManagedThreadId;
            done.Set();
        });

        done.Wait(TimeSpan.FromSeconds(5));
        capturedId.Should().Be(staId);
        dispatcher.Dispose();
    }
}
