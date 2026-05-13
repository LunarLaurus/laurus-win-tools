namespace WindowsAppCore;

public sealed class AppIdentity
{
    public string AppName { get; }
    public string Version { get; }

    public AppIdentity(string appName, string version)
    {
        AppName = appName;
        Version = version;
    }
}
