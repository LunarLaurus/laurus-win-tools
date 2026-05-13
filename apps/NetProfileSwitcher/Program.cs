using System.Windows.Forms;
using NetProfileSwitcher.Models;
using NetProfileSwitcher.Services;
using NetProfileSwitcher.UI;
using WindowsAppCore;
using StartupOptions = WindowsAppCore.StartupOptions;

namespace NetProfileSwitcher;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (!SingleInstanceActivation.TryClaim("NetProfileSwitcher", dispatchToUi: null, out var activation))
            return;

        using var log = new AppLog("NetProfileSwitcher", Application.ProductVersion);
        UnhandledExceptionWatcher.Install(log, "NetProfileSwitcher");

        var cfg = ConfigStore.Load();

        var startupOptions = StartupOptions.Parse(args);
        int delaySeconds = startupOptions.DelaySeconds > 0
            ? startupOptions.DelaySeconds
            : cfg.StartupDelaySeconds;
        if (delaySeconds > 0)
            Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (activation!)
            Application.Run(new MainForm(log, activation!));
    }
}
