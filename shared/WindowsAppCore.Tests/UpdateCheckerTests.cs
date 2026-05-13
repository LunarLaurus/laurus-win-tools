using System.Net;
using FluentAssertions;
using WindowsAppCore;
using WindowsAppTesting;
using Xunit;

namespace WindowsAppCore.Tests;

public sealed class UpdateCheckerTests
{
    private const string Owner = "LunarLaurus";
    private const string Repo = "laurus-win-tools";

    private static UpdateChecker Create(FakeHttpMessageHandler handler, string currentVersion)
    {
        var client = new HttpClient(handler);
        return new UpdateChecker(client, currentVersion, Owner, Repo);
    }

    [Fact]
    public async Task CheckAsync_WhenNewerVersionAvailable_ReturnsUpdateAvailable()
    {
        const string json = """{"tag_name":"v2.0.0","html_url":"https://github.com/LunarLaurus/laurus-win-tools/releases/tag/v2.0.0"}""";
        var checker = Create(FakeHttpMessageHandler.WithJson(json), "1.0.0");

        var result = await checker.CheckAsync();

        result.IsUpdateAvailable.Should().BeTrue();
        result.LatestVersion.Should().Be("2.0.0");
        result.ReleaseUrl.Should().Be("https://github.com/LunarLaurus/laurus-win-tools/releases/tag/v2.0.0");
    }

    [Fact]
    public async Task CheckAsync_WhenSameVersion_ReturnsNoUpdate()
    {
        const string json = """{"tag_name":"v1.0.0","html_url":"https://github.com/LunarLaurus/laurus-win-tools/releases/tag/v1.0.0"}""";
        var checker = Create(FakeHttpMessageHandler.WithJson(json), "1.0.0");

        var result = await checker.CheckAsync();

        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_WhenCurrentVersionIsNewer_ReturnsNoUpdate()
    {
        const string json = """{"tag_name":"v0.9.0","html_url":"https://github.com/LunarLaurus/laurus-win-tools/releases/tag/v0.9.0"}""";
        var checker = Create(FakeHttpMessageHandler.WithJson(json), "1.0.0");

        var result = await checker.CheckAsync();

        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_WhenResponseMalformed_ReturnsNoUpdate()
    {
        var checker = Create(FakeHttpMessageHandler.WithJson("{\"not_a_tag\":\"x\"}"), "1.0.0");

        var result = await checker.CheckAsync();

        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_WhenResponseIsNotJson_ReturnsNoUpdate()
    {
        var checker = Create(FakeHttpMessageHandler.WithJson("not json at all"), "1.0.0");

        var result = await checker.CheckAsync();

        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_WhenServerReturnsNotFound_ReturnsNoUpdate()
    {
        var checker = Create(FakeHttpMessageHandler.WithJson("{}", HttpStatusCode.NotFound), "1.0.0");

        var result = await checker.CheckAsync();

        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_WhenNetworkFails_ReturnsNoUpdate()
    {
        var checker = Create(FakeHttpMessageHandler.ThatThrows(new HttpRequestException("network error")), "1.0.0");

        var result = await checker.CheckAsync();

        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_CallsCorrectGitHubEndpoint()
    {
        const string json = """{"tag_name":"v1.0.0","html_url":"https://github.com/LunarLaurus/laurus-win-tools/releases/tag/v1.0.0"}""";
        var handler = FakeHttpMessageHandler.WithJson(json);
        var checker = Create(handler, "1.0.0");

        await checker.CheckAsync();

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].RequestUri!.ToString()
            .Should().Be($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");
    }

    [Fact]
    public async Task CheckAsync_SendsRequiredUserAgentHeader()
    {
        const string json = """{"tag_name":"v1.0.0","html_url":"https://example.com"}""";
        var handler = FakeHttpMessageHandler.WithJson(json);
        var checker = Create(handler, "1.0.0");

        await checker.CheckAsync();

        var ua = handler.Requests[0].Headers.UserAgent.ToString();
        ua.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CheckAsync_TagWithoutVPrefix_ParsedCorrectly()
    {
        const string json = """{"tag_name":"2.0.0","html_url":"https://example.com"}""";
        var checker = Create(FakeHttpMessageHandler.WithJson(json), "1.0.0");

        var result = await checker.CheckAsync();

        result.IsUpdateAvailable.Should().BeTrue();
        result.LatestVersion.Should().Be("2.0.0");
    }

    [Fact]
    public async Task CheckAsync_MinorVersionBump_DetectedAsUpdate()
    {
        const string json = """{"tag_name":"v1.1.0","html_url":"https://example.com"}""";
        var checker = Create(FakeHttpMessageHandler.WithJson(json), "1.0.5");

        var result = await checker.CheckAsync();

        result.IsUpdateAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_CurrentVersionWithBuildMetadata_ParsedAndCompared()
    {
        // GitVersion-stamped AssemblyInformationalVersion looks like "1.0.0+<sha>".
        // System.Version.TryParse rejects the +suffix — UpdateChecker must strip it.
        const string json = """{"tag_name":"v1.0.1","html_url":"https://example.com"}""";
        var checker = Create(FakeHttpMessageHandler.WithJson(json), "1.0.0+358b82bb8c350dc9c03951f52e1169600829123c");

        var result = await checker.CheckAsync();

        result.IsUpdateAvailable.Should().BeTrue();
        result.LatestVersion.Should().Be("1.0.1");
    }

    [Fact]
    public async Task CheckAsync_CurrentVersionWithPrereleaseSuffix_ParsedAndCompared()
    {
        const string json = """{"tag_name":"v1.0.0","html_url":"https://example.com"}""";
        var checker = Create(FakeHttpMessageHandler.WithJson(json), "1.0.0-beta.1");

        var result = await checker.CheckAsync();

        // 1.0.0-beta.1 should parse to 1.0.0 (core) — same as remote, so no update.
        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_RemoteTagWithBuildMetadata_ParsedAndCompared()
    {
        const string json = """{"tag_name":"v2.0.0+def456","html_url":"https://example.com"}""";
        var checker = Create(FakeHttpMessageHandler.WithJson(json), "1.0.0");

        var result = await checker.CheckAsync();

        result.IsUpdateAvailable.Should().BeTrue();
        result.LatestVersion.Should().Be("2.0.0+def456");
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("1.0.0+sha", "1.0.0")]
    [InlineData("1.0.0-beta.1", "1.0.0")]
    [InlineData("1.0.0-beta.1+sha", "1.0.0")]
    [InlineData("2.3.4", "2.3.4")]
    [InlineData("", null)]
    [InlineData("not-a-version", null)]
    [InlineData("v1.0.0", null)]
    public void ParseSemver_HandlesSuffixesAndEdgeCases(string input, string? expected)
    {
        var parsed = UpdateChecker.ParseSemver(input);
        if (expected is null)
            parsed.Should().BeNull();
        else
            parsed!.ToString().Should().Be(expected);
    }
}
