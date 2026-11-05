using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed class AssassinIncomingCompletionTests
{

    public Task Projectile_settled_incoming_damage_reaches_current_native_Life_and_death(bool lethal, bool trimBeforeRecovery) =>
        OrdinaryGameplayChecks.IncomingResourcePublication(lethal, trimBeforeRecovery);

    public void Actual_motion_callback_settles_stationary_bleed_before_moving_interval()
    {
        using var s=Setup(); var c=s.Conn; var state=Game.ResourceEffectsOf(c).Incoming;
        c.Walk.Teleport(100,100,0); Assert.True(state.Add(AssassinIncomingAilments.Kind.Bleed,0,70,5,42));
        s.Time.At(.5); c.Walk.Order(200,100,.5); Assert.Equal(965u,c.PlayerLife);
        s.Time.At(1.5); Game.AdvanceIncomingPhysicalAilmentsLocked(c,state,0,1.5,s.Read()); Assert.Equal(755u,c.PlayerLife);
        Game.ResourceEffectsOf(c).Freeze(1.5); Game.ResourceEffectsOf(c).Resume(100);
        Assert.True(state.Bleeding(100)); Assert.Equal(755u,c.PlayerLife);
    }

    public void Executing_MonsterCleave_retains_unbaked_31_percent_with_real_poison_and_bleed_sources()
    {
        using var s=Setup(); var c=s.Conn; c.IncomingHitRandom=new Zero();
        var monster=new SpawnedMonster("cleave",new(300,300,0),0,0,1000)
        { Mods=new([],[],new(new Dictionary<string,int>{{"physical_damage_+%",50},{"base_chance_to_poison_on_hit_%",100},{"bleed_on_hit_with_attacks_%",100}})) };
        int effect=Game.SkillsFor(c).EffectRow("MonsterCleave"); Assert.True(effect>=0);
        var source=Game.IncomingMonsterAilmentSources(c,monster,effect);
        Assert.Equal(31,source("physical_damage_+%"));
        var state=Game.ResourceEffectsOf(c).Incoming;
        Assert.Equal((true,true),Apply(s,state,source:source));
        Assert.Equal(130,Tick(s,state,0,1));
    }

    public void Actual_monster_spell_skips_attack_evasion_and_uses_spell_block()
    {
        using var s=Setup(); var c=s.Conn; c.IncomingHitRandom=new Zero();
        c.Pools=c.Pools with { BlockChance=100, SpellBlockChance=0 };
        var m=new SpawnedMonster("spell",new(300,300,0),0,0,1000); c.Monsters[42]=m;
        var attack=new MonsterAttack(1,1,30,Base) { Spell=true };
        var hit=Game.CommitMonsterHitLocked("incoming",c,42,m,attack,0,(300,300),(300,300),false);
        Assert.NotNull(hit); Assert.False(hit!.Blocked); Assert.True(c.PlayerLife<1000);
    }
    static OrdinaryHierophantConnectedTests.Setup Setup(bool ruthless = false)
    {
        var s = new OrdinaryHierophantConnectedTests.Setup(ruthless);
        s.Conn.MainAscendancy = new(new(AscendancyFamily.Normal,"Assassin",AscendancyCatalog.Revision));
        return s;
    }
    static AssassinIncomingAilments State(ClientState c) => new(Game.ResourceEffectsOf(c));
    static DamagePacket Base => DamagePacket.Of(DamageType.Physical,100,100);
    static Func<string,int> Source(params (string Name,int Value)[] values) => n => values.Where(v => v.Name == n).Sum(v => v.Value);
    static (bool Poison,bool Bleed) Apply(OrdinaryHierophantConnectedTests.Setup s, AssassinIncomingAilments a,
        bool spell = false, bool crit = false, bool block = false, double at = 0, Func<string,int>? source = null) =>
        Game.ApplyIncomingPhysicalAilmentsLocked(s.Conn,a,Base,spell,crit,block,at,s.Read(),
            source ?? Source(("base_chance_to_poison_on_hit_%",100),("bleed_on_hit_with_attacks_%",100)),42);
    static int Tick(OrdinaryHierophantConnectedTests.Setup s, AssassinIncomingAilments a, double from,double to) =>
        Game.AdvanceIncomingPhysicalAilmentsLocked(s.Conn,a,from,to,s.Read());



    public void Real_admission_ticks_life_and_unaffected_retains_presence_until_source_refund(bool ruthless)
    {
        using var s=Setup(ruthless); var c=s.Conn; var a=State(c);
        s.Set("unaffected_by_damaging_ailments",1);
        Assert.Equal((true,true),Apply(s,a)); Assert.Equal(0,Tick(s,a,0,1));
        Assert.NotEmpty(a.Presentation(c,1)); Assert.Empty(a.Presentation(c,1));
        Assert.Equal(1000u,c.PlayerLife); Assert.Equal(1,a.Poisons(1)); Assert.True(a.Bleeding(1));
        s.Set("unaffected_by_damaging_ailments",0);
        Assert.Equal(100,Tick(s,a,1,2)); Assert.Equal(900u,c.PlayerLife);
        Assert.Equal(0,a.Poisons(2)); Assert.True(a.Bleeding(2));
        Assert.Equal(0,Tick(s,a,1,2)); // Replayed scheduler interval cannot earn again.
        Assert.Equal(210,Tick(s,a,2,5)); Assert.False(a.Bleeding(5));
        Assert.NotEmpty(a.Presentation(c,5));
        foreach (var test in new (double Seconds, (string Name,int Value)[] Stats)[] {
            (20, [("self_poison_duration_+%",900)]),
            (1.5, [("self_poison_duration_+%",50),("self_poison_duration_+%_final",-50)]),
            (2.2, [("base_all_ailment_duration_on_self_+%",20),("self_damaging_ailment_duration_+%",10),
                ("self_damaging_ailment_duration_+%_per_barkskin_stack",-5),("current_barkskin_stacks",2),
                ("duration_of_ailments_on_self_+%_per_fortification",-2)]),
            (4, [("keystone_lethe_shade",1)]),
            (2.04, [("self_poison_duration_+%_final",-30),("pantheon_shakari_self_poison_duration_+%_final",45)]),
            (3, [("self_poison_duration_+%",50),("combined_poison_duration_on_self_+%",900),
                ("combined_poison_duration_on_self_+%_final",900)]),
            (0, [("self_poison_duration_+%",-100)]),
            (0, [("self_poison_duration_+%_final",-100)]) })
        {
            using var duration=Setup(ruthless); var incoming=State(duration.Conn);
            foreach (var stat in test.Stats) duration.Set(stat.Name,stat.Value);
            if(test.Stats.Any(stat=>stat.Name=="duration_of_ailments_on_self_+%_per_fortification"))
                Assert.True(Game.FortificationOf(duration.Conn).State.TryGrantFromHit(0,5,0));
            duration.Set("unaffected_by_damaging_ailments",1);
            Assert.Equal((test.Seconds>0,false),Apply(duration,incoming,spell:true));
            if(test.Seconds==0) { Assert.Equal(0,incoming.Poisons(0)); continue; }
            Assert.InRange(Assert.Single(incoming.Capture(0).Effects).Until,test.Seconds-1e-9,test.Seconds+1e-9);
            Assert.Equal(1,incoming.Poisons(test.Seconds-.001));
            Assert.Equal(0,Tick(duration,incoming,0,test.Seconds/2));
            duration.Set("unaffected_by_damaging_ailments",0);
            int damage=Tick(duration,incoming,test.Seconds/2,test.Seconds+.001);
            Assert.InRange(damage,test.Seconds*15-1,test.Seconds*15+1);
            Assert.Equal(0,incoming.Poisons(test.Seconds+.001));
            Assert.Equal(0,Tick(duration,incoming,test.Seconds+.001,test.Seconds+1));
        }
    }

    public void Poison_stacks_and_critical_base_is_separate_from_hit_critical_multiplier()
    {
        using var s=Setup(); var a=State(s.Conn);
        Assert.Equal((true,false),Apply(s,a,spell:true));
        Assert.Equal((true,false),Apply(s,a,spell:true,crit:true));
        Assert.Equal(2,a.Poisons(0)); Assert.Equal(69,Tick(s,a,0,1));
        Assert.Equal(931u,s.Conn.PlayerLife); // 30 + 39, never 30 * monster hit crit multiplier twice.
    }

    public void Pinned_real_monster_mod_admits_poison_without_granting_it_to_unrelated_monsters()
    {
        using var s=Setup(); var a=State(s.Conn);
        var mod=Assert.IsType<ModCatalog.Mod>(new ModCatalog().Find("OshabiCombatVineGreen"));
        var stat=Assert.Single(mod.Stats); Assert.Equal("base_chance_to_poison_on_hit_%",stat.Id);
        Assert.Equal(33,stat.Min);
        var m=new SpawnedMonster("vine",new(300,300,0),0,0,1000)
        { Mods=new([],[],new(new Dictionary<string,int>{{stat.Id,stat.Min}})) };
        s.Conn.IncomingHitRandom=new Zero();
        var source=Game.IncomingMonsterAilmentSources(s.Conn,m,-1);
        Assert.Equal((true,false),Apply(s,a,source:source)); Assert.Equal(30,Tick(s,a,0,1));
        Assert.Equal((false,false),Apply(s,a,source:Game.IncomingMonsterAilmentSources(s.Conn,
            new("plain",new(300,300,0),0,0,1000),-1)));
    }

    public void Poison_bypasses_ES_bleed_uses_ES_and_ticks_never_start_hit_recoup()
    {
        using var s=Setup(); var c=s.Conn; var a=State(c);
        s.Set("damage_taken_goes_to_mana_%",100); s.Set("physical_damage_taken_recouped_as_life_%",100);
        Apply(s,a); c.EnergyShield.SetMaximum(100,true);
        Assert.Equal(30,Tick(s,a,0,1)); Assert.Equal(970u,c.PlayerLife); Assert.Equal(30u,c.EnergyShield.Current);
        Assert.Equal(0,Game.ResourceEffectsOf(c).Mana.Take(1));
        Assert.Equal(0,c.LifeRecoup.Take(1));
        s.Set("unaffected_by_damaging_ailments",1);
        Assert.Equal(0,Tick(s,a,1,2)); Assert.Equal(970u,c.PlayerLife); Assert.Equal(30u,c.EnergyShield.Current);
    }

    public void Live_chaos_resistance_and_physical_reduction_apply_to_ticks_but_armour_and_hit_avoidance_do_not()
    {
        using var s=Setup(); var c=s.Conn; var a=State(c);
        c.Pools=c.Pools with { ChaosResistance=50 };
        s.Set("base_additional_physical_damage_reduction_%",50);
        s.Set("avoid_damage_%",100); // Hit-only, never a DoT roll.
        Apply(s,a); Assert.Equal(50,Tick(s,a,0,1)); Assert.Equal(950u,c.PlayerLife);
        Assert.Equal(0,c.RecentEnemyHits.Count(1)); Assert.Equal(0,c.LifeRecoup.Take(1));
    }

    public void No_chance_no_unrelated_poison_spell_no_bleed_and_prohibitions_and_block_are_authoritative()
    {
        using var s=Setup(); var a=State(s.Conn);
        Assert.Equal((false,false),Apply(s,a,source:Source()));
        Assert.Equal((false,false),Apply(s,a,block:true));
        Assert.Equal((false,false),Apply(s,a,source:Source(("base_chance_to_poison_on_hit_%",100),("virtual_never_poison",1))));
        Assert.Equal((false,false),Apply(s,a,spell:true,source:Source(("bleed_on_hit_with_attacks_%",100))));
        s.Set("immune_to_poison",1); s.Set("immune_to_bleeding",1);
        Assert.Equal((false,false),Apply(s,a)); Assert.Equal(0,Tick(s,a,0,1));
    }

    public void Carry_transfer_freezes_duration_and_stale_owner_pause_and_death_cannot_mutate_resources()
    {
        using var s=Setup(); var c=s.Conn; var a=State(c); Apply(s,a);
        var carry=a.Capture(.5); a.Restore(carry,100);
        c.Clock.Stop(); Assert.Equal(0,Tick(s,a,100,101)); Assert.Equal(1000u,c.PlayerLife);
        c.Clock.Start(); Assert.Equal(100,Tick(s,a,100,101));
        c.LeavingArea=true; Assert.Equal(0,Tick(s,a,101,102)); c.LeavingArea=false;
        c.RetireResourceEffects(); Assert.Equal(0,Tick(s,a,101,102)); Assert.Equal(900u,c.PlayerLife);
        Assert.Equal((false,false),Apply(s,a));
        var fresh=State(c); Apply(s,fresh); c.PlayerLife=1;
        Assert.True(Tick(s,fresh,0,1)>0); Assert.Equal(0u,c.PlayerLife);
        Assert.Equal(0,fresh.Poisons(1)); Assert.False(fresh.Bleeding(1));
    }

    public void Moving_bleed_triples_only_bleed_and_weaker_bleed_resumes()
    {
        var owner=new ResourceEffectsState(null,null); var a=new AssassinIncomingAilments(owner);
        a.Add(AssassinIncomingAilments.Kind.Bleed,0,70,1,1);
        a.Add(AssassinIncomingAilments.Kind.Bleed,0,20,5,2);
        a.Add(AssassinIncomingAilments.Kind.Poison,0,30,2,1);
        Assert.Equal((30,210),a.Integrate(0,1,_=>0,(_,_)=>1,_=>true));
        Assert.Equal((30,20),a.Integrate(1,2,_=>0,(_,_)=>1,_=>false));
    }

    public void Actual_walk_arrival_splits_bleed_ticks_without_tripling_poison()
    {
        using var s=Setup(); var c=s.Conn; var a=State(c);
        Apply(s,a); var here=c.Walk.Position(0); c.Walk.Order(here.X+10,here.Y,0);
        double arrive=c.Walk.ArrivesAt; Assert.InRange(arrive,.01,.99);
        int loss=Tick(s,a,0,1);
        Assert.InRange(loss,(int)Math.Floor(100+140*arrive)-1,(int)Math.Floor(100+140*arrive)+1);
        Assert.Equal(1000u-(uint)loss,c.PlayerLife);
    }


    public void Legal_Shadowed_Blood_allocation_and_paid_refund_changes_damage_without_removing_ailments(bool ruthless)
    {
        using var s=Setup(ruthless); var c=s.Conn;
        var catalog=new AscendancyCatalog(); var rules=new AscendancyRules(catalog);
        var definition=catalog.Find(c.MainAscendancy.Selection!)!;
        c.MainAscendancy=rules.Select(definition.BaseClass,new(),definition.Identity);
        foreach(var trial in new[]{"normal","cruel","merciless","eternal"})
            c.MainAscendancy=rules.Award(definition.BaseClass,c.MainAscendancy,trial);
        c.MainAscendancy=rules.Allocate(definition.BaseClass,c.MainAscendancy,[51402,29844]);
        var a=State(c); Assert.Equal((true,true),Apply(s,a)); Assert.Equal(0,Tick(s,a,0,1));
        Assert.True(a.Bleeding(1)); Assert.Equal(1,a.Poisons(1));
        var refund=rules.Refund(definition.BaseClass,c.MainAscendancy,[29844],0,0);
        Assert.Equal(5,refund.Cost); c.MainAscendancy=refund.Progress;
        Assert.Equal(100,Tick(s,a,1,2)); Assert.Equal(900u,c.PlayerLife);
    }

    public void Source_retirement_blocks_all_new_mutations_and_ignored_fraction_never_catches_up()
    {
        using var s=Setup(); var c=s.Conn; var a=State(c);
        a.Add(AssassinIncomingAilments.Kind.Poison,0,.9,5,42);
        s.Set("unaffected_by_damaging_ailments",1); Assert.Equal(0,Tick(s,a,0,1));
        s.Set("unaffected_by_damaging_ailments",0); Assert.Equal(0,Tick(s,a,1,2));
        s.Lifetime.Stop(ex=>throw ex);
        Assert.Equal((false,false),Apply(s,a)); Assert.Equal(0,Tick(s,a,2,3));
        Assert.Equal(1000u,c.PlayerLife);
    }


    public void Avoided_attack_and_spell_admission_precedes_real_hit_HP_history_and_presentation(bool spell)
    {
        using var s=Setup(); var c=s.Conn;
        s.Set("gain_elusive_on_crit_%_chance",100); s.Set("elusive_effect_+%",100);
        Game.OrdinaryAssassinCriticalHit(c,s.Read(),0,new Zero());
        c.IncomingHitRandom=new Zero();
        var m=new SpawnedMonster("enemy",new(300,300,0),0,0,1000);
        c.Monsters[42]=m;
        var attack=new MonsterAttack(1,1,10,Base) { Spell=spell };
        int calls=0;
        var hit=Game.CommitAssassinIncomingHit(c,0,s.Read(),()=>
        {
            calls++; return Game.CommitMonsterHitLocked("test",c,42,m,attack,0,(300,300),(300,300),false);
        });
        Assert.Null(hit); Assert.Equal(0,calls); Assert.Equal(1000u,c.PlayerLife);
        Assert.Equal(0,c.RecentEnemyHits.Count(0)); Assert.NotNull(Game.OrdinaryAssassinPresentation(c));
        s.Set("gain_elusive_on_crit_%_chance",0); Game.ClearOrdinaryAssassin(c);
        // Actual existing hit executor positive control, using deterministic always-land evasion entropy.
        c.EvasionEntropy=100;
        double at=c.Clock.Elapsed.TotalSeconds+1;
hit=Game.CommitAssassinIncomingHit(c,at,s.Read(),()=>Game.CommitMonsterHitLocked("test",c,42,m,attack,at,(300,300),(300,300),false));
        Assert.NotNull(hit); Assert.True(c.PlayerLife<1000); Assert.Equal(1,c.RecentEnemyHits.Count(at));
        Assert.NotEmpty(hit.HitPacket);
    }
    sealed class Zero:Random
    {
        public override int Next(int maxValue)=>0;
        public override int Next(int minValue,int maxValue)=>minValue;
        public override double NextDouble()=>0;
    }
}

