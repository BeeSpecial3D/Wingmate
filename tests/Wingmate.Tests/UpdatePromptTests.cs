using Wingmate.Core;
using Xunit;

namespace Wingmate.Tests
{
    public class UpdatePromptTests
    {
        [Fact]
        public void ShowsOncePerVersionPerSession()
        {
            var g = new UpdatePrompt();
            Assert.True(g.ShouldShow("1.0.3"));
            Assert.False(g.ShouldShow("1.0.3"));
            Assert.False(g.ShouldShow("v1.0.3")); // same version, other spelling
            Assert.True(g.ShouldShow("1.0.4"));  // a newer one is announced again
            Assert.False(g.ShouldShow(""));
            Assert.False(g.ShouldShow(null));
        }

        [Fact]
        public void PreviewFlag()
        {
            Assert.True(UpdatePrompt.IsPreview(new[] { "--preview-update-popup" }));
            Assert.True(UpdatePrompt.IsPreview(new[] { "x", " --PREVIEW-UPDATE-POPUP " }));
            Assert.False(UpdatePrompt.IsPreview(new[] { "--preview" }));
            Assert.False(UpdatePrompt.IsPreview(System.Array.Empty<string>()));
            Assert.False(UpdatePrompt.IsPreview(null));
        }

        [Fact]
        public void Texts()
        {
            Assert.Equal("Wingmate 1.0.3 is available", UpdatePrompt.Title("1.0.3+abc"));
            Assert.Equal("You're on 1.0.2.", UpdatePrompt.Subtitle("v1.0.2"));
            Assert.Equal("Downloading\u2026", UpdatePrompt.Downloading(0));
            Assert.Equal("Downloading\u2026 42%", UpdatePrompt.Downloading(42));
            Assert.Equal("Downloading\u2026 100%", UpdatePrompt.Downloading(250));
        }
    }
}

namespace Wingmate.Tests
{
    public class LauncherAutoMinimizeTests
    {
        /// <summary>The retired "Auto-minimize" value is read and written back unchanged, and nothing else is lost.</summary>
        [Fact]
        public void OldValueRoundTripsWithOtherSettings()
        {
            var json = "{\"SettingsVersion\":7,\"LauncherAutoMinimize\":true,\"Theme\":\"honey\",\"AskBeforeQuit\":false}";
            var s = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json)!;
            s.Migrate();
            var back = System.Text.Json.JsonSerializer.Serialize(s);
            Assert.Contains("\"LauncherAutoMinimize\":true", back);
            Assert.Contains("\"Theme\":\"honey\"", back);
            Assert.Contains("\"AskBeforeQuit\":false", back);
        }
    }
}

namespace Wingmate.Tests
{
    public class LauncherSizeSettingsTests
    {
        [Fact]
        public void OldFile_HasNoSize_AndKeepsEverythingElse()
        {
            var json = "{\"SettingsVersion\":7,\"LauncherLeft\":100,\"LauncherTop\":200,\"Theme\":\"honey\"}";
            var s = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json)!;
            s.Migrate();
            Assert.Null(s.LauncherWidth); Assert.Null(s.LauncherHeight);
            Assert.Equal(100, s.LauncherLeft); Assert.Equal(200, s.LauncherTop); Assert.Equal("honey", s.Theme);
        }

        [Fact]
        public void Size_RoundTrips()
        {
            var s = new AppSettings { LauncherWidth = 420, LauncherHeight = 640 };
            var back = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(s))!;
            Assert.Equal(420, back.LauncherWidth); Assert.Equal(640, back.LauncherHeight);
        }
    }
}
