using GameServer.Core.Protocol.Instance;

static partial class Game
{
    // Wire helper only. Callers own live target/instance identity and buff-before-action FIFO admission.
    // ActiveSigil reads both context entries unchecked: MiscAnimated row, then normal/charged animation selector.
    // Selector0 is the bounded default consumer path (fadein -> loop_attached), not a universal producer claim.
    internal static byte[]? DefaultBrandAttachmentBuff(string effect, int variation, uint target,
        ushort instance, uint caster, ushort skill, float attachedSeconds)
    {
        if (variation != 0 || target == 0 || instance == 0 || caster == 0 || skill == 0
            || !float.IsFinite(attachedSeconds) || attachedSeconds <= 0) return null;
        (ushort Row, string Art)? context = effect switch
        {
            "StormBrand" => (1218, "storm_brand_attached"),
            "ArmageddonBrand" => (1230, "armageddon_brand_attached"),
            _ => null,
        };
        if (context is not { } selected || Art.AnimatedRow(selected.Art, variation) is not int art || art < 0) return null;
        return InstanceEntities.AddBuff(target, instance, selected.Row, caster, skill,
            u68: BitConverter.SingleToUInt32Bits(attachedSeconds), list: [(uint)art, 0u]);
    }
    internal sealed record BrandPresentationIdentity(string Effect, InstanceRetainedAction.Identity Action);
    internal sealed class BrandPresentationState(BrandPresentationIdentity identity)
    {
        internal readonly BrandPresentationIdentity Identity = identity;
        internal SpawnedMonster? Desired, Shown;
        internal uint Target, ShownTarget;
        internal ushort Buff, ShownBuff;
        internal int Revision;
        internal bool Retired;
    }
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ProjectileSession, HashSet<ushort>> BrandBuffs = new();

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ClientState, HashSet<InstanceRetainedAction.Identity>> BrandFamilies = new();

    // Only the first clone of a fresh, ordinary, unsupported handcast family is modeled.
    // Counter starts {1,0}; native Allocate returns 2. Never infer subsequent allocations from this identity.
    internal static BrandPresentationIdentity? CaptureBrandPresentation(ClientState c, ushort skill, ushort sequence)
    {
        var s = SupportedSkill(c, skill);
        var gem = GemSkillEffects(c).FirstOrDefault(x => x.Id == skill);
        string? effect = SkillsFor(c).EffectId(s.Effect);
        if (effect is not ("StormBrand" or "ArmageddonBrand") || sequence == 0 || skill >= 0x8000
            || s.Granted || s.SourceGem is not { GemVariant: 0 } || s.Supports.Count != 0
            || SkillArtVariation(c, gem.Gem) != 0) return null;
        var stats = SupportedStats(c, skill);
        // Actual pinned stat names. Unknown trigger/repeat producers fail closed as well.
        if (MarkStat(stats, "additional_number_of_brands_to_create") != 0
            || MarkStat(stats, "create_additional_brand_%_chance") != 0
            || stats.Any(x => x.Value != 0 && Stats.Name(x.Key) is { } name
                && (name.Contains("trigger", StringComparison.Ordinal)
                    || name.Contains("spell_repeat", StringComparison.Ordinal)))) return null;
        // The bounded native source had no linked skill (+a4). Admit a solitary gem link only;
        // no paired/support/source-item trigger context is reconstructed by this feature.
        var equipment = ItemsOf(c).EquipmentSnapshot();
        var host = equipment.Select(x => x.Item.Item).FirstOrDefault(x => x.Sockets.Any(y =>
            y.Gem?.RuntimeIdentity == s.SourceGem.RuntimeIdentity));
        if (host is null || host.Sockets.Count(x => x.Gem is not null) != 1 || host.Rarity != ItemRarity.Normal) return null;
        if (SkillsFor(c).ActionHash(s.Effect) is not ushort type
            || type != (effect == "StormBrand" ? 0x2bb2 : 0xa4d6)) return null;
        var identity = new InstanceRetainedAction.Identity(type, skill, sequence, 2);
        if (!BrandFamilies.GetValue(c, _ => []).Add(identity)) return null;
        return new(effect, identity);
    }

    static void PresentBrandAttachment(ProjectileSession session, HierophantEngineActor actor)
    {
        if (actor.Presentation is not { Retired: false } p) return;
        if (ReferenceEquals(p.Desired, actor.Attached) && p.Target == actor.Target) return;
        p.Desired = actor.Attached; p.Target = actor.Target; p.Revision++;
        p.Buff = 0;
        if (p.Desired is not null)
        {
            var used = BrandBuffs.GetValue(session, _ => []);
            // Retain our IDs until this session ends, including delayed cleanup. Leave a slot for other producers.
            for (int i = 0; i < ushort.MaxValue
                && session.Conn.ReservedBrandBuffs.Count < MonsterElementalAilments.FirstInstance - 2; i++)
            {
                ushort candidate = session.Conn.NextBuff++;
                if (candidate != 0 && !TargetBuffReserved(session.Conn, p.Target, p.Desired, candidate)
                    && used.Add(candidate) && session.Conn.ReservedBrandBuffs.TryAdd(candidate, session))
                { p.Buff = candidate; break; }
            }
        }
        int revision = p.Revision;
        session.Packet(() => revision == p.Revision ? SnapshotBrandPresentation(session, actor, p, false) : null);
    }

