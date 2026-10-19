using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Velopack;
using Velopack.Sources;
using Wingmate.Core;

namespace Wingmate
{
    /// <summary>Background updates from GitHub Releases (public, no token). Checks shortly after start and every 6 h,
    /// downloads quietly, then offers "Restart to update"; otherwise Velopack applies it on the next start.
    /// Only active in the installed app (the loose test exe never updates itself).</summary>
    public sealed class Updater
    {
        private readonly UpdateManager? _mgr;
        private readonly DispatcherTimer _timer = new();
        private bool _busy;

        /// <summary>The downloaded update waiting for a restart, if any.</summary>
        public VelopackAsset? Ready { get; private set; }
        public string ReadyVersion => Ready?.Version?.ToString() ?? "";
        public event Action? ReadyChanged;

        public Updater()
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource(Release.RepoUrl, null, false));
                if (mgr.IsInstalled) _mgr = mgr;
                if (_mgr?.UpdatePendingRestart is { } pending) Ready = pending; // downloaded earlier, not applied yet
            }
            catch (Exception ex) { Log.Write("updater: " + ex.Message); }
        }

        public bool Enabled => _mgr != null;

        public void Start()
        {
            if (_mgr == null) return;
            _timer.Interval = Release.FirstCheckAfter;
            _timer.Tick += async (s, e) => { _timer.Interval = Release.CheckEvery; await CheckAsync(); };
            _timer.Start();
        }

        public async Task CheckAsync()
        {
            if (_mgr == null || _busy) return;
            _busy = true;
            try
            {
                var info = await _mgr.CheckForUpdatesAsync().ConfigureAwait(true);
                if (info == null) return;
                if (Ready != null && Ready.Version == info.TargetFullRelease.Version) return;
                await _mgr.DownloadUpdatesAsync(info).ConfigureAwait(true);
                Ready = info.TargetFullRelease;
                Log.Write("update downloaded: " + ReadyVersion);
                ReadyChanged?.Invoke();
            }
            catch (Exception ex) { Log.Write("update check: " + ex.Message); } // offline, rate limited, no release yet...
            finally { _busy = false; }
        }

        /// <summary>Applies the downloaded update and restarts (the caller saves everything first).</summary>
        public void ApplyAndRestart()
        {
            if (_mgr == null || Ready == null) return;
            try { _mgr.WaitExitThenApplyUpdates(Ready, silent: true, restart: true); }
            catch (Exception ex) { Log.Write("update apply: " + ex.Message); }
        }
    }
}
