using GameServer.Core.Protocol.Instance;
using GameServer.Core.Protocol.Login;

namespace GameServer.Gateway.Tests;

public partial class ConditionalCriticalGatewayTests
{
    sealed partial class Contact
    {
        public Task GroundAdmission(ushort skill, int x, int y, ushort flags = 0x408)
        {
            var bytes = new LoginWriter().U16(InstanceProtocol.UseSkillAtLocationId).U32((uint)x).U32((uint)y)
                .U16(skill).U16(0).U16(flags).ToArray();
            return Program.OnClientMessages("ordinary-ground-admission", writer.GetStream(), ciphers,
                [(InstanceProtocol.UseSkillAtLocationId, bytes)], [], Conn).WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    static void GroundGale(Contact f)
    {
        var definition = Program.Ascendancies.Find(2, AscendancyFamily.Normal, "Deadeye")!;
        f.Conn.MainAscendancy = new(definition.Identity, [59837, 24848], AscendancyRules.AwardKeys);
        new AscendancyRules(Program.Ascendancies).Validate(2, f.Conn.MainAscendancy);
        f.Conn.Monsters.Clear();
        f.Set("global_cannot_crit", 1);
        f.Set("attack_speed_+%", -90); // A real long reservation makes the immediate-repeat rejection deterministic.
        f.Refresh();
    }

    static int GroundGaleCount(Contact f) =>
        Program.CharacterPlayerStats(f.Conn).GetValueOrDefault(Program.Stats.Key("gale_force_stacks")!.Value);

    [Theory]
    [InlineData("Rusted Sword")]
    [InlineData("Crude Bow")]
    public async Task Deadeye_ground_default_attacks_admit_once_reject_early_repeats_and_scale_action_time_once(string weapon)
    {
        using var f = new Contact(new Draws(), weaponName: weapon, classRow: 3);
        GroundGale(f);
        f.Set("virtual_action_speed_+%", 0);
        double firstAt = f.Conn.Clock.Elapsed.TotalSeconds;
        await f.GroundAdmission(0x102, 32, 32);
        double firstUpper = f.Conn.NextAttackAt - firstAt;
        double firstLower = f.Conn.NextAttackAt - f.Conn.Clock.Elapsed.TotalSeconds;
        Assert.Equal(1, GroundGaleCount(f));
        Assert.True(firstLower > 1);
        int action = f.Conn.ActionCount;
        double deadline = f.Conn.NextAttackAt;
        await f.GroundAdmission(0x102, 32, 32);
        Assert.Equal(action, f.Conn.ActionCount);
        Assert.Equal(deadline, f.Conn.NextAttackAt);
        Assert.Equal(1, GroundGaleCount(f));

        f.Conn.NextAttackAt = 0;
        f.Set("virtual_action_speed_+%", 100);
        double secondAt = f.Conn.Clock.Elapsed.TotalSeconds;
        await f.GroundAdmission(0x102, 32, 32);
        double secondUpper = f.Conn.NextAttackAt - secondAt;
        double secondLower = f.Conn.NextAttackAt - f.Conn.Clock.Elapsed.TotalSeconds;
        Assert.Equal(2, GroundGaleCount(f));
        // Each sample lies between dispatch entry and completion, including cold data initialization.
        Assert.True(2 * secondLower <= firstUpper && 2 * secondUpper >= firstLower,
            $"Action factor2 must halve the reservation: [{firstLower},{firstUpper}] vs [{secondLower},{secondUpper}]");
    }

    [Fact]
    public async Task Deadeye_ground_move_and_unforced_melee_approach_do_not_grant_Gale_but_forced_swing_does()
    {
        using var f = new Contact(new Draws(), weaponName: "Rusted Sword", classRow: 3);
        GroundGale(f);
        await f.GroundAdmission(0x2909, 100, 32);
        Assert.Equal(0, GroundGaleCount(f)); Assert.Equal(0, f.Conn.NextAttackAt);
        await f.GroundAdmission(0x102, 100, 32);
        Assert.Equal(0, GroundGaleCount(f)); Assert.Equal(0, f.Conn.NextAttackAt);
        Assert.Equal((100, 32), f.Conn.Walk.Destination);
        await f.GroundAdmission(0x102, 100, 32, 0x409);
        Assert.Equal(1, GroundGaleCount(f)); Assert.True(f.Conn.NextAttackAt > 0);
        Assert.NotEqual((100, 32), f.Conn.Walk.Destination);
    }

    [Theory]
    [InlineData("paused")] [InlineData("dead")] [InlineData("leaving")] [InlineData("retired")]
    public async Task Deadeye_rejected_ground_melee_use_never_reserves_or_grants_Gale(string reason)
    {
        using var f = new Contact(new Draws(), weaponName: "Rusted Sword", classRow: 3);
        GroundGale(f);
        switch (reason)
        {
            case "paused": f.Conn.Clock.Stop(); break;
            case "dead": f.Conn.PlayerLife = 0; break;
            case "leaving": f.Conn.LeavingArea = true; break;
            case "retired": f.Conn.RageRetired = true; break;
        }
        int action = f.Conn.ActionCount;
        await f.GroundAdmission(0x102, 32, 32);
        Assert.Equal(0, f.Conn.NextAttackAt); Assert.Equal(action, f.Conn.ActionCount);
        Assert.Equal(0, GroundGaleCount(f));
    }
}
