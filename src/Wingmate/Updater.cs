using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Velopack;
using Velopack.Sources;
using Wingmate.Core;

namespace Wingmate
{
    /// <summary>Updates from GitHub Releases (public, no token). Checks shortly after start and every 6 h. A new version is
    /// only announced (<see cref="Available"/>); nothing is downloaded until the user picks "Update now" (pop-up, launcher
    /// link or tray). Only active in the installed app (the loose test exe never updates itself). Updating only replaces
    /// the program folder; the data folder (lists, settings) is separate and never touched.</summary>
    public sealed class Updater
    {
        private readonly UpdateManager? _mgr;
        private readonly DispatcherTimer _timer = new();
        private bool _checking;

        /// <summary>A newer version found online, not downloaded yet.</summary>
        public UpdateInfo? Available { get; private set; }
        public string AvailableVersion => Available?.TargetFullRelease?.Version?.ToString() ?? "";
        /// <summary>The downloaded update waiting for a restart, if any.</summary>
        public VelopackAsset? Ready { get; private set; }
        public string ReadyVersion => Ready?.Version?.ToString() ?? "";
        /// <summary>Version for the launcher/tray: the downloaded one, else the available one.</summary>
        public string OfferVersion => Ready != null ? ReadyVersion : AvailableVersion;
        public bool HasOffer => Ready != null || Available != null;
        public bool Downloading { get; private set; }
        /// <summary>Raised (on the UI thread) when <see cref="Available"/> or <see cref="Ready"/> changes.</summary>
        public event Action? Changed;

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

        /// <summary>The running version ("1.0.2"), from Velopack when installed, else the exe's own version.</summary>
        public string CurrentVersion
        {
            get
            {
                try { if (_mgr?.CurrentVersion is { } v) return UpdatePrompt.Clean(v.ToString()); } catch { }
                var a = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return a == null ? "" : $"{a.Major}.{a.Minor}.{a.Build}";
            }
        }

        public void Start()
        {
            if (_mgr == null) return;
            _timer.Interval = Release.FirstCheckAfter;
            _timer.Tick += async (s, e) => { _timer.Interval = Release.CheckEvery; await CheckAsync(); };
            _timer.Start();
        }

        /// <summary>Looks for a newer version; never downloads.</summary>
        public async Task CheckAsync()
        {
            if (_mgr == null || _checking || Downloading) return;
            _checking = true;
            try
            {
                var info = await _mgr.CheckForUpdatesAsync().ConfigureAwait(true);
                if (info?.TargetFullRelease == null) return;
                if (Ready != null && Ready.Version == info.TargetFullRelease.Version) return; // already downloaded
                if (Available?.TargetFullRelease?.Version == info.TargetFullRelease.Version) return; // already known
                Available = info;
                Log.Write("update available: " + AvailableVersion);
                Changed?.Invoke();
            }
            catch (Exception ex) { Log.Write("update check: " + ex.Message); } // offline, rate limited, no release yet...
            finally { _checking = false; }
        }

        /// <summary>Downloads the available update (progress 0-100 on the UI thread). Returns true when it is ready to apply.
        /// Throws on network errors so the caller can offer a retry.</summary>
        public async Task<bool> DownloadAsync(Action<int>? progress, CancellationToken cancel = default)
        {
            if (Ready != null) return true;
            if (_mgr == null || Available == null) return false;
            if (Downloading) return false;
            Downloading = true;
            var ui = Dispatcher.CurrentDispatcher;
            try
            {
                var info = Available;
                await _mgr.DownloadUpdatesAsync(info, p => ui.BeginInvoke(new Action(() => progress?.Invoke(p))), cancel).ConfigureAwait(true);
                Ready = info.TargetFullRelease;
                Available = null;
                Log.Write("update downloaded: " + ReadyVersion);
                Changed?.Invoke();
                return true;
            }
            catch (Exception ex) { Log.Write("update download: " + ex.Message); throw; }
            finally { Downloading = false; }
        }

        /// <summary>Applies the downloaded update and restarts (the caller saves everything first).</summary>
        public bool ApplyAndRestart()
        {
            if (_mgr == null || Ready == null) return false;
            try { _mgr.WaitExitThenApplyUpdates(Ready, silent: true, restart: true); return true; }
            catch (Exception ex) { Log.Write("update apply: " + ex.Message); return false; }
        }
    }
}
