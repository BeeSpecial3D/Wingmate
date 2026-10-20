using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wingmate.Core
{
    /// <summary>All characters' checklists in one file (checklist.json, format version 2).
    /// Each character is a full ChecklistState: own Dailies/Weeklies/custom lists, checks, hidden sections and reset marks.</summary>
    public sealed class ChecklistBook
    {
        public const string DefaultName = "Character name";
        public const string PreCharactersBackup = "checklist.backup-pre-characters.json";

        public int Version { get; set; } = 2;
        public string CurrentId { get; set; } = "";
        public List<ChecklistState> Characters { get; set; } = new();

        /// <summary>True when the file on disk could not be read: we never write over data we failed to parse.</summary>
        [JsonIgnore] public bool ReadOnly { get; private set; }
        /// <summary>True when an old single-checklist file was converted on load (caller saves the new format).</summary>
        [JsonIgnore] public bool Migrated { get; private set; }

        private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

        [JsonIgnore] public ChecklistState Current
        {
            get
            {
                if (Characters.Count == 0) Characters.Add(NewCharacter(DefaultName));
                var c = Characters.FirstOrDefault(x => x.Id == CurrentId);
                if (c == null) { c = Characters[0]; CurrentId = c.Id; }
                return c;
            }
        }

        public static ChecklistState NewCharacter(string name)
        {
            var c = ChecklistState.CreateDefault();
            c.Id = Guid.NewGuid().ToString("N");
            c.Name = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim();
            return c;
        }

        public static ChecklistBook CreateNew()
        {
            var b = new ChecklistBook();
            var c = NewCharacter(DefaultName);
            b.Characters.Add(c); b.CurrentId = c.Id;
            return b;
        }

        /// <summary>Names that are only placeholders (the built-in default and the old sample name), compared trimmed and
        /// case-insensitively.</summary>
        public static readonly string[] PlaceholderNames = { DefaultName, "My Character Name", "Your character name", "Enter your character name" };

        public static bool IsPlaceholderName(string? name)
        {
            var n = (name ?? "").Trim();
            return n.Length == 0 || PlaceholderNames.Any(p => string.Equals(p, n, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>First run: true while the only character still has a placeholder name. Anyone with a real name, or with
        /// more than one character, never sees the name prompt. An unreadable checklist.json (read-only) never asks either,
        /// because the name could not be saved.</summary>
        [JsonIgnore] public bool NeedsName => !ReadOnly && Characters.Count == 1 && IsPlaceholderName(Characters[0].Name);

        /// <summary>Saves the first name: renames the placeholder character in place (same id, lists and progress). Returns
        /// false (and changes nothing) for an empty or placeholder name, or when no name is needed.</summary>
        public bool SetFirstName(string? name)
        {
            var n = (name ?? "").Trim();
            if (!NeedsName || IsPlaceholderName(n)) return false;
            var c = Characters[0];
            c.Name = n.Length > MaxNameLength ? n[..MaxNameLength].TrimEnd() : n;
            CurrentId = c.Id;
            return true;
        }

        public const int MaxNameLength = 40;

        /// <summary>Next character, looping to the first after the last. Returns the new current one.</summary>
        public ChecklistState Next()
        {
            int i = Characters.IndexOf(Current);
            var c = Characters[(i + 1) % Characters.Count];
            CurrentId = c.Id;
            return c;
        }

        public bool Select(string id)
        {
            if (!Characters.Any(c => c.Id == id)) return false;
            CurrentId = id;
            return true;
        }

        public ChecklistState Add(string? name = null)
        {
            var c = NewCharacter(name ?? DefaultName);
            Characters.Add(c); CurrentId = c.Id;
            return c;
        }

        /// <summary>Removes a character (never the last one). If it was current, the next one (or previous at the end) becomes current.</summary>
        public bool Remove(string id)
        {
            if (Characters.Count <= 1) return false;
            int i = Characters.FindIndex(c => c.Id == id);
            if (i < 0) return false;
            bool wasCurrent = Characters[i].Id == CurrentId;
            Characters.RemoveAt(i);
            if (wasCurrent) CurrentId = Characters[Math.Min(i, Characters.Count - 1)].Id;
            return true;
        }

        /// <summary>Gives every character an id, a name and its built-in lists.</summary>
        private void Normalize()
        {
            Characters ??= new List<ChecklistState>();
            Characters.RemoveAll(c => c == null);
            foreach (var c in Characters)
            {
                if (string.IsNullOrWhiteSpace(c.Id)) c.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrWhiteSpace(c.Name)) c.Name = DefaultName;
                c.Sections ??= new List<ChecklistSection>();
                c.EnsureBuiltIns();
            }
            _ = Current; // fixes CurrentId / empty list
            Version = 2;
        }

        /// <summary>Converts an old single checklist into the first character (nothing dropped).</summary>
        public static ChecklistBook FromLegacy(ChecklistState old, string legacyName)
        {
            old.Id = string.IsNullOrWhiteSpace(old.Id) ? Guid.NewGuid().ToString("N") : old.Id;
            old.Name = string.IsNullOrWhiteSpace(legacyName) ? DefaultName : legacyName.Trim();
            old.Version = 1;
            var b = new ChecklistBook { Characters = { old }, CurrentId = old.Id, Migrated = true };
            b.Normalize();
            return b;
        }

        /// <summary>Reads checklist.json in either format. Old format: a backup copy is written next to it first, then it becomes
        /// the first character named legacyName. Missing file: one new character "Character name". Unreadable file: a copy is kept,
        /// defaults are used in memory and the book is ReadOnly so the original is never overwritten.</summary>
        public static ChecklistBook Load(string path, string legacyName)
        {
            if (!File.Exists(path)) return CreateNew();
            string text;
            try { text = File.ReadAllText(path, Encoding.UTF8); }
            catch (Exception ex) { Log.Write("checklist read failed (not saving): " + ex.Message); return Failed(); }
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) throw new JsonException("not an object");
                bool isBook = root.EnumerateObject().Any(p => string.Equals(p.Name, "Characters", StringComparison.OrdinalIgnoreCase));
                if (isBook)
                {
                    var b = JsonSerializer.Deserialize<ChecklistBook>(text, Opts) ?? throw new JsonException("empty");
                    b.Normalize();
                    return b;
                }
                var old = JsonSerializer.Deserialize<ChecklistState>(text, Opts) ?? throw new JsonException("empty");
                var backup = Path.Combine(Path.GetDirectoryName(path)!, PreCharactersBackup);
                try
                {
                    if (File.Exists(backup)) backup = Path.Combine(Path.GetDirectoryName(path)!, "checklist.backup-pre-characters-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                    File.Copy(path, backup, false);
                }
                catch (Exception ex)
                {
                    Log.Write("checklist migration: backup failed, keeping the old file untouched (not saving): " + ex.Message);
                    var ro = FromLegacy(old, legacyName); ro.ReadOnly = true; ro.Migrated = false;
                    return ro;
                }
                var m = FromLegacy(old, legacyName);
                Log.Write($"checklist migration: old single checklist -> character \"{m.Current.Name}\" ({m.Current.Sections.Count} lists, " +
                          $"{m.Current.Sections.Sum(x => x.Items.Count)} items); backup at {Path.GetFileName(backup)}");
                return m;
            }
            catch (Exception ex)
            {
                Log.Write("checklist load failed (keeping the file untouched, not saving): " + ex.Message);
                try { File.Copy(path, path + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), false); } catch { }
                return Failed();
            }
        }

        private static ChecklistBook Failed() { var b = CreateNew(); b.ReadOnly = true; return b; }

        public void Save(string path)
        {
            if (ReadOnly) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(this, Opts), new UTF8Encoding(false));
                File.Move(tmp, path, true);
            }
            catch (Exception ex) { Log.Write("checklist save: " + ex.Message); }
        }
    }
}
