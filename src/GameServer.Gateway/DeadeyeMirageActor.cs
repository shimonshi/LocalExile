using GameServer.Core.Projectiles;

/// <summary>Explicit player-skill proxy attribution; separate from minions and from the owner's on-hit resources.</summary>
sealed record DeadeyeMirageAttribution
{
    public bool CountsAsYourSkill => true;
    public bool KillCreditToOwner => true;
    public bool AllowCull => true;
    public bool GrantOwnerLeech => false;
    public bool GrantOwnerRage => false;
    public bool GrantOwnerOnHit => false;
    public bool GrantOwnerChargeOnHit => false;
    public bool RecursiveMirage => false;
    public static readonly DeadeyeMirageAttribution Policy = new();
}

sealed class DeadeyeMirageActor(uint id, int generation, Program.DeadeyeMarkSource source,
    string signature, bool detached, ProjectileVector position, EffectClock clock)
{
    public uint Id { get; } = id;
    public int Generation { get; } = generation;
    public Program.DeadeyeMarkSource Source { get; } = source;
    public string Signature { get; } = signature;
    public bool Detached { get; } = detached;
    public ProjectileVector Position = position;
    public EffectClock Clock { get; private set; } = clock;
    public void RefreshLifetime(double duration, double now, double rate) => Clock = new(duration, now, rate);
    public double NextAttack;
    public bool Created;
    public bool Retired;
    public int AttackGeneration;
}

sealed record DeadeyeMirageOffense(ProjectileOffense Shot, DeadeyeMirageActor Actor, int AttackGeneration)
{
    public DeadeyeMarkHitSource? DirectMarkSource { get; init; }
    public DeadeyeMarkHitSource? AreaMarkSource { get; init; }
    public DeadeyeMirageAttribution Attribution => DeadeyeMirageAttribution.Policy;
}
