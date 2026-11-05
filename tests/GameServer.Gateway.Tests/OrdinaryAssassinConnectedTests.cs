using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;
namespace GameServer.Gateway.Tests;

public sealed class OrdinaryAssassinConnectedTests
{
    static uint K(string n) => Program.Stats.Key(n)!.Value;
    static int Get(IReadOnlyDictionary<uint, int> raw, string n) => Program.Stats.Key(n) is uint key ? raw.GetValueOrDefault(key) : 0;
    static OrdinaryHierophantConnectedTests.Setup Setup()
    {
        var s = new OrdinaryHierophantConnectedTests.Setup();
        s.Conn.MainAscendancy = new(new(AscendancyFamily.Normal, "Assassin", AscendancyCatalog.Revision));
        return s;
    }

    public void Converted_elemental_poison_uses_history_and_applies_critical_bonus_once_at_real_admission()
    {
        using var s = Setup(); var c = s.Conn;
        s.Set("all_damage_can_poison", 1); s.Set("base_chance_to_poison_on_hit_%", 100);
        s.Set(OrdinaryAssassinRules.CriticalPoison, 20);
        s.Set("physical_damage_+%", 100); s.Set("fire_damage_+%", 100);
        var raw = s.Read();
        var conversion = new DamageConversion().Add(DamageType.Physical, DamageType.Fire, 100);
        var rows = AilmentPanelStats.Compute(new("spell", true), DamagePacket.Of(DamageType.Physical, 100, 100), conversion, n => Get(raw,n));
        var settings = MonsterPoisons.Settings.From(rows, "spell", n => Get(raw,n));
        // 100 source, 100% physical + 100% fire, 30% poison, then critical-only 20% more.
        Assert.Equal(90, settings.Min, 8); Assert.Equal(162, settings.CriticalMin, 8);
        var target = new SpawnedMonster("target", new(100,100,0), 0,0,1000);
        Assert.NotNull(Game.PoisonOnHitLocked(c, target, DamagePacket.Of(DamageType.Fire, 1,1), true,true,raw,null,settings,0,new(1)));
        Assert.Equal(162, target.Poisons.TotalDps(0), 8);
        s.Set("virtual_never_poison", 1);
        Assert.Null(Game.PoisonOnHitLocked(c,target,DamagePacket.Of(DamageType.Fire,1,1),true,true,s.Read(),null,settings,0,new(1)));
    }

