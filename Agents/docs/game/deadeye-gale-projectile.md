# Deadeye Gale, Avidity and projectile execution candidate

Issue #392 remains owned by the coordinator.
This worker owns isolated engines, new adapters/tests and Core projectile continuation changes.
The coordinator owns common Gateway wiring, native verification, general documentation and PR publication.
No existing Gateway source/test file is changed by this candidate.
An adapter test is not evidence that an unapplied common hook already executes in production.

## Revisions and prerequisite ownership

The allocated branch `t3/ordinary-deadeye-gale-projectile` was clean at `864da152` and is preserved.
The new branch is `candidate/ordinary-deadeye-gale-projectile-392-20261031a`.
Its exact base is `f15a3abf5ebf841159d264fd43bfb259fa256feb`, containing upstream `5fc27fa2`.
No unrelated active feature branch or checkout was reset, edited, pushed or adopted.

- `37aa00194b472377cd27d98626bf56f601b3f527`: only `ActionSpeed.cs` and `EffectClock.cs`.
  Their Git blobs equal #339's `13aeea192edc10bc49cc88f76f77f01624a2f3a5` byte for byte.
  The parent explicitly approved this early prerequisite; Slayer cherry-picks it instead of making another clock.
- `ef8ab11975af64f5a11e5e8d24b60069f5222bfd`: Core terrain chance, target split and barrage metadata.
- `24052c98bc97a45af5306dd6c07f0d576bb578e3`: six new feature engine/adapter files.
- `865ba96304f35ff56300ffee977b9ce5e6a0a948`: explicit ally world scope and recipient presentation generations.

The final test/document descendant also invalidates the Gale publication revision on recipient-rate changes.
This makes the ticker republish the changed native remaining duration even when the count is unchanged.
Neither general timing contract is modified for that fix.

The exact relevant diffs of #328 `4c90a1f9`, #333 `1aa9a177`, #339 `13aeea19` and #385 `ad1e7e32` were read.
#246 owns the existing action-speed work; this task introduces no competing timing or recipient clock engine.
The parent retains #328 -> #333 -> #339 ordering and verifies the exact combined sequence before publication.
The two common files need a prerequisite PR first or a declared stack, rather than duplicated sibling rewrites.
Existing Juggernaut #152, Inquisitor #363 and selection #151 are outside this candidate.

## Pinned evidence and deliberate policies

The source is client 3.29.3b Default, not Phrecia, Ruthless alternates or current PoE2 mechanics.
The previously audited Default graph SHA-256 is recorded in [deadeye.md](deadeye.md).
The extracted graph fixture SHA-256 is `CB0B73E546F2FA1D19097741C1B67AFACC0585BF7B48F37C900BD7E4DC4460F5`.
The fixture contains 797 normal groups and seven orbits; the public hideout catalog has 14 collision radii.
All supplied data uses the explicit coordinator whitelist; no accounts, keys, saves, databases or stamps were copied.

`BuffTemplates` row 44, `DeadeyeTailwind`, references buff 1094, value -8 and radius 60.
`BuffDefinitions` row 1094 is `tailwind`, category 1, granted action reduction and `has_tailwind`.
Row 1095 is `gathering_winds_stacking_buff`, name Gale Force, category 18, count display and limit 10.
It has UIStackMode 3, `__col7=1` and the `gale_force_stacks` granted flag.
The meaning of its hidden merge-mode field has not been recovered; the server does not invent that interpretation.
The current Default nodes supply 10% Tailwind effect per Gale and -4% final damage taken per Gale.
Old public 15%/3% values are not current pinned evidence.

The [3.13 primary patch notes][patch] establish skill-use Gale, Tailwind base 8 and terrain-chain chance 30.
Their historical coefficients and prerequisite layout are superseded by the pinned client audit.
The pinned [PoB parser][parser] supplies the Gale multiplier and skill-use condition.
Pinned [PoB calculation][perform] floors Tailwind's 8% after increased effect before action-speed composition.
PoB is calculation evidence, not an observation of the official server.

