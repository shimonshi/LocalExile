ï»¿# Ordinary Slayer: connected completion candidate

Issue [#391](https://github.com/LocalExile/LocalExile/issues/391) owns ordinary Slayer.
The source candidate connects pinned Default effects to supported owner hit, Ignite and RF routes.
Parent native testing exposed a missing Headsman native-buff presentation despite working kill/stat execution.
The native presentation follow-up supplies the adapter and the local publication candidate includes its tick hook.
It is an isolated source candidate for parent integration, not native acceptance or publication readiness.
The parent owns combined integration, general documentation, the shared runtime and PR publication.
No client or Gateway process was started, stopped, attached to or commanded by this worker.

## Revisions and evidence

The ordinary feature source is `762fbe43`; the local publication candidate uses main `89b7aa90` plus #339 `13aeea19`.
Its explicit snapshot, publication boundary and revalidation requirements are recorded in
[the combined acceptance contract](deadeye.md#related-integration-and-final-acceptance-work).
The parent's bounded native run on `762fbe43` found missing Headsman buff presentation despite derived stats.
The adapter and tick hook are integrated; final local automated checks are recorded in the combined acceptance doc.
Representative native controls on the reconciled publication head passed; remaining boundaries are recorded below.
The following revisions document earlier isolated implementation evidence.

The original allocated branch `t3/ordinary-slayer-full` was clean at `864da152` and remains untouched.
The new branch is `candidate/ordinary-slayer-full-391-20261031`, created at exact coordinator `f15a3abf`.
That coordinator contains upstream main `5fc27fa2` and the earlier connected Slayer/Deadeye slices.
Shared clock prerequisite `37aa0019` was cherry-picked as `d1d81d6d` before feature work was committed.
Its two files are byte-identical to the approved #339 `13aeea19` blobs.
Production commit `40abed736a5ffb9e8d2fa3d8cbd951f99cc1c8a1` contains the complete remaining Slayer hooks.
Existing common Gateway production ownership was released to the parent at that immutable commit.
Tests and this document follow separately without changing that production revision.

The client is 3.29.3b; Ascendancy row 9 is Slayer, Character row 4, ClassNo 1.
The Default graph is `metadata/passiveskillgraph.psg`, with start 33795 and seven small plus seven notable nodes.
Its original SHA-256 is `82D82CD3CD2DAD81606AF0DA9D18FCD53B2E92CF006550947D65A4F7E102DC32`.
The public graph export has 797 normal groups and seven orbits, SHA-256 `CB0B73E5...4460F5`.
Phrecia, Aristocrat, graph installation and startup archive writes are outside this change.
`OrdinarySlayerPinnedAuditTests` pins complete membership and every stat vector against extracted Default data.
Client stat keys below equal the corresponding Stats row plus one.

Pinned secondary calculations use PoB commit `16de4b82d57f1c0de6eb40f37143c32d4da36a02`.
[ModParser](https://github.com/PathOfBuildingCommunity/PathOfBuilding/blob/16de4b82/src/Modules/ModParser.lua)
identifies the independent MORE buckets, nearby multiplier cap, melee proximity flag and unaffected bleeding.
[ModStore](https://github.com/PathOfBuildingCommunity/PathOfBuilding/blob/16de4b82/src/Classes/ModStore.lua#L594)
models Impact at maximum potency through 15 units, then linearly to zero at 40.
This establishes calculation evidence, not measured official-server spatial or lifetime parity.
The continuous interpolation between those endpoints is an explicit LocalExile model.
One server cell is the existing 0.1 metre combat unit; spatial tests use centre-to-centre distance.

## Every pinned node and its execution

- Start 33795 is implicit and free; ordinary selection, membership and connected allocation validate it.
- Small 61393 supplies key 557 +15 critical chance and key 2655 +10 attack damage.
  The existing attack critical and player damage calculations consume these sources.
- Smalls 15286 and 42293 each supply key 71 +5 attack speed and key 2655 +10 attack damage.
  Existing pools, supported/default attack timing and hit damage consume them.
- Smalls 34215 and 45696 each supply key 2746 =40 attack Life leech and +10 attack damage.
  Forty permyriad means 0.4%; admitted attack contacts use the existing common leech engine.
- Small 20954 supplies key 437 +10 base skill area and +10 attack damage.
  Existing area projection and actual area admission consume the same source.
- Small 50845 supplies keys 424/421 +15 Frenzy/Endurance duration and +10 attack damage.
  Existing charge limits, expiry and panel projection consume both duration sources.

### Headsman 3184

Key 4766 enables inclusive post-hit 20% culling at an admitted owner skill contact.
A zero-damage accepted hit qualifies; block, evasion, phase interception and running stages do not.
Culled Life never enters damage dealt, Impale, Ignite bases, ordinary leech or overkill.
Keys 9886/9887 grant nonstacking, refreshing +10 movement and rare/unique-kill +10 attack speed for 20 seconds.
Normal and magic kills refresh movement only; rare and unique kills refresh both.
`WithSlayer` adds fresh contributions to raw movement, attack speed and area before normal pool/stat projection.
The inherent unarmed/dual-wield 10% MORE attack speed remains a separate existing multiplier.
A grant updates actual walking and attack speed synchronously; the player tick publishes grant/expiry changes.
Queued stat output rebuilds at its send gate, so expired/refunded contributions cannot be replayed.
BuffDefinitions rows 1341/1342 are `bane_of_legends_movement_speed` and `bane_of_legends_attack_speed`.
Both have category 18, despite their historical names, and use the shared recipient EffectClock accordingly.
Category-only elapsed modifiers for categories 1/7/13/16 therefore do not speed up Headsman.

`SlayerKillState.ReadBuffs` exposes immutable identity/value/remaining/deadline/rate snapshots under the owner lock.
A refresh or transfer restore gets a new grant identity even when its stat remains +10.
`SlayerBuffPresentation` projects rows 1341/1342 with +0x68 remaining wall seconds.
Both rows have `IsRecovery=false`; the native reader consumes no stat-value words for them.
The timed add is 31 bytes, including the empty trailing list; +10 remains in the common stat projection.
That countdown is remaining effect seconds divided by the unchanged shared EffectClock's current recipient rate.
It follows the existing Gale timed-buff packet pattern and does not add another player-stat contribution.
An unchanged timer sends nothing; refresh and rate/deadline changes remove the old instance and add a new one.
Movement and attack grants are independent: a normal kill never replaces the attack buff.
Expiry, source removal and in-area death remove only the instances previously shown to that owner.
The adapter allocates IDs from `conn.NextBuff` and builds every byte at the send gate under `conn.Monsters`.
It rereads sources and Life there and rejects closed, leaving, retired, unsent-player or stale-area-epoch leases.
Pause leaves the game clock frozen; projecting a pending valid buff during pause cannot spend its remaining time.
Loading emits no old-world packets, failed-transfer resume replaces rebased grants, and a new connection starts fresh.
No independent timer, replacement EffectClock, archive mutation or existing common hook is introduced by this fix.

The publication candidate adds this one call in `RunPlayerTick`, after the bleed/native-condition publication:

```csharp
await SendSlayerBuffs(remote, stream, ciphers, conn);
```

It is immediately before `SendAntiquarian`, preserving earlier condition-send ordering and held-gate tests.
The production tick invokes the adapter at this point; independent table-driven framing checks include a next packet.
Parent `0b997b37` has actual timer/UI observations below; that source is distinct from this publication composition.

### Overwhelm 17315

Key 9893 replaces weapon base critical chance with 800 permyriad before global increases.
It does not replace spell or unarmed base chance.
Key 9894 adds ten critical multiplier points per living nearby hostile, capped at 100.
LocalExile defines nearby as 70 cells centre-to-centre, including the target when eligible.
Owned actors, daemons, dead and nonhostile objects are excluded from this enumeration.
This radius is an explicit policy because no exact official radius was recovered from the pinned evidence.
Default attacks, supported area hits and projectiles add the bonus to source multiplier before target mitigation.
Target no-extra-critical-damage then resolves once, including to 100 when appropriate.
New Impale storage sees the completed source critical multiplier; old stored reflection is not multiplied again.

### Bane of Legends 62817

Keys 9885/4768 are separate unique-target 20% MORE and recent-kill 10% MORE buckets: together 1.32.
Default attacks, supported melee/area attacks and spells, and projectile contacts scale their direct typed packet.
Scaling occurs before target mitigation; target armour and resistance therefore see the scaled packet once.
Reflected old Impale is added afterward and does not receive these modifiers again.
Supported Ignite and Righteous Fire consume Bane in their actual burn ticks before target DoT mitigation.
Ignite keeps its independently rolled, frozen pre-hit ailment basis, including qualified Deadeye distance inputs.
It is never reconstructed from a mitigated or critical-multiplied hit.
LocalExile samples Bane's generic source and current recent/unique conditions live at hit or burn execution.
Refunding Bane therefore removes its future contribution, including to an existing Ignite's next tick.
This live generic-MORE policy is explicit; exact official snapshot parity is not claimed.

Key 7839 now has a real reflected-damage consumer at admitted owner hits.
Enemy raw flat when-hit/melee-thorns and percentage-reflect inputs enter the reflected typed packet.
The pinned `ReflectPhysicalDamageToSelfOnHitUnique__1` item modifier also executes on actual attacks.
It is the real Hiltless-style 100 physical self-reflection source, not a developer-only damage command.
Physical reflection immunity suppresses physical reflection and does not become elemental immunity.
Other reflected types pass their own resistances, hit reduction, taken bucket, Fortification and resource damage.
Reflection can consume ES/Mana/Life and cause authoritative player death.
Reflection-specific increased/reduced taken joins generic taken, Shock and leech taken after armour/resistance.
Armour sees unreduced reflected damage; Fortification's separate LESS factor applies after the combined bucket.
It grants no leech, enemy-hit history, Rage, charge, ailment, player kill credit or recursive reflection.
Owner Life/Mana/ES and death output are published by the player ticker from current state.
Proxy reflection-to-owner is suppressed under the current proxy policy; parent integration must retain that rule.

### Endless Hunger 34484 and Brutal Fervour 10143

Key 3467 grants 20% of mitigated direct damage exceeding pre-hit Life as nominal Life leech.
It joins ordinary leech before one common instance cap, conversion, prohibition and instant-fraction calculation.
Spells qualify; old Impale reflection, DoT, culled Life and intercepted phases do not inflate overkill.
Keys 3469/9415 retain Life leech at full Life and double maximum per-instance amount, not total recovery rate.
Keys 3468/3470/9888 grant attack speed, stun immunity and -10 increased damage taken while leeching.
Any active Life, Mana or ES instance qualifies, including retained full-Life leech.
Taken reduction joins the increased/reduced bucket with Shock; it is not a separate 10% LESS multiplier.
Explicit final stun replacement zero still wins, as verified in the prior connected candidate.

Key 10772 now suppresses actual incoming bleeding damage while qualifying leech is active.
`PlayerBleeding` retains individual durations and integrates only the strongest active bleed.
Incoming admitted monster attacks use raw bleed chance/mod sources and an independent physical-base roll.
Pinned `MonsterAddedPhysicalDamageAndBleed` and other ordinary monster bleed modifiers can actually inflict it.
Block/evasion, avoidance and immunity prevent admission; positive physical base is required.
Armour mitigation of the hit does not become the bleed basis.
The model uses 70% physical base per second for five seconds, and triples damage while moving.
Monster critical ailment multiplier is the existing Monster.ot 1.3, not the player's 1.5.
Live additional physical reduction and damage taken apply to DoT; hit armour and block do not.
Bleed goes through ES then Life, can kill, and grants no player kill credit or leech.
Unaffected retains the ailment and its expiry: damage resumes if leech ends before the bleed does.
Immunity instead prevents/removes bleeding, which the negative controls distinguish from unaffected.
The native `is_bleeding` stat and admission log expose the actual state without treating it as immunity.
Bleeding's effect list and recovery fraction are synchronized for reads, application, advancement and clearing.
The player tick tracks admission/removal and rebuilds the transmitted boolean at the send gate.
Immunity reconciles there; unaffected bleeding remains present and publishes one until removed or expired.
Continuous incoming DoT follows the existing 250 ms resource-writer stat cadence.
Sub-tick leech-expiry or movement-boundary integration is a documented model limit, not exact native parity.

### Impact 38180

Key 9890 adds five increased-area points per credited kill in the rolling last four world seconds, capped at 50.
Each kill expires separately; this is not one refreshing four-second stack timer.
Actual area admission and the native skill-area projection share the base-area contribution.
Key 1237 adds four melee cells to approach, contact reach, ground-attack routing and native weapon reach.
It does not enlarge bow or wand attack reach.
Key 85 uses existing 50% increased accuracy.
Key 9954 gives up to 15% MORE to actual melee attacks using the pinned PoB proximity ramp described above.
Spells and projectile contacts do not acquire that melee factor.
Removing the node removes reach, area coefficient and proximity damage through normal source rebuilds.

### Masterful Form 16306

Key 183 adds maximum Frenzy and key 9892 replaces completed maximum Endurance with completed maximum Frenzy.
Own Endurance modifiers do not add on top of the equality replacement.
Presence-aware explicit final replacements, including zero, apply afterward.
Panel, charge state, minimum clamping and special-charge mirrors share the authoritative maximum resolver.
The prior connected tests and native resource smoke establish maxima behavior, not new combat acceptance here.

## Kill attribution and lifecycle

`TakeOwnerDamageLocked` settles boss interception first and ordinary/overkill leech before assigning target Life.
It then applies eligible cull and records one owner-credited positive-to-zero lethal result synchronously.
Direct default, supported area, projectile, Ignite and RF commits all use this path.
No kill mutation waits for `MonsterKilled`, outbound packets, loot, experience or quest processing.
An area sweep stops admitting targets after cancellation or owner death, then drains its committed death list.
Lethal reflection therefore cannot discard an already-killed target's quest item and reward processing.
Dead/nonhostile/owned-target/daemon and phase outcomes cannot produce Slayer credit.
All credited kills enter four-second history even without a currently allocated recent-kill consumer.
Headsman's twenty-second grants require their actual source at the kill, so later allocation is not retrospective.
Source removal is observed in successful stat/pool rebuilds after durable payment/save commits.
Rejected or failed-save refunds cannot erase a live buff; reallocation after a real refund cannot resurrect it.

The game clock pauses with the existing pause mechanism.
Recent kills use world-time seconds; Headsman uses category-18 recipient effect time independently of action speed.
Transfer ingress freezes the kill state before waits and keeps output from the leaving owner inactive.
A failed transfer rebases exactly the remaining state once when that owner resumes.
Successful transfer carries remaining history/effect lifetime by one destination ticket and matching character.
Loading does not spend carried lifetime; stale unused tickets expire after five wall-clock minutes.
Restore occurs at actual destination player creation, behind its publication gate.
Death clears immediately in the Life setter, before notifications; retirement and disconnect also clear state.
Relog has no ticket carry and starts without kill history or temporary grants.
Incoming bleed follows the existing connection-local ailment policy: death/retirement/area change cleans it.
Failed loading resumes without back-paying skipped DoT; the existing resource writer owns that interval.

## Shared integration contract and order

Only this worker edited existing common Gateway production files during this phase.
Parent and Deadeye workers integrate afterward on their combined checkout.
No existing Core projectile or Deadeye-exclusive engine/test file was edited here.

`OwnerSkillHit` carries DirectDamage, Taken, Leech and Sources plus Melee, Attack and Attribution.
`SkillDamageAttribution` fields are CountsAsYourSkill, KillCreditToOwner, AllowCull, GrantOwnerLeech,
GrantOwnerRage, GrantOwnerOnHit, GrantOwnerChargeOnHit and RecursiveMirage, in that order.
Player is eight true flags; Mirage is `true,true,true,false,false,false,false,false`.
IndependentActor is all false; no attacker entity-id heuristic substitutes for these explicit flags.
Slayer's mutation consumes kill credit, cull and leech eligibility.
Parent must gate the other reactions at their sites.
`ApplyMonsterHitLocked(conn,target,totalDamage,ownerHit)` is the common proxy hit route.
Remove the temporary Mirage manual cull and separate kill path when adopting it.
`TakeOwnerDamageLocked(conn,target,damage,hit=null,attribution=null)` also permits explicit future DoT attribution.
The null-hit default is existing player Ignite/RF damage; it cannot cull or grant hit leech.

Preserve this target order when adding marks: source packets and generic MORE, mark target modifiers,
completed source critical multiplier, target mitigation, direct OwnerSkillHit context, then old reflected Impale.
Run the single phase/leech/cull/lethal commit, then the allowed source-specific reactions and notification.
Mark unique-target context must not be bypassed by reconstructing damage inside the owner mutation helper.
Independent Ignite bases and qualified Deadeye distance provenance remain separate from the hit.

The exact relevant #328 `4c90a1f9`, #333 `1aa9a177`, #339 `13aeea19` and #385 `ad1e7e32` diffs were inspected.
Their existing owners and #328 -> #333 -> #339 order remain unchanged.
At #328 merge, keep one TakeOwnerDamageLocked and call RampageOnKillLocked once after cull on the lethal result.
Do not return early from the null-hit branch: Ignite/RF owner kills still need both consumers.
Preserve independent Rampage weapon ownership, melee hit, Vaal, Rage, Fortification and projectile notifications.
OwnerId's identical #328 target-ownership property must be reconciled, not duplicated.
At #333 merge retain Mana-recoup disposal in TrimLeechLocked and its blind/accuracy consumers.
The publication snapshot retains #339's common clock blobs and recipient-rate/publication consumers.
Attack/cast helpers already consume raw action speed; their runtime wrappers apply the ailment clock only.
#385 is already in pinned main; retain support qualification and its authoritative local/gem damage ordering.
Parent owns exact-head combined regressions and any final ordered target context for marks/Mirage/Gale.

## Automated evidence and retained failures

All commands ran from this worktree root with the portable .NET 10 SDK.
Only whitelisted public extraction files were copied.
No accounts, keys, DBs, stamps or private client data were copied.
Build/test outputs stay under this worktree and save tests use isolated TestSaves.
Production builds with zero warnings/errors; Core 411 and Data 34 pass.
Focused tests exercise real admitted contacts and actual burn/resource mutations.
Scalar geometry and pure clock tests are distinguished by their names and test setup.
The 148-check focused pass preceded the final added reach/held-expiry controls.
Three legal eight-point builds cover all seven notables.
They include durable reload, paid refund and failed-save rollback.
Logs are retained under `logs/slayer-full/`; no unsuccessful attempt is gameplay acceptance evidence.
The first full Gateway pass had one existing 300 ms AI timeout and one nondeterministic new evasion control failure.
The evasion control now fixes the defender's entropy at the admitted test time instead of relying on random startup.
The initial missing-namespace build errors and earlier test-harness/time-boundary failures remain in their own logs.
Final full Gateway rerun passes 3,650 tests with zero failures/skips in default parallel execution.
Its code is production commit `40abed73`; the final pending test/document changes add 46 checks over the base.
Full logs are `build-final.log`, `core-final.log`, `data-final.log` and `gateway-final-repeat.log`.
Native acceptance and the parent's final combined-head regressions remain pending.
The isolated review follow-up starts at exact `bd722add` on `candidate/ordinary-slayer-review-fixes-391-20261031`.
Code/test fix `65562d9e572e0d6b8a5c754bcbc19be7c517e42b` addresses four independent P2 findings.
It changes only the reserved area death-drain, reflection taken, bleeding synchronization and tick-publication blocks.
Twelve focused regressions pass, including real quest-item drops, mitigation arithmetic and encrypted status packets.
The held-output test removes bleeding with immunity before the queued admission packet can be assembled.
The concurrency test verifies atomic reconciliation versus Clear, then stresses all state operations together.
Build has zero warnings/errors; Core 411, Data 34 and default-parallel Gateway 3,662 pass with no failures/skips.
These results test exact code/test commit `65562d9e`; the following documentation-only commit changes no code.
Logs are `review-build-final.log`, `review-core-final.log`, `review-data-final.log` and `review-gateway-final.log`.
Initial analyzer and test-control failures remain in `review-focused-first.log` and `review-focused-second.log`.
The successful focused run is `review-focused-third.log`.
An additional out-of-reservation finding was sent to the parent without another common-code edit:
the general unaffected source is `base_unaffected_by_bleeding`, not the existing misspelled lookup.
Slayer's pinned leech-conditioned source is correctly named and has actual unaffected-retention coverage.

The native presentation follow-up starts at exact `762fbe43182d4cf331fd40e522a78aec41e05e06`.
Its isolated branch is `candidate/ordinary-slayer-native-buffs-391-20261031`.
Code/test commit `7ac2573711831dd93618f1269b9401e1f8fa35cd` edits only SlayerKillState and adds owned files.
Parent reports native Default selection, eight-point allocation/relog, cull boundary and paid-refund controls passed.
A real normal hostile kill also passed, but no movement buff was received before the 25-second oracle timed out.
That is a failed native presentation check, not evidence against or a replacement for the buff oracle.
Parent evidence is coordinator `logs/ordinary-native-full-prep/run-20261031-022519-38932/result.json`.
The adjacent `native-events.jsonl` contains the parent's received-event record.
This worker did not operate the client or shared Gateway and does not claim a native rerun passed.
The new twelve focused checks pass without missing-data early returns.
They decode actual encrypted loopback output for real allocation, credited kills, refresh, refund, death and transfers.
Pure clock/presentation coverage separately checks independent expiry and category-18 recipient rate projection.
Held-send tests reject refunded/reallocated, dead, closed, retired, wrong-epoch and leaving leases.
Rejected queued adds allocate no native IDs.
Model and packet tests preserve the original stat projection rather than weakening the native-buff oracle.
The first test compile found an invalid assignment to read-only Paused; the corrected tests stop the real game clock.
Logs remain in `logs/slayer-full/native-buffs-*`; the single common hook and native acceptance are parent-owned.
Exact code/test `7ac25737` builds with zero warnings/errors and passes Core 434, Data 34 and Gateway 3,811.
The Gateway run uses default parallel execution; all three suites report zero failures/skips.
Logs are `native-buffs-build-final.log` and `native-buffs-{core,data,gateway}-final.log`.
Review subsequently found that its packet tests incorrectly assumed stat words for non-recovery rows.
Those green checks are retained history, not evidence of correct native framing.
The publication candidate includes `0b997b37`'s independent table-flag reader and following-packet sentinel checks.
That worker's following documentation-only commit did not alter tested code or install the common hook.
The publication branch separately installs the hook and validates its full current-main composition.

## Coordinator native subset

The parent reports actual-client controls on coordinator `0b997b37`; these paths are unchanged by `b3be9ba9`.
They cover cull boundaries and paid refunds; normal/rare Headsman grants show +10 for twenty seconds.
Bane damage controls observed 120/132, Overwhelm 160/170 and Impact 115 versus refund 100 with area expiry.
Masterful Form observed 4/0 maxima; actual retained full-Life leech, caps and source removal also passed.
Legal connected builds, allocation/refund and relog are included in the parent's bounded checks.

The coordinator repeated the automatic subset on actual publication head `dcbe3afce125604ded71487bffad388ccd23beca`.
Its code and tests equal fully tested `94d9d5bf`; base `7476a054` includes current main and exact #339.
Gateway SHA-256: `BCBB98B15B0C18E76AF76EF6172596ED1AA09FB8C7274F54781099AF40E1C078`.
All six legal Slayer/Deadeye builds, paid refunds and relogs passed in that run.
The Slayer combat controls above passed again, with natural Headsman attack-buff expiry at 20.071 seconds.
Evidence is publication `logs/ordinary-native-full-prep/run-20261031-033311-47360/`.
The run used one `live.ps1` client, synthetic saves and a frozen build; stopped state was restored and released.
No whole-class, every-manual-row or official parity claim follows from the automated subset.
Earlier failed presentation and fixture/protocol attempts remain retained, rather than being relabelled as passes.

## Offline native matrix for the parent

These are prepared oracles, not observations.
Reserve one runtime window, build the final combined source and start the client only through `scripts/dev/live.ps1`.
Record exact revisions, DLL hashes, source/stats, positions, admitted contacts and logs for every control.
Use separate synthetic saves and restore/release the shared runtime according to its existing ownership protocol.

1. Allocate builds `[3184,17315,38180]`, `[34484,16306,38180]`, then `[3184,34484]` by connected paths.
   Each has eight allocated nodes, covers all notables across builds, and preserves ordinary/Phrecia separation.
   Refund an endpoint for five points, relog and reallocate; also record rejected insufficient-payment behavior.
2. Kill normal/magic then rare/unique enemies; observe movement +10 and rare/unique attack +10 for twenty seconds.
   Confirm normal kills never grant attack speed, buffs refresh without stacking, and pause freezes expiry.
   Refund/reallocate Headsman during the window and verify no old grant revives.
3. Kill ten enemies within four seconds and observe Impact area reach a +50 increased cap.
   Stagger kills and verify individual expiry, actual larger area contacts and removal after refund.
4. Use default melee and supported melee attacks at 15, intermediate and 40-cell target distance.
   Compare source reach +4 and proximity damage; repeat spell/bow/wand negative controls.
5. With Overwhelm, vary living hostile count within 70 cells and outside it, up to the +100 multiplier cap.
   Exclude owned actors/daemons and repeat a no-extra-critical-damage target control.
6. Compare fixed hit damage on normal/unique targets before and within four seconds after a kill.
   The unique/recent factors are 1.2, 1.1 and 1.32; repeat accepted attack, spell, area and projectile contacts.
   Repeat old Impale release, ensuring old reflection is not scaled a second time.
7. Apply independently fixed Ignite and RF bases to unique/nonunique targets with and without recent kills.
   Observe burn damage and synchronous kill buffs, then refund Bane during an existing Ignite.
   Repeat qualified Deadeye distance Ignite and mark target context on the final combined head.
8. Equip the pinned Hiltless reflection modifier and land an actual attack with/without Bane.
   Compare reflected physical Life/ES damage and immunity; repeat a nonphysical-reflection negative control.
   Verify reflection cannot grant player leech, Rage, charges, on-hit ailments or a second kill credit.
9. Admit a real monster attack carrying MonsterAddedPhysicalDamageAndBleed, with block/evasion controls.
   Compare stationary/moving bleeding, retained full-Life leech unaffected behavior and resumed damage after leech.
   Compare immunity removal, armour hit mitigation versus independent bleed base, ES and lethal outcome.
10. Repeat cull boundary/overkill and leech taken/speed/stun controls already covered by automated contacts.
    Preserve boss stage protection, resource maxima/final-zero mirrors and source removal across all builds.
11. Enter loading during live Headsman windows, force failed transfer then retry successfully.
    Verify no loading-time loss, one matching-character ticket restore, pause/death/retirement and relog cleanup.
12. Preserve native logs and explicit runtime restoration/release before the parent decides PR readiness.
