using GameServer.Core.World;

static partial class Game
{
    internal sealed record MarkHitSource(Func<DamageRange, DamagePacket> Hit,
        Func<DamageRange, MonsterIgnites.Settings> Ignite);

    internal static MarkHitSource? CaptureMarkHitSource(ClientState c, ushort skill, bool area,
        SkillCatalog.Unarmed? weapon = null, bool suppress = false, double scale = 1)
    {
        if (SupportedHitInputsFor(c, skill, area, weapon) is not { } inputs) return null;
        var gem = SupportedSkill(c, skill);
        double effectiveness = AddedEffectiveness(c, gem.Effect, gem.Level);
        SupportedHitInputs With(DamageRange flat) => inputs with
            { Raw = inputs.Raw.Add(DamageType.Physical, flat.Scale(effectiveness)) };
        string hand = inputs.Attack ? "main_hand" : "spell";
        return new(flat => SupportedHitFrom(With(flat), suppress).Scale(scale),
            flat => MonsterIgnites.Settings.From(SupportedAilmentRowsFrom(With(flat), hand), hand, inputs.Get));
    }

    internal static MarkHitSource CaptureOrdinaryMarkHitSource(ClientState c, bool melee)
    {
        var player = new Dictionary<uint, int>(CharacterPlayerStats(c));
        int Get(string name) => MarkStat(player, name);
        var weapon = WeaponFor(c); var raw = SupportedAttackDamage.Raw(weapon, Get);
        var conversion = DamageStats.Conversion(player, Stats, attack: true);
        return new(flat => MaximumAttackDamage.Apply(RageStats.ApplyAttack(PlayerHitDamage.Resolve(
            raw.Add(DamageType.Physical, flat), Get, conversion, spell: false, melee: melee,
            projectile: !melee, wieldingWand: weapon.WieldingWand), Get), Get)
                .Scale(melee ? GamblerFor(player).DamageFactor : 1),
            flat => MonsterIgnites.Settings.From(AilmentPanelStats.Compute(
                new("main_hand", Spell: false, Melee: melee, Projectile: !melee),
                raw.Add(DamageType.Physical, flat), conversion, Get), "main_hand", Get));
    }

    static DamageRange MarkFlat(ClientState c, SpawnedMonster target) => new(
        MarkOn(c, target, "minimum_added_physical_damage_taken"), MarkOn(c, target, "maximum_added_physical_damage_taken"));
    internal static DamagePacket MarkHit(ClientState c, SpawnedMonster target, DamagePacket hit, MarkHitSource? source) =>
        source is not null && MarkFlat(c, target) is var flat && !flat.IsZero ? source.Hit(flat) : hit;
    internal static MonsterIgnites.Settings MarkIgnite(ClientState c, SpawnedMonster target,
        MonsterIgnites.Settings ignite, MarkHitSource? source) =>
        source is not null && MarkFlat(c, target) is var flat && !flat.IsZero ? source.Ignite(flat) : ignite;

    internal static int MarkOn(ClientState c, SpawnedMonster target, string name) =>
        CurrentMark(c) is { } mark && ReferenceEquals(mark.Monster, target) ? MarkAmount(c, mark, name) : 0;

    internal static HitAnimation? MarkFlinch(ClientState c, SpawnedMonster target, int damage, double now, Random random)
    {
        var clip = HitReaction.TryFlinch(target, damage, now, random);
        if (clip is null) return null;
        int chance = MarkOn(c, target, "enemy_chance_to_double_stun_duration_%_vs_self");
        if (chance > 0 && (chance >= 100 || random.Next(100) < chance))
            target.FlinchUntil = now + 2 * (target.FlinchUntil - now);
        int rage = MarkOn(c, target, "enemy_grant_X_rage_on_stun");
        if (rage > 0)
        {
            var stats = CharacterPlayerStats(c);
            c.Rage.Gain(rage, ProjectileWorldTime(c), RageStats.Inputs(n => MarkStat(stats, n)),
                MarkStat(stats, "has_soul_gain_prevention") != 0);
        }
        return clip;
    }

    internal static LeechSources MarkLeech(ClientState c, SpawnedMonster target, LeechSources source)
    {
        if (!source.Attack) return source;
        int Get(string name) => MarkOn(c, target, name);
        foreach (var (type, name) in new[] { (ChargeType.Power, "power"), (ChargeType.Frenzy, "frenzy"), (ChargeType.Endurance, "endurance") })
            if (Get($"grant_attacker_{name}_charge_when_hit_%_chance") is > 0 and var chance
                && (chance >= 100 || Random.Shared.Next(100) < chance))
                ChargesOf(c).Enqueue(new ChargeEvent.Grant(type, 1, "marked enemy hit"));
        DamagePacket Pool(string name)
        {
            var result = DamagePacket.Empty;
            foreach (var type in DamageTypes.ConversionOrder) result = result.Add(DamagePacket.Of(type, Get(name), Get(name)));
            return result;
        }
        var gain = source.GainOnHit;
        return source with
        {
            Life = source.Life.Add(Pool("life_leech_on_any_damage_when_hit_by_attack_permyriad")),
            Mana = source.Mana.Add(Pool("mana_leech_on_any_damage_when_hit_by_attack_permyriad")),
            GainOnHit = (gain.Life + Get("life_granted_when_hit_by_attacks"),
                gain.Mana + Get("mana_granted_when_hit_by_attacks"), gain.EnergyShield)
        };
    }
}
