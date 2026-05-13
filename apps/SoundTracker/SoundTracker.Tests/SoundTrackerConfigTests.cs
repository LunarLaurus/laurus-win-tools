using FluentAssertions;
using WindowsAppCore;
using Xunit;

namespace SoundTracker.Tests;

public class SoundTrackerConfigTests : IDisposable
{
    private readonly TempAppData _temp = new("SoundTracker");

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Load_NoFile_ReturnsCurrentSchemaVersion()
    {
        var store = new JsonSettingsStore<SoundTracker.App.SoundTrackerConfig>("SoundTracker");
        var config = store.Load();
        config.SchemaVersion.Should().Be(SoundTracker.App.SoundTrackerConfig.CurrentSchemaVersion);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsRunAtStartup()
    {
        var store = new JsonSettingsStore<SoundTracker.App.SoundTrackerConfig>("SoundTracker");
        var config = store.Load();
        config.RunAtStartup = true;
        store.Save(config);
        store.Load().RunAtStartup.Should().BeTrue();
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsSchemaVersion()
    {
        var store = new JsonSettingsStore<SoundTracker.App.SoundTrackerConfig>("SoundTracker");
        var config = store.Load();
        store.Save(config);
        store.Load().SchemaVersion.Should().Be(SoundTracker.App.SoundTrackerConfig.CurrentSchemaVersion);
    }
}
