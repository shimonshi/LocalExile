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
| Pious Path | 32816 | Four-second per-recipient effect linger; life regeneration also feeds the owner's additional ES recovery |
| Instruments of Virtue | 19417 | Local main-hand weapon damage added to spells before effectiveness/scaling/conversion; recent manual spells give up to 30% more attack damage |
| Instruments of Zeal | 13851 | Periodic Fanatic charges from attacks; maximum charges grant timed Fanaticism, with self-cast spell speed, cost and area consumers |

The graph's small-node stats continue through the normal passive stat aggregation.
Ruthless raw stat alternatives come from the existing mode-specific passive catalog.
Augury uses its separate Ruthless attachment: 8% rather than 16% increased elemental damage taken.
It remains a separate buff from Sanctuary's increased damage taken.
These increases add to the monster's existing increased/reduced damage taken before separate more/less multipliers.
Physical/chaos damage does not receive Augury's elemental effect.
Sanctuary also affects reflected Impale; Augury reduces the pre-critical base of supported incoming ignite.
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
Active equipped-item buff effect scales all three magnitudes, with developer replacements applied to the raw total.
It does not speed up attacks.
The manual skill admission path records attacks and non-instant spell uses; triggered spells are excluded.
The pausable world clock governs history, periodic gains and effect expiry.
Source removal clears its transient state immediately on the next authoritative observation.
Death clears it; accepted area tickets carry combat history, charges and remaining Fanaticism once.
Loading time is frozen.
Transient state is not saved across logout or a process restart.
Raw Inquisitor observations resolve equipment sources without publishing character-global qualification.
Retired resource/player/charge/flask writers stop, and queued publications reject retired connections.
The common source publisher checks ownership under the connection lock.
An old area therefore cannot remove the live connection's qualified gear or socketed Fireball.
Temporary leaving can roll back and resume recovery; accepted combat history still carries after leaving is set.

Pious Path keeps effects on recipients after they leave; it does not leave a stationary ground at an old position.
Owner-created Sulphur ground participates without duplicating its numerical regeneration.
Its native owner buff and the stationary-ground buff have separate ownership, with one displayed ground buff at a time.
Enemy observations run on the player tick, while damage independently checks current hostility, life and distance.
Generic `PlayerWalk.Changing` and `Changed` notifications observe ground on both sides of accepted motion mutations.
A short walk between ticks therefore records departure and arrival instead of appearing permanently stationary.
Natural arrival starts at `ArrivesAt` only when the previous observation was moving with unchanged live sources.
New allocations and resurrection start their effects when observed rather than at an older arrival.
Relocating stationary ground closes old enemy recipients at the teleport time, before the next target query.
The stationary ground uses the existing extracted Consecrated Ground art.
Pious Path copies regeneration, including the existing temporary regeneration sources; it does not copy flask recovery.
This is additional ES recovery, not base ES regeneration: ES regeneration increases do not scale it again.
ES recovery rate applies once, while life recovery rate does not scale the copy.
Client corrections publish the combined recovery rate; the resource writer adds the separate recovery integral.

## Verification and supported model

The Gateway regressions exercise connected ordinary allocation, five-point refunds, reloads and actual consumers.
They include all five equipped weapon damage types, reflected Impale, incoming critical ignite, accepted motion,
four-second native-sender removal and the separation of life regeneration from life-only recovery multipliers.
Pure timing tests additionally cover pause, death, source removal and one-use area carry.

### Recorded checks

Final production source `6b3c7a2fd86505cd48d9e89692e4c5d5432554f3` contains main
`5d5b5150e1b2e287c642edcaf75a49d075b9b4fe` and the declared panel prerequisite
`f651d0e6af8ea42614a8a137fced4b005a23b189`.
The solution builds with zero warnings/errors; Core **390**, Data **34** and Gateway **2629** pass.
The 67 Inquisitor checks are included in that Gateway total.
Gateway test collections run serially because the existing chill timer is sensitive to parallel machine load.
Game data was generated through `extract-data.ps1`; production paths and shared test setup are unchanged.

Native verification uses an isolated account/database through `scripts/dev/live.ps1` and ordinary client actions.
Read-only probes distinguish received stats and buffs from actual resource changes and enemy life loss.
All **39** final-source checks pass and cover:

- Ordinary choice and eight separate points; Providence attributes/critical chance; five-point refunds;
  Pious recovery publication; Justice main-hand critical base, cannot-crit and relog.
- Actual fixed fire hits against a level-70 Goatman with 40% fire resistance: baseline 60, Augury 70,
  Sanctuary 69, additive Sanctuary/Augury 79, Judgement noncritical penetration 70 and critical ignore 170.
  The critical-ignore prohibition restores 102; full Judgement refund restores the resisted base critical hit of 90.
  Judgement's connected small node supplies the extra 20 critical multiplier in the 170/102 controls.
- Accepted bow attacks, one/two partial Fanatic charges, the consumed maximum, the five-second native buff,
  natural expiry, paid refund and no idle resurrection of a removed source.
- Actual Fireball mana payments, Virtue's three attack stages and natural expiry, Battlemage and paid refund.
  The comparisons replace independent Strength/melee and connected elemental-increase inputs with zero.
  A controlled active-item buff-effect source of 10% raises Fanaticism's magnitudes from 75 to 82.
  The native self-cast values and actual mana payment change accordingly; refunds restore the baseline.
  These are received skill records and real payments, not independently timed projectile contacts.
