using System.Windows.Forms;
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

        var startupOptions = StartupOptions.Parse(args);
        if (startupOptions.DelaySeconds > 0)
            Thread.Sleep(TimeSpan.FromSeconds(startupOptions.DelaySeconds));

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (activation!)
            Application.Run(new MainForm(log, activation!));
    }
}
