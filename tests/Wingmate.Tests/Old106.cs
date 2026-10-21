using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Wingmate.Core;

/// <summary>Builds checklist data the way Wingmate 1.0.6 saved it (no Events list, no AppliedUpdates field).</summary>
public static class Old106
{
    public static readonly string[] Dailies = { "Daily attendance reward", "Duty missions", "Daily dungeon runs", "Nightmare", "Transcendence", "Dimensional Invasion", "Shugo Festival" };
    public static readonly string[] Weeklies = { "Raids", "Ascension Trial", "Subjugation", "Exploration", "Use Abyss time", "Arena of Tactics (3 matches)", "Artifact Siege" };

    public static ChecklistState Character(string name)
    {
        var c = ChecklistBook.NewCharacter(name);
        c.AppliedUpdates.Clear();
        c.Sections.RemoveAll(s => s.Kind == EventsList.Kind);
        c.Sections.Single(s => s.Kind == "daily").Items = Dailies.Select(t => new ChecklistItem { Text = t }).ToList();
        c.Sections.Single(s => s.Kind == "weekly").Items = Weeklies.Select(t => new ChecklistItem { Text = t }).ToList();
        return c;
    }

    public static ChecklistSection Daily(ChecklistState c) => c.Sections.Single(s => s.Kind == "daily");
    public static ChecklistSection Weekly(ChecklistState c) => c.Sections.Single(s => s.Kind == "weekly");
    public static ChecklistSection Events(ChecklistState c) => c.Sections.Single(s => s.Kind == EventsList.Kind);

    public static string BookJson(params ChecklistState[] chars)
    {
        var b = new ChecklistBook { CurrentId = chars[0].Id };
        b.Characters.AddRange(chars);
        var node = JsonNode.Parse(JsonSerializer.Serialize(b))!;
        foreach (var c in node["Characters"]!.AsArray()) c!.AsObject().Remove("AppliedUpdates");
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static string StateJson(ChecklistState c)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(c))!.AsObject();
        node.Remove("AppliedUpdates");
        return node.ToJsonString();
    }
}
