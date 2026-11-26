using System.Diagnostics;

namespace VideoThumbnailer.Core.Util;

public sealed record ProcessResult(int ExitCode, byte[] StdOut, string StdErr);

public static class ProcessRunner
{
    /// <summary>Runs a process, captures stdout as bytes and stderr as text. Kills the process on cancellation.</summary>
    public static async Task<ProcessResult> RunAsync(string fileName, IEnumerable<string> args, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        process.Start();
        try
        {
            using var stdout = new MemoryStream();
            var outTask = process.StandardOutput.BaseStream.CopyToAsync(stdout, ct);
            var errTask = process.StandardError.ReadToEndAsync(ct);
            await Task.WhenAll(outTask, errTask).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return new ProcessResult(process.ExitCode, stdout.ToArray(), await errTask.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
    }

    private static void TryKill(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { /* ignore */ }
    }
}
