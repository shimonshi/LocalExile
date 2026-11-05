using System.Net;
using System.Net.Sockets;
using GameServer.Core.Projectiles;
using GameServer.Core.Protocol.Login;
using GameServer.Core.World;
using GameServer.Core.Protocol.Instance;

namespace GameServer.Gateway.Tests;

public sealed class HierophantEngineCompletionTests
{
    sealed class Midpoint : Random { public override double NextDouble() => .5; public override int Next(int maxValue) => maxValue / 2; public override int Next(int minValue,int maxValue) => (minValue+maxValue)/2; }
    sealed record DurationCase(string Name,int Attached,int Detached,(string Key,int Value)[] Inputs,string? Support=null);

    public async Task Published_Brand_durations_match_authoritative_budgets_and_supported_modifiers(bool ruthless)
    {
        DurationCase[] cases =
        [
            new("base",5000,6000,[]),
            new("generic and Brand",7500,9000,[("skill_effect_duration_+%",20),("sigil_duration_+%",30)]),
            new("secondary only",5000,9000,[("secondary_skill_effect_duration_+%",50)]),
            new("independent more",7350,8820,[("skill_effect_duration_+%",50),("support_more_duration_skill_effect_duration_+%_final",40),("support_reduced_duration_skill_effect_duration_+%_final",-30)]),
            new("native more intermediate tie",5100,6120,[("support_reduced_duration_skill_effect_duration_+%_final",-30),("support_more_duration_skill_effect_duration_+%_final",45)]),
            new("masteries",4950,5940,[("passive_mastery_more_skill_effect_duration_+%_final",10),("passive_mastery_less_skill_effect_duration_+%_final",-10)]),
            new("Swift Affliction",3750,4500,[("support_swift_affliction_skill_effect_and_damaging_ailment_duration_+%_final",-25)]),
            new("different Swiftbrand channels",2500,1200,[("support_rapid_activation_brand_skill_only_primary_duration_+%_final",-50),("support_rapid_activation_brand_skill_only_secondary_duration_+%_final",-80)]),
            new("fixed",5000,6000,[("fixed_skill_effect_duration",1),("skill_effect_duration_+%",100)],"Swiftbrand Support"),
            new("additional base",6000,7000,[("virtual_additional_skill_effect_duration",1000)]),
            new("strength once",7250,8700,[("strength",455),("skill_effect_duration_+%_per_10_strength",1)]),
            new("native aggregates",7500,9000,[("virtual_skill_effect_duration_+%",50),("skill_effect_duration_+%",200)]),
            new("increase aggregate zero",5000,6000,[("virtual_skill_effect_duration_+%",0),("skill_effect_duration_+%",200)]),
            new("aggregate zero",5000,6000,[("virtual_skill_effect_duration_pluspercent_final",0),("support_more_duration_skill_effect_duration_+%_final",40)]),
            new("float half milliseconds",1,501,[("virtual_additional_skill_effect_duration",-4999),("skill_effect_duration_+%",-50)]),
            new("real Swiftbrand",1750,2100,[],"Swiftbrand Support"),
        ];
        foreach (string name in new[] { "Storm Brand","Armageddon Brand" })
            foreach (var row in cases)
            {
                using var s=new OrdinaryHierophantConnectedTests.Setup(ruthless); using var wire=new Wire(); var c=s.Conn;
                ushort skill=Equip(s,name);
                if (row.Support is { } support)
                {
                    var host=Game.ItemsOf(c).Items(PlayerInventories.Weapon1).Single();
                    Game.ItemsOf(c).Change(PlayerInventories.Weapon1,host.Id,i=>i with
                        { Sockets=[..i.Sockets,new(SocketColour.White,ItemCatalog.Create(Program.Items.Find(support)!))],Links=[2] });
                    Assert.Single(Game.SupportedSkill(c,skill).Supports);
                }
                foreach (var (key,value) in row.Inputs) s.Set(key,value);
                var published=Game.PlayerSkills(c).Single(g=>g.Id==skill).Stats!.ToDictionary(x=>x.Item1,x=>x.Item2);
                uint attached=Program.Stats.Key(BrandDurationModifiers.Primary)!.Value;
                uint detached=Program.Stats.Key(BrandDurationModifiers.Secondary)!.Value;
                Assert.Equal(row.Attached,published[attached]); Assert.Equal(row.Detached,published[detached]);
                var session=new Game.ProjectileSession(row.Name,wire.Stream,wire.Ciphers,c); c.ProjectileRuntimeState=session;
                try
                {
                    Assert.True(Game.HierophantEngineAdmit(session,skill,Paid(c,skill),(300,300)));
                    var clock=Assert.Single(Game.HierophantEngineActors(session)).Brand!;
                    Assert.Equal(row.Attached/1000d,clock.AttachedLimit); Assert.Equal(row.Detached/1000d,clock.DetachedLimit);
                }
                finally { session.Invalidate(); await session.OutputCompleted; }
                if (row.Name=="generic and Brand")
                {
                    Game.ItemsOf(c).Add(PlayerInventories.BodyArmour,0,0,ItemCatalog.Create(Program.Items.Find("Simple Robe")!) with
                        { Sockets=[new(SocketColour.White,ItemCatalog.Create(Program.Items.Find("Spark")!))],Links=[1] });
                    var spark=Game.GemSkillEffects(c).Single(g=>g.Name=="Spark");
                    int basis=Game.SupportedSkill(c,spark.Id).Stats[Program.Stats.Key("base_skill_effect_duration")!.Value];
                    Assert.Equal((int)(basis*1.2),Game.PlayerSkills(c).Single(g=>g.Id==spark.Id).Stats!.Single(x=>x.Item1==attached).Item2);
                }
            }
    }

    public async Task Zero_Brand_duration_is_published_and_cannot_admit_or_replay_a_live_actor()
    {
        foreach (var input in new[] { (BrandDurationModifiers.Primary,0), (BrandDurationModifiers.Secondary,0),
            ("skill_effect_duration_+%",-100),("secondary_skill_effect_duration_+%",-100),
            ("support_more_duration_skill_effect_duration_+%_final",-100) })
        {
            using var s=new OrdinaryHierophantConnectedTests.Setup(); using var wire=new Wire(); var c=s.Conn;
            ushort skill=Equip(s,"Storm Brand"); s.Set(input.Item1,input.Item2);
            var rows=Game.PlayerSkills(c).Single(g=>g.Id==skill).Stats!.ToDictionary(x=>x.Item1,x=>x.Item2);
            Assert.True(rows[Program.Stats.Key(BrandDurationModifiers.Primary)!.Value]==0
                || rows[Program.Stats.Key(BrandDurationModifiers.Secondary)!.Value]==0);
            var session=new Game.ProjectileSession("zero duration",wire.Stream,wire.Ciphers,c); c.ProjectileRuntimeState=session;
            try
            {
                int action=Paid(c,skill); Assert.False(Game.HierophantEngineAdmit(session,skill,action,(300,300)));
                Assert.Empty(Game.HierophantEngineActors(session));
                c.PlayerStats.Remove(Program.Stats.Key(input.Item1)!.Value);
                var fresh=Game.PlayerSkills(c).Single(g=>g.Id==skill).Stats!.ToDictionary(x=>x.Item1,x=>x.Item2);
                Assert.Equal(5000,fresh[Program.Stats.Key(BrandDurationModifiers.Primary)!.Value]);
                Assert.Equal(6000,fresh[Program.Stats.Key(BrandDurationModifiers.Secondary)!.Value]);
                Assert.False(Game.HierophantEngineAdmit(session,skill,action,(300,300)));
                Assert.True(Game.HierophantEngineAdmit(session,skill,Paid(c,skill),(300,300)));
            }
            finally { session.Invalidate(); await session.OutputCompleted; }
        }
    }