static partial class OrdinaryGameplayChecks
{
    sealed class RecoverySchedule : IPlayerUpdateSchedule
    {
        internal double At=.5;
        internal readonly TaskCompletionSource Sampled=new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly System.Threading.Channels.Channel<TaskCompletionSource> waits=System.Threading.Channels.Channel.CreateUnbounded<TaskCompletionSource>();
        public double Now(ClientState conn)=>At;
        public double ResourceNow(ClientState conn) { Sampled.TrySetResult(); return At; }
        public Task Wait(int milliseconds,CancellationToken stopping)
        {
            var next=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            waits.Writer.TryWrite(next); return next.Task.WaitAsync(stopping);
        }
        internal async Task<TaskCompletionSource> Pending()=>await waits.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
    internal static async Task IncomingResourcePublication(bool lethal, bool trimBeforeRecovery)
    {
        var definition=new AscendancyCatalog().Definitions.Single(d=>d.Identity.Family==AscendancyFamily.Normal&&d.Name=="Assassin");
        using var h=new Harness(definition); var c=h.Conn;
        c.MainAscendancy=new(definition.Identity,[51402,29844,23024,19598,47531,21192,12850,4242],AscendancyRules.AwardKeys);
        c.Walk.Teleport(100,100,0); if(lethal)c.PlayerLife=20;
        var incoming=Game.ResourceEffectsOf(c).Incoming;
        Assert.True(incoming.Add(AssassinIncomingAilments.Kind.Bleed,0,70,10,42));
        var before=Game.CharacterPlayerStats(c);
        Assert.Equal(1,before[Key("unaffected_by_damaging_ailments")]);
        Game.AdvanceIncomingPhysicalAilmentsLocked(c,incoming,0,.5,before);
        var refund=new AscendancyRules(new()).Refund(definition.BaseClass,c.MainAscendancy,[29844],0,0);
        Assert.Equal(5,refund.Cost); c.MainAscendancy=refund.Progress;
        var schedule=new RecoverySchedule(); h.Time.At=.5;
        var updates=new PlayerUpdatesState(Game.CombatLifetime!,schedule); c.PlayerUpdatesState=updates;
        Assert.True(c.PlayerUpdatesWorker.TryStart(c,updates.Rules,null,updates,out long generation)); updates.Generation=generation;
        var recovery=Game.RunResourceRecovery("incoming publication",h.Wire,h.Ciphers,c,updates);
        try
        {
            var tick=await schedule.Pending();
            var gate=Program.SendGates.GetValue(h.Ciphers,_=>new SemaphoreSlim(1,1)); await gate.WaitAsync();
            try
            {
                h.Time.At=1;
                using(Game.ProjectileMutation(c,advance:false)) { }
                Assert.Equal(lethal?0u:965u,c.PlayerLife);
                if(trimBeforeRecovery)
                {
                    Game.LeechContext(c); // The independent player ticker can run before recovery.
                    Assert.False(Game.LeechOf(c).InstantLifeChanged);
                }
                schedule.At=1; tick.SetResult();
                await schedule.Sampled.Task.WaitAsync(TimeSpan.FromSeconds(5));
                // ResourceNow is read inside the worker's combat lock. Acquire it after that calculation,
                // then commit another interval while network output is blocked at the real cipher gate.
                using(Game.ProjectileMutation(c,advance:false))
                {
                    if(!lethal)
                    {
                        h.Time.At=1.5;
                        Game.IncomingMotion(c,1.5,false);
                        Assert.Equal(930u,c.PlayerLife);
                    }
                }
            }
            finally { gate.Release(); }
            var nextTick=await schedule.Pending(); // The complete resource iteration has finished publication.
            uint[] Lives()=>Program.HistoryFor(h.Ciphers).Lines().Where(l=>l.Contains("=> 0x01b1"))
                .Select(l=>Convert.FromHexString(l.Split(" B ")[1].TrimEnd('…')))
                .Where(p=>p.Length>=21&&p[20]==0).Select(p=>System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(p.AsSpan(12,4))).ToArray();
            var life=Lives();
            Assert.Contains(lethal?0u:930u,life);
            Assert.DoesNotContain(965u,life);
            if(lethal)
            {
                Assert.Equal(c.IncomingHitEpoch,c.DeathNotificationEpoch);
                int publishedEpoch=c.DeathNotificationEpoch;
                h.Time.At=1.75; schedule.At=1.75; nextTick.SetResult();
                _=await schedule.Pending();
                Assert.Equal(publishedEpoch,c.IncomingHitEpoch);
                Assert.Equal(publishedEpoch,c.DeathNotificationEpoch);
                Assert.Single(Lives(),v=>v==0);
            }
        }
        finally
        {
            updates.Stop.Cancel();
            try { await recovery.WaitAsync(TimeSpan.FromSeconds(5)); } catch(OperationCanceledException) { }
            c.RetirePlayerUpdates();
        }
    }
}
