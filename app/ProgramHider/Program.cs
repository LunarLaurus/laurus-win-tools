namespace ProgramHider;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var startupOptions = StartupOptions.Parse(args);
        if (startupOptions.DelaySeconds > 0)
        {
            Thread.Sleep(TimeSpan.FromSeconds(startupOptions.DelaySeconds));
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new ProgramHiderContext(startupOptions));
    }
}