    public async Task Brand_dispatch_and_activation_preserve_time_conditional_crits_and_owner_procs()
    {
        foreach (string name in new[] { "Storm Brand", "Armageddon Brand", "Shockwave Totem" })
        {
            using var s = new OrdinaryHierophantConnectedTests.Setup(); using var wire = new Wire();
            var c = s.Conn; ushort skill = Equip(s, name);
            var session = new Game.ProjectileSession("brand corrections", wire.Stream, wire.Ciphers, c);
            c.ProjectileRuntimeState = session;
            try
            {
                session.AdvanceTo(ProjectileRuntime.Time(10));
                if (name != "Shockwave Totem")
                {
                    var gem = Game.SupportedSkill(c, skill);
                    double fraction = Game.Animations.SpellAreaRelease(Game.SkillsFor(c).ClassFor(c.ClassRow).AnimatedObject);
                    Assert.InRange(fraction,double.Epsilon,.999999);
                    double delay = (Game.CastMs(c, gem.Effect, skill) ?? 0) / 1000d * fraction;
                    Assert.True(delay > 0);
                    Assert.True(await Game.CastGemSkill("brand dispatch", wire.Stream, wire.Ciphers, c, skill, (300,300), (310,300)));
                    session.AdvanceTo(ProjectileRuntime.Time(10 + delay) - 1);
                    Assert.Empty(Game.HierophantEngineActors(session));
                    session.AdvanceTo(ProjectileRuntime.Time(10 + delay));
                }
                else
                {
                    TotemState.Totem? totem = name == "Shockwave Totem" ? new(100, skill, (310,300), 1000, 100, null) : null;
                    if (totem is not null) c.Totems.Place(totem, 1, 10);
                    Assert.True(Game.HierophantEngineAdmit(session, skill, Paid(c,skill), (310,300), totem));
                }
                var actor = Assert.Single(Game.HierophantEngineActors(session));
                c.Pools = c.Pools with { SpellCriticalStrikeChancePct = -100, VsBlindedCriticalPct = 2000, SpellCriticalStrikeMultiplier = 250 };
                s.Set("add_power_charge_on_critical_strike_%", 100); s.Set("recharge_flasks_on_crit", 1);
                var flask = Game.ItemsOf(c).Add(PlayerInventories.Flask, 0, 0,
                    ItemCatalog.Create(Program.Items.Find("Diamond Flask")!) with { Charges = 10 });
                var ordinary = Target(c,10); var blind = Target(c,11);
                blind.RampageBlindUntil = 100;
                Assert.Equal(0, Game.SupportedReleaseCriticalChance(c,skill));
                Assert.Equal(10000, Game.SupportedCriticalChanceVsBlinded(c,skill,true)!.Value);
                actor.Target = 0;
                Game.HierophantEngineActivate(session,actor,random:new Midpoint());
                uint regular = ordinary.MaxLife - ordinary.Life, conditional = blind.MaxLife - blind.Life;
                Assert.True(regular > 0); Assert.True(conditional > regular * 2, $"{name}: {regular}/{conditional}");
                bool owner = actor.Brand is not null;
                Assert.Equal(owner ? 1 : 0, Game.ChargesOf(c).State.Held(ChargeType.Power));
                Assert.Equal(owner ? 11 : 10, Game.ItemsOf(c).Items(PlayerInventories.Flask).Single(f=>f.Id==flask.Id).Item.Charges);
            }
            finally { session.Invalidate(); await session.OutputCompleted; }
        }
        foreach(string payload in new[] { "Arc", "Frostbolt" })
        {
            using var s=new OrdinaryHierophantConnectedTests.Setup(); using var wire=new Wire(); var c=s.Conn;
            ushort skill=Equip(s,payload,totem:true); var gem=Game.SupportedSkill(c,skill);
            Assert.Contains(gem.Supports,applied=>applied.Effect.Id=="SupportSpellTotem");
            s.Set(OrdinaryPathfinderRules.DistillCost,3);
            var items=Game.ItemsOf(c);
            var flask=items.Add(PlayerInventories.Flask,0,0,ItemCatalog.Create(Program.Items.Find("Bismuth Flask")!) with { Charges=40 });
            uint mana=c.PlayerMana,life=c.PlayerLife,revision=items.NextRevision(PlayerInventories.Flask);
            int action=c.ActionCount; var target=Target(c,10);
            Assert.NotNull(Game.GemSkillInstanceDisabled(c,gem.Effect,skill));
            bool admitted=false;
            Assert.False((await Game.BeginSkillCooldownUseAsync(c,skill,gem.Effect,null,false,()=>admitted=true)).Accepted);
            Assert.False(admitted);
            Assert.False(await Game.CastGemSkill("unsupported totem",wire.Stream,wire.Ciphers,c,skill,(300,300),(310,300)));
            Assert.Equal(mana,c.PlayerMana); Assert.Equal(life,c.PlayerLife); Assert.Equal(action,c.ActionCount);
            Assert.Empty(c.SingleUseCooldowns); Assert.Empty(c.SkillReadyAt); Assert.Empty(c.Totems.All);
            Assert.Equal(target.MaxLife,target.Life);
            Assert.Equal(40,items.Items(PlayerInventories.Flask).Single(f=>f.Id==flask.Id).Item.Charges);
            Assert.Equal(revision+1,items.NextRevision(PlayerInventories.Flask));
            items.Add(PlayerInventories.BodyArmour,0,0,ItemCatalog.Create(Program.Items.Find("Simple Robe")!) with
                { Sockets=[new(SocketColour.White,ItemCatalog.Create(Program.Items.Find(payload)!))],Links=[1] });
            var ordinary=Game.GemSkillEffects(c).Single(g=>g.Effect==gem.Effect&&g.Id!=skill);
            Assert.Null(Game.GemSkillInstanceDisabled(c,ordinary.Effect,ordinary.Id));
        }
    }

    public async Task Dispatcher_Recall_resource_failure_is_atomic_and_accepted_replay_never_pays_again()
    {
        using var s=new OrdinaryHierophantConnectedTests.Setup(); using var wire=new Wire(); var c=s.Conn;
        c.Walk.Teleport(300,300,0); ushort brand=Equip(s,"Storm Brand");
        Game.ItemsOf(c).Add(PlayerInventories.BodyArmour,0,0,ItemCatalog.Create(Program.Items.Find("Simple Robe")!) with
            { Sockets=[new(SocketColour.White,ItemCatalog.Create(Program.Items.Find("Brand Recall")!))],Links=[1] });
        var recall=Game.GemSkillEffects(c).Single(g=>g.Name=="Brand Recall");
        c.MainAscendancy=new(new(AscendancyFamily.Normal,"Pathfinder",AscendancyCatalog.Revision),[59800,6038],[]);
        Assert.Equal(3,s.Read()[Program.Stats.Key(OrdinaryPathfinderRules.DistillCost)!.Value]);
        s.Set("global_cannot_crit",1);
        s.Set("spell_minimum_added_physical_damage",100); s.Set("spell_maximum_added_physical_damage",100);
        var items=Game.ItemsOf(c);
        foreach(var flask in items.Items(PlayerInventories.Flask)) items.Remove(PlayerInventories.Flask,flask.Id);
        var paidFlasks=new[]{"Bismuth Flask","Diamond Flask","Amethyst Flask"}.Select((name,i)=>
            items.Add(PlayerInventories.Flask,(byte)i,0,ItemCatalog.Create(Program.Items.Find(name)!) with { Charges=10 })).ToArray();
        var duplicate=items.Add(PlayerInventories.Flask,3,0,ItemCatalog.Create(Program.Items.Find("Amethyst Flask")!) with { Charges=10 });
        uint revision=items.NextRevision(PlayerInventories.Flask);
        void Bar(int charges)
        {
            foreach(var flask in paidFlasks) Assert.Equal(charges,items.Items(PlayerInventories.Flask).Single(f=>f.Id==flask.Id).Item.Charges);
            Assert.Equal(10,items.Items(PlayerInventories.Flask).Single(f=>f.Id==duplicate.Id).Item.Charges);
        }
        var session=new Game.ProjectileSession("Recall transaction",wire.Stream,wire.Ciphers,c); c.ProjectileRuntimeState=session;
        try
        {
            Assert.True(Game.HierophantEngineAdmit(session,brand,Paid(c,brand),(310,300)));
            var actor=Assert.Single(Game.HierophantEngineActors(session)); int action=c.ActionCount;
            async Task<(bool Accepted,CooldownUseTicket? Ticket)> Admit()=>await Game.BeginSkillCooldownUseAsync(c,recall.Id,recall.Effect,
                Game.CooldownFor(c,recall.Effect,recall.Level,recall.Quality),true,()=>++c.ActionCount);
            c.PlayerMana=7; var failed=await Admit(); Assert.False(failed.Accepted);
            Assert.Equal(action,c.ActionCount); Assert.Empty(c.SingleUseCooldowns); Assert.Equal((310,300),actor.At);
            Bar(10); Assert.Equal(++revision,items.NextRevision(PlayerInventories.Flask));
            var unpaid=Game.HierophantEngineHit(c,actor,true);
            var victim=Target(c,10);
            c.PlayerMana=1000; var accepted=await Admit(); Assert.True(accepted.Accepted); Assert.NotNull(accepted.Ticket!.Recall);
            Assert.Equal(992u,c.PlayerMana); Bar(7);
            Assert.Equal(revision+2,items.NextRevision(PlayerInventories.Flask)); revision+=2;
            Assert.True(await Game.CastGemSkill("Recall",wire.Stream,wire.Ciphers,c,recall.Id,(300,300),(300,300),cooldownUse:accepted.Ticket));
            Assert.False(await Game.CastGemSkill("Recall replay",wire.Stream,wire.Ciphers,c,recall.Id,(300,300),(300,300),cooldownUse:accepted.Ticket));
            Assert.Equal(992u,c.PlayerMana); Assert.Equal(c.Walk.Position(Game.ResourceEffectsTime(c)),actor.At);
            Assert.False((await Admit()).Accepted); Assert.Equal(992u,c.PlayerMana);
            Bar(7); Assert.Equal(++revision,items.NextRevision(PlayerInventories.Flask));
            Assert.InRange(victim.MaxLife-victim.Life,
                (uint)unpaid.Components.Sum(x=>Math.Floor(x.Range.Min+1e-9)),
                (uint)unpaid.Components.Sum(x=>Math.Floor(x.Range.Max+1e-9)));
            Assert.Equal(0,Game.HierophantEngineHit(c,actor,true)[DamageType.Chaos].Max);
        }
        finally { session.Invalidate(); await session.OutputCompleted; }
        foreach(string stale in new[]{"empty","source","epoch","action","death","module"})
        {
            using var pending=new OrdinaryHierophantConnectedTests.Setup(); var conn=pending.Conn;
            ushort skill=Equip(pending,"Brand Recall"); var gem=Game.SupportedSkill(conn,skill);
            pending.Set(OrdinaryPathfinderRules.DistillCost,3);
            var inventory=Game.ItemsOf(conn);
            foreach(var old in inventory.Items(PlayerInventories.Flask)) inventory.Remove(PlayerInventories.Flask,old.Id);
            var flask=inventory.Add(PlayerInventories.Flask,0,0,ItemCatalog.Create(Program.Items.Find("Bismuth Flask")!) with { Charges=10 });
            uint before=inventory.NextRevision(PlayerInventories.Flask);
            uint mana=conn.PlayerMana; int action=conn.ActionCount;
            using var pendingWire=new Wire();
            var pendingSession=new Game.ProjectileSession("pending Recall "+stale,pendingWire.Stream,pendingWire.Ciphers,conn);
            conn.ProjectileRuntimeState=pendingSession;
            try
            {
                await conn.FlaskBarGate.WaitAsync();
                var admission=Game.BeginSkillCooldownUseAsync(conn,skill,gem.Effect,
                    Game.CooldownFor(conn,gem.Effect,gem.Level,gem.Quality),true,()=>++conn.ActionCount);
                Assert.False(admission.IsCompleted);
                try
                {
                    if(stale=="source") inventory.Remove(PlayerInventories.Weapon1,inventory.Items(PlayerInventories.Weapon1).Single().Id);
                    if(stale=="epoch") conn.ProjectileEpoch++;
                    if(stale=="action") conn.ActionCount++;
                    if(stale=="death") conn.PlayerLife=0;
                    if(stale=="module") pending.Lifetime.Stop(ex=>throw ex);
                }
                finally { conn.FlaskBarGate.Release(); }
                var result=await admission.WaitAsync(TimeSpan.FromSeconds(5));
                if(stale=="empty")
                {
                    Assert.True(result.Accepted); Assert.Empty(result.Ticket!.Recall!.Brands);
                    Assert.Equal(mana-7,conn.PlayerMana); Assert.Equal(action+1,conn.ActionCount);
                    Assert.Equal(7,inventory.Items(PlayerInventories.Flask).Single(f=>f.Id==flask.Id).Item.Charges);
                    Assert.Equal(before+2,inventory.NextRevision(PlayerInventories.Flask));
                }
                else
                {
                    Assert.False(result.Accepted); Assert.Equal(mana,conn.PlayerMana); Assert.Empty(conn.SingleUseCooldowns);
                    Assert.Equal(action+(stale=="action"?1:0),conn.ActionCount);
                    Assert.Equal(10,inventory.Items(PlayerInventories.Flask).Single(f=>f.Id==flask.Id).Item.Charges);
                    Assert.Equal(before+1,inventory.NextRevision(PlayerInventories.Flask));
                }
            }
            finally { pendingSession.Invalidate(); await pendingSession.OutputCompleted; }
        }
    }