The [Mark_GGG primary explanation][block] says blocked hits remain hits for defender when-hit behavior.
Wind Ward therefore clears on accepted blocked and zero-damage hits as well as ordinary hits.
Evasion, rejected/stale source contacts and damage over time do not clear it.
The parent's correction explicitly supersedes the original task's mistaken block exclusion.

The following unrecovered details are explicit LocalExile policies, with executable controls:

- Gale instances have independent four-second lifetimes, capped at ten.
  A use at the cap replaces only the oldest instance with one fresh four-second instance.
  A use below the cap never refreshes the other stacks.
  This agrees with the established four-second reminder model; no official runtime timing is claimed.
- Gale is lost on source removal, death and retirement; a new login starts without stacks.
  Transfer snapshots freeze remaining effect time during loading and restore it once at the accepted destination.
  Failed loading resumes the original state; no archive or persistent character state stores Gale.
- Terrain chaining spends exactly one existing chain and is impossible without a remaining chain.
  It selects the nearest visible eligible enemy within chain range and remaining flight range.
  Same-distance ties use actor ID and generation; it never produces a free reflection/bounce.
  It retains flight distance, expiry, ledger, phase and normal pierce/fork/split/return ordering.
- Barrage uses the existing contact delay followed by a half-action sequential release span.
  Its hidden angular cone is five degrees with independent seeded directions, adjusted by the native spread stat.
  Far Shot removes only that classified barrage cone.
  These angular/animation choices are bounded server policies, not recovered official geometry.

## Node coverage and real execution

| Default node | Engine consumer | Common Gateway/native status |
|---|---|---|
| 24848 Gathering Winds | `DeadeyeGaleState`, native presentation, shared action contract | hooks proposed |
| 23169 Wind Ward | pre-loss factor and synchronous clear, independent clocks | hit/DoT hooks proposed |
| 44482 Avidity | `DeadeyeAvidityAccuracy`, actual standard charge state and supports | calculation hooks proposed |
| 61627 Ricochet | actual Core terrain-to-enemy trajectory and chain budget | release source hook proposed |
| 45313 Far Shot, no spread | classified Barrage and sequential Core emission | timing/capture hooks proposed |

The already connected Far Shot distance source remains separate and is preserved.
Existing small-node projectile damage, accuracy, attack speed and projectile speed consumers remain intact.
Endless Munitions quantity remains in the single existing count resolver.
Focal Point, mark cast speed, Occupying Force and both Mirage duration smalls belong to the marks/Mirage worker.
This document does not claim their implementation or whole-class acceptance from this candidate.

`DeadeyeGaleState.Observe` resolves current source membership and coefficients on the host's combat lock.
`AdmitUse` admits one actual owner use, never failed payment, proxy actors, triggered actions or channel repeats.
`IncomingHit` returns the pre-loss factor before clearing; the second accepted hit sees the cleared state.
`Advance` integrates each unchanged #339 EffectClock, including exact-deadline expiry despite floating residue.
Category 18 uses general/other effect-time modifiers but excludes category-only buff acceleration.
`Revision` covers grants, loss, source/effect changes, expiry, suspension and recipient-rate changes.

`DeadeyeGalePresentation.Build` samples current state at the send gate.
It sends Tailwind 1094 with signed -8 and one timed count-one buff 1095 per independent Gale instance.
Expiry/removal removes its own native instance; a changed recipient rate replaces the affected duration.
It never replays a captured grant after paid refund or retires a newer buff using an old instance number.
Natural duration expiry leaves the base Tailwind source active while the node remains allocated.

`DeadeyeAvidityAccuracy` computes flat plus increased inputs before multiplying its distinct MORE bucket.
It retains `Pools.AccuracyBase`, includes selected support inputs in the same buckets and floors only once.
Explicit final accuracy replacements, including zero, win afterward.
BigInteger intermediates avoid overflow and saturate the final integer rating.
It reads actual held standard Frenzy through `ChargeSession.Effective`, including ordinary minimum charges.
Affliction conversion suppresses the standard Frenzy count; maximums and display aliases are never the count input.
Offhand recomputation uses the unrounded base with the offhand local difference before the same final multiplier.
It must never multiply a previously floored `Pools.Accuracy` by Avidity a second time.

