using System.Net;
using System.Net.Sockets;
using GameServer.Core.Projectiles;
using GameServer.Core.Protocol.Login;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed class AssassinCriticalExecutionTests
{


    public void Deathmarked_uses_pinned_grant_replaces_and_removes_with_source_and_respects_admission(bool ruthless)
    {
        using var s = new OrdinaryHierophantConnectedTests.Setup(ruthless); Select(s); var c=s.Conn;
        var target=MarkedTarget(MonsterRarity.Rare);c.Monsters[10]=target;
        s.Set("local_display_trigger_level_X_assassins_mark_when_you_critically_hit_rare_or_unique_enemy_with_attacks",30);
        Game.ChargesOnHit(c,target,true,false);Assert.Null(Game.CurrentMark(c)); // Display alone grants nothing.
        c.MainAscendancy=new(new(AscendancyFamily.Normal,"Assassin",AscendancyCatalog.Revision),[48239,55686],[]);
        var grant=Game.DeathmarkedGrant(c)!.Value;Assert.Equal(30,grant.Level);Assert.Equal(11199,grant.EffectsPerLevelRow);
        c.PlayerMana=0;
        Game.ChargesOnHit(c,target,true,true);Assert.Null(Game.CurrentMark(c));
        Game.ChargesOnHit(c,target,false,false);Assert.Null(Game.CurrentMark(c));
        Game.ChargesOnHit(c,Target(),true,false);Assert.Null(Game.CurrentMark(c));

        s.Set("your_spells_are_disabled",1);Game.ChargesOnHit(c,target,true,false);Assert.Null(Game.CurrentMark(c));
        s.Set("your_spells_are_disabled",0);
        s.Set("mark_skills_cost_no_mana",0);Game.ChargesOnHit(c,target,true,false);Assert.Null(Game.CurrentMark(c));
        s.Set("mark_skills_cost_no_mana",1);Game.ChargesOnHit(c,target,true,false);
        var mark=Assert.IsType<Game.MarkTarget>(Game.CurrentMark(c));Assert.Equal(30,mark.Source.Level);Assert.Equal(0u,c.PlayerMana);
        Assert.NotEmpty(Game.MarkPresentation(c));Assert.Empty(Game.MarkPresentation(c));
        s.Set("virtual_maximum_power_charges",7);s.Set("curse_effect_+%",20);
        int basis=Game.SkillsFor(c).EffectStats(grant.Effect,30).Single(v=>v.Item1==K("enemy_additional_critical_strike_multiplier_against_self")).Item2;
        Assert.Equal((int)(basis*2d),Game.MarkAmount(c,mark,"enemy_additional_critical_strike_multiplier_against_self")); // 10 small+70 max+20 generic.
        var immune=MarkedTarget(MonsterRarity.Unique,"immune_to_curses");c.Monsters[11]=immune;
        Game.ChargesOnHit(c,immune,true,false);Assert.Same(mark,Game.CurrentMark(c));Assert.Equal(0u,c.PlayerMana);
        var next=MarkedTarget(MonsterRarity.Unique);c.Monsters[12]=next;
        Game.ChargesOnHit(c,next,true,false);Assert.Same(next,Game.CurrentMark(c)!.Monster);Assert.NotEmpty(Game.MarkPresentation(c));
        c.MainAscendancy=new(new(AscendancyFamily.Normal,"Assassin",AscendancyCatalog.Revision));
        Assert.Null(Game.CurrentMark(c));Assert.NotEmpty(Game.MarkPresentation(c));Assert.Empty(Game.MarkPresentation(c));
    }


    public void Selected_fireball_publishes_mystical_less_before_cap_and_independent_probabilities()
    {
        using var s=new OrdinaryHierophantConnectedTests.Setup();Select(s);var c=s.Conn;
        s.Set("virtual_intelligence_for_requirements",1000);s.Set("virtual_strength_for_requirements",1000);
        s.Set("virtual_dexterity_for_requirements",1000);
        s.Set("spell_critical_strikes_bifurcate",1);s.Set(OrdinaryAssassinRules.SpellChance,-30);
        var gem=ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SkillGemFireball")!);
        Game.ItemsOf(c).Add(PlayerInventories.Weapon1,0,0,ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!) with {Sockets=[new(SocketColour.White,gem)],Links=[1]});
        var skill=Assert.Single(Game.GemSkillEffects(c),g=>g.Name=="Fireball");
        s.Set("spell_additional_critical_strike_chance_permyriad",4500); // Fireball intrinsic500 ->5000.
        var rows=Game.PlayerSkills(c).Single(g=>g.Id==skill.Id).Stats!.ToDictionary(v=>v.Key,v=>v.Value);
        Assert.Equal(3500,rows[K("spell_critical_strike_chance")]);
        Assert.Equal(5775,rows[K("spell_effective_total_chance_permyriad_for_hit_to_be_critical")]);
        Assert.Equal(1225,rows[K("spell_effective_total_chance_permyriad_for_hit_to_be_double_critical")]);
        var packet=Game.SupportedHitFor(c,skill.Id,true);
        Assert.Equal(SpellDamage.Display((packet.MinTotal,packet.MaxTotal),1,1.35).PerUse,
            rows[K("display_hundred_times_damage_per_skill_use")]);
        Assert.Equal(1.35,CriticalStrikePolicy.DamageFactor(3500,150,bifurcate:true),8);
        s.Set("spell_additional_critical_strike_chance_permyriad",11500);
        rows=Game.PlayerSkills(c).Single(g=>g.Id==skill.Id).Stats!.ToDictionary(v=>v.Key,v=>v.Value);
        Assert.Equal(8400,rows[K("spell_critical_strike_chance")]);
        s.Set("global_cannot_crit",1);
        rows=Game.PlayerSkills(c).Single(g=>g.Id==skill.Id).Stats!.ToDictionary(v=>v.Key,v=>v.Value);
        Assert.Equal(0,rows[K("spell_effective_total_chance_permyriad_for_hit_to_be_critical")]);
        s.Set("global_cannot_crit",0);
        var old=Game.ItemsOf(c).Items(PlayerInventories.Weapon1).Single();Game.ItemsOf(c).Remove(PlayerInventories.Weapon1,old.Id);
        Game.ItemsOf(c).Add(PlayerInventories.Weapon1,0,0,ItemCatalog.Create(Program.Items.Find("Metadata/Items/Weapons/TwoHandWeapon/FishingRods/FishingRod1")!)
            with {Sockets=[new(SocketColour.White,gem)],Links=[1]});
        s.Set("always_crit_while_holding_fishing_rod",1);
        skill=Assert.Single(Game.GemSkillEffects(c),g=>g.Name=="Fireball");
        rows=Game.PlayerSkills(c).Single(g=>g.Id==skill.Id).Stats!.ToDictionary(v=>v.Key,v=>v.Value);
        Assert.Equal(10000,rows[K("spell_effective_total_chance_permyriad_for_hit_to_be_critical")]);
        Assert.Equal(0,rows[K("spell_effective_total_chance_permyriad_for_hit_to_be_double_critical")]);
    }



    public async Task Protected_projectile_hit_keeps_source_critical_impale(bool bifurcate,int stored)
    {
        using var s=new OrdinaryHierophantConnectedTests.Setup();Select(s);var c=s.Conn;using var wire=new Wire();
        var session=new Game.ProjectileSession("impale",wire.Stream,wire.Ciphers,c);c.ProjectileRuntimeState=session;
        try
        {
            var target=MarkedTarget(MonsterRarity.Normal,"self_take_no_extra_damage_from_critical_strikes");c.Monsters[10]=target;
            var shot=new ProjectileOffense(DamagePacket.Of(DamageType.Physical,100,100),true,default,10000,150,"impale test")
            {CriticalRoll=new(0),AssassinCriticalSources=n=>n==OrdinaryAssassinRules.NotLowMultiplier?100:n=="spell_critical_strikes_bifurcate"&&bifurcate?1:0,ImpaleSettings=new(100)};
            var use=new ProjectileUse(new("impale") {Speed=1,Range=10},new(Program.PlayerId),new(Program.PlayerId),1,new(300,300),new(320,300)) {Payload=shot};
            lock(c.Monsters)
            {
                session.Runtime.Release(use);var entity=session.Runtime.Projectiles.First();
                session.Hit(new(entity,session.ActorKey(10,target),0,"direct",new(310,300)));
                Assert.Equal(9900u,target.Life);Assert.Equal(stored,target.Impale.Bonus,8);
                var melee=MarkedTarget(MonsterRarity.Normal,"self_take_no_extra_damage_from_critical_strikes");
                int source=Game.OwnerSourceCriticalMultiplier(melee,150,new(bifurcate?2:1),shot.AssassinCriticalSources);
                Game.WithImpaleHitLocked(c,melee,100,DamagePacket.Of(DamageType.Physical,100,100),true,source,new(100));
                Assert.Equal(stored,melee.Impale.Bonus,8);
            }
        }
        finally {session.Invalidate();await session.OutputCompleted;}
    }
    static SpawnedMonster MarkedTarget(MonsterRarity rarity,string stat = "") => new("mark target",new(100,100,0),310,300,10000) { Rarity=rarity, Mods=new([],[],new(new Dictionary<string,int>{{stat,1}})) };
    sealed class Wire : IDisposable
    {
        readonly TcpListener listener = new(IPAddress.Loopback, 0);
        readonly TcpClient writer = new();
        readonly TcpClient reader;
        public NetworkStream Stream => writer.GetStream();
        public readonly ConnectionCiphers Ciphers = new(new byte[64], false);
        public Wire()
        {
            listener.Start(); var accepting = listener.AcceptTcpClientAsync();
            writer.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            reader = accepting.GetAwaiter().GetResult();
        }
        public void Dispose() { reader.Dispose(); writer.Dispose(); listener.Stop(); }
    }
    static uint K(string name) => Program.Stats.Key(name)!.Value;
    static SpawnedMonster Target(int life = 10000) => new("critical target", new(100,100,0), 310,300,10000) { Life = (uint)life };
    static void Select(OrdinaryHierophantConnectedTests.Setup s) => s.Conn.MainAscendancy =
        new(new(AscendancyFamily.Normal,"Assassin",AscendancyCatalog.Revision));


    public async Task Inquisitor_sources_and_assassin_release_compose_with_live_mark_and_refund()
    {
        using var s=new OrdinaryHierophantConnectedTests.Setup();Select(s);var c=s.Conn;using var wire=new Wire();
        c.MainAscendancy=new(new(AscendancyFamily.Normal,"Assassin",AscendancyCatalog.Revision),[48239,55686],[]);
        foreach(var attribute in new[]{"strength","dexterity","intelligence"})s.Set($"virtual_{attribute}_for_requirements",1000);
        s.Set(InquisitorOffense.Justice,1);s.Set(InquisitorOffense.Battlemage,1);s.Set(InquisitorOffense.Providence,1);
        s.Set("spell_critical_strikes_bifurcate",1);s.Set(OrdinaryAssassinRules.SpellChance,-30);
        var gem=ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SkillGemFireball")!);
        Game.ItemsOf(c).Add(PlayerInventories.Weapon1,0,0,ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!)
            with {Sockets=[new(SocketColour.White,gem)],Links=[1]});
        var skill=Assert.Single(Game.GemSkillEffects(c),g=>g.Name=="Fireball");
        var session=new Game.ProjectileSession("combined",wire.Stream,wire.Ciphers,c);c.ProjectileRuntimeState=session;
        try
        {
            var shot=(ProjectileOffense)Game.CaptureGemProjectile(c,session,skill.Id,skill,(500,300),new(0)).Payload!;
            var raw=Game.FinalPlayerStats(c).ToDictionary(v=>v.Item1,v=>v.Item2);
            var expected=SpellCriticalStrike.ChanceExact(Game.WeaponProfileFor(c,activeSet:true).Critical,
                n=>raw.GetValueOrDefault(K(n)),n=>Game.SupportedSkill(c,skill.Id).Stats.GetValueOrDefault(K(n)),c.Pools.SpellCriticalStrikeChancePct);
            Assert.Equal(expected,shot.CriticalChance);
            var target=MarkedTarget(MonsterRarity.Rare);c.Monsters[10]=target;
            Game.ChargesOnHit(c,target,true,false);Assert.NotNull(Game.CurrentMark(c));
            Assert.True(Game.CriticalChanceOn(c,10,shot.CriticalChance,null).Numerator*shot.CriticalChance.Denominator
                > shot.CriticalChance.Numerator*Game.CriticalChanceOn(c,10,shot.CriticalChance,null).Denominator);
            s.Set(InquisitorOffense.Justice,0);s.Set(InquisitorOffense.Battlemage,0);
            s.Set("spell_critical_strikes_bifurcate",0);
            Assert.Equal(expected,shot.CriticalChance);Assert.Equal(1,shot.AssassinCriticalSources!("spell_critical_strikes_bifurcate"));
            c.MainAscendancy=new(new(AscendancyFamily.Normal,"Assassin",AscendancyCatalog.Revision));
            Assert.Null(Game.CurrentMark(c));Assert.Equal(expected,Game.CriticalChanceOn(c,10,shot.CriticalChance,null));
        }
        finally {session.Invalidate();await session.OutputCompleted;}
    }


    public async Task Released_real_fireball_retains_exact_chance_and_bonus_across_refund_and_live_target_life()
    {
        using var s = new OrdinaryHierophantConnectedTests.Setup(); Select(s); var c=s.Conn;
        using var wire = new Wire();
        s.Set("virtual_strength_for_requirements",1000); s.Set("virtual_dexterity_for_requirements",1000);
        s.Set("virtual_intelligence_for_requirements",1000);
        s.Set("spell_additional_critical_strike_chance_permyriad",20000);
        s.Set("spell_critical_strikes_bifurcate",1); s.Set(OrdinaryAssassinRules.SpellChance,-30);
        s.Set(OrdinaryAssassinRules.NotLowMultiplier,100);
        s.Set("base_chance_to_poison_on_hit_%",100); s.Set("all_damage_can_poison",1);
        s.Set(OrdinaryAssassinRules.RecentPoisonDuration,5);
        s.Set("add_power_charge_on_critical_strike_%",100);
        var gem=ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SkillGemFireball")!);
        var item=ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!);
        Game.ItemsOf(c).Add(PlayerInventories.Weapon1,0,0,item with { Sockets=[new ItemSocket(SocketColour.White,gem)],Links=[1] });
        var skill=Assert.Single(Game.GemSkillEffects(c),g=>g.Name=="Fireball");
        var session=new Game.ProjectileSession("test",wire.Stream,wire.Ciphers,c); c.ProjectileRuntimeState=session;
        try
        {
            ProjectileUse use;
            using(Game.BeginOrdinaryUse(c))
            {
                Assert.True(Game.PayMana(c,skill.Id,out _,out _));
                use=Game.CaptureGemProjectile(c,session,skill.Id,skill,(500,300),new(0));
            }
            var captured=(ProjectileOffense)use.Payload!;
            // Both shared draws exceed an incorrectly pre-capped 70% chance.
            captured.CriticalRoll!.Successes(9000,new Sequence(9000,9000),true);
            Assert.True(captured.CriticalChance.Numerator > 10000 * captured.CriticalChance.Denominator);
            var shot=captured with { Hit=DamagePacket.Of(DamageType.Fire,100,100), AreaHit=null, CriticalMultiplier=150 };
            s.Set("spell_critical_strikes_bifurcate",0);s.Set(OrdinaryAssassinRules.NotLowMultiplier,0);
            var high=Target(); var low=Target(5000); c.Monsters[10]=high;c.Monsters[11]=low;
            lock(c.Monsters)
            {
                session.Runtime.Release(use with { Payload=shot,ProjectilePayloads=null });
                var entity=session.Runtime.Projectiles.First();
                Assert.Equal(ProjectileHitResult.Hit,session.Hit(new(entity,session.ActorKey(10,high),0,"direct",new(310,300))));
                Assert.Equal(ProjectileHitResult.Hit,session.Hit(new(entity,session.ActorKey(11,low),0,"direct",new(310,300))));
                Assert.Equal(9600u,high.Life); // 100 + 2*(250-100), not 2*250
                Assert.Equal(4800u,low.Life);  // pre-hit exactly 50% selects the other multiplier
                Assert.Equal(1,high.Poisons.Stacks(0));Assert.Equal(1,low.Poisons.Stacks(0));
                Assert.Equal(2,Game.OrdinaryAssassinOf(c).RecentPoisons(0));
                Assert.Equal(2,Game.ChargesOf(c).State.Held(ChargeType.Power));
                s.Set("global_cannot_crit",1);
                var prohibited=Target();c.Monsters[12]=prohibited;
                session.Hit(new(entity,session.ActorKey(12,prohibited),0,"area",new(310,300)));
                Assert.Equal(9900u,prohibited.Life);
            }
        }
        finally { session.Invalidate(); await session.OutputCompleted; }
    }





    public async Task Actual_rf_burn_uses_one_opportunistic_factor(bool ruthless,int nearby,int expected,int resistance)
    {
        using var s=new OrdinaryHierophantConnectedTests.Setup(ruthless);Select(s);var c=s.Conn;
        using var wire=new Wire();
        s.Set(OrdinaryAssassinRules.NearbyDamage,ruthless?20:25);
        var target=new SpawnedMonster("rf target",new(100,100,0),310,300,10000){Rarity=nearby==0?MonsterRarity.Normal:MonsterRarity.Rare, Mods=new([],[],new(new Dictionary<string,int>{{"base_fire_damage_resistance_%",resistance}}))};c.Monsters[10]=target;
        if(nearby==2)c.Monsters[11]=new("nearby",new(100,100,0),320,300,10000){Rarity=MonsterRarity.Unique};
        // 1000 maximum life * 600 / 6000 = 100 outgoing DPS; no self-burn in this method.
        uint before=c.PlayerLife;
        await Game.BurnAround("test",wire.Stream,wire.Ciphers,c,new(0,0,null,1,600,0,20),1);
        Assert.Equal(10000u-(uint)expected,target.Life); Assert.Equal(before,c.PlayerLife);
        s.Set("base_chance_to_ignite_%",100);var raw=s.Read();
        var ignite=Game.IgniteFromHit(100,false,n=>Program.Stats.Key(n) is uint k?raw.GetValueOrDefault(k):0,1,target,new Random(1));
        Assert.NotNull(ignite);Assert.Equal(90*(nearby<2?(ruthless?1.2:1.25):1),ignite.Value.DamagePerSecond,6);
    }


    public void Uncapped_rational_conditions_luck_and_noextra_keep_status_separate()
    {
        using var s=new OrdinaryHierophantConnectedTests.Setup();Select(s);var c=s.Conn;
        s.Set("spell_critical_strikes_bifurcate",1);s.Set(OrdinaryAssassinRules.SpellChance,-30);
        s.Set(OrdinaryAssassinRules.LowChance,100);s.Set(OrdinaryAssassinRules.NotLowMultiplier,100);
        var low=Target(5000);var high=Target(5001); var raw=s.Read();
        var exact=CriticalChanceFinal.Exact(12000,0,_=>0,null);
        Assert.Equal(8400,exact.More(-30).Project());
        Assert.Equal(10000,exact.More(100).More(-30).Project());
        var rolls=new SkillCriticalRoll(1);var random=new Sequence(9000,1000,9000,2000);
        Assert.Equal(2,rolls.Successes(3000,random,true));Assert.Equal(4,random.Calls);
        Assert.Equal(2,rolls.Successes(3000,random,true));Assert.Equal(4,random.Calls);
        Assert.Equal(0,rolls.Successes(500,random,true));
        var result=Game.ResolveOwnerCritical(c,low,exact,true,null,new(0),raw);
        Assert.Equal(2,result.Successes);
        Assert.Equal(400,Game.OwnerCriticalMultiplier(high,150,result,Game.OrdinaryAssassinSources(c,raw)));
        high=new SpawnedMonster("no extra",new(100,100,0),310,300,10000){ Mods=new([],[],new(new Dictionary<string,int>{{"self_take_no_extra_damage_from_critical_strikes",1}})) };
        Assert.Equal(100,Game.OwnerCriticalMultiplier(high,150,result,Game.OrdinaryAssassinSources(c,raw)));
        Assert.True(result.Critical);
        s.Set("global_cannot_crit",1);
        Assert.Equal(0,Game.ResolveOwnerCritical(c,low,exact,true,null,new(0),s.Read()).Successes);
    }



    public void Catalog_exclusive_choice_refund_and_pre_hit_boundary(int option, bool lowChance)
    {
        using var s=new OrdinaryHierophantConnectedTests.Setup();Select(s);var c=s.Conn;
        var catalog=new AscendancyCatalog(); var rules=new AscendancyRules(catalog);
        var d=catalog.Find(c.MainAscendancy.Selection!)!;Assert.Equal(21,d.Nodes.Count);
        c.MainAscendancy=rules.Select(d.BaseClass,new(),d.Identity);
        foreach(var award in new[]{"normal","cruel","merciless","eternal"})c.MainAscendancy=rules.Award(d.BaseClass,c.MainAscendancy,award);
        var queue=new Queue<ushort[]>();queue.Enqueue([d.StartNode]);var seen=new HashSet<ushort>();ushort[] path=[];
        while(queue.TryDequeue(out var candidate))
        {
            ushort tail=candidate[^1];if(!seen.Add(tail))continue;
            if(tail==(ushort)option){path=candidate;break;}
            foreach(ushort next in d.Nodes[tail].Adjacent)
                if(!d.Nodes[next].IsChoiceOption || next==option)queue.Enqueue([..candidate,next]);
        }
        Assert.NotEmpty(path);c.MainAscendancy=rules.Allocate(d.BaseClass,c.MainAscendancy,path.Skip(1));
        var raw=s.Read(); Assert.Equal(100,raw[K(lowChance?OrdinaryAssassinRules.LowChance:OrdinaryAssassinRules.NotLowChance)]);
        Assert.Throws<AscendancyRuleException>(()=>rules.Allocate(d.BaseClass,c.MainAscendancy,[(ushort)(lowChance?21264:18335)]));
        var get=Game.OrdinaryAssassinSources(c,raw);
        Assert.Equal(lowChance?100:0,get(OrdinaryAssassinRules.LowChance));
        Assert.Equal(lowChance?0:100,get(OrdinaryAssassinRules.NotLowChance));
        var action=new SkillCriticalRoll(0);action.Successes(5000,new Sequence(9000),false);
        Assert.Equal(lowChance?1:0,Game.ResolveOwnerCritical(c,Target(5000),5000,false,null,action,raw).Successes);
        Assert.Equal(lowChance?0:1,Game.ResolveOwnerCritical(c,Target(5001),5000,false,null,action,raw).Successes);
        var refund=rules.Refund(d.BaseClass,c.MainAscendancy,[(ushort)option],0,0);Assert.Equal(5,refund.Cost);
        CharacterSaves.SetMainAscendancy(c.Character!,refund.Progress);
        c.MainAscendancy=CharacterSaves.Load(c.Character!)!.MainAscendancy!;
        Assert.Equal(0,s.Read().GetValueOrDefault(K(lowChance?OrdinaryAssassinRules.LowChance:OrdinaryAssassinRules.NotLowChance)));
    }


    public async Task Paid_area_contact_keeps_two_success_damage_and_actual_single_poison_admission()
    {
        using var s=new OrdinaryHierophantConnectedTests.Setup();Select(s);var c=s.Conn;
        using var wire=new Wire();
        s.Set("virtual_strength_for_requirements",1000);s.Set("virtual_dexterity_for_requirements",1000);s.Set("virtual_intelligence_for_requirements",1000);
        s.Set("spell_additional_critical_strike_chance_permyriad",20000);s.Set("spell_critical_strikes_bifurcate",1);
        s.Set(OrdinaryAssassinRules.SpellChance,-30);s.Set("all_damage_can_poison",1);s.Set("base_chance_to_poison_on_hit_%",100);
        var gem=ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SkillGemIceNova")!);
        var item=ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!);
        Game.ItemsOf(c).Add(PlayerInventories.Weapon1,0,0,item with { Sockets=[new ItemSocket(SocketColour.White,gem)],Links=[1] });
        var skill=Assert.Single(Game.GemSkillEffects(c),g=>g.Name=="Ice Nova");
        var target=Target();c.Monsters[10]=target;
        var poison=Game.SupportedPoison(c,skill.Id,true,s.Read());Assert.True(poison.CriticalMin>0 && poison.Seconds>0,poison.ToString());
        var packet=Game.SupportedHit(c,skill.Id);int multiplier=new CriticalStrikeResult(2).Multiplier(Game.SupportedCriticalMultiplier(c,skill.Id));
        using(Game.BeginOrdinaryUse(c))
        {
            Assert.True(Game.PayMana(c,skill.Id,out _,out _));
            await Game.AreaSkillHit("test",wire.Stream,wire.Ciphers,c,skill.Id,skill,(100,100),(100,100),immediate:true,criticalRoll:new(0));
        }
        Assert.InRange(10000-target.Life,Math.Floor(packet.MinTotal*multiplier/100d)-1,Math.Ceiling(packet.MaxTotal*multiplier/100d)+1);
        Assert.Equal(1,target.Poisons.Stacks(Game.ProjectileWorldTime(c)));
    }



    public void Existing_guaranteed_rod_policy_excludes_bifurcation_and_cannot_crit_wins()
    {
        using var s=new OrdinaryHierophantConnectedTests.Setup();Select(s);var c=s.Conn;
        var rod=ItemCatalog.Create(Program.Items.Find("Metadata/Items/Weapons/TwoHandWeapon/FishingRods/FishingRod1")!);
        Game.ItemsOf(c).Add(PlayerInventories.Weapon1,0,0,rod);
        s.Set("always_crit_while_holding_fishing_rod",1);s.Set("spell_critical_strikes_bifurcate",1);
        Assert.Equal(1,Game.ResolveOwnerCritical(c,Target(),0,true,null,new(1),s.Read()).Successes);
        s.Set("global_cannot_crit",1);
        Assert.Equal(0,Game.ResolveOwnerCritical(c,Target(),10000,true,null,new(1),s.Read()).Successes);
        s.Set("global_cannot_crit",0);s.Set("always_crit_while_holding_fishing_rod",0);
        c.MainAscendancy=new(new(AscendancyFamily.Normal,"Hierophant",AscendancyCatalog.Revision));
        Assert.Equal(1,Game.ResolveOwnerCritical(c,Target(),10000,true,null,new(1),s.Read()).Successes);
        var raw=s.Read();c.MainAscendancy=new(new(AscendancyFamily.Phrecia,"Surfcaster",AscendancyCatalog.Revision));
        Assert.Equal(1,Game.ResolveOwnerCritical(c,Target(),10000,true,null,new(1),raw).Successes);
    }



    public async Task Mirage_ailments_do_not_grant_owner_assassin_procs()
    {
        using var s = new OrdinaryHierophantConnectedTests.Setup();
        Select(s); var c = s.Conn; using var wire = new Wire();
        c.MainAscendancy = new(c.MainAscendancy.Selection, [28782, 19598, 48239], []);
        foreach (var name in new[] { "strength", "dexterity", "intelligence" })
            s.Set("virtual_" + name + "_for_requirements", 1000);
        s.Set("base_chance_to_poison_on_hit_%", 100);
        s.Set("bleed_on_hit_with_attacks_%", 100);
        s.Set("base_chance_to_ignite_%", 100);
        s.Set("attack_minimum_added_fire_damage", 10);
        s.Set("attack_maximum_added_fire_damage", 20);
        var arrow = ItemCatalog.Create(Program.Items.Find("Tornado Shot")!);
        var support = ItemCatalog.Create(Program.Items.Find("Mirage Archer Support")!);
        Game.ItemsOf(c).Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find("Crude Bow")!) with
            { Sockets = [new(SocketColour.White, arrow), new(SocketColour.White, support)], Links = [2] });
        var gem = Assert.Single(Game.GemSkillEffects(c), g => Game.ArrowsFor(c, g.Effect));
        var session = new Game.ProjectileSession("proxy provenance", wire.Stream, wire.Ciphers, c);
        c.ProjectileRuntimeState = session;
        try
        {
            c.Walk.Teleport(300, 300, 0);
            var target = MarkedTarget(MonsterRarity.Rare); c.Monsters[10] = target;
            Assert.NotNull(Game.MirageSignature(c, gem.Id));
            int action = ++c.ActionCount;
            Assert.True(Game.PayMana(c, gem.Id, out _, out _));
            Game.OrdinaryAcceptedUse(c, action);
            var admission = Assert.IsType<DeadeyeMirageLifecycle.Release>(Game.PaidMirageAdmission(c, action));
            Game.MirageBody body;
            lock (c.Monsters)
            {
                var use = Game.PaidMirageRelease(c, admission, gem.Id,
                    Game.CaptureGemProjectile(c, session, gem.Id, gem, (310, 300), new(0)));
                var receipt = Assert.IsType<Game.MirageReceipt>(((ProjectileOffense)use.Payload!).MirageRelease);
                body = Assert.IsType<Game.MirageBody>(Game.MirageOwnerHit(session, receipt, target));
            }
            var flushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            session.Enqueue(() => { flushed.SetResult(); return Task.CompletedTask; });
            await flushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(body.Created);
            lock (c.Monsters)
            {
                var capturedOwner = (ProjectileOffense)Game.CaptureGemProjectile(c,session,gem.Id,gem,(310,300),new(0)).Payload!;
                var proxy = Game.CaptureMirage(session, body, (310, 300));
                var capturedProxy = (ProjectileOffense)proxy.Payload!;
                Assert.True(capturedOwner.Poison.Min>0); Assert.True(capturedOwner.Bleed.Min>0);
                Assert.True(capturedOwner.Ignite.Min>0);
                foreach(var (ownerBound,proxyBound) in new[] {
                    (capturedOwner.Poison.Min,capturedProxy.Poison.Min),(capturedOwner.Poison.Max,capturedProxy.Poison.Max),
                    (capturedOwner.Poison.CriticalMin,capturedProxy.Poison.CriticalMin),(capturedOwner.Poison.CriticalMax,capturedProxy.Poison.CriticalMax),
                    (capturedOwner.Bleed.Min,capturedProxy.Bleed.Min),(capturedOwner.Bleed.Max,capturedProxy.Bleed.Max),
                    (capturedOwner.Bleed.CriticalMin,capturedProxy.Bleed.CriticalMin),(capturedOwner.Bleed.CriticalMax,capturedProxy.Bleed.CriticalMax),
                    (capturedOwner.Ignite.Min,capturedProxy.Ignite.Min),(capturedOwner.Ignite.Max,capturedProxy.Ignite.Max),
                    (capturedOwner.Ignite.CriticalMin,capturedProxy.Ignite.CriticalMin),(capturedOwner.Ignite.CriticalMax,capturedProxy.Ignite.CriticalMax) })
                    Assert.Equal(ownerBound*.6,proxyBound,8);
                Assert.Equal(capturedOwner.Poison with { Min=capturedProxy.Poison.Min,Max=capturedProxy.Poison.Max,
                    CriticalMin=capturedProxy.Poison.CriticalMin,CriticalMax=capturedProxy.Poison.CriticalMax },capturedProxy.Poison);
                Assert.Equal(capturedOwner.Bleed with { Min=capturedProxy.Bleed.Min,Max=capturedProxy.Bleed.Max,
                    CriticalMin=capturedProxy.Bleed.CriticalMin,CriticalMax=capturedProxy.Bleed.CriticalMax },capturedProxy.Bleed);
                ProjectileUse Deterministic(ProjectileUse use) => use with
                {
                    ProjectilePayloads = null,
                    Payload = ((ProjectileOffense)use.Payload!) with
                    {
                        Hit = DamagePacket.Of(DamageType.Physical, 10, 10), AreaHit = null,
                        CriticalChance = 10000, CriticalRoll = new(0), AccuracyProfile = null, AlwaysHits = true,
                        Poison = new(100, 10, 10, 10, 10, 2),
                    }
                };
                session.Runtime.Release(Deterministic(proxy));
                var entity = session.Runtime.Projectiles.First();
                Assert.Equal(ProjectileHitResult.Hit, session.Hit(new(entity, session.ActorKey(10, target), 0, "direct", new(310, 300))));
                Assert.Equal(1, target.Poisons.Stacks(0));
                Assert.Equal(0, Game.OrdinaryAssassinOf(c).RecentPoisons(0));
                Assert.Equal(0, Game.OrdinaryAssassinOf(c).ElusiveEffect(0));
                Assert.Null(Game.CurrentMark(c));

                var owner = Game.CaptureGemProjectile(c, session, gem.Id, gem, (310, 300), new(0));
                session.Runtime.Release(Deterministic(owner));
                var ownerEntity = session.Runtime.Projectiles.Last();
                Assert.Equal(ProjectileHitResult.Hit, session.Hit(new(ownerEntity, session.ActorKey(10, target), 0, "direct", new(310, 300))));
                Assert.Equal(2, target.Poisons.Stacks(0));
                Assert.Equal(1, Game.OrdinaryAssassinOf(c).RecentPoisons(0));
                Assert.True(Game.OrdinaryAssassinOf(c).ElusiveEffect(0) > 0);
                Assert.Same(target, Game.CurrentMark(c)!.Monster);
            }
        }
        finally { session.Invalidate(); await session.OutputCompleted; }
    }

    sealed class GateSources(ClientState conn,IReadOnlyDictionary<uint,int> raw):IReadOnlyDictionary<uint,int>
    {
        public bool AllReadsLocked=true;
        public bool TryGetValue(uint key,out int value){AllReadsLocked &= Monitor.IsEntered(conn.Monsters);return raw.TryGetValue(key,out value);}
        public int this[uint key]=>raw[key]; public IEnumerable<uint> Keys=>raw.Keys;
        public IEnumerable<int> Values=>raw.Values;public int Count=>raw.Count;
        public bool ContainsKey(uint key)=>raw.ContainsKey(key);
        public IEnumerator<KeyValuePair<uint,int>> GetEnumerator()=>raw.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
    }

    public void Owner_lease_and_poison_observation_hold_gate_for_the_entire_state_mutation()
    {
        using var s=new OrdinaryHierophantConnectedTests.Setup();Select(s);var c=s.Conn;
        s.Set(OrdinaryAssassinRules.RecentPoisonDuration,5);
        var checkedSources=new GateSources(c,s.Read());
        Game.OrdinaryAssassinAppliedPoison(c,checkedSources,0);
        Assert.True(checkedSources.AllReadsLocked);
        var owner=Game.ResourceEffectsOf(c);c.RetireResourceEffects();
        Game.OrdinaryAssassinAppliedPoison(c,checkedSources,1);
        Assert.Equal(0,owner.Assassin.RecentPoisons(1));
    }

    sealed class Sequence(params int[] values):Random
    {
        public int Calls;
        public override int Next(int maxValue)=>values[Calls++];
    }


    public async Task Stat_and_movement_observers_wait_for_monster_gate_through_expiry_and_application()
    {
        using var s=new OrdinaryHierophantConnectedTests.Setup();Select(s);var c=s.Conn;
        s.Set(OrdinaryAssassinRules.RecentPoisonDuration,5);s.Set(OrdinaryAssassinRules.NearbyDamage,25);
        var raw=s.Read();Game.OrdinaryAssassinAppliedPoison(c,raw,0);s.Time.At(4);
        Task[] readers;
        using var started=new CountdownEvent(2);
        using var completed=new ManualResetEventSlim();
        lock(c.Monsters)
        {
            readers=[Task.Run(()=>{started.Signal();Game.ReconcileOrdinaryAssassinSources(c,new(raw));completed.Set();}),
                Task.Run(()=>{started.Signal();Game.OrdinaryAssassinMovement(c);})];
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(completed.Wait(100));
            Assert.All(readers,t=>Assert.False(t.IsCompleted));
            c.Monsters[10]=Target();Game.OrdinaryAssassinAppliedPoison(c,raw,4);
        }
        await Task.WhenAll(readers).WaitAsync(TimeSpan.FromSeconds(5));
        lock(c.Monsters) Assert.Equal(1,Game.OrdinaryAssassinOf(c).RecentPoisons(4));
    }
}
