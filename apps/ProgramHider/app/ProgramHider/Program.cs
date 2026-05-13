using WindowsAppCore;

namespace ProgramHider;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (!SingleInstanceActivation.TryClaim("ProgramHider", dispatchToUi: null, out var activation))
            return;

        var startupOptions = StartupOptions.Parse(args);
        if (startupOptions.DelaySeconds > 0)
            Thread.Sleep(TimeSpan.FromSeconds(startupOptions.DelaySeconds));

        ApplicationConfiguration.Initialize();
        using (activation!)
            Application.Run(new ProgramHiderContext(startupOptions, activation!));
    }
}
