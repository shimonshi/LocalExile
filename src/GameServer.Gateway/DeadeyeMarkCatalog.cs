using System.Text.Json;

/// <summary>Native ordinary Mark identities and ordered BuffDefinitions values, read without archive mutation.</summary>
sealed class DeadeyeMarkCatalog(string folder = "data")
{
    internal sealed record Mark(int Effect, string Id, ushort Buff, uint[] BuffKeys);
    readonly Lazy<Mark[]> marks = new(() => Load(folder));
    public IReadOnlyList<Mark> All => marks.Value;
    public Mark? Find(int effect) => marks.Value.SingleOrDefault(m => m.Effect == effect);

    static Mark[] Load(string folder)
    {
        using var effects = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "tables", "GrantedEffects.json")));
        using var buffs = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "tables", "BuffDefinitions.json")));
        var names = new Dictionary<string, string>
        {
            ["SnipersMark"] = "curse_snipers_mark", ["AssassinsMark"] = "curse_assassins_mark",
            ["PoachersMark"] = "curse_poachers_mark", ["WarlordsMark"] = "curse_warlords_mark"
        };
        var result = new List<Mark>();
        foreach (var (effect, row) in effects.RootElement.EnumerateArray().Select((r, i) => (i, r)))
            if (names.TryGetValue(row.GetProperty("Id").GetString()!, out string? buff))
            {
                var match = buffs.RootElement.EnumerateArray().Select((r, i) => (Row: i, Data: r))
                    .Single(r => r.Data.GetProperty("Id").GetString() == buff);
                result.Add(new(effect, row.GetProperty("Id").GetString()!, checked((ushort)match.Row),
                    [.. match.Data.GetProperty("StatsKeys").EnumerateArray().Select(s => s.GetUInt32() + 1)]));
            }
        return [.. result];
    }
}
