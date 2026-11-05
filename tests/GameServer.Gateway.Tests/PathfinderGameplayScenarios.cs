using GameServer.Core.World;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.Protocol.Login;
using GameServer.Core.Projectiles;
using GameServer.Modding;
using System.Net;
using System.Net.Sockets;

using League = GameServer.Core.Accounts.League;
using MonsterRarity = GameServer.Core.Protocol.Instance.MonsterRarity;

namespace GameServer.Gateway.Tests;

static partial class OrdinaryGameplayChecks
{
    internal static async Task ConditionalToxicistProjectile()
    {
        var definition=new AscendancyCatalog().Definitions.Single(d=>d.Identity.Family==AscendancyFamily.Normal&&d.Name=="Pathfinder");
        foreach(string control in new[]{"active","inactive","refund"})
        {
            using var h=new Harness(definition); var c=h.Conn;
            c.MainAscendancy=new(definition.Identity,[59800,6038,20480,1697,32640,65296,40631,61805],AscendancyRules.AwardKeys);
            Assert.Equal(1,Game.CharacterPlayerStats(c).GetValueOrDefault(Key(OrdinaryPathfinderRules.SpreadDuringFlask)));
            foreach(var old in h.Items.Items(PlayerInventories.Flask)) h.Items.Remove(PlayerInventories.Flask,old.Id);
            var flask=h.Items.Add(PlayerInventories.Flask,0,0,ItemCatalog.Create(Program.Items.Find("Amethyst Flask")!) with { Charges=65 });
            if(control!="inactive")
            {
                await Game.UseFlask("Toxicist utility",h.Wire,h.Ciphers,c,0);
                Assert.NotNull(c.UtilityFlaskEffects.Of(flask.Id));
            }
            var session=new Game.ProjectileSession("Toxicist owner",h.Wire,h.Ciphers,c); c.ProjectileRuntimeState=session;
            try
            {
                double at=Math.Max(1,(c.UtilityFlaskEffects.Of(flask.Id)?.Started??0)+.1);
                session.AdvanceTo(ProjectileRuntime.Time(at));
                at=Game.ProjectileWorldTime(c);
                Assert.Equal(control!="inactive",Game.ActualFlaskEffectLocked(c,at));
                var donor=new SpawnedMonster("donor",new(102,100,0),0,0,1000);
                var recipient=new SpawnedMonster("recipient",new(107,100,0),0,0,1000);
                c.Monsters[808]=donor; c.Monsters[809]=recipient;
                var player=Game.CharacterPlayerStats(c); player[Key("base_chance_to_poison_on_hit_%")]=100;
                var poison=Game.PoisonOnHitLocked(c,donor,DamagePacket.Of(DamageType.Physical,10,10),true,false,
                    player,null,new(100,10,10,10,10,3),at,new(1));
                Assert.NotNull(poison); Assert.Equal(1,poison.Value.Poison.Stacks);
                if(control=="refund")
                {
                    var refunded=new AscendancyRules(new()).Refund(definition.BaseClass,c.MainAscendancy,[1697],0,0);
                    Assert.Equal(5,refunded.Cost); c.MainAscendancy=refunded.Progress;
                    Assert.Equal(0,Game.CharacterPlayerStats(c).GetValueOrDefault(Key(OrdinaryPathfinderRules.SpreadDuringFlask)));
                }
                using(Game.ProjectileMutation(c,advance:false))
                {
                    var offense=new ProjectileOffense(DamagePacket.Of(DamageType.Physical,100000,100000),true,0,0,150,"Toxicist finisher")
                        { CriticalRoll=new(0) };
                    session.Runtime.Release(new(new("Toxicist finisher") { Speed=1,Range=10 },new(Program.PlayerId),new(Program.PlayerId),1,
                        new(100,100),new(110,100)) { Payload=offense });
                    var entity=Assert.Single(session.Runtime.Projectiles);
                    Assert.Equal(ProjectileHitResult.Hit,session.Hit(new(entity,session.ActorKey(808,donor),session.Runtime.Now,"direct",new(102,100))));
                }
                Assert.Equal(0u,donor.Life);
                Assert.Equal(control=="active"?1:0,recipient.Poisons.Stacks(at));
                if(control=="active") Assert.Equal(poison.Value.Poison.StrongestDps,recipient.Poisons.TotalDps(at));
                session.AdvanceTo(ProjectileRuntime.Time(at+.5));
                await Game.TickPoisons("Toxicist tick",h.Wire,h.Ciphers,c);
                Assert.Equal(control=="active",recipient.Life<recipient.MaxLife);
            }
            finally { session.Invalidate(); await session.OutputCompleted; }
        }
    }
}
