using System;
using System.IO;
using System.Linq;
using Wingmate.Core;
using Xunit;

public class SafeFileTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "wm-safe-" + Guid.NewGuid().ToString("N"));
    public SafeFileTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }
    string P(string name) => Path.Combine(_dir, name);
    static string? Parse(string t) => t.StartsWith("ok:") ? t : throw new FormatException("bad");

    [Fact] public void Write_CreatesFile_KeepsPreviousAsBak_LeavesNoTemp()
    {
        var p = P("a.json");
        SafeFile.WriteAtomic(p, "ok:1");
        Assert.Equal("ok:1", File.ReadAllText(p));
        Assert.False(File.Exists(SafeFile.BackupPath(p)));
        SafeFile.WriteAtomic(p, "ok:2");
        Assert.Equal("ok:2", File.ReadAllText(p));
        Assert.Equal("ok:1", File.ReadAllText(SafeFile.BackupPath(p)));
        Assert.False(File.Exists(SafeFile.TempPath(p)));
    }

    [Fact] public void Write_WithoutBackup_MakesNoBak()
    {
        var p = P("cache.json");
        SafeFile.WriteAtomic(p, "ok:1", keepBackup: false);
        SafeFile.WriteAtomic(p, "ok:2", keepBackup: false);
        Assert.Equal("ok:2", File.ReadAllText(p));
        Assert.False(File.Exists(SafeFile.BackupPath(p)));
    }

    [Fact] public void Read_UsesMain_WhenGood()
    {
        var p = P("b.json");
        SafeFile.WriteAtomic(p, "ok:1"); SafeFile.WriteAtomic(p, "ok:2");
        var (v, from) = SafeFile.Read(p, Parse);
        Assert.Equal("ok:2", v); Assert.Equal(SafeFile.Source.Main, from);
    }

    [Fact] public void Read_CorruptMain_FallsBackToBak_KeepsBrokenCopy_RestoresMain()
    {
        var p = P("c.json");
        SafeFile.WriteAtomic(p, "ok:good"); SafeFile.WriteAtomic(p, "ok:newer");
        File.WriteAllText(p, "{half a fi"); // torn write
        var (v, from) = SafeFile.Read(p, Parse);
        Assert.Equal("ok:good", v); Assert.Equal(SafeFile.Source.Backup, from);
        Assert.Equal("ok:good", File.ReadAllText(p)); // put back in place
        var broken = Directory.GetFiles(_dir, "c.json.broken-*").Single();
        Assert.Equal("{half a fi", File.ReadAllText(broken)); // nothing thrown away
    }

    [Fact] public void Read_MissingMain_UsesBak()
    {
        var p = P("d.json");
        File.WriteAllText(SafeFile.BackupPath(p), "ok:bak");
        var (v, from) = SafeFile.Read(p, Parse);
        Assert.Equal("ok:bak", v); Assert.Equal(SafeFile.Source.Backup, from);
        Assert.True(File.Exists(p));
    }

    [Fact] public void Read_NothingUsable_ReturnsNone_AndLeavesFilesAlone()
    {
        var p = P("e.json");
        Assert.Equal(SafeFile.Source.None, SafeFile.Read(p, Parse).From);
        File.WriteAllText(p, "bad"); File.WriteAllText(SafeFile.BackupPath(p), "bad too");
        var (v, from) = SafeFile.Read(p, Parse);
        Assert.Null(v); Assert.Equal(SafeFile.Source.None, from);
        Assert.Equal("bad", File.ReadAllText(p));
    }

    // ---- settings.json
    [Fact] public void Settings_SaveThenLoad_RoundTrips_AndKeepsBak()
    {
        var p = P("settings.json");
        var s = new AppSettings { Region = "EU", Theme = "honey", WarnMinutes = 7, SettingsVersion = 7 };
        s.SaveTo(p);
        s.WarnMinutes = 9; s.SaveTo(p);
        Assert.True(File.Exists(SafeFile.BackupPath(p)));
        var l = AppSettings.Load(p);
        Assert.Equal("EU", l.Region); Assert.Equal("honey", l.Theme); Assert.Equal(9, l.WarnMinutes);
    }

    [Fact] public void Settings_CorruptFile_LoadsTheBak_NotDefaults()
    {
        var p = P("settings.json");
        new AppSettings { Region = "NA_West", Theme = "midnight", WarnMinutes = 3, SettingsVersion = 7 }.SaveTo(p);
        new AppSettings { Region = "NA_West", Theme = "midnight", WarnMinutes = 4, SettingsVersion = 7 }.SaveTo(p);
        File.WriteAllText(p, "{\"Region\": \"NA_");  // cut off mid-save
        var l = AppSettings.Load(p);
        Assert.Equal("NA_West", l.Region); Assert.Equal("midnight", l.Theme); Assert.Equal(3, l.WarnMinutes);
        Assert.Single(Directory.GetFiles(_dir, "settings.json.broken-*"));
    }

    [Fact] public void Settings_CorruptWithoutBak_KeepsACopyBeforeUsingDefaults()
    {
        var p = P("settings.json");
        File.WriteAllText(p, "not json");
        var l = AppSettings.Load(p);
        Assert.Equal("current", l.Theme);
        Assert.Equal("not json", File.ReadAllText(Directory.GetFiles(_dir, "settings.json.broken-*").Single()));
    }

    // ---- checklist.json (characters and lists)
    [Fact] public void Checklist_CorruptFile_LoadsTheBak_AndStillSaves()
    {
        var p = P("checklist.json");
        var b = ChecklistBook.CreateNew();
        b.SetFirstName("Daeva");
        b.Current.Sections[0].Items.Add(new ChecklistItem { Text = "My item" });
        b.Save(p);
        b.Current.Sections[0].Items.Add(new ChecklistItem { Text = "Newer item" });
        b.Save(p);
        File.WriteAllText(p, "{\"Version\": 2, \"Charac"); // torn write
        var l = ChecklistBook.Load(p, "x");
        Assert.False(l.ReadOnly);
        Assert.Equal("Daeva", l.Current.Name);
        Assert.Contains(l.Current.Sections[0].Items, i => i.Text == "My item");
        Assert.Single(Directory.GetFiles(_dir, "checklist.json.broken-*"));
    }

    [Fact] public void Checklist_MissingFile_WithBak_UsesTheBak()
    {
        var p = P("checklist.json");
        var b = ChecklistBook.CreateNew(); b.SetFirstName("Daeva"); b.Save(p); b.Save(p);
        File.Delete(p);
        var l = ChecklistBook.Load(p, "x");
        Assert.Equal("Daeva", l.Current.Name);
        Assert.False(l.NeedsName);
    }

    [Fact] public void Checklist_CorruptWithoutBak_StaysReadOnly()
    {
        var p = P("checklist.json");
        File.WriteAllText(p, "garbage");
        var l = ChecklistBook.Load(p, "x");
        Assert.True(l.ReadOnly);              // never overwrites the user's file
        Assert.Equal("garbage", File.ReadAllText(p));
    }
}
