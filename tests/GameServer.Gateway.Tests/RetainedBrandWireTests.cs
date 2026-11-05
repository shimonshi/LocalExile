using GameServer.Core.Protocol.Instance;

namespace GameServer.Gateway.Tests;

public sealed class RetainedBrandWireTests
{

    public void Recovered_Brand_states_preserve_full_identity_and_inner_endian()
    {
        // Independent native writer/reader contract: outer 141fdec00/141fdecb0, Brand reader 141bfa2d0.
        // These are reviewable witness-shaped bytes, not a recording of official-server packets.
        var storm=new InstanceRetainedAction.Identity(0x2bb2,0x104,0x1002,2);
        var armageddon=new InstanceRetainedAction.Identity(0xa4d6,0x105,0x1004,2);
        Assert.Equal(Convert.FromHexString("02C6000010000000000000002BB20104100202000702CC3000001E00"),
            InstanceRetainedAction.Attach(4096,storm,12492,30));
        Assert.Equal(Convert.FromHexString("02C600001000000000000000A4D60105100402000702CC3000001E00"),
            InstanceRetainedAction.Attach(4096,armageddon,12492,30));
        Assert.Equal(Convert.FromHexString("02C6000010000000000000002BB2010410020200050100009040"),
            InstanceRetainedAction.Detach(4096,storm,4.5f));
        Assert.Equal(Convert.FromHexString("02C600001000000000000000A4D60105100402000104"),
            InstanceRetainedAction.Retire(4096,armageddon));
        Assert.Equal(Convert.FromHexString("02C6000010000000000000002BB2010410020200050100000000"),
            InstanceRetainedAction.Detach(4096,storm,0));
        foreach(float invalid in new[]{-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            Assert.Throws<ArgumentOutOfRangeException>(()=>InstanceRetainedAction.Detach(4096,storm,invalid));
        Assert.Equal(4478,Game.Art.AnimatedRow("storm_brand_attached",0));
        Assert.Equal(4479,Game.Art.AnimatedRow("armageddon_brand_attached",0));
        Assert.Null(Game.Art.AnimatedRow("missing_attached_art",0));
        Assert.Equal(Convert.FromHexString("01C1000030CC000000000000001E04C20000100001040240600000000000020000117E00000000"),
            Game.DefaultBrandAttachmentBuff("StormBrand",0,12492,30,4096,260,3.5f));
        Assert.Equal(Convert.FromHexString("01C1000030CC000000000000001E04CE0000100001050240600000000000020000117F00000000"),
            Game.DefaultBrandAttachmentBuff("ArmageddonBrand",0,12492,30,4096,261,3.5f));
        Assert.Null(Game.DefaultBrandAttachmentBuff("Fireball",0,12492,30,4096,260,3.5f));
        Assert.Null(Game.DefaultBrandAttachmentBuff("StormBrand",1,12492,30,4096,260,3.5f));
        Assert.Null(Game.DefaultBrandAttachmentBuff("StormBrand",0,0,30,4096,260,3.5f));
        Assert.Null(Game.DefaultBrandAttachmentBuff("StormBrand",0,12492,0,4096,260,3.5f));
        foreach(float invalid in new[]{0f,-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            Assert.Null(Game.DefaultBrandAttachmentBuff("StormBrand",0,12492,30,4096,260,invalid));
    }
}
