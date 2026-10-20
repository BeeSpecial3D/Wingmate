using System.Collections.Generic;
using System.Text.Json;
using Wingmate.Core;
using Xunit;

namespace Wingmate.Tests
{
    /// <summary>1.0.5: the Custom colors editor moved into its own window. Only position fields were added; the colours
    /// themselves stay in the same settings fields and carry over untouched.</summary>
    public class CustomColorsWindowTests
    {
        [Fact]
        public void OldFile_KeepsCustomColors_AndHasNoWindowPosition()
        {
            var json = "{\"SettingsVersion\":7,\"Theme\":\"custom\",\"CustomBase\":\"honey\",\"CustomColors\":{\"windowBg\":\"#112233\",\"title\":\"#AABBCC\"},"
                     + "\"ThemeWarnColor\":{\"custom\":\"#FF8800\"}}";
            var s = JsonSerializer.Deserialize<AppSettings>(json)!;
            s.Migrate();
            Assert.Null(s.CustomColorsLeft); Assert.Null(s.CustomColorsTop);
            Assert.Equal("#112233", s.CustomColors!["windowBg"]);
            Assert.Equal("#AABBCC", s.CustomColors["title"]);
            Assert.Equal("honey", s.CustomBase);
            Assert.Equal("#FF8800", s.WarnColorFor("custom"));
            var back = JsonSerializer.Serialize(s);
            Assert.Contains("\"windowBg\":\"#112233\"", back);
            Assert.Contains("\"title\":\"#AABBCC\"", back);
            Assert.DoesNotContain("HasCustomTheme", back);
        }

        /// <summary>The warning colour left Settings (it is edited with the Custom colors now), but a colour already saved
        /// for a preset theme keeps applying on that theme, unchanged, after load / migrate / save.</summary>
        [Fact]
        public void SavedWarningColor_OnPresetTheme_CarriesOver()
        {
            var json = "{\"SettingsVersion\":7,\"Theme\":\"honey\",\"ThemeWarnColor\":{\"honey\":\"#00CCFF\",\"custom\":\"#FF8800\"}}";
            var s = JsonSerializer.Deserialize<AppSettings>(json)!;
            s.Migrate();
            Assert.Equal("#00CCFF", s.ActiveWarnColor);
            Assert.Equal("#FF8800", s.WarnColorFor("custom"));
            var back = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s))!;
            back.Migrate();
            Assert.Equal("#00CCFF", back.WarnColorFor("honey"));
            Assert.Equal("#FF8800", back.WarnColorFor("custom"));
        }

        [Fact]
        public void Position_RoundTrips()
        {
            var s = new AppSettings { CustomColorsLeft = 300, CustomColorsTop = 120 };
            var back = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s))!;
            Assert.Equal(300, back.CustomColorsLeft); Assert.Equal(120, back.CustomColorsTop);
            var p = new AppSettings { ColorPickerLeft = 410, ColorPickerTop = 90 };
            var pb = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(p))!;
            Assert.Equal(410, pb.ColorPickerLeft); Assert.Equal(90, pb.ColorPickerTop);
            Assert.Null(JsonSerializer.Deserialize<AppSettings>("{\"SettingsVersion\":7}")!.ColorPickerLeft);
        }

        [Fact]
        public void HasCustomTheme_OnlyOnceOneExists()
        {
            Assert.False(new AppSettings { Theme = "honey" }.HasCustomTheme);
            Assert.False(new AppSettings { Theme = "honey", CustomColors = new Dictionary<string, string>() }.HasCustomTheme);
            Assert.True(new AppSettings { Theme = "custom" }.HasCustomTheme);
            Assert.True(new AppSettings { Theme = "honey", CustomColors = new Dictionary<string, string> { ["windowBg"] = "#112233" } }.HasCustomTheme);
        }
    }
}
