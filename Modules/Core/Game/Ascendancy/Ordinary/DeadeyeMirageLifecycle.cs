using GameServer.Core.Projectiles;

/// <summary>Actor lifecycle only. Existing projectile runtime remains the sole simulator and release owner.</summary>
sealed class DeadeyeMirageLifecycle
{
    public sealed record Actor(long Identity, long GameGeneration, string EquipmentSignature, ushort Skill,
        bool Detached, ProjectileVector Position, OrdinaryEffectClock Clock)
    {
        public OrdinaryEffectClock Clock { get; set; } = Clock;
        public double NextAttack { get; set; }
        public long Attack { get; set; }
    }
    readonly List<Actor> actors = [];
    readonly HashSet<(long Area, long Release)> releases = [];
    long next;
    public IReadOnlyList<Actor> Actors => actors;

    // Called once on a qualifying direct owner bow hit tied to an accepted paid release. Parent reserves an
    // object id only after this succeeds. A proxy hit can never recurse or grant owner on-hit/leech resources.
    public Actor? OwnerHit(double now, long area, long release, bool paidRelease, bool ownerDirect, long gameGeneration,
        string signature, ushort skill, ProjectileVector position, Func<string, int> qualified)
    {
        Step(now, gameGeneration, signature);
        if (!paidRelease || !ownerDirect || !releases.Add((area, release))) return null;
        bool detached = qualified("mirage_archers_do_not_attach") != 0;
        int radius = Math.Max(0, qualified("cannot_summon_mirage_archer_if_near_mirage_archer_radius"));
        if (detached && actors.Any(a => (a.Position - position).Length <= radius)) return null;
        double seconds = qualified("support_mirage_archer_base_duration") / 1000d
            * Math.Max(0, 1 + ((long)qualified("mirage_archer_duration_+%")
                + qualified("skill_effect_duration_+%")) / 100d);
        if (seconds <= 0) return null;
        var clock = new OrdinaryEffectClock(seconds, now, OrdinaryEffectClock.RecipientRate(18, qualified));
        if (!detached && actors.FirstOrDefault() is { } old && old.Skill == skill)
        {
            old.Clock = clock;
            return old;
        }
        if (!detached) actors.Clear();
        int limit = (int)Math.Clamp(1L + qualified("additional_max_mirage_archers"), 1, 256);
        while (actors.Count >= limit) actors.RemoveAt(0);
        var actor = new Actor(++next, gameGeneration, signature, skill, detached, position, clock) { NextAttack = now };
        actors.Add(actor);
        return actor;
    }

    public void Step(double now, long generation, string signature)
    {
        foreach (var actor in actors) actor.Clock.Advance(now);
        actors.RemoveAll(a => a.GameGeneration != generation || a.EquipmentSignature != signature || a.Clock.Remaining <= 0);
    }

    public bool TryAttack(Actor actor, double now, long generation, string signature, double attackSeconds,
        out long attack)
    {
        Step(now, generation, signature);
        attack = 0;
        if (!actors.Contains(actor) || now < actor.NextAttack || !double.IsFinite(attackSeconds) || attackSeconds <= 0)
            return false;
        actor.NextAttack = now + attackSeconds;
        attack = ++actor.Attack;
        return true;
    }

    public bool Current(Actor actor, long attack, long generation) =>
        actor.GameGeneration == generation && actors.Contains(actor) && actor.Attack == attack;
    public void Clear() { actors.Clear(); releases.Clear(); }
}
