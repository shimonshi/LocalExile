using GameServer.Core.World;

/// <summary>Frozen real skill damage inputs. Contact supplies the marked target's flat physical source, before
/// effectiveness, conversion, increased/more and suppression; it never appends flat damage to a final hit.</summary>
sealed record DeadeyeMarkHitSource(Func<DamageRange, DamagePacket> Resolve,
    Func<DamageRange, MonsterIgnites.Settings>? ResolveIgnite = null);
