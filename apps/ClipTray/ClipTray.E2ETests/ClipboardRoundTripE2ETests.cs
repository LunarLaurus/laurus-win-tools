using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using FluentAssertions;
using Xunit;

namespace ClipTray.E2ETests;

public class ClipboardRoundTripE2ETests
{
    private static void RunOnSta(Action action)
    {
        Exception? captured = null;
        var t = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join(TimeSpan.FromSeconds(10));
        if (captured is not null) throw captured;
    }

    [WindowsFact]
    public void ListenAndCapture_SetText_FiresUpdate()
    {
        RunOnSta(() =>
        {
            using var listener = new ClipboardListener();
            int updates = 0;
            listener.ClipboardChanged += (_, _) => Interlocked.Increment(ref updates);

            Clipboard.SetText("cliptray-e2e-marker " + Guid.NewGuid().ToString("N"));
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (updates == 0 && DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(25);
            }

            updates.Should().BeGreaterThan(0);
        });
    }

    [WindowsFact]
    public void ImageRoundTrip_PreservesPixelSample()
    {
        RunOnSta(() =>
        {
            using var bmp = new Bitmap(4, 4);
            bmp.SetPixel(1, 2, Color.FromArgb(255, 100, 200, 50));
            Clipboard.SetImage(bmp);

            using var roundTrip = (Bitmap)Clipboard.GetImage()!;
            var px = roundTrip.GetPixel(1, 2);

            px.R.Should().BeInRange((byte)98,  (byte)102);
            px.G.Should().BeInRange((byte)198, (byte)202);
            px.B.Should().BeInRange((byte)48,  (byte)52);
        });
    }
}