`DeadeyeRicochetProfile` freezes terrain and barrage inputs at release.
`TerrainChainChancePercent` drives an independent terrain RNG stream without consuming combat/reward/reaction draws.
A successful terrain continuation precedes terminal areas, return turnaround and end emissions.
Failed chance, missing target, exhausted chain, blocked visibility or prohibited chaining preserve terminal behavior.
Continuation children preserve the existing actor ledger and normal enemy-chain budget.

`projectiles_barrage` is the native classification stat, key 10702.
Ordinary `Barrage` has six base projectiles in current data, rather than an invented legacy four-arrow recipe.
The real SupportBarrage path grants four additional projectiles, -50% damage at level 1 and 200% mana multiplier.
Its native per-projectile attack-time coefficient is 10%.
The existing support damage/cost consumers remain authoritative and pay once per skill use.
The recipe applies after count resolution and preserves every projectile's sequential time and geometry.
Its separate emission groups allow sequential barrage projectiles to hit the same target through normal admission.
Non-primary competing count consumers fail explicitly instead of converting secondary clusters into a barrage.
Ordinary simultaneous three- and nine-projectile fans remain unchanged with Far Shot allocated.

## Allied Tailwind and identity

`DeadeyeGaleAura.Source` carries explicit owner key, team, position, state and required `World` lease object.
`ForRecipient` takes explicit recipient key/team/position/alive and a named `world` token.
An absent token or nonidentical world token rejects every source.
Eligibility is same team, active source and distance at most 60.
Tailwind does not stack across eligible sources; the strongest self contribution wins, with deterministic source ties.
Gale's self effect applies only to the explicit source owner, never to nearby Mirage actors.
The recipient's own Tailwind-effect stat applies through the unchanged generic ActionSpeed contract.
Already present `has_tailwind` plus the base reduction does not receive another -8.
Cooldown recovery is not a consumer.

`Program.DeadeyeTailwindSource` defaults its lease to the connection and its key to that connection's local player key.
That default is suitable for owned proxies in the same connection's actor view.
Several connections reuse player ID 0x1000 and can reuse the same epoch.
They must never be collected into one aura query without a real shared world/lease and uniquely rebased actor keys.
The optional explicit `world` and `owner` parameters support such a host-owned registry.
The engine does not infer a global identity from the native player ID.

`DeadeyeGaleAura.Presentation.Build` is recipient-local and must run at the send gate with a fresh eligibility query.
It replaces sources and removes lost grants using its own buff number.
A different lease or recipient generation forgets the old object instead of removing a new object's buff.
Native recipient stats and action timing must be rebuilt beside the buff when source/effect context changes.

## Unapplied common hook contract

`logs/deadeye-gale/common-hooks.patch` is an exact baseline-context proposal, not installed production wiring.
Its SHA-256 is `EC2E0DF77E5B73E2B969A27846958EC7B8028317228D6A856B8E63EB7DCBB7C2`.
`git apply --check` passes against this candidate's unchanged f15 common Gateway files.
It was generated only from copies under this worktree's ignored logs directory.
It covers source assembly, accuracy, incoming loss, death, transfer, ticker and release-profile hooks.
Parent integration must preserve Slayer's changed blocks instead of applying one side wholesale.
Admission and action timing below remain separate single-writer integration requirements.

1. Wrap the completed `CharacterStatSources` runtime layer with `WithDeadeyeGale` after `WithCharges`.
   Do not feed derived Tailwind/Gale stats back into equipment, saved sources or a subsequent raw build.
   Resolve actual Default node 24848 membership, not merely the presence of a raw scalar.
2. Call `DeadeyeGaleAdmitUse(conn,true)` once at successful owner skill admission.
   `TryBeginRangedAttack` does so immediately before its successful return after pacing/token reservation.
   A targeted default attack does so after the range recheck immediately before `ActionStarted`, not while approaching.
   The ordinary gem branch does so inside the successful `PayMana`/critical-roll commit.
   Handle early-return Warcry, Righteous Fire, instant location skill, accepted aura-toggle and mark branches too.
   Their successful payment/admission callback must grant once even when they bypass the ordinary gem branch.
   Failed mana/cooldown/reservation, rejected instances, movement, proxy shots and channel repeats grant nothing.