- Pious owner-buff/marker removal while walking, stationary teleport and a short arrival followed by a new departure.
  The server deadline is four seconds; native observations include the existing tick/cache and probe latency.
- Actual Pious ES recovery with ordinary ES regeneration/recharge disabled and a 1000% ES-regeneration increase.
  Life-only recovery and ES recovery controls verify the copy's separate scaling; paid refund stops it.
- A real Gateway-process restart restoring saved Providence nodes, attributes and critical increase.

Final native Gateway DLL SHA-256:
`7eb64825efc60cbf073bbbb12743ea6f77d9ee9de525a87676456801ddc39048`.
Source/build metadata and local logs are retained under `logs/` and contain local game/account data.
They are not committed.
Earlier isolated source revisions also supplied native evidence; they are superseded for final combined acceptance.

The basic lifecycle is reproducible with `scripts/re/inquisitor-smoke.py` on a fresh isolated Templar.
Its default invocation checks protocol/path assumptions offline.
`--run --expected-head <sha> --private-file <json>` requires matching running services and an isolated database.
The private JSON contains `account`, `password` and `character`; do not commit it.
The script does not start services or install game assets.
Its instance-only read probe is `scripts/re/frida/trace-inquisitor-stats.js`.
The final spell observations use `scripts/re/frida/trace-inquisitor-skill-stats.js`: the local Actor's current
Fireball instance, with matching received stat containers at +0xf8 and +0x138.
The runtime CLI excludes its RE script directory from module lookup so the sibling `dis.py` cannot shadow
the standard-library module while Frida initializes.
The offline self-check does not load Frida.

### Explicit engine boundaries

This acceptance covers the supported server executors above.
Native Labyrinth/Altar acquisition remains #151; selection and point awards use development commands.
Unsupported allied-player/minion recovery, totems, proxies and triggered skills remain shared engine work.
Current skill payment supports mana.
A future life-cost engine must apply Fanaticism before final rounding to both pools.
An accepted paid non-instant manual spell counts at admission, including one whose later animation is cancelled.
Refused uses and instant spells do not count.
Partial Fanatic charges persist until consumption, death, source removal or session cleanup in this server model.
The pinned buff rows and PoB's configured condition do not establish a retail partial-charge idle duration.
The shared resource-rate cache retains its existing 250 ms refresh boundary.
These boundaries do not claim exact official-server rounding or sub-tick timing.

## Ownership and integration

Issue #363 owns this class and its feature-specific helpers/tests.
Juggernaut (#152), native acquisition (#151) and the panel/leech implementation in PR #318 keep their existing owners.
Shared feature edits call `Program.Inquisitor.cs`, propagate the reflected-hit owner and reject stale publishers.
The retirement guards also close the existing slower resource/critical/charge/flask publication paths.
No client graph, archive maintenance, extraction format or production data-path change is added.

The [Inquisitor draft PR](https://github.com/shimonshi/LocalExile/pull/1) is based on
`candidate/pr318-main361-admission` in the contributor fork.
Its base is the standalone reconciliation candidate `f651d0e6`, proposed to the #318 owner in
[evrydayjunglist/evryPath#1](https://github.com/evrydayjunglist/evryPath/pull/1).
That candidate includes exact #318 head `ff61e93791682558d25f4dc609c2217e9245772c` and current main.
The post-merge fixture migrations change tests/docs only and contain no Inquisitor source or tests.
Its independent build and Core **389**, Data **34**, Gateway **2562** tests pass.
They preserve main's host inactivation for unmet socketed gems and the client-predicted projectile policy.

Intended upstream order: the owner reconciles/accepts the proposal into #318, #318 merges, then Inquisitor rebases
onto the resulting main and repeats the required checks before upstream readiness.
No other owner's published branch is modified.
The contributor-fork draft provides a class-only review diff while the upstream prerequisite is pending.
It is not an upstream merge-ready PR.

A disposable checkout tested main `5d5b5150` -> repaired #318 `f651d0e6` -> Inquisitor `6b3c7a2f` ->
#328 `07699a51a0b235ca668c6223a51d1362d375d7e9` ->
#333 `7b9e1061862ea410d05f2304bd8ebb43a0b554de` ->
#339 `e7d4cf43013e6175157970c13c3c1aed4292b62a`.
The private candidate is `161cad85cce341c07b6cce59b9319e0694574560`.
It builds with zero warnings/errors and passes Core **394**, Data **34**, Gateway **3025**,
including **72** Inquisitor checks in that composition.
Resolutions retain Vaal/Rampage, lethal-ignite Recoup cleanup, mana-recovery precision, typed ES bypass,
recipient clocks and the new retirement guards.
Private regressions cover the combined Inquisitor paths and retirement during queued #339 refreshes.
Those related features and their fixture migrations are kept out of the published Inquisitor diff.

This does not establish readiness for the entire future merge block.
#339 introduces typed recipient expiry; its interaction with Inquisitor's world-clock Fanaticism and ground linger
needs an explicit adapter/oracle and native validation before that combination is accepted.
The candidate preserves the existing five-second Fanaticism and four-second linger rather than guessing new semantics.
Recheck exact downstream heads after each prerequisite merge; do not substitute isolated green checks for that work.
