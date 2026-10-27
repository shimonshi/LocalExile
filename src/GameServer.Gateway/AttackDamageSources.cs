/// <summary>Tracks which derived attack inputs have already joined this transient source snapshot.
/// The marker is server-only; it is never serialized as a stat or saved on the character.</summary>
sealed class AttackDamageSources : Dictionary<uint, int>
{
    internal bool ArmourDerived { get; set; }

    internal AttackDamageSources(IReadOnlyDictionary<uint, int> source) : base(source)
        => ArmourDerived = source is AttackDamageSources { ArmourDerived: true };
}