3. Multiply `PlayerDamageTakenFactor` by `DeadeyeGaleIncoming(conn,false)` before actual incoming mitigation.
   This includes damage over time and uses the pre-hit count without losing stacks.
   In `TryCommitMonsterAttackLocked`, clear through `DeadeyeGaleIncoming(conn,true)` after accepted mitigation.
   The call must precede notification awaits and happen even for block or zero damage, never after an evasion return.
   Mirror that accepted commit in `TryDevEnemyHit`; do not add another clear in awaited presentation callbacks.
   Use the same combat lock/source snapshot to avoid a refund/source race between factor capture and loss.
   Incoming Ignite has a separate `PlayerElementalAilments.Advance` consumer, outside PlayerDamageTakenFactor.
   Pass its Gale final multiplier into the unrounded per-second integral before the integer carry/floor.
   Multiplying its returned integer damage loses fractions and does not implement the same mitigation bucket.
   LocalExile samples current Gale at the existing resource writer's cadence for this continuous-damage path.
   This is not an exact expiry-boundary integral or a claim of native sub-tick parity.
4. Start `RunDeadeyeGale` beside `RunCharges` after `DeadeyeGaleReceive` on accepted entry.
   Use `DeadeyeGaleCarry` before `CarryRage` retires the old connection.
   If suspending before control acceptance, call `ResumeFailedTransfer` for every failure branch.
   Clear on authoritative death and connection retirement; relog never restores persistent Gale state.
   Buff/stats output samples the current generation at the send gate, including removal while output is held.
5. Apply Avidity from the unrounded basis in `BuildPoolsFor` after ordinary pool computation.
   Replace `AccuracyRating.Selected` in supported accuracy with `DeadeyeAvidityAccuracy.Selected`.
   Replace offhand recomputation with `Calculate` using its local delta and selected support inputs.
   Preserve explicit main/offhand final replacements and the existing unique/close target MORE controls.
   Ordinary bow/wand capture consumes the completed pool accuracy; supported capture consumes SupportedAccuracy.
   Both still use the original evasion/always-hit admission and one accuracy value per frozen release.
6. Apply terrain input to both ordinary and gem release profiles after modifier resolution.
   Preserve it on child recipes and after release specialization; do not re-read it at contact.
   Releasing without the source after refund has zero chance while the old frozen profile retains its source.
7. Classify Barrage by native `projectiles_barrage` or ordinary supported `Barrage`, never fan count.
   Resolve all counts and prohibitions before `DeadeyeRicochetProfile.Barrage` at final release specialization.
   Reserve total `AttackSeconds(ordinary, classifiedProfile, get)` once, including native timing projection.
   The release helper expects ordinary time and internally computes the supported total for its interval.
   If capture receives a total duration, divide by the coefficient ratio first as shown in the proposal patch.
   Validate recipe legality before payment; unsupported non-primary/lobbed conversions cannot fail only at release.
8. On f15 the existing `PlayerActionFactor` already applies ailment timing to ordinary/gem attack and cast waits.
   Reuse ActionSpeed.From for the independent raw action factor and multiply with ailments exactly once.
   Movement uses `MovementFactor(Pools.MovementVelocityPct)` and existing ailments once.
   Native actor/skill virtual action speed needs the combined factor; weapon/cast BASE milliseconds stay unscaled.
   Remove f15's additive chill-to-raw-action-reduction projection before projecting the multiplicative result.
   Do not also install #339's weapon/timing scalar at a second call site and divide again through PlayerActionFactor.
   The exact #339 composition must reconcile this changed main seam under the parent's shared timing ownership.
9. Mirage uses its own recipient position, actor key and stats through `ForRecipient` with the same actual lease.
   Strip owner-self action speed from captured base timing before applying proxy Tailwind once.
   Its native Add/Remove output uses the recipient presentation generation and never transfers owner Gale effect.
10. Preserve #328 owned-actor exclusions, #333 Blind inputs and #385 support resolution at every common seam.
    Do not attach player-use Gale admission or ordinary owner-hit context to Mirage/projectile proxy actors.

