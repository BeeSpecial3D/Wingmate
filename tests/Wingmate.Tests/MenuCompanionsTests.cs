using System.Collections.Generic;
using System.Text.Json;
using Wingmate.Core;
using Xunit;

namespace Wingmate.Tests
{
    public class MenuCompanionsTests
    {
        [Fact]
        public void OpenWhenHidden_ReopenOnce()
        {
            var m = new MenuCompanions();
            m.Hidden(new[] { "settings", "customcolors" });
            Assert.Equal(new[] { "settings", "customcolors" }, m.Shown());
            Assert.Empty(m.Shown()); // only the first show after hiding
        }

        [Fact]
        public void ClosedByUserBeforeHiding_StaysClosed()
        {
            var m = new MenuCompanions();
            m.Hidden(new[] { "settings" }); // custom colors was already closed by the user
            Assert.Equal(new[] { "settings" }, m.Shown());
        }

        [Fact]
        public void UserChoiceWhileHidden_Wins()
        {
            var m = new MenuCompanions();
            m.Hidden(new[] { "settings", "customcolors" });
            m.Touched("settings"); // opened (and maybe closed) from the tray meanwhile
            Assert.Equal(new[] { "customcolors" }, m.Shown());
        }

        [Fact]
        public void NothingOpen_NothingReopens()
        {
            var m = new MenuCompanions();
            m.Hidden(new string[0]);
            Assert.Empty(m.Shown());
            m.Hidden(new[] { "settings" });
            m.Hidden(new string[0]); // hid again with nothing open: the old set is gone
            Assert.Empty(m.Shown());
        }

        [Fact]
        public void Waiting_CountsAsOpen_ForNextStart()
        {
            var m = new MenuCompanions();
            m.Hidden(new[] { "settings", "customcolors" });
            Assert.Equal(new[] { "alerts", "settings", "customcolors" }, m.WithPending(new[] { "alerts" }));
            Assert.Equal(new[] { "settings", "customcolors" }, m.WithPending(new[] { "settings" }));
            m.Shown();
            Assert.Equal(new[] { "alerts" }, m.WithPending(new[] { "alerts" }));
        }

        [Fact]
        public void CustomColors_IsAKnownStartupWindow_OldFilesUnaffected()
        {
            var s = new AppSettings { OpenAtExit = new List<string> { "launcher", "settings", "customcolors", "junk" } };
            Assert.Equal(new[] { "launcher", "settings", "customcolors" }, s.StartupWindows);
            var old = JsonSerializer.Deserialize<AppSettings>("{\"SettingsVersion\":7,\"OpenAtExit\":[\"launcher\",\"alerts\",\"checklist\",\"settings\"]}")!;
            Assert.Equal(new[] { "launcher", "alerts", "checklist", "settings" }, old.StartupWindows);
        }
    }
}
