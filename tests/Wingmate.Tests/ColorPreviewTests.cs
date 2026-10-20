using System.Collections.Generic;
using System.Text.Json;
using Wingmate.Core;
using Xunit;

namespace Wingmate.Tests
{
    /// <summary>The colour picker's live preview lives in memory only: saving mid-preview (or quitting with the picker open)
    /// writes the original colours, Cancel brings them back exactly, and OK is saved through the normal path.</summary>
    public class ColorPreviewTests
    {
        private static AppSettings Sample() => new()
        {
            Theme = "custom", CustomBase = "honey",
            CustomColors = new Dictionary<string, string> { ["windowBg"] = "#1E140A", ["title"] = "#FFC53D" },
            ThemeWarnColor = new Dictionary<string, string> { ["custom"] = "#F0A030" },
        };

        [Fact]
        public void Preview_ShowsLive_ButIsNeverSaved()
        {
            var s = Sample();
            s.SetPreview("title", "#00FF00");
            s.SetPreview(AppSettings.PreviewWarnKey, "#3D7BFF");
            Assert.Equal("#00FF00", s.EffectiveCustomColors()["title"]);
            Assert.Equal("#1E140A", s.EffectiveCustomColors()["windowBg"]);
            Assert.Equal("#3D7BFF", s.ActiveWarnColor);
            Assert.Equal("#3D7BFF", s.WarnColorFor("custom"));

            var json = JsonSerializer.Serialize(s); // what any save mid-preview (or on quit) writes
            Assert.DoesNotContain("#00FF00", json);
            Assert.DoesNotContain("#3D7BFF", json);
            Assert.DoesNotContain("Preview", json);
            var back = JsonSerializer.Deserialize<AppSettings>(json)!;
            Assert.Equal("#FFC53D", back.CustomColors["title"]);
            Assert.Equal("#F0A030", back.WarnColorFor("custom"));
            Assert.False(back.HasPreview);
        }

        [Fact]
        public void Cancel_RestoresExactly()
        {
            var s = Sample();
            var before = JsonSerializer.Serialize(s);
            s.SetPreview("title", "#123456"); s.SetPreview("windowBg", "#654321"); s.SetPreview(AppSettings.PreviewWarnKey, "#FFFFFF");
            s.ClearPreview();
            Assert.False(s.HasPreview);
            Assert.Equal(before, JsonSerializer.Serialize(s));
            Assert.Equal("#FFC53D", s.EffectiveCustomColors()["title"]);
            Assert.Equal("#F0A030", s.ActiveWarnColor);
        }

        [Fact]
        public void Ok_SavesThroughNormalPath()
        {
            var s = Sample();
            s.SetPreview("title", "#00FF00");
            s.ClearPreview();              // the picker closes (preview ends) ...
            s.SetCustomColor("title", "#00ff00"); // ... then OK saves the picked colour as before
            Assert.Equal("#00FF00", JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s))!.CustomColors["title"]);
        }

        [Fact]
        public void Preview_IgnoresJunk_AndOnlyTouchesCustomWarning()
        {
            var s = Sample();
            s.SetPreview("title", "nope"); s.SetPreview("notAGroup", "#00FF00");
            Assert.False(s.HasPreview);
            s.SetPreview(AppSettings.PreviewWarnKey, "#3D7BFF");
            Assert.NotEqual("#3D7BFF", s.WarnColorFor("honey")); // preset themes never see it
        }
    }
}
