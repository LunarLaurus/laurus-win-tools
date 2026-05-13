using System.IO;
using FluentAssertions;
using WindowsAppCore;
using Xunit;

namespace SoundTracker.Tests;

public class SoundTrackerConfigTests
{
    [Fact]
    public void Load_NoFile_ReturnsCurrentSchemaVersion()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"SoundTrackerTest-{Guid.NewGuid():N}");
        try
        {
            Environment.SetEnvironmentVariable("SOUNDTRACKER_DATA", tempRoot);
            var store = new JsonSettingsStore<SoundTracker.App.SoundTrackerConfig>("SoundTracker");
            var config = store.Load();
            config.SchemaVersion.Should().Be(SoundTracker.App.SoundTrackerConfig.CurrentSchemaVersion);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOUNDTRACKER_DATA", null);
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsSchemaVersion()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"SoundTrackerTest-{Guid.NewGuid():N}");
        try
        {
            Environment.SetEnvironmentVariable("SOUNDTRACKER_DATA", tempRoot);
            var store = new JsonSettingsStore<SoundTracker.App.SoundTrackerConfig>("SoundTracker");
            var config = store.Load();
            store.Save(config);
            var reloaded = store.Load();
            reloaded.SchemaVersion.Should().Be(SoundTracker.App.SoundTrackerConfig.CurrentSchemaVersion);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOUNDTRACKER_DATA", null);
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }
}
