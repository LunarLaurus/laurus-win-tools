using System.Windows.Forms;
using NetProfileSwitcher.UI;
using WindowsAppCore;

namespace NetProfileSwitcher;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var log = new AppLog("NetProfileSwitcher", Application.ProductVersion);
        UnhandledExceptionWatcher.Install(log, "NetProfileSwitcher");

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm(log));
    }
}
