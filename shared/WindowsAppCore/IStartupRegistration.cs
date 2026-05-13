namespace WindowsAppCore;

public enum StartupRegistrationResult { Success, Failed, UserCancelled }

public interface IStartupRegistration
{
    bool IsRegistered { get; }
    StartupRegistrationResult Register();
    StartupRegistrationResult Unregister();
}