## Offline evidence and native matrix

Final source build: zero warnings/errors with portable .NET 10.0.402.
Core 423 and Data 34 pass without skips; twelve new Core cases use actual trajectory/contact outcomes.
Default parallel Gateway 3645 passes, including the final recipient-rate publication fix and regression assertion.
The final source run is recorded in `logs/deadeye-gale/gateway-final.log`.
The new Gateway cases exercise real supports/charge state, packet construction and owned adapter lifecycle.
They explicitly distinguish pending common hooks from manual adapter invocation.
A final test-only refinement additionally exercises the existing production attack/cast timing calculations.
Its focused timing-contract rerun is recorded beside the full-suite logs.

Logs are under this worktree's ignored `logs/deadeye-gale/` directory.
Full build outputs remain under this worktree; no external artifacts folder or test cwd workaround is used.
Tests use the existing shared TestEnvironment and isolated TestSaves.
An early build-copy failure was caused by this worker's own running test host and is retained separately.
The earlier test binary's passing run is not final source evidence.
No runtime process was killed to release the file; the owned test run completed normally before rebuilding.
Initial focused failures corrected an expected pinned support cost and exact-deadline floating residue.
Those unsuccessful attempts are retained separately rather than fabricated as passing observations.

The parent alone must reserve and run this native matrix through `scripts/dev/live.ps1`:

- Pin combined code/test head, Default graph, DLL hashes, actual launch command and synthetic database.
  Observe all 19 Default nodes across the agreed legal eight-point builds, including paid refund/relog/restart.
- Compare idle Tailwind 8% with Gale counts 1, 5 and 10: expected floored action bonuses are 8%, 12% and 16%.
  Check actual attack, spell and movement cadence plus native stat/skill agreement; cooldown recovery is unchanged.
- Repeated admitted owner uses reach ten; blocked/cooldown/mana-rejected/proxy uses do not grant stacks.
  Observe independent four-second expiry and cap renewal of the oldest instance without refreshing the others.
- With ten stacks and deterministic incoming damage, the first accepted hit uses factor 0.6 then loses all stacks.
  A second hit uses 1; blocked and zero-damage hits clear; evasion and damage over time do not clear.
  Repeat refund/removal during held output, death, failed transfer, loading, relog and connection retirement.
- Verify recipient-rate changes republish actual remaining buff durations without changing cooldown/resource clocks.
  Verify pause consumes no game time and source removal cannot resurrect a queued buff.
- Check nearby allied/Mirage recipients at distances 60 and greater than 60 with correct world and actor keys.
  They receive base 8% Tailwind plus their own effects, never the owner's Gale-scaled 16%.
- Compare Avidity at actual Frenzy 0/1/4, not maximum charges, with accuracy supports and known enemy evasion.
  Test Affliction conversion, final zero override, always-hit and refund/death/expiry before the next capture.
- With deterministic terrain seeds compare chance 0/30/100, exhausted chain, cannot-chain and visible enemy choice.
  Confirm cumulative range, no repeated actor hit, split/pierce/fork precedence and return/terminal negative controls.
- Observe ordinary six-projectile Barrage and a valid bow skill with actual Barrage Support.
  Check sequential count/timing and one cost/cooldown/use; Far Shot removes only the barrage cone.
  Keep native ordinary three- and nine-arrow fans as negative controls with their quantity/spread preserved.
- Preserve logs, restore the agreed initial runtime and explicitly release the one-client window.

No client/Gateway runtime was started, stopped, restarted, attached or commanded by this worker.
No PR was created, pushed, retargeted, merged or marked ready; the parent owns publication.
No shutdown/reboot was invoked or scheduled by this worker.

[patch]: https://www.pathofexile.com/forum/view-thread/3009157
[block]: https://www.reddit.com/r/pathofexile/comments/h0z19u/comment/funpjve/
[parser]: https://github.com/PathOfBuildingCommunity/PathOfBuilding/blob/16de4b8/src/Modules/ModParser.lua
[perform]: https://github.com/PathOfBuildingCommunity/PathOfBuilding/blob/16de4b8/src/Modules/CalcPerform.lua
