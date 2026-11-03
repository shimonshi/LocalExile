using GameServer.Core.Projectiles;

/// <summary>Residual release inputs applied after the existing generic profile/count resolver.</summary>
static class DeadeyeResidualProjectile
{
    public readonly record struct Frozen(int TerrainChainChance, bool NoBarrageSpread)
    {
        public ProjectileProfile ApplySpread(ProjectileProfile profile, bool barrage) =>
            barrage && NoBarrageSpread ? profile with { SpreadRadians = 0, AimSpreadDistance = 0 } : profile;
        // Existing collision/chain owner supplies one deterministic roll only after an eligible terrain contact.
        public bool ChainsFromTerrain(bool eligible, int roll) => eligible && roll >= 0 && roll < 100
            && roll < TerrainChainChance;
    }
    public static Frozen Capture(Func<string, int> qualified) =>
        new(Math.Clamp(qualified("projectile_chain_from_terrain_chance_%"), 0, 100),
            qualified("no_barrage_projectile_spread") != 0);
}
