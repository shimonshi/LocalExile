using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Independent residual behavior, called under the owner's combat lock. The integration owner supplies final
/// qualified stats and committed event identities. No static tickets or Game objects cross an assembly reload.
/// </summary>
sealed class OrdinaryResidualSession(long generation)
{
    public SlayerKillState Slayer { get; } = new();
    public DeadeyeGaleState Gale { get; } = new();
    public DeadeyeMarkRules Marks { get; } = new();
    public DeadeyeMirageLifecycle Mirages { get; } = new();
    // Epochs and committed event serials are monotonic under the owner's combat lock.
    // Delayed projectiles carry their own admission receipt, not this area's full use history.
    (long Area, long Event)? lastKill;
    long? useArea, lastAction, lastUtility;
    bool retired;
    static readonly JsonSerializerOptions JsonOptions = new()
    { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
    public long Generation { get; } = generation;

    internal enum Credit { OwnerDirect, OwnerDamageOverTime, OwnedProxy, Reflected }
    internal readonly record struct Lethal(long Area, long Event, Credit Source, bool Committed,
        bool PhaseTransition, bool RareOrUnique, uint LifeBefore, int DirectDamage);
    internal readonly record struct AcceptedUse(long Area, long Event, bool Paid, bool Accepted,
        bool Triggered = false, bool Repeat = false, bool OwnedActor = false);

    // Called synchronously at lethal commit, before any awaited rewards. Proxy credit is deliberately explicit;
    // it may grant owner kill conditions, but never owner direct-hit overkill leech.
    public (bool Recorded, double OverkillLife) Kill(double now, Lethal hit, Func<string, int> qualified)
    {
        if (retired || !hit.Committed || hit.PhaseTransition || hit.LifeBefore == 0
            || hit.Source == Credit.Reflected || !RememberKill(hit.Area, hit.Event)) return default;
        Slayer.RecordKill(now, hit.RareOrUnique, qualified);
        return (true, hit.Source == Credit.OwnerDirect
            ? SlayerRules.OverkillLifeLeech(hit.DirectDamage, hit.LifeBefore, qualified) : 0);
    }

    public bool Use(double now, AcceptedUse use)
    {
        if (retired || !use.Paid || !use.Accepted || use.Triggered || use.Repeat || use.OwnedActor
            || !RememberUse(use.Area, use.Event)) return false;
        Gale.AdmitUse(now, true);
        return true;
    }

    public bool CanPublish(long currentGeneration) => !retired && Generation == currentGeneration;
    internal bool Accepted(long area, long action) => !retired && useArea == area
        && (lastAction == action || lastUtility == action);

    bool RememberKill(long area, long serial)
    {
        if (lastKill is { } old && (area < old.Area || area == old.Area && serial <= old.Event)) return false;
        lastKill = (area, serial);
        return true;
    }

    bool RememberUse(long area, long serial)
    {
        if (useArea is long previous && area < previous) return false;
        if (useArea != area) { useArea = area; lastAction = lastUtility = null; }
        if (serial >= 0)
        {
            if (lastAction is long old && serial <= old) return false;
            lastAction = serial;
        }
        else
        {
            if (lastUtility is long old && serial >= old) return false;
            lastUtility = serial;
        }
        return true;
    }

    long[][] KillKeys() => lastKill is { } key ? [new long[] { key.Area, key.Event }] : [];
    long[][] UseKeys() => new[] { lastAction, lastUtility }.Where(s => s.HasValue)
        .Select(s => new long[] { useArea!.Value, s!.Value }).ToArray();

    // Host stores this UTF-8-neutral string on its connection/transfer payload. Capture at the exact source
    // transition. World clock does not reset on hotreload. Transfer uses its existing paused owner clock.
    public string Capture(double now) => JsonSerializer.Serialize(new Payload(1, Slayer.Carry(now),
        Gale.Capture(now), KillKeys(), UseKeys()), JsonOptions);

    public static OrdinaryResidualSession Restore(string json, long generation, double now,
        bool gatheringWinds, Func<string, int> qualified)
    {
        var value = JsonSerializer.Deserialize<Payload>(json, JsonOptions) ?? throw new ArgumentException("Missing ordinary payload.");
        if (value.Version != 1) throw new ArgumentException("Unsupported ordinary payload version.");
        var state = new OrdinaryResidualSession(generation);
        state.Slayer.Restore(value.Slayer, now);
        state.Gale.Observe(now, gatheringWinds, qualified);
        state.Gale.RestoreClocks(now, value.Gale);
        state.Gale.Observe(now, gatheringWinds, qualified);
        // Version-one history arrays from an older Game compact to current high-water marks on restore.
        foreach (var key in value.Kills) state.RememberKill(key[0], key[1]);
        foreach (var key in value.Uses) state.RememberUse(key[0], key[1]);
        return state;
    }

    public static string RebaseTransfer(string json, double oldNow, double newNow)
    {
        var value = JsonSerializer.Deserialize<Payload>(json, JsonOptions)!;
        double shift = newNow - oldNow;
        OrdinaryEffectClock.Carried? Move(OrdinaryEffectClock.Carried? c) => c is null ? null
            : c with { Sampled = c.Sampled + shift, Deadline = c.Deadline + shift };
        return JsonSerializer.Serialize(value with
        {
            Slayer = value.Slayer with { Kills = value.Slayer.Kills.Select(t => t + shift).ToArray(),
                Movement = Move(value.Slayer.Movement), Attack = Move(value.Slayer.Attack) },
            Gale = value.Gale.Select(c => Move(c)!).ToArray(), Kills = [], Uses = []
        }, JsonOptions);
    }

    // Mark/actor native identities retire on reload/transfer. The parent removes their old objects/buffs, then
    // creates new identities if desired. Never resurrect an old queued attack or mark by reusing its object id.
    public void Retire()
    {
        retired = true; Slayer.Clear(); Gale.Clear(true); Marks.Clear(); Mirages.Clear();
    }

    public sealed record Payload(int Version, SlayerKillState.Carried Slayer,
        OrdinaryEffectClock.Carried[] Gale, long[][] Kills, long[][] Uses);
}