    // Live target-local records can outlast a full global allocator cycle. An AddBuff with their ID updates them.
    static bool TargetBuffReserved(ClientState c, uint id, SpawnedMonster target, ushort buff) =>
        target.NativeBuffReservations.Contains(buff) || target.PlayerAuras.ContainsValue(buff) || target.TauntBuff == buff || target.IntimidateBuff == buff
        || target.UnnerveBuff == buff || target.CrushBuff == buff || target.MaimBuff == buff
        || target.BlindBuff == buff || target.WitherBuff == buff || target.ExposureBuff.Contains(buff)
        || target.Ignites.Buff == buff || target.Bleeds.Buff == buff || target.Poisons.Buff == buff
        || c.RighteousFire?.Auras.GetValueOrDefault(id) == buff
        || (ReferenceEquals(c.OrdinaryMarkMonster, target) && c.OrdinaryMarkBuff == buff)
        || c.BlindAura.HoldsBuff(id,buff) || c.FlaskGrounds.HoldsBuff(id,buff)
        // A ground removal owns its instance until consumed under the common cipher send gate (or discarded).
        || c.FlaskAutomation.Packets.Any(output => output.TargetBuff == (id,buff))
        || c.TargetBuffOutputs.Holds(id,buff)
        || (c.RampageWorld.Blinds.TryGetValue(id,out var rampage) && rampage.Buff == buff)
        || (c.ResourceEffectsState is ResourceEffectsState resources
            && ((resources.Targets.TryGetValue(id,out var own) && ReferenceEquals(own.Target,target) && own.Blind.Buff == buff)
                || resources.RetiredBuffs.Contains((id,buff))))
        || c.RetiredResourceBuffs.Contains((id,buff));

    // Invalidate discards this session's FIFO. Release only its own reservations; a successor can then allocate anew.
    static void ReleaseBrandBuffReservations(ProjectileSession session)
    {
        if (!BrandBuffs.TryGetValue(session, out var used)) return;
        foreach (ushort instance in used)
            if (session.Conn.ReservedBrandBuffs.TryGetValue(instance, out var owner) && ReferenceEquals(owner, session))
                session.Conn.ReservedBrandBuffs.TryRemove(instance, out _);
        used.Clear();
    }

    static void RetireBrandPresentation(ProjectileSession session, HierophantEngineActor actor)
    {
        if (actor.Presentation is not { Retired: false } p) return;
        p.Retired = true; p.Revision++;
        // Inactive/old-area sessions discard their entire FIFO. Do not bypass the output-generation guard.
        session.Packet(() => SnapshotBrandPresentation(session, actor, p, true));
    }

    static byte[]? SnapshotBrandPresentation(ProjectileSession session, HierophantEngineActor actor,
        BrandPresentationState p, bool retire)
    {
        var c = session.Conn;
        if (actor.Use is not { } use || use.Epoch != c.ProjectileEpoch) return null;
        var bytes = new List<byte>();
        void RemoveShown()
        {
            // An object id reused for a later monster must never receive our old removal.
            if (p.ShownBuff != 0 && c.ReservedBrandBuffs.TryGetValue(p.ShownBuff, out var owner)
                && ReferenceEquals(owner, session) && c.Monsters.GetValueOrDefault(p.ShownTarget) == p.Shown)
                bytes.AddRange(InstanceEntities.RemoveBuff(p.ShownTarget, p.ShownBuff));
            p.Shown = null; p.ShownBuff = 0; p.ShownTarget = 0;
        }
        double now = ResourceEffectsTime(c);
        var clock = actor.Brand!;
        double elapsed = Math.Max(0, now - clock.Last);
        float attached = (float)Math.Max(0, clock.AttachedLimit - clock.AttachedUsed
            - (actor.Attached is null ? 0 : elapsed));
        bool alive = HierophantEngineCurrent(session, actor) && attached > 0
            && (actor.Attached is not null || clock.DetachedLimit - clock.DetachedUsed > elapsed);
        if (retire || !alive)
        {
            p.Retired = true; p.Revision++;
            bytes.AddRange(InstanceRetainedAction.Retire(PlayerId, p.Identity.Action));
            RemoveShown();
            return bytes.ToArray();
        }
        bool valid = c.ReservedBrandBuffs.TryGetValue(p.Buff, out var reserved) && ReferenceEquals(reserved, session)
            && p.Desired is { Dead: false, Life: > 0 } target && p.Buff != 0
            && ReferenceEquals(actor.Attached, target) && actor.Target == p.Target
            && c.Monsters.GetValueOrDefault(p.Target) == target;
        if (valid && DefaultBrandAttachmentBuff(p.Identity.Effect, 0, p.Target, p.Buff, PlayerId,
                actor.Skill, attached) is { } add)
        {
            RemoveShown();
            // One FIFO group: target buff context must exist before the native state2 cache lookup.
            bytes.AddRange(add);
            bytes.AddRange(InstanceRetainedAction.Attach(PlayerId, p.Identity.Action, p.Target, p.Buff));
            p.Shown = p.Desired; p.ShownTarget = p.Target; p.ShownBuff = p.Buff;
        }
        else if (p.ShownBuff != 0)
        {
            bytes.AddRange(InstanceRetainedAction.Detach(PlayerId, p.Identity.Action, attached));
            RemoveShown();
        }
        return bytes.Count == 0 ? null : bytes.ToArray();
    }
}