    public async Task Real_below_level_XP_preserves_actor_and_resource_replacement_never_revives_old_receipts()
    {
        using var s = new OrdinaryHierophantConnectedTests.Setup(); using var wire = new Wire();
        var c = s.Conn; ushort skill = Equip(s, "Storm Brand");
        var session = new Game.ProjectileSession("source", wire.Stream, wire.Ciphers, c); c.ProjectileRuntimeState = session;
        try
        {
            int action = Paid(c, skill); Assert.True(Game.HierophantEngineAdmit(session, skill, action, (300,300)));
            var old = Assert.Single(Game.HierophantEngineActors(session));
            var xp = Game.CommitGemExperience(c, 10); Assert.NotEmpty(xp.Packets); Assert.False(xp.Leveled);
            Assert.True(Game.HierophantEngineCurrent(session, old));
            c.RetireResourceEffects();
            Assert.True(Game.HierophantEngineAdmit(session, skill, Paid(c,skill), (300,300)));
            var current = Assert.Single(Game.HierophantEngineActors(session));
            Assert.NotSame(old,current); Assert.True(Game.HierophantEngineCurrent(session,current));
            Assert.False(Game.HierophantEngineCurrent(session,old));
            Assert.False(Game.HierophantEngineAdmit(session,skill,action,(300,300)));
            Game.ItemsOf(c).Remove(PlayerInventories.Weapon1,Game.ItemsOf(c).Items(PlayerInventories.Weapon1).Single().Id);
            Assert.False(Game.HierophantEngineCurrent(session,current));
        }
        finally { session.Invalidate(); await session.OutputCompleted; }
        await PendingBrandPlacement();
        await BlockedBrandCostPublication();
    }
    static async Task PendingBrandPlacement()
    {
        foreach(string change in new[]{"valid","XP","source","owner","cancel","unpaid","death","epoch","interruption","pause","late","expired"})
        {
            using var s=new OrdinaryHierophantConnectedTests.Setup(); using var wire=new Wire(); var c=s.Conn;
            ushort skill=Equip(s,"Storm Brand");
            var session=new Game.ProjectileSession("pending Brand "+change,wire.Stream,wire.Ciphers,c); c.ProjectileRuntimeState=session;
            try
            {
                session.AdvanceTo(ProjectileRuntime.Time(10));
                int action=change=="unpaid"?++c.ActionCount:Paid(c,skill);
                var use=Assert.IsType<Game.HierophantBrandUse>(Game.CaptureHierophantBrandUse(c,skill,action,0x1234));
                bool current=true;
                if(change=="late"||change=="expired") session.AdvanceTo(use.ContactAt+ProjectileRuntime.Time(change=="late"?.5:6.1));
                Game.ScheduleHierophantBrand(session,use,(300,300),()=>current);
                switch(change)
                {
                    case "XP": Assert.False(Game.CommitGemExperience(c,10).Leveled); break;
                    case "source":
                        foreach(var host in Game.ItemsOf(c).Items(PlayerInventories.Weapon1)) Game.ItemsOf(c).Remove(PlayerInventories.Weapon1,host.Id);
                        Equip(s,"Storm Brand"); break;
                    case "owner": c.RetireResourceEffects(); break;
                    case "cancel": current=false; break;
                    case "death": c.PlayerLife=0; break;
                    case "epoch": c.ProjectileEpoch++; break;
                    case "interruption": c.FreezeGeneration++; break;
                    case "pause": c.Clock.Stop(); break;
                }
                if(change=="pause")
                {
                    session.AdvanceTo(use.ContactAt+ProjectileRuntime.Time(100));
                    Assert.Equal(ProjectileRuntime.Time(10),session.Runtime.Now);
                    Assert.Empty(Game.HierophantEngineActors(session)); c.Clock.Start();
                }
                session.AdvanceTo(Math.Max(session.Runtime.Now,use.ContactAt));
                if(change is "valid" or "XP" or "late" or "pause")
                {
                    var actor=Assert.Single(Game.HierophantEngineActors(session));
                    Assert.Same(use,actor.Use); Assert.Equal((ushort)0x1234,actor.Use!.Sequence);
                    Assert.Equal(change=="late"?.5:0,actor.Brand!.DetachedUsed,5);
                }
                else
                {
                    Assert.Empty(Game.HierophantEngineActors(session));
                    if(change=="expired") Assert.False(Game.HierophantEngineAdmit(session,skill,action,(300,300)));
                }
            }
            finally { session.Invalidate(); await session.OutputCompleted; }
        }
    }
    static async Task BlockedBrandCostPublication()
    {
        foreach(string change in new[]{"late","expired","source","owner","interruption"})
        {
            using var s=new OrdinaryHierophantConnectedTests.Setup(); using var wire=new Wire(); var c=s.Conn;
            ushort skill=Equip(s,"Storm Brand");
            var session=new Game.ProjectileSession("blocked Brand cost "+change,wire.Stream,wire.Ciphers,c); c.ProjectileRuntimeState=session;
            Task<bool>? cast=null;
            var gate=Program.SendGates.GetValue(wire.Ciphers,_=>new SemaphoreSlim(1,1));
            try
            {
                session.AdvanceTo(ProjectileRuntime.Time(10));
                var gem=Game.SupportedSkill(c,skill);
                long contact=ProjectileRuntime.Time(10+(Game.CastMs(c,gem.Effect,skill)??0)/1000d
                    *Game.Animations.SpellAreaRelease(Game.SkillsFor(c).ClassFor(c.ClassRow).AnimatedObject));
                uint mana=c.PlayerMana; int action=++c.ActionCount;
                await gate.WaitAsync();
                try
                {
                    cast=Game.CastGemSkill("blocked paid Brand",wire.Stream,wire.Ciphers,c,skill,(300,300),(310,300),
                        sequence:0x3456,actionToken:action);
                    Assert.False(cast.IsCompleted); Assert.True(c.PlayerMana<mana);
                    Assert.Empty(Game.HierophantEngineActors(session));
                    session.AdvanceTo(contact+ProjectileRuntime.Time(change=="expired"?6.1:.5));
                    if(change=="source")
                    {
                        foreach(var host in Game.ItemsOf(c).Items(PlayerInventories.Weapon1)) Game.ItemsOf(c).Remove(PlayerInventories.Weapon1,host.Id);
                        Equip(s,"Storm Brand");
                    }
                    if(change=="owner") c.RetireResourceEffects();
                    if(change=="interruption") c.FreezeGeneration++;
                }
                finally { gate.Release(); }
                Assert.True(await cast.WaitAsync(TimeSpan.FromSeconds(5)));
                session.AdvanceTo(session.Runtime.Now);
                if(change=="late")
                {
                    var actor=Assert.Single(Game.HierophantEngineActors(session));
                    Assert.Equal(contact,actor.Use!.ContactAt); Assert.Equal((ushort)0x3456,actor.Use.Sequence);
                    Assert.Equal(.5,actor.Brand!.DetachedUsed,5);
                }
                else Assert.Empty(Game.HierophantEngineActors(session));
            }
            finally
            {
                try { if(cast is not null) await cast.WaitAsync(TimeSpan.FromSeconds(5)); }
                finally { session.Invalidate(); await session.OutputCompleted; }
            }
        }
    }

    public async Task Resource_replacement_rebinds_new_cast_and_never_revives_old_receipts()
    {
        using var s = new OrdinaryHierophantConnectedTests.Setup(); using var wire = new Wire();
        var c = s.Conn; ushort skill = Equip(s, "Storm Brand");
        var session = new Game.ProjectileSession("source", wire.Stream, wire.Ciphers, c); c.ProjectileRuntimeState = session;
        try
        {
            int action = Paid(c, skill); Assert.True(Game.HierophantEngineAdmit(session, skill, action, (300,300)));
            var old = Assert.Single(Game.HierophantEngineActors(session));

            Assert.True(Game.HierophantEngineCurrent(session, old));
            c.RetireResourceEffects();
            Assert.True(Game.HierophantEngineAdmit(session, skill, Paid(c,skill), (300,300)));
            var current = Assert.Single(Game.HierophantEngineActors(session));
            Assert.NotSame(old,current); Assert.True(Game.HierophantEngineCurrent(session,current));
            Assert.False(Game.HierophantEngineCurrent(session,old));
            Assert.False(Game.HierophantEngineAdmit(session,skill,action,(300,300)));
            Game.ItemsOf(c).Remove(PlayerInventories.Weapon1,Game.ItemsOf(c).Items(PlayerInventories.Weapon1).Single().Id);
            Assert.False(Game.HierophantEngineCurrent(session,current));
        }
        finally { session.Invalidate(); await session.OutputCompleted; }
    }

