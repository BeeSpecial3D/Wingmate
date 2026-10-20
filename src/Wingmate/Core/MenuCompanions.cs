using System.Collections.Generic;
using System.Linq;

namespace Wingmate.Core
{
    /// <summary>Settings and the Custom colors window close when the launcher (the Wingmate menu) hides, and come back when it
    /// shows again. This remembers which ones were open at that moment. If the user opens or closes one of them themselves
    /// while the launcher is hidden, that choice wins and it is no longer reopened. They still count as open for the next
    /// start while they are waiting to come back.</summary>
    public sealed class MenuCompanions
    {
        private readonly List<string> _pending = new();

        public IReadOnlyList<string> Pending => _pending;

        /// <summary>The launcher hid; these companion windows were open and are being closed with it.</summary>
        public void Hidden(IEnumerable<string> openNow)
        {
            _pending.Clear();
            foreach (var n in openNow) if (!_pending.Contains(n)) _pending.Add(n);
        }

        /// <summary>The user opened or closed this window themselves (not with the launcher): never reopen it automatically.</summary>
        public void Touched(string name) => _pending.Remove(name);

        /// <summary>The launcher is showing again: the windows to reopen (each once).</summary>
        public List<string> Shown()
        {
            var r = _pending.ToList();
            _pending.Clear();
            return r;
        }

        /// <summary>Open windows plus the ones waiting for the launcher, for "open at next start".</summary>
        public List<string> WithPending(IEnumerable<string> open)
        {
            var r = open.ToList();
            foreach (var n in _pending) if (!r.Contains(n)) r.Add(n);
            return r;
        }
    }
}
