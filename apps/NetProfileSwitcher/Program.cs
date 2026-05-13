using System.Windows.Forms;
using NetProfileSwitcher.UI;
using WindowsAppCore;

namespace NetProfileSwitcher;

static class Program
{
    [STAThread]
    static void Main()
    {
        if (!SingleInstanceActivation.TryClaim("NetProfileSwitcher", dispatchToUi: null, out var activation))
            return;

        using var log = new AppLog("NetProfileSwitcher", Application.ProductVersion);
        UnhandledExceptionWatcher.Install(log, "NetProfileSwitcher");

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (activation!)
            Application.Run(new MainForm(log, activation!));
    }
}