    public async Task Same_pulse_victims_share_pretrigger_offense_and_next_pulse_benefits()
    {
        using var s = new OrdinaryHierophantConnectedTests.Setup(); using var wire = new Wire();
        var c = s.Conn; ushort skill = Equip(s,"Storm Brand");
        s.Set(OrdinaryHierophantRules.HitSurge,1); s.Set("global_cannot_crit",1);
        s.Set(OrdinaryHierophantRules.SurgeMore,100);
        var session = new Game.ProjectileSession("pulse",wire.Stream,wire.Ciphers,c); c.ProjectileRuntimeState=session;
        try
        {
            var a=Target(c,10); var b=Target(c,11);
            Assert.True(Game.HierophantEngineAdmit(session,skill,Paid(c,skill),(310,300)));
            Game.HierophantEngineStep(session); var actor=Assert.Single(Game.HierophantEngineActors(session));
            actor.Target=0; Game.HierophantEngineActivate(session,actor,random:new Midpoint());
            uint first=a.MaxLife-a.Life; Assert.Equal(first,b.MaxLife-b.Life);
            uint before=a.Life; actor.Target=0; Game.HierophantEngineActivate(session,actor,random:new Midpoint());
            Assert.True(before-a.Life>first);
        }
        finally { session.Invalidate(); await session.OutputCompleted; }
    }

    public async Task Autonomous_projectile_keeps_release_critical_sources_after_live_change()
    {
        using var s=new OrdinaryHierophantConnectedTests.Setup(); using var wire=new Wire();
        var c=s.Conn; ushort skill=Equip(s,"Fireball",true);
        s.Set("critical_strike_chance_+%",100000); c.Pools=c.Pools with { SpellCriticalStrikeMultiplier=10000, SpellCriticalStrikeChancePct=100000 };
        var session=new Game.ProjectileSession("frozen crit",wire.Stream,wire.Ciphers,c); c.ProjectileRuntimeState=session;
        try
        {
            var target=Target(c,10); var totem=new TotemState.Totem(100,skill,(300,300),1000,8,null); c.Totems.Place(totem,1,0);
            Assert.True(Game.HierophantEngineAdmit(session,skill,Paid(c,skill),totem.At,totem));
            Game.HierophantEngineStep(session); var shot=Assert.Single(session.Runtime.Projectiles);
            s.Set("global_cannot_crit",1); s.Set("base_critical_strike_multiplier_+",0);
            Assert.Equal(ProjectileHitResult.Hit,session.Hit(new(shot,session.ActorKey(10,target),0,"direct",new(310,300))));
            Assert.True(target.MaxLife-target.Life>100);
            var second=Target(c,11);
            Assert.Equal(ProjectileHitResult.Hit,session.Hit(new(shot,session.ActorKey(11,second),0,"direct",new(310,300))));
            Assert.True(second.MaxLife-second.Life>100);
            Assert.True(target.Ignites.Burning);
            target.Ignites.Advance(Game.ResourceEffectsTime(c)+1,1);
            Assert.False(target.Ignites.LastTickPlayerOwned);
        }
        finally { session.Invalidate(); await session.OutputCompleted; }
    }

