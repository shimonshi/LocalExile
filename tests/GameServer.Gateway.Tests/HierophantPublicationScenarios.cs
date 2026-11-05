using GameServer.Core.Protocol.Instance;

namespace GameServer.Gateway.Tests;

static partial class OrdinaryGameplayChecks
{
    internal static void DistillerHierophant()
    {
        using var s = new OrdinaryHierophantConnectedTests.Setup(true); var c = s.Conn;
        s.Set(OrdinaryHierophantRules.ManaSurge,200); s.Set(OrdinaryHierophantRules.SurgeMore,8);
        s.Set(OrdinaryPathfinderRules.DistillCost,3);
        s.Set("pathfinder_skills_critical_strike_chance_+%_if_charges_consumed_from_diamond_flask",150);
        s.Set("virtual_intelligence_for_requirements",1000);
        var items = Game.ItemsOf(c);
        foreach (var old in items.Items(PlayerInventories.Flask)) items.Remove(PlayerInventories.Flask, old.Id);
        items.Add(PlayerInventories.Weapon1,0,0, ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!) with
            { Sockets = [new(SocketColour.White,ItemCatalog.Create(Program.Items.Find("Fireball")!))], Links = [1] });
        items.Add(PlayerInventories.Flask,0,0,ItemCatalog.Create(Program.Items.Find("Diamond Flask")!) with { Charges = 10 });
        var gem = Game.GemSkillEffects(c).Single(g => Game.SkillsFor(c).EffectId(g.Effect) == "Fireball");
        _ = s.Read(); var state = Game.HierophantOf(c); state.Spend(0,199);
        using var use = Game.BeginOrdinaryUse(c);
        Assert.True(Game.PayDistillerUseLocked(c,true,() => Game.PayMana(c,gem.Id,out _,out _,extraMana:10)));
        Assert.True(state.Active(0));
        Assert.Equal(0,Game.HierophantOffenseStats(c).GetValueOrDefault(Key("has_arcane_surge")));
        Assert.True(use.Distillation!.Mana > 10); Assert.Equal(1000u-use.Distillation.Mana,c.PlayerMana);
        Assert.Equal((int)use.Distillation.Mana-1,state.ManaRemainder);
        s.Set(OrdinaryPathfinderRules.DistillCost,0);
        using var read = new Game.DistillerOffense();
        Assert.Equal(150,Game.DistillerInputs(c,new Dictionary<uint,int>())[Key("critical_strike_chance_+%")]);
        Assert.Equal(7,items.Items(PlayerInventories.Flask)[0].Item.Charges);
    }
}
