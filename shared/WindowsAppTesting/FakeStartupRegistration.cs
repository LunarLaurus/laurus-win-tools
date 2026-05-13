using WindowsAppCore;

namespace WindowsAppTesting;

/// <summary>
/// In-memory IStartupRegistration for unit tests. Always reports success and tracks
/// registration state without touching the registry or task scheduler.
/// </summary>
public sealed class FakeStartupRegistration : IStartupRegistration
{
    public bool IsRegistered { get; private set; }

    public FakeStartupRegistration(bool initiallyRegistered = false)
    {
        IsRegistered = initiallyRegistered;
    }

    public StartupRegistrationResult Register()
    {
        IsRegistered = true;
        return StartupRegistrationResult.Success;
    }

    public StartupRegistrationResult Unregister()
    {
        IsRegistered = false;
        return StartupRegistrationResult.Success;
    }
}