    public void Accepted_poison_history_drives_merged_duration_and_single_live_to_dead_recovery()
    {
        using var s = Setup(); var c = s.Conn; c.PlayerLife = 500;
        s.Set("base_chance_to_poison_on_hit_%",100); s.Set(OrdinaryAssassinRules.RecentPoisonDuration,5);
        s.Set(OrdinaryAssassinRules.PoisonKillRecovery,50);
        var raw=s.Read(); var merged=new Dictionary<uint,int>(raw) { [K("poison_duration_+%")] = 50, [K("faster_poison_%")]=100 };
        var settings=MonsterPoisons.Settings.From(AilmentPanelStats.Compute(new("spell",true),DamagePacket.Of(DamageType.Physical,100,100),new(),n=>Get(merged,n)),"spell",n=>Get(merged,n));
        var target=new SpawnedMonster("target",new(100,100,0),0,0,1000);
        var hit=DamagePacket.Of(DamageType.Physical,10,10);
        Assert.NotNull(Game.PoisonOnHitLocked(c,target,hit,true,false,raw,null,settings,0,new(1)));
        Assert.Equal(1.5,target.Poisons.Until,8);
        Assert.NotNull(Game.PoisonOnHitLocked(c,target,hit,true,false,raw,null,settings,0,new(1)));
        Assert.Equal(1.55,target.Poisons.Until,8);
        var result=Game.TakeOwnerDamageLocked(c,target,2000);
        Assert.Equal(0u,result.Life); Assert.Equal(510u,c.PlayerLife);
        target.Life=result.Life;
        Game.TakeOwnerDamageLocked(c,target,2000); Assert.Equal(510u,c.PlayerLife);
        var other=new SpawnedMonster("minion target",new(100,100,0),0,0,1000);
        other.Poisons.Add(0,100,2);
        Game.TakeOwnerDamageLocked(c,other,2000,playerKill:false); Assert.Equal(510u,c.PlayerLife);
        var owner=Game.ResourceEffectsOf(c); owner.Freeze(.5); owner.Resume(100);
        Assert.Equal(2,owner.Assassin.RecentPoisons(100));
        owner.Clear(); Assert.Equal(0,owner.Assassin.RecentPoisons(100));
        const string less="support_faster_ailments_ailment_duration_+%_final";
        merged[K(less)]=-70;
        var sadism=MonsterPoisons.Settings.From(AilmentPanelStats.Compute(new("spell",true),
            DamagePacket.Of(DamageType.Physical,100,100),new(),n=>Get(merged,n)),"spell",n=>Get(merged,n));
        merged[K(less)]=0; // Contacts retain the captured independent multiplier, not current source values.
        var shortened=new SpawnedMonster("Sadism",new(100,100,0),0,0,1000);
        Assert.NotNull(Game.PoisonOnHitLocked(c,shortened,hit,true,false,raw,null,sadism,100,new(1)));
        double first=shortened.Poisons.Until;
        Assert.Equal(.45,first-100,8);
        Assert.NotNull(Game.PoisonOnHitLocked(c,shortened,hit,true,false,raw,null,sadism,100,new(1)));
        double second=shortened.Poisons.Until;
        Assert.Equal(.465,second-100,8);
        int tick=shortened.Poisons.Advance(first,1);
        Assert.Equal(1,shortened.Poisons.Stacks(first));
        tick+=shortened.Poisons.Advance(second,1);
        Assert.Equal(0,shortened.Poisons.Stacks(second));
        Assert.Equal((int)Math.Floor(sadism.Min*(first+second-200)+1e-9),tick);
        Assert.Equal(0,shortened.Poisons.Advance(second+1,1));
        owner.Clear(); merged[K(less)]=-100;
        var zero=MonsterPoisons.Settings.From(AilmentPanelStats.Compute(new("spell",true),
            DamagePacket.Of(DamageType.Physical,100,100),new(),n=>Get(merged,n)),"spell",n=>Get(merged,n));
        Assert.True(zero.Min>0); Assert.Equal(0,zero.Seconds);
        var refused=new SpawnedMonster("zero duration",new(100,100,0),0,0,1000);
        Assert.Null(Game.PoisonOnHitLocked(c,refused,hit,true,false,raw,null,zero,100,new(1)));
        Assert.False(refused.Poisons.Poisoned); Assert.Equal(0,owner.Assassin.RecentPoisons(100));
    }

    public void Catalog_choice_and_paid_refund_reload_preserve_ruthless_sources()
    {
        using var s=Setup(); var c=s.Conn;
        var catalog=new AscendancyCatalog();var rules=new AscendancyRules(catalog);
        var d=catalog.Find(c.MainAscendancy.Selection!)!;
        c.MainAscendancy=rules.Select(d.BaseClass,new(),d.Identity);
        foreach(var trial in new[]{"normal","cruel","merciless","eternal"}) c.MainAscendancy=rules.Award(d.BaseClass,c.MainAscendancy,trial);
        c.MainAscendancy=rules.Allocate(d.BaseClass,c.MainAscendancy,[23024,19598]);
        Assert.Equal(20,Get(s.Read(),OrdinaryAssassinRules.CriticalPoison));
        var refund=rules.Refund(d.BaseClass,c.MainAscendancy,[19598],0,0); Assert.Equal(5,refund.Cost);
        CharacterSaves.SetMainAscendancy(c.Character!,refund.Progress);
        c.MainAscendancy=CharacterSaves.Load(c.Character!)!.MainAscendancy!;
        Assert.Equal(0,Get(s.Read(),OrdinaryAssassinRules.CriticalPoison));
    }

