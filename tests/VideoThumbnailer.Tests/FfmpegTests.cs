using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using VideoThumbnailer.Core.Ffmpeg;

namespace VideoThumbnailer.Tests;

public class FfmpegTests
{
    private const string Checksums = """
        1111111111111111111111111111111111111111111111111111111111111111  ffmpeg-master-latest-win64-gpl.zip
        2222222222222222222222222222222222222222222222222222222222222222  ffmpeg-n8.1-latest-win64-gpl-8.1.zip
        3333333333333333333333333333333333333333333333333333333333333333  ffmpeg-n9.0-latest-win64-gpl-9.0.zip
        4444444444444444444444444444444444444444444444444444444444444444  ffmpeg-n9.0-latest-win64-lgpl-9.0.zip
        5555555555555555555555555555555555555555555555555555555555555555  ffmpeg-n9.0-latest-win64-gpl-shared-9.0.zip
        6666666666666666666666666666666666666666666666666666666666666666  ffmpeg-n9.0-latest-linux64-gpl-9.0.tar.xz
        7777777777777777777777777777777777777777777777777777777777777777  ffmpeg-n10.0-latest-linuxarm64-gpl-10.0.tar.xz
        not a checksum line
        """;

    [Fact]
    public void Parses_sha256sum_format()
    {
        var c = FfmpegSources.ParseChecksums(Checksums);
        Assert.Equal(7, c.Count);
        Assert.Equal(new string('3', 64), c["ffmpeg-n9.0-latest-win64-gpl-9.0.zip"]);
    }

    [Theory]
    [InlineData("win64", "ffmpeg-n9.0-latest-win64-gpl-shared-9.0.zip", "9.0", ArchiveKind.Zip)]
    [InlineData("linux64", "ffmpeg-n9.0-latest-linux64-gpl-9.0.tar.xz", "9.0", ArchiveKind.TarXz)]
    [InlineData("linuxarm64", "ffmpeg-n10.0-latest-linuxarm64-gpl-10.0.tar.xz", "10.0", ArchiveKind.TarXz)]
    public void BtbN_picks_newest_release_branch_static_on_linux_shared_on_windows(string target, string file, string version, ArchiveKind kind)
    {
        var r = new BtbNSource(target).SelectRelease(FfmpegSources.ParseChecksums(Checksums))!;
        Assert.Equal(version, r.Version);
        var a = Assert.Single(r.Assets);
        Assert.Equal(file, a.FileName);
        Assert.Equal(kind, a.Kind);
        Assert.Equal(BtbNSource.BaseUrl + file, a.Url.ToString());
        Assert.Equal(target.StartsWith("win") ? [".dll"] : null, a.Libraries);
    }

    [Theory]
    [InlineData("win-x64", ".dll")]
    [InlineData("linux-x64", null)]
    [InlineData("linux-arm64", null)]
    [InlineData("osx-x64", null)]
    [InlineData("osx-arm64", null)]
    public void Embedded_pins_cover_every_platform_but_windows_arm64(string rid, string? library)
    {
        var pin = FfmpegPins.For(rid);
        Assert.NotNull(pin);
        var r = new PinnedSource(pin).ToRelease();
        Assert.NotEmpty(r.Version);
        Assert.Equal(["ffmpeg", "ffprobe"], r.Assets.SelectMany(a => a.Binaries).Order());
        Assert.All(r.Assets, a =>
        {
            Assert.Matches("^[0-9a-f]{64}$", a.Sha256);
            Assert.Equal("https", a.Url.Scheme);
            Assert.DoesNotContain("latest", a.Url.AbsolutePath); // fixed file, not a moving "latest" link
            Assert.Equal(library is null ? null : [library], a.Libraries);
        });
        Assert.Equal(rid.StartsWith("osx"), r.MacTeamId is { Length: 10 });
        Assert.Null(FfmpegPins.For("win-arm64"));
    }

    [Fact]
    public void Default_source_is_pinned_latest_is_not()
    {
        var pinned = FfmpegSources.ForCurrentPlatform();
        Assert.NotNull(pinned);
        Assert.Equal(FfmpegSources.CurrentRid() != "win-arm64", pinned.IsPinned);
        Assert.False(FfmpegSources.ForCurrentPlatform(latest: true)!.IsPinned);
    }

