# Ordinary Inquisitor

This is an ordinary Templar ascendancy on the original `Default` passive graph in client **3.29.3b**.
It uses the existing choice, separate point budget, connected allocation, paid refunds and character saves.
No Phrecia opt-in or client graph installation is needed.
Native Labyrinth/Altar acquisition remains issue #151; the development commands select the class and award points.

## Nodes and consumers

| Notable | Graph ID | Execution |
|---|---|---|
| Righteous Providence | 53884 | +50 Strength/Intelligence; increased attack and spell critical chance from the lower final attribute |
| Inevitable Judgement | 48214 | Existing common hit resistance stage: critical elemental resistance ignore; noncritical penetration |
| Augury of Penitence | 40059 | Hostile living enemies within 60 cells take increased elemental damage and deal 8% less elemental damage |
| Instruments of Justice | 3154 | Main-hand weapon local critical base replaces the spell base before skill-added critical chance and increases |
| Sanctuary | 39790 | Radius-18 stationary Consecrated Ground, actual life regeneration/curse mitigation and increased enemy damage taken |
| Pious Path | 32816 | Four-second per-recipient effect linger; life regeneration also feeds the owner's actual ES regeneration |
| Instruments of Virtue | 19417 | Local main-hand weapon damage added to spells before effectiveness/scaling/conversion; recent manual spells give up to 30% more attack damage |
| Instruments of Zeal | 13851 | Periodic Fanatic charges from attacks; maximum charges grant timed Fanaticism, with self-cast spell speed, cost and area consumers |

The graph's small-node stats continue through the normal passive stat aggregation.
Ruthless raw stat alternatives come from the existing mode-specific passive catalog.
Augury uses its separate Ruthless attachment: 8% rather than 16% increased elemental damage taken.
It remains a separate buff from Sanctuary's increased damage taken.
These increases add to the monster's existing increased/reduced damage taken before separate more/less multipliers.
Physical/chaos damage does not receive Augury's elemental effect.
Critical resistance ignore continues to respect the common prohibition against penetrating or ignoring resistances.

Justice reads only the active usable main-hand weapon, including its local critical modifiers.
It does not copy global attack critical chance or the inactive/off-hand weapon.
An empty main hand supplies zero; generic cannot-crit still wins.
Battlemage similarly copies the weapon's local physical/elemental/chaos damage, not its globally scaled attack damage.
Spell effectiveness applies once to that addition; spell/type increases and conversion then use the existing pipeline.

## Timing and lifecycle

The pinned `Metadata/StatDescriptions/stat_descriptions.txt` says Fanaticism lasts **five seconds**.
The underlying stat name still contains `for_4_seconds`; its name is not a duration source.
The extracted node grants three maximum Fanatic charges in the ordinary mode and five in Ruthless.
The buff grants self-cast spells 75% more cast speed, 75% less cost and 75% increased area.
It does not speed up attacks.
The manual skill admission path records attacks and non-instant spell uses; triggered spells are excluded.
The pausable world clock governs history, periodic gains and effect expiry.
Source removal clears its transient state immediately on the next authoritative observation.
Death clears it; accepted area tickets carry combat history, charges and remaining Fanaticism once, freezing loading time.
Transient state is not saved across logout or a process restart.

Pious Path keeps effects on recipients after they leave; it does not leave a stationary ground at an old position.
Owner-created Sulphur ground participates without duplicating its numerical regeneration.
Its native owner buff and the stationary-ground buff have separate ownership, with one displayed ground buff at a time.
Enemy observations run on the player tick, while damage independently checks current hostility, life and distance.
The stationary ground uses the existing extracted Consecrated Ground art.
Pious Path copies regeneration, including the existing temporary regeneration sources; it does not copy flask recovery.
Client corrections use the same ES regeneration input as the resource writer.

## Verification and limits

The new tests cover pure state timing and production Gateway paths, including actual connected ordinary allocation,
five-point refunds, reloads, main-hand critical base, Battlemage damage, recipient effects and additive damage taken.
Client evidence must distinguish received numerical state, accepted actions and actual elapsed/resource behavior.
Passing these tests alone is not whole-class native acceptance.

The existing execution engines still bound the class: unsupported triggered skills, totems and allied-player/minion
recovery do not become supported by forwarding Inquisitor stats.
Fanatic charges' idle lifetime needs an independent native/reference check before claiming exact retail parity.
Ground observation and resource-rate refresh follow the existing player tick; exact sub-tick departure recovery has
not been independently accepted.

## Ownership and integration

Issue #363 owns this class and its feature-specific helpers/tests.
Juggernaut (#152), native acquisition (#151), and the leech implementation in PR #318 are not taken over.
The branch starts at main `5d5b5150e1b2e287c642edcaf75a49d075b9b4fe`.
Shared edits are minimal calls into `Program.Inquisitor.cs`; there are no copied unmerged Phrecia implementations.
Before readiness, recheck exact related heads at stat assembly, spell cost, damage and transfer hooks.
In particular, a future life/mana cost engine must apply Fanaticism's less-cost factor before final rounding to both
cost pools, rather than retaining the current mana-only supported payment approximation.
Do not call the class complete while native acceptance or its intended integration order remains unverified.