    public void Unaffected_ignite_retains_presence_and_does_not_accumulate_fractional_damage()
    {
        var ailments=new PlayerElementalAilments();
        var raw=new Dictionary<string,int>{{"unaffected_by_damaging_ailments",1}};
        int Read(string n)=>raw.GetValueOrDefault(n);
        ailments.Inflict(PlayerElementalAilments.Kind.Ignite,100,10,0,Read,new(1));
        Assert.Equal(0,ailments.Advance(0,1,Read,0,75,false)); Assert.True(ailments.Snapshot(1).Ignite);
        raw.Clear(); Assert.Equal(100,ailments.Advance(1,2,Read,0,75,false));
    }

    public void Accepted_critical_hit_grants_owned_nonrefreshing_elusive_and_retirement_removes_native_buff()
    {
        using var s=Setup();var c=s.Conn;
        s.Set("gain_elusive_on_crit_%_chance",100); s.Set("elusive_effect_+%",100);
        s.Set("self_take_no_extra_damage_from_critical_strikes_while_affected_by_elusive",1);
        var raw=s.Read(); var target=new SpawnedMonster("target",new(100,100,0),0,0,1000);
        Game.AilmentsOnHitLocked(c,target,DamagePacket.Of(DamageType.Physical,10,10),false,true,150,raw,null,0,new(1));
        Assert.Equal(60,Game.OrdinaryAssassinMovement(c));
        Assert.Equal(100,Game.SelfCriticalReduction(c));
        Assert.True(Game.OrdinaryAssassinAvoidHit(c,raw,0,new Zero()));
        Assert.NotNull(Game.OrdinaryAssassinPresentation(c));
        var owner=Game.ResourceEffectsOf(c); Assert.NotEqual(0,owner.ElusiveBuff);
        s.Time.At(1);
        Game.AilmentsOnHitLocked(c,target,DamagePacket.Of(DamageType.Physical,10,10),false,true,150,raw,null,1,new(1));
        Assert.Equal(54,Game.OrdinaryAssassinMovement(c)); // second crit did not refresh
        owner.Freeze(1);owner.Resume(100); Assert.Equal(1.8,owner.Assassin.ElusiveEffect(100),8);
        s.Time.At(100); Assert.NotNull(Game.OrdinaryAssassinPresentation(c));
        ushort buff=owner.ElusiveBuff; c.RetireResourceEffects();
        Assert.Contains((Program.PlayerId,buff),c.RetiredResourceBuffs);
        s.Lifetime.Stop(ex=>throw ex);
        Assert.Equal(0,Game.OrdinaryAssassinMovement(c));
    }
    sealed class Zero:Random { public override int Next(int maxValue)=>0; public override int Next(int minValue,int maxValue)=>minValue; }
    public void Opportunistic_more_and_additive_incoming_bucket_and_effective_maximum_instant_leech_are_connected()
    {
        using var s=Setup();var c=s.Conn;c.PlayerLife=500;
        s.Set(OrdinaryAssassinRules.NearbyDamage,25);s.Set(OrdinaryAssassinRules.NearbyTaken,-35);
        s.Set("leech_%_is_instant_per_maximum_power_charge",3);s.Set("max_power_charges",1);
        double Damage(Dictionary<uint,int> raw)=>PlayerHitDamage.Resolve(DamagePacket.Of(DamageType.Fire,100,100),n=>Get(raw,n),new(),true).MinTotal;
        Assert.Equal(125,Damage(s.Read()),8);
        var at=c.Walk.Position(0);
        c.Monsters[1]=new("rare",new(at.X,at.Y,0),0,0,1000){Rarity=MonsterRarity.Rare};
        c.Monsters[2]=new("rare",new(at.X,at.Y,0),0,0,1000){Rarity=MonsterRarity.Rare};
        var raw=s.Read(); Assert.Equal(100,Damage(raw),8);
        Assert.Equal(-35,Game.OrdinaryAssassinIncomingIncreased(c,raw,0));
        var target=c.Monsters[1];
        var leech=new LeechSources(DamagePacket.Of(DamageType.Fire,1000,1000),DamagePacket.Empty,DamagePacket.Empty);
        Game.LeechOnHitLocked(c,target,DamagePacket.Of(DamageType.Fire,100,100),100,leech);
        Assert.Equal(501u,c.PlayerLife); // 10 Life leech * (4 maximum power * 3%) = 1.2 instant
    }}