    [Fact]
    public async Task Pinned_source_needs_no_network()
    {
        var pin = FfmpegPins.Parse("""
            { "platforms": { "x": { "source": "s", "homepage": "h", "version": "1.0", "macTeamId": "ABCDE12345",
              "assets": [ { "fileName": "a.tar.xz", "url": "https://example.invalid/a.tar.xz", "sha256": "AB", "kind": "TarXz", "binaries": ["ffmpeg"] } ] } } }
            """).Platforms["x"];
        using var http = new HttpClient(new StaticHandler([]));
        var r = await new PinnedSource(pin).GetLatestAsync(http, CancellationToken.None);
        Assert.Equal("ab", r.Assets[0].Sha256);
        Assert.Equal(ArchiveKind.TarXz, r.Assets[0].Kind);
        Assert.Null(r.Assets[0].Libraries);
        Assert.Equal("ABCDE12345", r.MacTeamId);
    }

    [Theory]
    [InlineData("Executable=/x/ffmpeg\nIdentifier=ffmpeg\nTeamIdentifier=KU3N25YGLU\nSealed Resources=none", "KU3N25YGLU")]
    [InlineData("Identifier=ffmpeg\nTeamIdentifier=not set", null)]
    [InlineData("", null)]
    public void Parses_team_id_from_codesign(string output, string? expected) =>
        Assert.Equal(expected, FfmpegInstaller.ParseTeamId(output));

    [Fact]
    public void Extract_takes_shared_libraries_from_bin_but_nothing_else()
    {
        var dir = TestUtil.TempDir();
        var zip = Path.Combine(dir, "s.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            foreach (var name in new[]
                     {
                         "f/bin/" + FfmpegLocator.ExeName("ffmpeg"), "f/bin/" + FfmpegLocator.ExeName("ffprobe"), "f/bin/" + FfmpegLocator.ExeName("ffplay"),
                         "f/bin/avcodec-62.dll", "f/bin/avutil-60.dll", "f/lib/avcodec.dll.a", "f/doc/x.dll",
                     })
                using (var w = new StreamWriter(z.CreateEntry(name).Open())) w.Write(name);
        }
        var dest = Directory.CreateDirectory(Path.Combine(dir, "out")).FullName;
        var asset = new FfmpegAsset("s.zip", new Uri("https://example.invalid/s.zip"), "", ArchiveKind.Zip, ["ffmpeg", "ffprobe"], [".dll"]);
        var files = FfmpegInstaller.Extract(zip, asset, dest).Select(Path.GetFileName).Order().ToList();
        Assert.Equal(new[] { "avcodec-62.dll", "avutil-60.dll", FfmpegLocator.ExeName("ffmpeg"), FfmpegLocator.ExeName("ffprobe") }.Order(), files);

