using System;
using System.IO;
using System.Linq;
using Wingmate.Core;
using Xunit;

public class ReleaseTests
{
    [Fact] public void PackId_IsNotTheDataFolderName()
    {
        Assert.NotEqual("Wingmate", Release.PackId, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Wingmate", Path.GetFileName(AppSettings.Dir));
        var local = Path.GetDirectoryName(AppSettings.Dir)!;
        Assert.True(Release.DataIsSeparateFromInstall(AppSettings.Dir, Release.InstallRoot(local)));
    }

    [Theory]
    [InlineData("/l/Wingmate", "/l/BeeSpecial3D.Wingmate", true)]
    [InlineData("/l/Wingmate", "/l/Wingmate", false)]                  // packId "Wingmate": uninstall would delete the data
    [InlineData("/l/Wingmate", "/l/Wingmate/current", false)]          // program inside the data folder
    [InlineData("/l/BeeSpecial3D.Wingmate/data", "/l/BeeSpecial3D.Wingmate", false)] // data inside the install folder
    [InlineData("/l/Wingmate", "/l/WingmateX", true)]                  // a name prefix is not nesting
    [InlineData("/l/wingmate/", "/l/WINGMATE", false)]                 // Windows paths are case-insensitive
    public void DataVsInstallFolder(string data, string install, bool separate)
        => Assert.Equal(separate, Release.DataIsSeparateFromInstall(data, install));

    [Fact] public void UpdatesComeFromThePublicRepo_OnASlowSchedule()
    {
        Assert.Equal("https://github.com/BeeSpecial3D/Wingmate", Release.RepoUrl);
        Assert.True(Release.CheckEvery >= TimeSpan.FromHours(1)); // GitHub allows 60 unauthenticated requests/hour per IP
        Assert.True(Release.FirstCheckAfter > TimeSpan.Zero);
    }

    /// <summary>Simulates what Velopack does to the disk (install, update = swap "current", uninstall = delete the install root)
    /// next to an existing data folder written by the old loose exe, and checks the data survives and still loads.</summary>
    [Fact] public void InstallUpdateUninstall_LeaveExistingDataUntouched()
    {
        var local = Path.Combine(Path.GetTempPath(), "wm-sim-" + Guid.NewGuid().ToString("N"));
        try
        {
            var data = Path.Combine(local, "Wingmate");
            Directory.CreateDirectory(data);
            // what the loose test exe left behind (older formats included)
            File.WriteAllText(Path.Combine(data, "settings.json"), "{\"SettingsVersion\":4,\"Region\":\"NA_East\",\"Theme\":\"abyss\",\"Opacity\":0.7,\"WarnColor\":\"#A45CFF\"}");
            var book = ChecklistBook.CreateNew();
            book.Current.Sections[0].Items.Add(new ChecklistItem { Text = "My own item" });
            File.WriteAllText(Path.Combine(data, "checklist.json"), System.Text.Json.JsonSerializer.Serialize(book));
            var before = Directory.GetFiles(data).ToDictionary(f => f, File.ReadAllBytes);

            var root = Release.InstallRoot(local);
            Assert.True(Release.DataIsSeparateFromInstall(data, root));
            void Install(string v) { var cur = Path.Combine(root, "current"); if (Directory.Exists(cur)) Directory.Delete(cur, true); Directory.CreateDirectory(cur); File.WriteAllText(Path.Combine(cur, "Wingmate.exe"), v); Directory.CreateDirectory(Path.Combine(root, "packages")); }
            Install("1.0.0");
            Install("1.0.1");              // update replaces the program folder
            Directory.Delete(root, true);  // uninstall removes the whole install root

            foreach (var (f, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(f));
            var s = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path.Combine(data, "settings.json")))!;
            s.Migrate();
            Assert.Equal(0.7, s.WindowOpacity); Assert.Equal("#A45CFF", s.ActiveWarnColor);
            var back = System.Text.Json.JsonSerializer.Deserialize<ChecklistBook>(File.ReadAllText(Path.Combine(data, "checklist.json")))!;
            Assert.Contains(back.Characters.SelectMany(c => c.Sections).SelectMany(x => x.Items), i => i.Text == "My own item");
        }
        finally { try { Directory.Delete(local, true); } catch { } }
    }
}
