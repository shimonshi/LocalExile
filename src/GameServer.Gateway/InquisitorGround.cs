/// <summary>
/// The ordinary Inquisitor's ground geometry and per-recipient lingering effects.
/// The caller holds the owner's combat lock and supplies its pausable world clock.
/// Pious Path preserves an effect on a recipient; it does not leave a new ground at the old position.
/// </summary>
sealed class InquisitorGround
{
    public const string StationaryRadiusStat = "consecrated_ground_while_stationary_radius";
    public const string EnemyDamageTakenStat = "consecrated_ground_enemy_damage_taken_+%";
    public const string LingerStat = "your_consecrated_ground_effect_lingers_for_ms_after_leaving_the_area";
    public const string MirrorRegenerationStat = "consecrated_ground_allies_recover_es_as_well_as_life_from_life_regeneration";

    public readonly record struct Sources(int Radius, int EnemyDamageTaken, int LingerMilliseconds, bool MirrorRegeneration)
    {
        public static Sources From(Func<string, int> get) => new(Math.Max(0, get(StationaryRadiusStat)),
            get(EnemyDamageTakenStat), Math.Max(0, get(LingerStat)), get(MirrorRegenerationStat) != 0);
        public bool Any => Radius > 0 || EnemyDamageTaken != 0 || LingerMilliseconds > 0 || MirrorRegeneration;
        public double LingerSeconds => Math.Max(0, LingerMilliseconds) / 1000d;
    }

    sealed class Recipient
    {
        public bool Inside;
        public double Entered = double.NegativeInfinity, Left = double.NegativeInfinity;
        public void Observe(double now, bool inside, double linger)
        {
            if (inside && !Inside && !Active(now, linger)) Entered = now;
            if (Inside && !inside) Left = now;
            Inside = inside;
        }
        public bool Active(double now, double linger) => Inside || now < Left + linger;
        public double Until(double linger) => Inside ? double.PositiveInfinity : Left + linger;
    }

    Sources sources;
    readonly Recipient owner = new();
    readonly Dictionary<uint, Recipient> enemies = [];
    bool alive, stationary;
    (int X, int Y) position;

    public bool HasStationaryGround => alive && stationary && sources.Radius > 0;
    public (int X, int Y) Position => position;
    public int Radius => HasStationaryGround ? sources.Radius : 0;
    public bool OwnerAffected(double now) => alive && sources.Any && owner.Active(now, sources.LingerSeconds);
    public bool OwnerMirrorsLifeRegeneration(double now) => sources.MirrorRegeneration && OwnerAffected(now);
    public double OwnerEffectUntil => alive && sources.Any
        ? owner.Until(sources.LingerSeconds) : double.NegativeInfinity;

    /// <summary>
    /// Observe at accepted movement/source/life changes and on the world tick. externalOwnGround means an actual
    /// ground created by this player (for example a Sulphur Flask), never another player's consecrated ground.
    /// Refunds use the new source vector immediately, so removing Pious Path also removes its old linger.
    /// </summary>
    public void Observe(double now, (int X, int Y) at, bool moving, bool living, Sources value,
        bool externalOwnGround = false)
    {
        if (!double.IsFinite(now)) throw new ArgumentOutOfRangeException(nameof(now));
        bool oldGround = HasStationaryGround;
        alive = living;
        sources = value;
        position = at;
        stationary = !moving;
        if (!alive || !sources.Any) { Clear(); return; }
        owner.Observe(now, HasStationaryGround || externalOwnGround, sources.LingerSeconds);
        if (oldGround && !HasStationaryGround)
            foreach (var recipient in enemies.Values)
                recipient.Observe(now, false, sources.LingerSeconds);
        if (sources.EnemyDamageTaken == 0) enemies.Clear();
        else
            foreach (uint id in enemies.Where(e => !e.Value.Active(now, sources.LingerSeconds))
                .Select(e => e.Key).ToArray()) enemies.Remove(id);
    }

    public bool Covers((int X, int Y) at)
    {
        double dx = (double)at.X - position.X, dy = (double)at.Y - position.Y;
        return HasStationaryGround && dx * dx + dy * dy <= (double)sources.Radius * sources.Radius;
    }

    /// <summary>
    /// Observe living hostile recipients while sampling the ground, including when their movement leaves it.
    /// The returned increase belongs in the target's increased-damage-taken bucket, shared by hits and DoT.
    /// </summary>
    public int EnemyDamageTaken(double now, uint id, (int X, int Y) at, bool livingHostile,
        bool externalOwnGround = false)
    {
        if (!alive || !sources.Any || !livingHostile || sources.EnemyDamageTaken == 0)
        {
            enemies.Remove(id);
            return 0;
        }
        bool inside = Covers(at) || externalOwnGround;
        if (!enemies.TryGetValue(id, out var recipient))
        {
            if (!inside) return 0;
            enemies[id] = recipient = new();
        }
        recipient.Observe(now, inside, sources.LingerSeconds);
        if (recipient.Active(now, sources.LingerSeconds)) return sources.EnemyDamageTaken;
        enemies.Remove(id);
        return 0;
    }

    /// <summary>
    /// Integrate only the current owner-effect interval, including an exact linger expiry inside a recovery step.
    /// The input is life regeneration, not life recovery from flasks, leech or recoup; resource multipliers and
    /// cannot-regenerate rules remain the ES writer's responsibility. Integrate before changing sources.
    /// </summary>
    public double MirroredLifeRegeneration(double from, double to, double lifeRegenerationPerSecond)
    {
        if (!alive || !sources.MirrorRegeneration || to <= from || lifeRegenerationPerSecond <= 0) return 0;
        double seconds = Math.Max(0, Math.Min(to, owner.Until(sources.LingerSeconds)) - Math.Max(from, owner.Entered));
        return seconds * lifeRegenerationPerSecond;
    }

    public void Clear()
    {
        alive = stationary = false;
        owner.Inside = false;
        owner.Entered = owner.Left = double.NegativeInfinity;
        enemies.Clear();
    }
}
