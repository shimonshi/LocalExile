using GameServer.Core.Projectiles;
using GameServer.Core.Protocol.Instance;

/// <summary>Gathering Winds' pinned radius60 ally source. Tailwind never stacks; Gale scales its owner only.</summary>
static class DeadeyeGaleAura
{
    public const double Radius = 60;
    // Actor keys must be unique within World. Per-connection PlayerId/epoch pairs are not global identities.
    public sealed record Source(ProjectileActorKey Owner, int Team, ProjectileVector Position, DeadeyeGaleState State)
    {
        public required object World { get; init; }
    }
    public readonly record struct Received(ProjectileActorKey Source, int SelfEffect)
    {
        public double ActionFactor(Func<string, int> recipient)
        {
            int selfEffect = SelfEffect;
            return ActionSpeed.From(name => name switch
            {
                ActionSpeed.HasTailwind => 1,
                ActionSpeed.Reduction => recipient(name) - (recipient(ActionSpeed.HasTailwind) != 0 ? 0 : 8),
                ActionSpeed.TailwindEffect => recipient(name) + selfEffect,
                _ => recipient(name)
            }).Factor;
        }

        public byte[] Native(uint recipient, ushort buff) =>
            InstanceEntities.AddBuff(recipient, buff, 1094, Source.Id);
    }

    public static Received? ForRecipient(IEnumerable<Source> sources, ProjectileActorKey recipient, int team,
        ProjectileVector position, bool alive = true, object? world = null) => !alive || world is null ? null : sources
        .Where(s => ReferenceEquals(s.World, world) && s.State.Tailwind && s.Team == team
            && (s.Position - position).Length <= Radius)
        .OrderByDescending(s => s.Owner == recipient ? s.State.TailwindEffect : 0)
        .ThenBy(s => s.Owner.Id).ThenBy(s => s.Owner.Generation)
        .Select(s => (Received?)new Received(s.Owner, s.Owner == recipient ? s.State.TailwindEffect : 0))
        .FirstOrDefault();

    /// <summary>Recipient-local native lifetime. Evaluate at the send gate with current source/lease inputs.</summary>
    public sealed class Presentation
    {
        ushort buff;
        Received? shown;
        object? lease;
        ProjectileActorKey actor;
        public byte[] Build(Received? current, ProjectileActorKey recipient, object world, Func<ushort> next)
        {
            if (!ReferenceEquals(lease, world) || actor != recipient)
            { buff = 0; shown = null; lease = world; actor = recipient; }
            if (shown == current) return [];
            var packets = new List<byte>();
            if (buff != 0) packets.AddRange(InstanceEntities.RemoveBuff(recipient.Id, buff));
            buff = 0; shown = current;
            if (current is { } received)
            { buff = next(); packets.AddRange(received.Native(recipient.Id, buff)); }
            return [.. packets];
        }
    }
}