        // A shared build without its libraries is rejected.
        var noLibs = Path.Combine(dir, "n.zip");
        using (var z = ZipFile.Open(noLibs, ZipArchiveMode.Create))
            foreach (var name in new[] { "f/bin/" + FfmpegLocator.ExeName("ffmpeg"), "f/bin/" + FfmpegLocator.ExeName("ffprobe") })
                using (var w = new StreamWriter(z.CreateEntry(name).Open())) w.Write(name);
        Assert.Throws<InvalidDataException>(() => FfmpegInstaller.Extract(noLibs, asset, Directory.CreateDirectory(Path.Combine(dir, "o2")).FullName));
    }

    [Fact]
    public void BtbN_returns_null_for_unknown_target()
    {
        Assert.Null(new BtbNSource("win32").SelectRelease(FfmpegSources.ParseChecksums(Checksums)));
    }

    [Fact]
    public async Task Verify_rejects_wrong_checksum()
    {
        var f = Path.Combine(TestUtil.TempDir(), "a.bin");
        await File.WriteAllTextAsync(f, "hello", TestContext.Current.CancellationToken);
        var good = Convert.ToHexStringLower(SHA256.HashData("hello"u8));
        await FfmpegInstaller.VerifyAsync(f, good, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => FfmpegInstaller.VerifyAsync(f, new string('0', 64), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Extract_finds_binaries_in_nested_zip_folders()
    {
        var dir = TestUtil.TempDir();
        var zip = Path.Combine(dir, "a.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            foreach (var name in new[] { "ffmpeg-x/bin/" + FfmpegLocator.ExeName("ffmpeg"), "ffmpeg-x/bin/" + FfmpegLocator.ExeName("ffprobe"), "ffmpeg-x/doc/readme.txt" })
                using (var w = new StreamWriter(z.CreateEntry(name).Open())) w.Write(name);
        }
        var dest = Directory.CreateDirectory(Path.Combine(dir, "out")).FullName;
        var asset = new FfmpegAsset("a.zip", new Uri("https://example.invalid/a.zip"), "", ArchiveKind.Zip, ["ffmpeg", "ffprobe"]);
        var files = FfmpegInstaller.Extract(zip, asset, dest);
        Assert.Equal(2, files.Count);
        Assert.False(File.Exists(Path.Combine(dest, "readme.txt")));
    }

    [Fact]
    public void Extract_reads_tar_xz()
    {
        if (OperatingSystem.IsWindows()) Assert.Skip("needs tar with xz");
        var dir = TestUtil.TempDir();
        var src = Directory.CreateDirectory(Path.Combine(dir, "ffmpeg-n9.0", "bin")).FullName;
        File.WriteAllText(Path.Combine(src, "ffmpeg"), "a");
        File.WriteAllText(Path.Combine(src, "ffprobe"), "b");
        var archive = Path.Combine(dir, "x.tar.xz");
        if (!TestUtil.Run("tar", "-cJf", archive, "-C", dir, "ffmpeg-n9.0")) Assert.Skip("tar/xz not available");
        var dest = Directory.CreateDirectory(Path.Combine(dir, "out")).FullName;
        var asset = new FfmpegAsset("x.tar.xz", new Uri("https://example.invalid/x"), "", ArchiveKind.TarXz, ["ffmpeg", "ffprobe"]);
        Assert.Equal(2, FfmpegInstaller.Extract(archive, asset, dest).Count);
        Assert.Equal("b", File.ReadAllText(Path.Combine(dest, "ffprobe")));
    }

    [Fact]
    public async Task Install_downloads_verifies_and_swaps_in_atomically()
    {
        if (OperatingSystem.IsWindows()) Assert.Skip("uses shell scripts as fake ffmpeg");
        var ct = TestContext.Current.CancellationToken;
        var zipBytes = FakeFfmpegZip("6.0");
        var sha = Convert.ToHexStringLower(SHA256.HashData(zipBytes));
        var root = Path.Combine(TestUtil.TempDir(), "ffmpeg");
        var http = new HttpClient(new StaticHandler(zipBytes));

        var installer = new FfmpegInstaller(new FakeSource(sha, "6.0"), http, root);
        var tools = await installer.InstallAsync(null, null, ct);
        Assert.Equal(FfmpegOrigin.Managed, tools.Origin);
        Assert.Equal("ffmpeg version 6.0", await FfmpegLocator.GetVersionAsync(tools.FfmpegPath, ct));
        Assert.Equal("6.0", installer.ReadManifest()!.Version);
        Assert.False((await installer.CheckForUpdateAsync(ct)).UpdateAvailable);

        // A newer build is detected via its checksum and replaces the old one.
        var zip2 = FakeFfmpegZip("7.0");
        var sha2 = Convert.ToHexStringLower(SHA256.HashData(zip2));
        var updater = new FfmpegInstaller(new FakeSource(sha2, "7.0"), new HttpClient(new StaticHandler(zip2)), root);
        Assert.True((await updater.CheckForUpdateAsync(ct)).UpdateAvailable);
        await updater.InstallAsync(null, null, ct);
        Assert.Equal("ffmpeg version 7.0", await FfmpegLocator.GetVersionAsync(Path.Combine(root, "bin", "ffmpeg"), ct));
        Assert.Equal(["bin", "manifest.json"], Directory.EnumerateFileSystemEntries(root).Select(p => Path.GetFileName(p)!).Order().ToArray());

        // A corrupted download must not replace the working installation.
        var broken = new FfmpegInstaller(new FakeSource(new string('0', 64), "8.0"), new HttpClient(new StaticHandler(zip2)), root);
        await Assert.ThrowsAsync<InvalidDataException>(() => broken.InstallAsync(null, null, ct));
        Assert.Equal("7.0", updater.ReadManifest()!.Version);
        Assert.NotNull(FfmpegLocator.FromDirectory(Path.Combine(root, "bin"), FfmpegOrigin.Managed));
    }

    private static byte[] FakeFfmpegZip(string version)
    {
        using var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var tool in new[] { "ffmpeg", "ffprobe" })
                using (var w = new StreamWriter(z.CreateEntry($"pkg/bin/{tool}").Open()))
                    w.Write($"#!/bin/sh\necho \"{tool} version {version}\"\n");
        }
        return ms.ToArray();
    }

    private sealed class FakeSource(string sha, string version) : IFfmpegSource
    {
        public string Name => "fake";
        public string Homepage => "https://example.invalid";
        public Task<FfmpegRelease> GetLatestAsync(HttpClient http, CancellationToken ct) =>
            Task.FromResult(new FfmpegRelease(Name, version,
                [new FfmpegAsset("pkg.zip", new Uri("https://example.invalid/pkg.zip"), sha, ArchiveKind.Zip, ["ffmpeg", "ffprobe"])]));
    }

    private sealed class StaticHandler(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
    }
}