    public async Task Paid_default_Brand_publishes_owned_attachment_before_retained_action()
    {
        foreach (string name in new[] { "Storm Brand", "Armageddon Brand" })
        {
            using var s = new OrdinaryHierophantConnectedTests.Setup(); using var wire = new Wire();
            var c = s.Conn; ushort skill = Equip(s, name); Target(c, 10);
            var session = new Game.ProjectileSession("connected Brand art", wire.Stream, wire.Ciphers, c);
            c.ProjectileRuntimeState = session;
            try
            {
                session.AdvanceTo(ProjectileRuntime.Time(10));
                Assert.True(await Game.CastGemSkill("connected Brand art", wire.Stream, wire.Ciphers, c,
                    skill, (300,300), (310,300), sequence: 0x4567));
                session.AdvanceTo(ProjectileRuntime.Time(11));
                var actor = Assert.Single(Game.HierophantEngineActors(session));
                Assert.NotNull(actor.Attached);
                var bytes = await wire.Drain(session);
                string hex = Convert.ToHexString(bytes);
                Assert.Contains("02C600001000000000000000" + (name == "Storm Brand" ? "2BB2" : "A4D6") + skill.ToString("X4") + "456702000702", hex);
                int attach = hex.IndexOf("02C600001000", StringComparison.Ordinal);
                int add = hex.LastIndexOf("01C10000000A", StringComparison.Ordinal);
                Assert.True(add >= 0 && add < attach);
                ushort instance = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(add / 2 + 12));
                Assert.NotEqual((ushort)0, instance);
                Assert.Equal(instance, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(attach / 2 + 26)));
                Assert.Equal(name == "Storm Brand" ? "000000020000117E00000000" : "000000020000117F00000000", hex.Substring(add + 54, 24));
                c.Monsters[10].Dead = true; Game.HierophantEngineStep(session);
                string detached = Convert.ToHexString(await wire.Drain(session));
                Assert.Contains("000501", detached);
                Assert.Contains(Convert.ToHexString(InstanceEntities.RemoveBuff(10, instance)), detached);
                Game.ItemsOf(c).Remove(PlayerInventories.Weapon1, Game.ItemsOf(c).Items(PlayerInventories.Weapon1).Single().Id);
                Game.HierophantEngineStep(session);
                Assert.Contains("000104", Convert.ToHexString(await wire.Drain(session)));
            }
            finally { session.Invalidate(); await session.OutputCompleted; }
        }
        // Actual independent publishers must retain their target-local buffs through allocator wrap.
        foreach (string producer in new[] { "Pride", "BlindAura", "Smoke", "Profane", "Rarity", "SmokePending", "ProfanePending", "RawIgnite", "RawBleed", "IgniteExpiry", "BleedExpiry", "PoisonRefresh", "PoisonExpiry",
            "RawIgniteMinion", "RawIgniteDiscard", "RighteousFireExit", "RighteousFireEnd" })
        {
            using var auraSetup = new OrdinaryHierophantConnectedTests.Setup(); using var auraWire = new Wire();
            var auraConn = auraSetup.Conn; auraConn.Walk.Teleport(300,300,0);
            ushort auraSkill = Equip(auraSetup, "Storm Brand"); var auraTarget = Target(auraConn, 10);
            if (producer == "Pride")
            {
                int pride = Game.SkillsFor(auraConn).EffectRow("Pride");
                auraConn.Auras[0x999] = new(0x999, pride, "Pride", "Pride", 1, AuraCatalog.BuffRow("Pride"), 50, 0);
            }
            if (producer == "BlindAura") auraConn.Passives = [5087]; // Pinned SaboteurBlind template source.
            bool rawDot = producer.StartsWith("Raw",StringComparison.Ordinal) || producer.EndsWith("Expiry",StringComparison.Ordinal)
                || producer == "PoisonRefresh";
            bool pendingRf = producer.StartsWith("RighteousFire",StringComparison.Ordinal);
            if (pendingRf) auraConn.RighteousFire = new(0,0,null,auraSkill,0,0,20);
            List<byte[]>? dotPackets = null;
            if (producer.StartsWith("RawIgnite",StringComparison.Ordinal) || producer == "IgniteExpiry")
                dotPackets = Game.IgniteLocked(auraConn,10,auraTarget,new(DamageType.Fire,1,producer == "IgniteExpiry" ? 9 : 100),0);
            if (producer is "RawBleed" or "BleedExpiry")
                dotPackets = Game.BleedOnHitLocked(auraConn,10,auraTarget,new(100,1,1,1,1,producer == "BleedExpiry" ? 9 : 100),false,0);
            if (producer is "PoisonRefresh" or "PoisonExpiry")
            {
                Assert.True(auraTarget.Poisons.TryApply(new(100,1,1,1,1,producer == "PoisonExpiry" ? 9 : 100),false,0,0,false,new Random(1)));
                dotPackets = Game.PreparePoisonTick("prepare poison",auraWire.Stream,auraWire.Ciphers,auraConn).Buffs;
            }
            bool pendingGround = producer.EndsWith("Pending",StringComparison.Ordinal);
            var updates = new PlayerUpdatesState(Game.CombatLifetime!, new BrandGroundSchedule());
            if (producer.StartsWith("Smoke",StringComparison.Ordinal) || producer.StartsWith("Profane",StringComparison.Ordinal))
            {
                auraConn.PlayerUpdatesState = updates;
                Assert.True(auraConn.PlayerUpdatesWorker.TryStart(auraConn,updates.Rules,null,updates,out long generation));
                updates.Generation = generation;
                auraConn.FlaskGrounds.Add(new(producer.StartsWith("Smoke",StringComparison.Ordinal) ? FlaskGrounds.Kind.SmokeCloud : FlaskGrounds.Kind.ProfaneGround,
                    310,300,16,pendingGround ? 9 : 100));
            }
            if (producer == "Rarity")
            {
                auraConn.Monsters.Remove(10); auraConn.NextObjectId = 10;
                var variety = Assert.IsType<MonsterCatalog.Variety>(new MonsterCatalog().Find("Metadata/Monsters/ZombieDismember/ZombieDrownedStrand3"));
                Assert.Equal(10u, await Game.SpawnMonster("Brand rarity wrap",auraWire.Stream,auraWire.Ciphers,auraConn,
                    variety,1,MonsterRarity.Rare,310,300,log:false,unrisen:false,
                    mods:new MonsterModSet([],[],new MonsterModEffects(new Dictionary<string,int>()))));
                auraTarget = auraConn.Monsters[10];
            }
            async Task Publish()
            {
                if (producer == "Rarity") return;
                if (rawDot || pendingRf && dotPackets is not null)
                {
                    foreach (var packet in dotPackets ?? []) await Program.Send("pending DoT",auraWire.Stream,auraWire.Ciphers,packet,null);
                    dotPackets = null; return;
                }
                if (pendingRf)
                {
                    if (auraConn.RighteousFire is { } rf)
                        await Game.BurnAround("RF aura wrap",auraWire.Stream,auraWire.Ciphers,auraConn,rf,0);
                    return;
                }
                if (producer is "Pride" or "BlindAura")
                    await Game.EnemyDebuffsTick("Brand aura wrap", auraWire.Stream, auraWire.Ciphers, auraConn, 0);
                else
                {
                    Game.AdvanceFlaskGrounds(auraConn,updates);
                    await Game.PublishFlaskChanges("Brand aura wrap",auraWire.Stream,auraWire.Ciphers,auraConn);
                }
            }
            var auraSession = new Game.ProjectileSession("Brand aura wrap", auraWire.Stream, auraWire.Ciphers, auraConn);
            auraConn.ProjectileRuntimeState = auraSession;
            try
            {
                await Publish();
                byte[] published = await auraWire.Drain(auraSession);
                int add = Convert.ToHexString(published).IndexOf("01C10000000A",StringComparison.Ordinal);
                Assert.True(add >= 0,producer); add /= 2;
                ushort auraInstance = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(published.AsSpan(add+12));
                auraSession.AdvanceTo(ProjectileRuntime.Time(10));
                Assert.True(await Game.CastGemSkill("Brand aura wrap", auraWire.Stream, auraWire.Ciphers, auraConn,
                    auraSkill, (300,300), (310,300), sequence: 0x4568));
                if (pendingRf)
                {
                    if (producer == "RighteousFireEnd") dotPackets = Game.PrepareRighteousFireEnd(auraConn).Buffs;
                    else
                    {
                        var rf = auraConn.RighteousFire!;
                        auraConn.Walk.Teleport(1000,1000,10);
                        dotPackets = Game.PrepareRighteousFireTick("RF exit",auraWire.Stream,auraWire.Ciphers,auraConn,rf,0).Buffs;
                        auraConn.Walk.Teleport(300,300,10);
                        Assert.Empty(rf.Auras);
                    }
                    Assert.True(auraConn.TargetBuffOutputs.Holds(10,auraInstance));
                }
                if (rawDot)
                {
                    List<byte[]>? replacement;
                    if (producer is "PoisonRefresh" or "PoisonExpiry")
                        replacement = Game.PreparePoisonTick("pending poison",auraWire.Stream,auraWire.Ciphers,auraConn).Buffs;
                    else if (producer is "IgniteExpiry" or "BleedExpiry")
                        replacement = Game.PrepareStrongestDotTick("pending strongest",auraWire.Stream,auraWire.Ciphers,auraConn,
                            m => producer == "IgniteExpiry" ? m.Ignites : m.Bleeds,(_,_) => 1).Buffs;
                    else replacement = producer.StartsWith("RawIgnite",StringComparison.Ordinal)
                        ? Game.IgniteLocked(auraConn,10,auraTarget,new(DamageType.Fire,2,100),10)
                        : Game.BleedOnHitLocked(auraConn,10,auraTarget,new(100,2,2,2,2,100),false,10);
                    dotPackets = []; dotPackets.AddRange(Assert.IsType<List<byte[]>>(replacement));
                    Assert.True(auraConn.TargetBuffOutputs.Holds(10,auraInstance));
                }
                if (pendingGround)
                {
                    // Expire through the production owner, but hold its independent FIFO until after Brand publication.
                    Game.AdvanceFlaskGrounds(auraConn,updates);
                    Assert.False(producer.StartsWith("Smoke",StringComparison.Ordinal)
                        ? auraConn.FlaskGrounds.Blinded(10) : auraConn.FlaskGrounds.Profane(10));
                    Assert.NotEmpty(auraConn.FlaskAutomation.Packets);
                }
                // Keep the target-local aura live while the actual nonzero allocator crosses its reserved boundary.
                for (int i = 0; auraConn.NextBuff != auraInstance; i++)
                {
                    Assert.True(i < ushort.MaxValue); _ = auraConn.NextBuff++;
                }
                auraSession.AdvanceTo(ProjectileRuntime.Time(11));
                var actor = Assert.Single(Game.HierophantEngineActors(auraSession));
                await auraWire.Drain(auraSession);
                ushort brandInstance = actor.Presentation!.ShownBuff;
                Assert.NotEqual((ushort)0, brandInstance);
                Assert.True(auraInstance != brandInstance,$"{producer}: retained and Brand instances both {auraInstance}");
                if (rawDot || pendingRf)
                {
                    if (producer == "RawIgniteMinion")
                    {
                        foreach (var packet in dotPackets!) auraConn.Minions.OutputQueue.Add(new(packet,null,10,auraTarget));
                        dotPackets = null;
                        await Game.PublishMinionOutcomes("pending minion DoT",auraWire.Stream,auraWire.Ciphers,auraConn);
                    }
                    else
                    {
                        if (producer == "RawIgniteDiscard")
                            foreach (var packet in dotPackets!) TargetBuffOutputLedger.Discard(packet);
                        await Publish();
                    }
                    string late = Convert.ToHexString(await auraWire.Drain(auraSession));
                    string oldRemoval = Convert.ToHexString(InstanceEntities.RemoveBuff(10,auraInstance));
                    if (producer == "RawIgniteDiscard") Assert.DoesNotContain(oldRemoval,late);
                    else Assert.Contains(oldRemoval,late);
                    Assert.DoesNotContain(Convert.ToHexString(InstanceEntities.RemoveBuff(10,brandInstance)),late);
                    Assert.False(auraConn.TargetBuffOutputs.Holds(10,auraInstance));
                }
                if (pendingGround)
                {
                    await Game.PublishFlaskChanges("late ground cleanup",auraWire.Stream,auraWire.Ciphers,auraConn);
                    string late = Convert.ToHexString(await auraWire.Drain(auraSession));
                    Assert.Contains(Convert.ToHexString(InstanceEntities.RemoveBuff(10,auraInstance)),late);
                    Assert.DoesNotContain(Convert.ToHexString(InstanceEntities.RemoveBuff(10,brandInstance)),late);
                }
                auraConn.NextBuff = brandInstance;
                Assert.NotEqual(brandInstance, auraConn.NextBuff++); // Other producers must retain the Brand's reservation.
                auraSetup.Set("base_number_of_sigils_allowed_per_target", -1);
                Game.HierophantEngineStep(auraSession);
                Assert.Null(actor.Attached);
                string cleanup = Convert.ToHexString(await auraWire.Drain(auraSession));
                Assert.Contains(Convert.ToHexString(InstanceEntities.RemoveBuff(10, brandInstance)), cleanup);
                Assert.DoesNotContain(Convert.ToHexString(InstanceEntities.RemoveBuff(10, auraInstance)), cleanup);
                await Publish();
                string reconciled = Convert.ToHexString(await auraWire.Drain(auraSession));
                if (!pendingRf) Assert.DoesNotContain("01C10000000A",reconciled); // RF may legitimately reenter after leaving its radius.
                Assert.DoesNotContain(Convert.ToHexString(InstanceEntities.RemoveBuff(10,auraInstance)),reconciled);
                if (producer == "Pride") Assert.Equal(auraInstance, auraTarget.PlayerAuras[AuraCatalog.PrideBuffRow]);
                else if (producer == "BlindAura") Assert.True(auraConn.BlindAura.Blinded(10));
                else if (producer == "Rarity") Assert.Contains(auraInstance,auraTarget.NativeBuffReservations);
                else if (!pendingGround && !rawDot && !pendingRf) Assert.True(producer == "Smoke" ? auraConn.FlaskGrounds.Blinded(10) : auraConn.FlaskGrounds.Profane(10));
            }
            finally { auraSession.Invalidate(); await auraSession.OutputCompleted; }
            Assert.Empty(auraConn.ReservedBrandBuffs);
            auraConn.RetirePlayerUpdates();
        }
    }
    sealed class BrandGroundSchedule : IPlayerUpdateSchedule
    {
        public double Now(ClientState conn) => Game.ResourceEffectsTime(conn);
        public Task Wait(int milliseconds,CancellationToken stopping) => throw new NotSupportedException();
    }

    public async Task Brand_output_revalidates_blocked_lifecycle_and_identity_admission()
    {
        foreach (string change in new[] { "valid", "target", "source", "owner", "epoch", "expiry", "cap", "recall",
            "additional", "chance", "repeat", "variant", "support", "simultaneous", "wrap" })
        {
            using var s = new OrdinaryHierophantConnectedTests.Setup(); using var wire = new Wire();
            var c = s.Conn; ushort skill = Equip(s, "Storm Brand"); Target(c, 10);
            var session = new Game.ProjectileSession(change, wire.Stream, wire.Ciphers, c); c.ProjectileRuntimeState = session;
            var gate = Program.SendGates.GetValue(wire.Ciphers, _ => new SemaphoreSlim(1,1));
            bool held = false;
            try
            {
                session.AdvanceTo(ProjectileRuntime.Time(10));
                if (change == "support")
                {
                    var host = Game.ItemsOf(c).Items(PlayerInventories.Weapon1).Single();
                    Game.ItemsOf(c).Change(PlayerInventories.Weapon1, host.Id, x => x with
                    { Sockets = [..x.Sockets, new(SocketColour.White, ItemCatalog.Create(Program.Items.Find("Swiftbrand Support")!))], Links = [2] });
                }
                if (change == "additional") s.Set("additional_number_of_brands_to_create", 1);
                if (change == "chance") s.Set("create_additional_brand_%_chance", 1);
                if (change == "repeat") s.Set("base_spell_repeat_count", 1);
                if (change == "variant")
                {
                    var host = Game.ItemsOf(c).Items(PlayerInventories.Weapon1).Single();
                    Game.ItemsOf(c).Change(PlayerInventories.Weapon1, host.Id, x => x with {
                        Sockets = [new(SocketColour.White, x.Sockets[0].Gem! with { GemVariant = 1 })] });
                    // Transfigured effects may have no supported engine; admission must fail closed in either case.
                    Assert.Null(Game.CaptureBrandPresentation(c, skill, 0x5000));
                    continue;
                }
                Assert.True(await Game.CastGemSkill(change, wire.Stream, wire.Ciphers, c, skill,
                    (300,300), (310,300), sequence: 0x5000));
                string initial = "";
                if (change == "simultaneous")
                {
                    session.AdvanceTo(ProjectileRuntime.Time(11));
                    initial = Convert.ToHexString(await wire.Drain(session));
                    Target(c, 11); c.ActionCount++;
                    Assert.True(await Game.CastGemSkill(change, wire.Stream, wire.Ciphers, c, skill,
                        (300,300), (310,300), sequence: 0x5001));
                }
                await wire.Drain(session);
                await gate.WaitAsync(); held = true;
                session.AdvanceTo(ProjectileRuntime.Time(change == "simultaneous" ? 12 : 11));
                if (change == "valid") session.AdvanceTo(ProjectileRuntime.Time(11.25));
                if (change == "target") c.Monsters[10] = new("replacement", new(310,300,0), 0,0,100000);
                if (change == "source") Game.ItemsOf(c).Remove(PlayerInventories.Weapon1,
                    Game.ItemsOf(c).Items(PlayerInventories.Weapon1).Single().Id);
                if (change == "owner") c.RetireResourceEffects();
                if (change == "epoch") c.ProjectileEpoch++;
                if (change == "expiry") session.AdvanceTo(ProjectileRuntime.Time(17));
                if (change == "cap") { s.Set("base_total_number_of_sigils_allowed", 0); Game.HierophantEngineStep(session); }
                if (change == "wrap") c.NextBuff = 0;
                if (change == "recall")
                {
                    var actor = Assert.Single(Game.HierophantEngineActors(session));
                    Game.ItemsOf(c).Add(PlayerInventories.BodyArmour,0,0,ItemCatalog.Create(Program.Items.Find("Simple Robe")!) with
                        { Sockets=[new(SocketColour.White,ItemCatalog.Create(Program.Items.Find("Brand Recall")!))],Links=[1] });
                    ushort recall = Game.GemSkillEffects(c).Single(x => x.Name == "Brand Recall").Id;
                    var plan = Game.PrepareHierophantRecall(session, recall, Paid(c, recall))!;
                    Assert.True(Game.CommitHierophantRecall(session, plan));
                    Assert.True(actor.Brand!.Alive); Assert.Null(actor.Attached);
                    Game.HierophantEngineStep(session); // Gameplay reattachment must not invent a recalled tuple.
                }
                gate.Release(); held = false;
                if (change == "epoch") session.Invalidate();
                string hex = initial + Convert.ToHexString(await wire.Drain(session));
                if (change is "valid" or "simultaneous" or "wrap")
                {
                    Assert.Contains("01C10000000A", hex);
                    if (change == "valid")
                    {
                        byte[] decoded = Convert.FromHexString(hex);
                        Assert.Equal(4.75f, System.Buffers.Binary.BinaryPrimitives.ReadSingleBigEndian(decoded.AsSpan(23)));
                    }
                    Assert.True(hex.Contains("500002000702"), change + ": " + hex);
                    if (change == "simultaneous")
                    {
                        Assert.Equal(2, Game.HierophantEngineActors(session).Count);
                        Assert.Contains("01C10000000B", hex); Assert.Contains("500102000702", hex);
                        var instances = Game.HierophantEngineActors(session).Select(x => x.Presentation!.ShownBuff).ToArray();
                        Assert.Equal(2, instances.Distinct().Count());
                        s.Set("base_total_number_of_sigils_allowed", 1); Game.HierophantEngineStep(session);
                        Assert.Contains("000104", Convert.ToHexString(await wire.Drain(session)));
                        session.AdvanceTo(ProjectileRuntime.Time(17));
                        string expired = Convert.ToHexString(await wire.Drain(session));
                        Assert.Contains("01C20000000B", expired); Assert.Contains("000104", expired);
                    }
                }
                else Assert.DoesNotContain("01C10000000A", hex);
            }
            finally
            {
                if (held) gate.Release(); session.Invalidate(); await session.OutputCompleted;
            }
        }
        // Distinct outputs for one instance keep independent leases, including copied lists and queue discard.
        using var leases = new OrdinaryHierophantConnectedTests.Setup(); using var leaseWire = new Wire();
        var leaseConn = leases.Conn; var leaseTarget = Target(leaseConn,10);
        var first = Game.IgniteLocked(leaseConn,10,leaseTarget,new(DamageType.Fire,1,100),0)!;
        var next = Game.IgniteLocked(leaseConn,10,leaseTarget,new(DamageType.Fire,2,100),0)!;
        ushort original = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(first[0].AsSpan(12));
        TargetBuffOutputLedger.Discard(first[0]);
        Assert.True(leaseConn.TargetBuffOutputs.Holds(10,original)); // The second list still owns its removal.
        TargetBuffOutputLedger.Discard(next[0]); Assert.False(leaseConn.TargetBuffOutputs.Holds(10,original));
        ushort queued = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(next[1].AsSpan(12));
        var leaseSession = new Game.ProjectileSession("discard DoT",leaseWire.Stream,leaseWire.Ciphers,leaseConn);
        leaseConn.ProjectileRuntimeState = leaseSession;
        var leaseGate = Program.SendGates.GetValue(leaseWire.Ciphers,_ => new SemaphoreSlim(1,1));
        await leaseGate.WaitAsync();
        try
        {
            leaseSession.Packet(next[1]); Assert.True(leaseConn.TargetBuffOutputs.Holds(10,queued));
            leaseSession.Invalidate(); Assert.False(leaseConn.TargetBuffOutputs.Holds(10,queued));
        }
        finally { leaseGate.Release(); leaseSession.Invalidate(); await leaseSession.OutputCompleted; }
        await Program.Send("discard cannot replay",leaseWire.Stream,leaseWire.Ciphers,next[1],null);
        Assert.Empty(await leaseWire.Drain(leaseSession));
        var abandoned = Game.IgniteLocked(leaseConn,10,leaseTarget,new(DamageType.Fire,3,100),0)!;
        foreach (var packet in abandoned) leaseConn.Minions.OutputQueue.Add(new(packet,null,10,leaseTarget));
        leaseConn.Monsters[10] = new("replaced",new(310,300,0),0,0,100000);
        await Game.PublishMinionOutcomes("stale DoT target",leaseWire.Stream,leaseWire.Ciphers,leaseConn);
        foreach (var packet in abandoned)
        {
            Assert.False(TargetBuffOutputLedger.IsPending(packet));
            await Program.Send("discarded minion cannot replay",leaseWire.Stream,leaseWire.Ciphers,packet,null);
        }
        Assert.Empty(await leaseWire.Drain(leaseSession));
    }



    public async Task Linked_payload_dispatch_pays_places_and_repeats_without_owner_fallback(string payload)
    {
        foreach (string invalidation in new[] { "source", "owner", "death", "cap" })
        {
            using var s = new OrdinaryHierophantConnectedTests.Setup(); using var wire = new Wire(); var c = s.Conn;
            ushort skill = Equip(s, payload, true);
            s.Set("global_cannot_crit", 1);
            var session = new Game.ProjectileSession(payload, wire.Stream, wire.Ciphers, c); c.ProjectileRuntimeState = session;
            try
            {
                c.Walk.Teleport(100, 100, 0);
                session.AdvanceTo(ProjectileRuntime.Time(10));
                var near = Target(c, 10); var ownerOnly = Target(c, 11, 100); ownerOnly.Walk.Teleport(100, 100, 0);
                var supported = Game.SupportedSkill(c, skill);
                Assert.Contains(supported.Supports, x => x.Effect.Id == "SupportSpellTotem");
                Assert.Null(Game.GemSkillInstanceDisabled(c, supported.Effect, skill));
                Assert.Equal("Metadata/Monsters/Totems/IntelligenceTotem", Game.HierophantOffensiveTotemVariety(c, skill)!.Id);
                uint mana = c.PlayerMana;
                c.ActionCount++;
                Assert.True(await Game.CastGemSkill(payload, wire.Stream, wire.Ciphers, c, skill, (100,100), (300,300)));
                Assert.True(c.PlayerMana < mana); uint paidMana = c.PlayerMana, paidLife = c.PlayerLife;
                Assert.Empty(c.Totems.All); Assert.Equal(near.MaxLife, near.Life);
                // Only game time advances. Yield to the existing placement wakeup/output, never sleep in the test.
                session.AdvanceTo(ProjectileRuntime.Time(12));
                async Task Placement()
                {
                    while (true)
                    {
                        lock (c.Monsters) if (Game.HierophantEngineActors(session).Count != 0) return;
                        await Task.Yield();
                    }
                }
                await Placement().WaitAsync(TimeSpan.FromSeconds(5));
                var actor = Assert.Single(Game.HierophantEngineActors(session));
                Assert.Same(Assert.Single(c.Totems.All), actor.Totem); Assert.Equal(actor.Totem!.At, actor.At);
                Game.HierophantEngineStep(session);
                if (payload == "Freezing Pulse")
                {
                    var shot = Assert.Single(session.Runtime.Projectiles);
                    Assert.Equal(new ProjectileVector(actor.At.X,actor.At.Y), shot.Use.Origin);
                    Assert.Equal(ProjectileHitResult.Hit, session.Hit(new(shot, session.ActorKey(10,near), 0, "direct", new(310,300))));
                }
                Assert.True(near.Life < near.MaxLife); uint first = near.Life;
                Assert.Equal(ownerOnly.MaxLife, ownerOnly.Life);
                Assert.False(Game.CommitGemExperience(c, 10).Leveled); Assert.True(Game.HierophantEngineCurrent(session, actor));
                session.AdvanceTo(ProjectileRuntime.Time(14));
                if (payload == "Freezing Pulse")
                {
                    var repeat = Assert.Single(session.Runtime.Projectiles);
                    Assert.Equal(ProjectileHitResult.Hit, session.Hit(new(repeat, session.ActorKey(10,near), 0, "direct", new(310,300))));
                }
                Assert.True(near.Life < first); Assert.Equal(ownerOnly.MaxLife, ownerOnly.Life);
                Assert.Equal(paidMana, c.PlayerMana); Assert.Equal(paidLife, c.PlayerLife);
                if (invalidation == "source") Game.ItemsOf(c).Remove(PlayerInventories.Weapon1, Game.ItemsOf(c).Items(PlayerInventories.Weapon1).Single().Id);
                if (invalidation == "owner") c.RetireResourceEffects();
                if (invalidation == "death") c.PlayerLife = 0;
                if (invalidation == "cap") c.Totems.Place(new(999, skill, (400,400), 100, 20, null), 1, 14);
                Game.HierophantEngineStep(session); Assert.False(Game.HierophantEngineCurrent(session, actor));
                Assert.Empty(Game.HierophantEngineActors(session));
            }
            finally { session.Invalidate(); await session.OutputCompleted; }
        }
    }






    public async Task New_totem_activation_captures_pre_surge_offense_and_support_damage_once(string payload)
    {
        using var s = new OrdinaryHierophantConnectedTests.Setup(); using var wire = new Wire(); var c = s.Conn;
        ushort skill = Equip(s, payload, payload != "Shockwave Totem");
        s.Set("global_cannot_crit", 1); s.Set(OrdinaryHierophantRules.HitSurge, 1); s.Set(OrdinaryHierophantRules.SurgeMore, 100);
        var session = new Game.ProjectileSession(payload, wire.Stream, wire.Ciphers, c); c.ProjectileRuntimeState = session;
        try
        {
            var a = Target(c, 10); var b = Target(c, 11);
            var body = new TotemState.Totem(100, skill, (300,300), 1000, 8, null); c.Totems.Place(body, 1, 0);
            Assert.True(Game.HierophantEngineAdmit(session, skill, Paid(c, skill), body.At, body));
            var actor = Assert.Single(Game.HierophantEngineActors(session));
            var ordinary = Game.SupportedProjectileHit(c, skill, area: true);
            var supported = Game.HierophantEngineHit(c, actor, true);
            Assert.Equal(ordinary.MinTotal * (payload == "Shockwave Totem" ? 1 : .45), supported.MinTotal, 6);
            s.Set("totem_damage_+%", 100);
            Assert.InRange(Game.HierophantEngineHit(c, actor, true).MinTotal / supported.MinTotal, 1.8, 2.1);
            s.Set("totem_damage_+%", 0);
            (uint Min,uint Max) Bounds(DamagePacket packet) =>
                ((uint)packet.Components.Sum(c=>Math.Floor(c.Range.Min+1e-9)),
                 (uint)packet.Components.Sum(c=>Math.Floor(c.Range.Max+1e-9)));
            var preSurge=Bounds(supported);
            bool projectile=payload is "Fireball" or "Freezing Pulse";
            long firstProjectile=-1;
            uint aBefore=a.Life,bBefore=b.Life;
            Game.HierophantEngineActivate(session, actor, (310,300), new Midpoint());
            if (projectile)
            {
                var shot = Assert.Single(session.Runtime.Projectiles);
                firstProjectile=shot.Id;
                // Changing offensive inputs after release cannot modify either victim's captured hit.
                string damageType=payload=="Fireball"?"fire_damage_+%":"cold_damage_+%";
                s.Set(damageType, 10000);
                Assert.Equal(ProjectileHitResult.Hit, session.Hit(new(shot,session.ActorKey(10,a),0,"direct",new(310,300))));
                Assert.Equal(ProjectileHitResult.Hit, session.Hit(new(shot,session.ActorKey(11,b),0,"direct",new(310,300))));
                s.Set(damageType, 0);
            }
            Assert.InRange(aBefore-a.Life,preSurge.Min,preSurge.Max);
            Assert.InRange(bBefore-b.Life,preSurge.Min,preSurge.Max);
            Assert.True(Game.HierophantOf(c).Active(0));
            var afterSurge=Bounds(Game.HierophantEngineHit(c,actor,true));
            Assert.True(afterSurge.Min>preSurge.Max);
            uint mana = c.PlayerMana, life = c.PlayerLife;
            aBefore=a.Life; bBefore=b.Life;
            Game.HierophantEngineActivate(session, actor, (310,300), new Midpoint());
            if(projectile)
            {
                var shot=Assert.Single(session.Runtime.Projectiles,p=>p.Id!=firstProjectile);
                Assert.Equal(ProjectileHitResult.Hit,session.Hit(new(shot,session.ActorKey(10,a),0,"direct",new(310,300))));
                Assert.Equal(ProjectileHitResult.Hit,session.Hit(new(shot,session.ActorKey(11,b),0,"direct",new(310,300))));
            }
            Assert.InRange(aBefore-a.Life,afterSurge.Min,afterSurge.Max);
            Assert.InRange(bBefore-b.Life,afterSurge.Min,afterSurge.Max);
            Assert.Equal(mana, c.PlayerMana); Assert.Equal(life, c.PlayerLife);
        }
        finally { session.Invalidate(); await session.OutputCompleted; }
    }

    sealed class Wire : IDisposable
    {
        readonly TcpListener listener = new(IPAddress.Loopback, 0);
        readonly TcpClient writer = new();
        readonly TcpClient reader;
        internal NetworkStream Stream => writer.GetStream();
        internal readonly ConnectionCiphers Ciphers = new(new byte[64], false);
        internal Wire()
        {
            listener.Start(); var accept = listener.AcceptTcpClientAsync();
            writer.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            reader = accept.GetAwaiter().GetResult();
        }
        readonly ConnectionCiphers client = new(new byte[64], true);
        internal async Task<byte[]> Drain(Game.ProjectileSession session)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (session.Active)
            {
                session.Enqueue(() => { done.SetResult(); return Task.CompletedTask; });
                await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            else await session.OutputCompleted;
            byte[] bytes = new byte[reader.Available];
            if (bytes.Length > 0) await reader.GetStream().ReadExactlyAsync(bytes);
            client.Receive.Process(bytes); return bytes;
        }
        public void Dispose() { reader.Dispose(); writer.Dispose(); listener.Stop(); }
    }
    static ushort Equip(OrdinaryHierophantConnectedTests.Setup s, string name, bool totem = false)
    {
        foreach (var n in new[] { "strength", "dexterity", "intelligence" }) s.Set("virtual_" + n + "_for_requirements", 1000);
        var gem = ItemCatalog.Create(Program.Items.Find(name)!);
        var sockets = new List<ItemSocket> { new(SocketColour.White, gem) };
        if (totem) sockets.Add(new(SocketColour.White, ItemCatalog.Create(Program.Items.Find("Spell Totem Support")!)));
        Game.ItemsOf(s.Conn).Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!) with
            { Sockets = sockets.ToArray(), Links = [(byte)sockets.Count] });
        return Game.GemSkillEffects(s.Conn).Single(g => g.Name == name).Id;
    }
    static int Paid(ClientState c, ushort skill)
    {
        int action = ++c.ActionCount;
        Assert.True(Game.PayMana(c, skill, out _, out _)); Game.OrdinaryAcceptedUse(c, action); return action;
    }
    static SpawnedMonster Target(ClientState c, uint id, int x = 310)
    {
        var m = new SpawnedMonster("engine target", new(x,300,0), 0,0,100000);
        c.Monsters[id] = m; return m;
    }

    public void Brand_budgets_detach_and_exact_late_boundary_are_separate()
    {
        var clock = new HierophantBrandClock(0, 5, 6);
        Assert.Equal(0, clock.Advance(2, false, .5, 1, 100));
        Assert.Equal(8, clock.Advance(6, true, .5, 1, 100));
        Assert.Equal(1, clock.Advance(6.25, true, .5, 1, 100));
        Assert.Equal(0, clock.Advance(8, false, .5, 1, 100));
        Assert.Equal(4.25, clock.AttachedUsed); Assert.Equal(3.75, clock.DetachedUsed);
        Assert.Equal(0, clock.Advance(9, true, .5, 1, 100)); Assert.False(clock.Alive);
    }

    public async Task Paid_brand_attaches_hits_detaches_pauses_and_retires_on_source_loss()
    {
        using var s = new OrdinaryHierophantConnectedTests.Setup(); using var wire = new Wire();
        var c = s.Conn; c.Walk.Teleport(300,300,0); ushort skill = Equip(s, "Storm Brand");
        s.Set(OrdinaryHierophantRules.HitSurge,1); s.Set("global_cannot_crit",1);
        var session = new Game.ProjectileSession("brand engine",wire.Stream,wire.Ciphers,c); c.ProjectileRuntimeState = session;
        try
        {
            var target = Target(c,10); int action = Paid(c,skill);
            Assert.True(Game.HierophantEngineAdmit(session,skill,action,(310,300)));
            Assert.False(Game.HierophantEngineAdmit(session,skill,action,(310,300)));
            Game.HierophantEngineStep(session); var actor = Assert.Single(Game.HierophantEngineActors(session));
            Assert.Same(target,actor.Attached);
            double now = Game.ResourceEffectsTime(c);
            session.AdvanceTo(ProjectileRuntime.Time(now + .5)); Game.HierophantEngineStep(session);
            Assert.True(target.Life < target.MaxLife); Assert.True(Game.HierophantOf(c).Active(Game.ResourceEffectsTime(c)));
            var inputs = Game.HierophantEngineHit(c,actor,true);
            s.Set("sigil_damage_+%",100);
            Assert.True(Game.HierophantEngineHit(c,actor,true).MinTotal > inputs.MinTotal);
            uint life = target.Life; c.Clock.Stop(); Game.HierophantEngineStep(session); Assert.Equal(life,target.Life); c.Clock.Start();
            target.Dead = true; Game.HierophantEngineStep(session); Assert.Null(actor.Attached);
            Game.ItemsOf(c).Remove(PlayerInventories.Weapon1,Game.ItemsOf(c).Items(PlayerInventories.Weapon1).Single().Id);
            Game.HierophantEngineStep(session); Assert.Empty(Game.HierophantEngineActors(session));
        }
        finally { session.Invalidate(); await session.OutputCompleted; }
    }



    public async Task Totem_projectile_contact_has_damage_surge_own_regeneration_and_no_owner_leech_or_elusive(string payload)
    {
        using var s = new OrdinaryHierophantConnectedTests.Setup(); using var wire = new Wire();
        var c = s.Conn; c.Walk.Teleport(300,300,0); ushort skill = Equip(s,payload,true);
        s.Set(OrdinaryHierophantRules.HitSurge,1);
        s.Set("you_and_totem_life_regeneration_rate_per_minute_%_per_active_totem",6000);
        var session = new Game.ProjectileSession("totem engine",wire.Stream,wire.Ciphers,c); c.ProjectileRuntimeState = session;
        try
        {
            Assert.NotNull(Game.HierophantOffensiveTotemVariety(c,skill));
            var target = Target(c,10); var totem = new TotemState.Totem(100,skill,(300,300),1000,8,null) { Life = 500 };
            c.Totems.Place(totem,1,0);
            Assert.True(Game.HierophantEngineAdmit(session,skill,Paid(c,skill),totem.At,totem));
            Game.HierophantEngineStep(session);
            var actor = Assert.Single(Game.HierophantEngineActors(session));
            var entity = Assert.Single(session.Runtime.Projectiles);
            Assert.IsType<ObjectProjectileOffense>(entity.Use.Payload);
            uint ownerLife = c.PlayerLife;
            Assert.Equal(ProjectileHitResult.Hit, session.Hit(new(entity,session.ActorKey(10,target),0,"direct",new(310,300))));
            Assert.True(target.Life < target.MaxLife); Assert.Equal(ownerLife,c.PlayerLife);
            Assert.True(Game.HierophantOf(c).Active(Game.ResourceEffectsTime(c)));
            c.Pools = c.Pools with { SpellCriticalStrikeChancePct=-100, VsBlindedCriticalPct=2000, SpellCriticalStrikeMultiplier=250 };
            Assert.Equal(0,Game.SupportedReleaseCriticalChance(c,skill));
            Assert.Equal(10000,Game.SupportedCriticalChanceVsBlinded(c,skill,true)!.Value);
            var ordinary=Target(c,11); var blinded=Target(c,12);
            var hit=Game.HierophantEngineHit(c,actor,false);
            uint normalMax=(uint)hit.Components.Sum(x=>Math.Floor(x.Range.Max+1e-9));
            uint criticalMin=(uint)hit.Components.Sum(x=>Math.Floor(x.Range.Min*2.5+1e-9));
            uint criticalMax=(uint)hit.Components.Sum(x=>Math.Floor(x.Range.Max*2.5+1e-9));
            Assert.True(criticalMin>normalMax);
            Game.HierophantEngineActivate(session,actor,(310,300),new Midpoint());
            var criticalShot=Assert.Single(session.Runtime.Projectiles,p=>p.Id!=entity.Id);
            // Source chances freeze at release; the target's blinded condition is selected at contact.
            c.Pools=c.Pools with { VsBlindedCriticalPct=0 };
            blinded.RampageBlindUntil=10;
            Assert.Equal(ProjectileHitResult.Hit,session.Hit(new(criticalShot,session.ActorKey(11,ordinary),0,"direct",new(310,300))));
            Assert.Equal(ProjectileHitResult.Hit,session.Hit(new(criticalShot,session.ActorKey(12,blinded),0,"direct",new(310,300))));
            Assert.InRange(ordinary.MaxLife-ordinary.Life,1u,normalMax);
            Assert.InRange(blinded.MaxLife-blinded.Life,criticalMin,criticalMax);
            Assert.Equal(ownerLife,c.PlayerLife);
            session.AdvanceTo(ProjectileRuntime.Time(1)); Game.HierophantEngineStep(session); Assert.Equal(1000u,totem.Life);
            totem.Life = 0; totem.DiedAt = 1; Game.HierophantEngineStep(session);
            Assert.False(Game.HierophantEngineCurrent(session,actor));
            Assert.Equal(ProjectileHitResult.Miss, session.Hit(new(entity,session.ActorKey(10,target),0,"direct",new(310,300))));
        }
        finally { session.Invalidate(); await session.OutputCompleted; }
    }

    public async Task Brand_caps_use_original_sources_once_and_reconcile_refunded_limits()
    {
        using var s = new OrdinaryHierophantConnectedTests.Setup(); using var wire = new Wire();
        var c = s.Conn; ushort skill = Equip(s,"Storm Brand"); Target(c,10);
        s.Set("base_number_of_sigils_allowed_per_target",1); // +1 beside the gem's base1, not its derived alias twice.
        var session = new Game.ProjectileSession("brand caps",wire.Stream,wire.Ciphers,c); c.ProjectileRuntimeState = session;
        try
        {
            for (int i=0;i<3;i++) { Assert.True(Game.HierophantEngineAdmit(session,skill,Paid(c,skill),(310,300))); Game.HierophantEngineStep(session); }
            Assert.Equal(2,Game.HierophantEngineActors(session).Count(x=>x.Attached is not null));
            s.Set("base_number_of_sigils_allowed_per_target",0); Game.HierophantEngineStep(session);
            Assert.Single(Game.HierophantEngineActors(session),x=>x.Attached is not null);
            s.Set("base_total_number_of_sigils_allowed",1); Game.HierophantEngineStep(session);
            Assert.Single(Game.HierophantEngineActors(session));
        }
        finally { session.Invalidate(); await session.OutputCompleted; }
    }

    public async Task Placement_reuses_real_native_totem_creation_life_cap_and_presentation()
    {
        using var s = new OrdinaryHierophantConnectedTests.Setup(); using var wire = new Wire();
        var c = s.Conn; ushort skill = Equip(s,"Shockwave Totem");
        var session = new Game.ProjectileSession("placement engine",wire.Stream,wire.Ciphers,c); c.ProjectileRuntimeState = session;
        try
        {
            int action = Paid(c,skill);
            var placing = Game.PlaceHierophantOffensiveTotem("placement",wire.Stream,wire.Ciphers,session,skill,action,(300,300));
            Assert.False(placing.IsCompleted);
            session.AdvanceTo(ProjectileRuntime.Time(1));
            uint id = Assert.IsType<uint>(await placing.WaitAsync(TimeSpan.FromSeconds(5)));
            var actor = Assert.Single(Game.HierophantEngineActors(session)); Assert.Equal(id,actor.Id);
            var totem = Assert.Single(c.Totems.All); Assert.Same(totem,actor.Totem);
            var variety = Game.HierophantOffensiveTotemVariety(c,skill)!;
            var gem = Game.SupportedSkill(c,skill);
            int level = Math.Clamp(Game.SkillsFor(c).PlayerLevelReq(gem.Effect,gem.Level),1,100);
            Assert.Equal((uint)new MonsterCatalog().BaseLife(variety,(byte)level)!.Value,totem.MaxLife);
            totem.Life = 0; totem.DiedAt = Game.ResourceEffectsTime(c);
            Assert.NotNull(Game.TotemPresentation(c)); Assert.True(totem.SentDeath);
            Assert.False(Game.HierophantEngineCurrent(session,actor));
        }
        finally { session.Invalidate(); await session.OutputCompleted; }
    }

    public async Task Recall_has_real_cooldown_recovery_extra_resource_plan_and_source_validation()
    {
        using var s = new OrdinaryHierophantConnectedTests.Setup(); using var wire = new Wire();
        var c = s.Conn; ushort skill = Equip(s,"Storm Brand");
        var recallGem = ItemCatalog.Create(Program.Items.Find("Brand Recall")!);
        var equipped = Game.ItemsOf(c).Add(PlayerInventories.BodyArmour,0,0,ItemCatalog.Create(Program.Items.Find("Simple Robe")!) with
            { Sockets = [new(SocketColour.White,recallGem)], Links = [1] });
        var recall = Game.GemSkillEffects(c).Single(g => g.Name == "Brand Recall");
        s.Set("sigil_recall_cooldown_speed_+%",100);
        var session = new Game.ProjectileSession("recall engine",wire.Stream,wire.Ciphers,c); c.ProjectileRuntimeState = session;
        try
        {
            Assert.True(Game.HierophantEngineAdmit(session,skill,Paid(c,skill),(300,300)));
            var plan = Assert.IsType<Game.HierophantRecallPlan>(Game.PrepareHierophantRecall(session,recall.Id,c.ActionCount+1));
            Assert.Equal(1u,plan.ExtraMana); Assert.Equal(0u,plan.ExtraLife); Assert.True(Game.HierophantRecallCurrent(session,plan));
            var ordinary = SingleUseCooldown.ForSkill(Game.SkillsFor(c),Program.Stats,recall.Effect,recall.Level,s.Read(),Game.SupportedSkill(c,recall.Id).Stats)!.Value;
            var cooldown = new SingleUseCooldownState(0,1);
            Assert.True(cooldown.TryUse(0,ordinary));
            cooldown.Refresh(1.999,2); Assert.False(cooldown.Ready); cooldown.Refresh(2,2); Assert.True(cooldown.Ready);
            Game.ItemsOf(c).Remove(PlayerInventories.BodyArmour,equipped.Id);
            Assert.False(Game.HierophantRecallCurrent(session,plan));
        }
        finally { session.Invalidate(); await session.OutputCompleted; }
    }

    public async Task Shockwave_uses_actual_supported_area_hit_and_death_clears_actor_generation()
    {
        using var s = new OrdinaryHierophantConnectedTests.Setup(); using var wire = new Wire();
        var c = s.Conn; c.Walk.Teleport(300,300,0); ushort skill = Equip(s,"Shockwave Totem");
        var session = new Game.ProjectileSession("shockwave engine",wire.Stream,wire.Ciphers,c); c.ProjectileRuntimeState = session;
        try
        {
            var near = Target(c,10); var far = Target(c,11,390);
            var totem = new TotemState.Totem(100,skill,(300,300),1000,8,null); c.Totems.Place(totem,1,0);
            Assert.True(Game.HierophantEngineAdmit(session,skill,Paid(c,skill),totem.At,totem));
            Game.HierophantEngineStep(session); Assert.True(near.Life < near.MaxLife); Assert.Equal(far.MaxLife,far.Life);
            var actor = Assert.Single(Game.HierophantEngineActors(session));
            c.PlayerLife = 0; Game.HierophantEngineStep(session); Assert.Empty(Game.HierophantEngineActors(session));
            c.PlayerLife = 1000; Assert.False(Game.HierophantEngineCurrent(session,actor));
        }
        finally { session.Invalidate(); await session.OutputCompleted; }
    }
}
