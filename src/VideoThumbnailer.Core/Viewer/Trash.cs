using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VideoThumbnailer.Core.Viewer;

/// <summary>
/// Moves files to the recycle bin / trash (#39). Never deletes for good: if the trash cannot take the file, an
/// exception is thrown and the file stays where it is.
/// </summary>
public static partial class Trash
{
    public static void MoveToTrash(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException(path, path);
        path = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows()) Windows(path);
        else if (OperatingSystem.IsMacOS()) Mac(path);
        else Freedesktop(path);
        if (File.Exists(path)) throw new IOException("the file is still there: " + path);
    }

    // ---------------------------------------------------------------- Windows: SHFileOperation with FOF_ALLOWUNDO

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperationW(ref ShFileOpStruct op);

    [SupportedOSPlatform("windows")]
    private static void Windows(string path)
    {
        const uint FO_DELETE = 3;
        const ushort FOF_SILENT = 0x4, FOF_NOCONFIRMATION = 0x10, FOF_ALLOWUNDO = 0x40, FOF_NOERRORUI = 0x400, FOF_WANTNUKEWARNING = 0x4000;
        var op = new ShFileOpStruct
        {
            wFunc = FO_DELETE,
            pFrom = path + "\0\0",
            // FOF_WANTNUKEWARNING: if the file cannot go to the recycle bin (e.g. network drive), Windows asks before
            // deleting it for good instead of doing it silently; answering "No" leaves it in place.
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI | FOF_WANTNUKEWARNING,
        };
        var r = SHFileOperationW(ref op);
        if (r != 0 || op.fAnyOperationsAborted) throw new IOException("SHFileOperation failed (" + r.ToString("X", CultureInfo.InvariantCulture) + ")");
    }

    // ---------------------------------------------------------------- macOS: Finder

    private static void Mac(string path)
    {
        var psi = new ProcessStartInfo("osascript") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true };
        foreach (var a in new[] { "-e", "on run argv", "-e", "tell application \"Finder\" to delete (POSIX file (item 1 of argv) as alias)", "-e", "end run", path })
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi) ?? throw new IOException("osascript");
        var err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new IOException(err.Trim());
    }

    // ---------------------------------------------------------------- Linux/BSD: gio, else the freedesktop.org trash

    private static void Freedesktop(string path)
    {
        // gio knows the trash folders on other drives ($topdir/.Trash-$uid) and avoids copying big videos home.
        try
        {
            var psi = new ProcessStartInfo("gio") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true };
            psi.ArgumentList.Add("trash");
            psi.ArgumentList.Add("--");
            psi.ArgumentList.Add(path);
            using var p = Process.Start(psi);
            if (p is not null)
            {
                p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode == 0 && !File.Exists(path)) return;
            }
        }
        catch (System.ComponentModel.Win32Exception) { /* no gio */ }
        MoveToHomeTrash(path, HomeTrashFolder());
    }

    /// <summary>$XDG_DATA_HOME/Trash, by default ~/.local/share/Trash.</summary>
    internal static string HomeTrashFolder()
    {
        var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrEmpty(data)) data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return Path.Combine(data, "Trash");
    }

    /// <summary>
    /// Trash specification 1.0: the file goes to <c>files/</c>, a <c>.trashinfo</c> with the original path and the
    /// time goes to <c>info/</c>, both under a free name. Returns the path in the trash.
    /// </summary>
    internal static string MoveToHomeTrash(string path, string trashDir)
    {
        var files = Path.Combine(trashDir, "files");
        var info = Path.Combine(trashDir, "info");
        Directory.CreateDirectory(files);
        Directory.CreateDirectory(info);
        var name = Path.GetFileName(path);
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        for (var n = 1; ; n++)
        {
            var candidate = n == 1 ? name : $"{stem}.{n}{ext}";
            var infoPath = Path.Combine(info, candidate + ".trashinfo");
            FileStream fs;
            try { fs = new FileStream(infoPath, FileMode.CreateNew, FileAccess.Write); }
            catch (IOException) when (File.Exists(infoPath)) { continue; }
            using (fs)
            using (var w = new StreamWriter(fs))
            {
                w.Write("[Trash Info]\nPath=" + EscapePath(path) + "\nDeletionDate="
                        + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "\n");
            }
            var target = Path.Combine(files, candidate);
            if (File.Exists(target) || Directory.Exists(target)) { File.Delete(infoPath); continue; }
            try { File.Move(path, target); }
            catch { File.Delete(infoPath); throw; }
            return target;
        }
    }

    /// <summary>Percent-encodes everything but unreserved characters and "/", as the spec asks.</summary>
    internal static string EscapePath(string path)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(path))
        {
            var c = (char)b;
            if (b < 0x80 && (char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_' or '.' or '~')) sb.Append(c);
            else sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}
