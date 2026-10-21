using System;
using System.IO;
using System.Text;

namespace Wingmate.Core
{
    /// <summary>Crash-safe saving for the user's files (settings.json, checklist.json). A save writes a temp file next to the
    /// real one, flushes it to disk, then swaps it in, keeping the previous version as "&lt;name&gt;.bak". A crash or power cut
    /// mid-save therefore leaves either the old file or the new one, never half a file. Loading falls back to the .bak when
    /// the main file is missing or unreadable.</summary>
    public static class SafeFile
    {
        public static string BackupPath(string path) => path + ".bak";
        public static string TempPath(string path) => path + ".tmp";

        /// <summary>Writes <paramref name="text"/> to <paramref name="path"/> atomically; the old file becomes the .bak.
        /// Throws on failure (the existing file and .bak are left as they were). <paramref name="keepBackup"/> false: no .bak (caches).</summary>
        public static void WriteAtomic(string path, string text, bool keepBackup = true)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
            Directory.CreateDirectory(dir);
            var tmp = TempPath(path);
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = new UTF8Encoding(false).GetBytes(text);
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true); // on disk before it replaces anything
            }
            if (!keepBackup || !File.Exists(path)) { File.Move(tmp, path, true); return; }
            try { File.Replace(tmp, path, BackupPath(path), ignoreMetadataErrors: true); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is PlatformNotSupportedException)
            {
                // some folders (synced or network drives) refuse File.Replace: keep a copy, then move over it
                File.Copy(path, BackupPath(path), true);
                File.Move(tmp, path, true);
            }
        }

        public enum Source { None, Main, Backup }

        /// <summary>Reads the main file, or the .bak when the main file is missing or <paramref name="parse"/> rejects it
        /// (returns null or throws). When the .bak is used, an unreadable main file is kept as "&lt;name&gt;.broken-&lt;time&gt;"
        /// and the .bak is copied back into place, so nothing is lost and the next save starts from good data.</summary>
        public static (T? Value, Source From) Read<T>(string path, Func<string, T?> parse, Action<string>? log = null) where T : class
        {
            bool mainExists = File.Exists(path);
            if (mainExists)
            {
                var v = TryParse(path, parse, log);
                if (v != null) return (v, Source.Main);
            }
            var bak = BackupPath(path);
            if (!File.Exists(bak)) return (null, Source.None);
            var b = TryParse(bak, parse, log);
            if (b == null) return (null, Source.None);
            log?.Invoke($"{Path.GetFileName(path)} {(mainExists ? "unreadable" : "missing")}: using {Path.GetFileName(bak)}");
            try
            {
                if (mainExists) File.Copy(path, BrokenPath(path), false);
                File.Copy(bak, path, true);
            }
            catch (Exception ex) { log?.Invoke("restore from backup: " + ex.Message); }
            return (b, Source.Backup);
        }

        public static string BrokenPath(string path) => path + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");

        private static T? TryParse<T>(string file, Func<string, T?> parse, Action<string>? log) where T : class
        {
            try { return parse(File.ReadAllText(file, Encoding.UTF8)); }
            catch (Exception ex) { log?.Invoke($"{Path.GetFileName(file)}: {ex.Message}"); return null; }
        }
    }
}
