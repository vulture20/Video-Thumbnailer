using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VideoThumbnailer.Gui.Input;

/// <summary>
/// Hands files given to a second start of the program (double-click on a preset or "Open with", #33) to the window
/// that is already open. A start without files still opens a window of its own. One local named pipe per user and
/// settings folder (portable copies have their own).
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly string _name;

    private SingleInstance(string name) => _name = name;

    public static string DefaultPipeName()
    {
        var key = Environment.UserName + "|" + Core.AppPaths.ConfigDir;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..12];
        return "VideoThumbnailer-" + hash;
    }

    /// <summary>On Unix the pipe is a socket file; a new server would replace one that is still in use.</summary>
    private static string? UnixSocketFile(string name) => OperatingSystem.IsWindows() ? null : Path.Combine(Path.GetTempPath(), "CoreFxPipe_" + name);

    /// <summary>Files and folders become full paths, because the running program has another current folder.</summary>
    internal static string[] Absolute(IEnumerable<string> args) => args.Select(a =>
        Core.Input.VideoSource.IsUrl(a) || a.StartsWith('-') || !(File.Exists(a) || Directory.Exists(a)) ? a : Path.GetFullPath(a)).ToArray();

    /// <summary>Sends the arguments to a running instance. True when one took them.</summary>
    public static bool TrySend(string name, IReadOnlyList<string> args, int timeoutMs = 250)
    {
        if (UnixSocketFile(name) is { } file && !File.Exists(file)) return false;
        try
        {
            using var client = new NamedPipeClientStream(".", name, PipeDirection.Out);
            client.Connect(timeoutMs);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(Absolute(args));
            client.Write(bytes);
            client.Flush();
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>
    /// Listens for arguments of later starts; <paramref name="received"/> runs on a worker thread. Null when another
    /// instance already listens (then this one simply runs on its own).
    /// </summary>
    public static SingleInstance? Listen(string name, Action<string[]> received)
    {
        // Do not take the pipe away from an instance that is still running (Unix replaces the socket file).
        if (UnixSocketFile(name) is { } file && File.Exists(file) && TrySend(name, [])) return null;
        NamedPipeServerStream server;
        try { server = Create(name); }
        catch (IOException) { return null; }
        var instance = new SingleInstance(name);
        _ = instance.LoopAsync(server, received);
        return instance;
    }

    private static NamedPipeServerStream Create(string name) =>
        new(name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    private async Task LoopAsync(NamedPipeServerStream server, Action<string[]> received)
    {
        var ct = _cts.Token;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                using var ms = new MemoryStream();
                await server.CopyToAsync(ms, ct).ConfigureAwait(false);
                var args = ms.Length == 0 ? [] : JsonSerializer.Deserialize<string[]>(ms.ToArray()) ?? [];
                if (args.Length > 0) received(args);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) when (ex is IOException or JsonException) { /* broken message: wait for the next one */ }
            finally
            {
                await server.DisposeAsync().ConfigureAwait(false);
            }
            if (ct.IsCancellationRequested) break;
            try { server = Create(_name); }
            catch (IOException) { return; }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
