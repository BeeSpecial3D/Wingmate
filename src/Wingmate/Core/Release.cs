using System;
using System.IO;

namespace Wingmate.Core
{
    /// <summary>Release/update constants and the data-folder safety rule (pure, so it is unit tested).</summary>
    public static class Release
    {
        /// <summary>Velopack package id. Velopack installs to %LOCALAPPDATA%\&lt;PackId&gt; and deletes that folder on uninstall,
        /// so it must never be "Wingmate": that is the user's data folder (settings, checklists).</summary>
        public const string PackId = "BeeSpecial3D.Wingmate";
        public const string RepoUrl = "https://github.com/BeeSpecial3D/Wingmate";
        /// <summary>First update check this long after start (keeps startup light), then every <see cref="CheckEvery"/>.</summary>
        public static readonly TimeSpan FirstCheckAfter = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan CheckEvery = TimeSpan.FromHours(6);

        /// <summary>Velopack's install root for a given LocalAppData.</summary>
        public static string InstallRoot(string localAppData) => Path.Combine(localAppData, PackId);

        /// <summary>True when the data folder and the install folder are separate (neither inside the other), so installing,
        /// updating or uninstalling can't touch the user's files.</summary>
        public static bool DataIsSeparateFromInstall(string dataDir, string installDir)
        {
            string a = Norm(dataDir), b = Norm(installDir);
            return !a.StartsWith(b, StringComparison.OrdinalIgnoreCase) && !b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
        }

        private static string Norm(string p) => Path.GetFullPath(p).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
    }
}
