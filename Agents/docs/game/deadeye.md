# Ordinary Deadeye: connected model, node audit and acceptance boundaries

Issue: [#392](https://github.com/LocalExile/LocalExile/issues/392).
Ordinary feature source: `762fbe43182d4cf331fd40e522a78aec41e05e06`, including ordinary Slayer #391.
The local publication candidate reconciles that feature delta with the explicit prerequisite described below.
All 19 pinned Default nodes have connected consumers in the supported execution paths described below.
This is an implemented LocalExile model; exact official-server formulas, AI and native presentation remain qualified.
Full native combat and exact related-head integration acceptance are pending with the parent.
The class remains **Partly working** until that acceptance is recorded.

[Marks and Mirage engines](deadeye-marks-mirage.md) and
[Gale, Avidity and projectile engines](deadeye-gale-projectile.md) retain their detailed evidence and policies.
Their candidate-only statements about proposed/unapplied common hooks describe the earlier isolated handoffs.
The current combined source installs those hooks; this document records the resulting shared contracts.
[Slayer](slayer.md) records the cooperating owner-hit, kill, leech and incoming-bleed model.

## Pinned identity and sources

The client is 3.29.3b, using the ordinary `Default` graph, not Phrecia or Ruthless alternatives.
Graph source: `metadata/passiveskillgraph.psg`.
Source SHA-256: `82D82CD3CD2DAD81606AF0DA9D18FCD53B2E92CF006550947D65A4F7E102DC32`.
Public positioned graph JSON SHA-256:
`CB0B73E546F2FA1D19097741C1B67AFACC0585BF7B48F37C900BD7E4DC4460F5`.
Join `PassiveSkills.PassiveSkillGraphId` to this graph and its `AscendancyKey` to `Ascendancy.json`.
Deadeye is ascendancy row 4, Ranger character row 3, base class 2, ClassNo 2, packed byte `0x82`.
Start `56856` is implicit and free; 18 paid nodes comprise eight notables and ten small nodes.
The same ascendancy row's Phrecia name is Whisperer; its graph is not ordinary Deadeye evidence.
`OrdinaryDeadeyeNodeAuditTests` pins all 19 records and every source key/value.
Client stat keys equal Stats row plus one; the values below are ordinary Standard values.

Distance interpolation follows pinned PoB `16de4b82d57f1c0de6eb40f37143c32d4da36a02`.
See [ModParser][pob-parser] and [CalcOffence][pob-offence].
The shipped graph fixes node stats; PoB supplies the secondary calculation model, not an official-server trace.
The engine documents also pin support/mark tables and primary historical references.
Historical coefficients do not override the current client audit.
Distances use existing combat cells (0.1 metre), measured centre to centre unless flight path length is specified.
No graph installation, archive write, extraction format or production `data/` path change is needed.

## Every node and current consumer

All ten small nodes also grant key 501, `projectile_damage_+% = 10`.
`DamageStats`, `PlayerHitDamage` and projectile ailment calculations consume that shared increase.
Each entry below lists the remaining source and its connected consumer.

- `56856`, Deadeye start: no stats; ordinary selection supplies the implicit root and validates membership.
- `53086`, Projectile Damage, Mark Cast Speed: key 13258, `mark_skill_cast_speed_+% = 25`.
  `TryDeadeyeMarkCast` uses this in the actual paid mark cast wait.
- `5443`, Focal Point: key 13529, `mark_skills_curse_effect_+% = 75`;
  key 13530, `damage_taken_+%_final_from_enemies_near_marked_enemy = -25`;
  key 12622, `your_marks_transfer_to_nearby_enemies_on_death_%_chance = 100`.
  Live mark effect, target modifiers, attacker-specific incoming hit/DoT defence and lethal transfer execute.
- `59837`, Projectile Damage, Attack Speed: key 71, `attack_speed_+% = 5`.
  Existing passive pools and ordinary/supported attack timing consume it.
- `61627`, Ricochet: key 175, `number_of_chains = 1`;
  key 13457, `projectile_chain_from_terrain_chance_% = 30`.
  The single count resolver supplies enemy chains; `DeadeyeRicochetProfile` freezes terrain chance for Core execution.
- `5082`, Projectile Damage, Accuracy: key 85, `accuracy_rating_+% = 10`.
  Existing accuracy and completed Avidity calculations consume it.
- `26067`, Endless Munitions: key 176, `number_of_additional_projectiles = 2`.
  Profile resolution, actual emission and panel projection consume the count once, respecting recipe prohibitions.
- `28995`, Projectile Damage, Projectile Speed: key 178, `base_projectile_speed_+% = 10`.
  Panel projection and release-time `ProjectileSpeed` consume it once.
- `45313`, Far Shot: key 17928,
  `deadeye_projectile_damage_+%_final_max_as_distance_travelled_increases = 30`;
  key 13972, `no_barrage_projectile_spread = 1`.
  Explicitly qualified projectile hits and Ignite use the distance snapshot, including projectile spells.
  Classified Barrage suppresses its own cone; simultaneous projectile fans retain their spread.
- `44482`, Avidity: key 183, `max_frenzy_charges = 1`;
  key 20752, `gain_a_frenzy_charge_every_X_seconds_while_moving = 1`;
  key 20754, `deadeye_accuracy_rating_+%_final_per_frenzy_charge = 5`.
  Existing limits/moving-only gain and `DeadeyeAvidityAccuracy` consume actual held standard Frenzy charges.
- `55985`, Projectile Damage, Accuracy: key 85, `accuracy_rating_+% = 10`.
  Existing accuracy and completed Avidity calculations consume it.
- `62136`, Projectile Damage, Mirage Archer Duration: key 13480, `mirage_archer_duration_+% = 15`.
  The linked-support actor's category-18 lifetime consumes it.
- `24848`, Gathering Winds: key 7841, `tailwind_effect_on_self_+%_per_gale_force = 10`.
  `DeadeyeGaleState`, admitted-use hooks, native buffs and shared action timing consume the source.
  BuffTemplates row 44, `DeadeyeTailwind`, supplies buff 1094, `action_speed_-% = -8` and radius 60.
- `1729`, Projectile Damage, Mirage Archer Duration: key 13480, `mirage_archer_duration_+% = 15`.
  It adds to the same linked-support lifetime as `62136`.
- `2872`, Occupying Force: key 13479, `mirage_archers_do_not_attach = 1`;
  key 13481, `additional_max_mirage_archers = 2`;
  key 13482, `cannot_summon_mirage_archer_if_near_mirage_archer_radius = 35`.
  Owned stationary actors, cap three and the inclusive radius-35 summon gate execute through the real roster.
- `56134`, Projectile Damage, Accuracy: key 85, `accuracy_rating_+% = 10`.
  Existing accuracy and completed Avidity calculations consume it.
- `64028`, Projectile Damage, Projectile Speed: key 178, `base_projectile_speed_+% = 10`.
  Existing projectile damage and release-speed consumers use it.
- `22852`, Projectile Damage, Attack Speed: key 71, `attack_speed_+% = 5`.
  Existing projectile damage and attack timing consume it.
- `23169`, Wind Ward: key 13557, `damage_taken_+%_final_per_gale_force = -4`;
  key 13558, `lose_all_gale_force_when_hit = 1`.
  Incoming hits use the pre-loss factor and clear synchronously; incoming DoT uses mitigation without consumption.

## Projectile distance, provenance and contact order

`DeadeyeProjectileDamage.Capture` freezes the distinct maximum and Point Blank flag at release.
The distinct multiplier is 1 through 35 cells, linear to 1.3 at 70, then capped at 1.3.
It has no near-distance penalty and no attack-only restriction.
Legacy `player_far_shot` remains its independent attack-only 0.8-to-1.6 ramp.
When both grants exist, their hit factors multiply independently.
Point Blank is a separate attack-hit factor: 1.3 through 10, 1 at 35, and 0.7 at 120 and farther.
Neither Point Blank nor legacy Far Shot enters the independently calculated Ignite basis.
Negative/nonfinite distances are rejected.

`ProjectileOffense.DistanceDamage` defaults neutral.
`ScheduleOrdinaryRanged` and `CaptureGemProjectile` capture it from existing release stats.
Ordinary bow/wand and supported direct profiles explicitly grant `CountsAsProjectileDamage`.
Raw Core, hostile and secondary recipes default false; a carrier, stage string or `IsArea` is not provenance.
Core forwards the dispatching recipe's grant into each `ProjectileDamage`.
Terminal substitution uses the terminal recipe's grant even if its direct profile disagrees.
Targeted hits, pulses and emission-child profiles require their own explicit grants.
Audited Fireball and Kinetic Blast areas, including Clustering's delayed sequence, grant projectile provenance.
`SupportedProjectileHit(area: true)` retains projectile classification while adding the area damage bucket.
Projectile/area increases share one additive bucket; recipe and distance multipliers each apply once.
This grants no blanket projectile classification to future secondary areas.

Pierce retains distance without reusing a scaled hit as the next raw basis.
Bent chains accumulate actual path length, not displacement or current owner position.
Fork/split inherit their profile and distance; return retains outbound distance with a separate phase ledger.
Terminal areas use triggering flight distance; delayed areas freeze distance in `AreaSnapshot` at scheduling.
New end/travel emission flights start at zero under `EmitChildren`, inheriting raw offensive inputs.
Changing emission semantics requires a separately audited per-skill recipe.

Accuracy and block admission precede damage effects.
The selected raw direct/area mark source is resolved before conversion/increases and outgoing hit suppression.
Qualified distance, continuation/return factors, legacy Far Shot and Slayer target MORE apply once before mitigation.
Target mark modifiers join common `MonsterDamage`/`MonsterTakenComponents`; there is no parallel damage engine.
`OwnerSkillHit` records mitigated direct damage before old reflected Impale is added to total damage.
The shared mutation settles phase interception, single leech/overkill, eligible cull and one lethal commit.
Cull contributes no damage dealt, Ignite basis or leech; reflected Impale adds no direct overkill.
Allowed reactions and reward/quest notifications retain the explicit attribution contract.
The marked Ignite basis is captured before a lethal hit can transfer the mark.
Surviving qualified contacts then scale all four frozen Ignite ranges once by distance.
Chance, duration, critical selection, target avoidance and fire eligibility retain their independent controls.
Paid Far Shot refund leaves in-flight inputs frozen and removes the source from the next release.

## Marks and Focal Point

`DeadeyeMarkCatalog` uses real GrantedEffects and BuffDefinitions for these logical stat consumers:
SnipersMark 1214/buff 78, AssassinsMark 1205/buff 77, PoachersMark 4044/buff 80,
and WarlordsMark 1176/buff 74.
`TryDeadeyeMarkCast` is installed before generic action/payment handling.
It validates selected source, hostile living target, range, town/pause/death/recovery and current cast ownership.
Native base cast time is 500 ms, modified by caster/support/mark cast speed and the common action factor.
Mana is paid once after admission; successful owner use grants Gale once.
Invalid ground/no-target mark input is rejected before payment.
Cancellation/source invalidation prevents late application without refunding an already accepted payment.

Each connection has one active mark, retaining caster, target reference, skill, level, quality and source gem identity.
Another successful application replaces it; ordinary rows have no duration.
Any positive sourced duration uses the category-14 recipient clock.
Caster death, transfer, retirement or source removal clears ephemeral marks; relog does not restore them.
Current combat worlds are connection-local, not a new shared cross-connection monster registry.
Focal effect/quality are live at the consumer; paid refund also refreshes the native mark instance at the send gate.
All four mark rows have `IsRecovery=false`; native AddBuff packets carry no stat-value vector.
The actual hitter's damage inputs remain release-frozen and separate from the markcaster's skill inputs.

Sniper's increased projectile HIT damage taken joins the common increased/reduced taken bucket after mitigation.
It does not multiply that bucket separately and never scales independent Ignite DPS.
Its target split query max-merges with existing split, respects CannotSplit and needs eligible neighbours.
Miss/block discards provisional split; SplitWithoutTargets is not globally enabled.
Native ordinary rows have zero legacy flask/knockback slots, so those historical effects are not claimed.
Assassin supplies target base critical chance/multiplier and capped Life/Mana recovery plus Power on death.
Poacher supplies flat physical damage before effectiveness/conversion and joins raw physical reduction before clamp.
Its admitted attack-hit Life/Mana gains and Frenzy death grant execute.
Warlord adds per-type attack Life/Mana leech, extends accepted flinch and grants Endurance on death.
Immediate Rage on an accepted Warlord stun is LocalExile policy, not recovered official delivery timing.
Native quality hit grants enqueue actual Power/Frenzy/Endurance charges for Assassin/Poacher/Warlord.
Native quality-20 5% becomes 8% through Focal's integer effect scaling.
Block grants no attacker mark on-hit effects under the documented LocalExile admission policy.

`DeadeyeMarkHitSource.ResolveIgnite` rebuilds all four Ignite ranges from the actual frozen hitter inputs.
Poacher's target flat source enters before conversion and damage-effectiveness/increased/more calculations.
Ordinary, supported direct/area and Mirage sources retain their independent noncritical/critical ranges.
Mirage applies its support damage penalty to the resolved ranges; projectile distance applies once afterward.
Chance/duration stay frozen; later hitter source changes cannot rewrite the released basis.
Removing the mark falls back to the original release settings.
A missing optional `ResolveIgnite` preserves existing callers rather than reconstructing Ignite from hit damage.

Focal transfer/nearby defence uses an explicit inclusive 60-cell centre-to-centre LocalExile radius.
The marked enemy is excluded from its own nearby-other-enemy reduction.
Death transfer chooses the nearest living hostile, then object ID, retaining original caster/source without payment.
It runs synchronously on real lethal commits, including cull and owner DoT, before dead-mark refresh.
Boss phase interception is not death; reference/generation checks reject stale targets and reused IDs.
Incoming hits retain the actual attacker's identity for nearby defence.
Incoming Ignite and Bleed also retain source ID/reference in a live source callback.
Each DoT advance checks that identity and current mark/nearby state before strongest-instance selection.
Refund, movement or mark loss therefore changes an existing ailment's future incoming factor without reapplication.
The source factor enters the unrounded integral before carry/floor, not the already rounded damage result.
The outer combat lock precedes the effect-list lock for advancement and callbacks, avoiding reversed lock order.
Unknown/replaced sources return neutral; DoT timing follows the existing resource-writer cadence.
This does not claim exact sub-tick position or mark-transition integration.

## Mirage Archer and shared attribution

Real compatible linked SupportMirageArcher 9076 and an admitted owner bow hit are required.
The support resolver checks MirageArcherCanUse and excludes Vaal, totems, traps, mines and minions.
Source skill, support link, bow, requirements and active weapon set are revalidated during actor work.
The category-18 base lifetime is 4 seconds, 4.6 with one duration small and 5.2 with both.
The native -60% final attack-speed and level-specific less-damage penalties apply.

The native RangerMirage variety has its own serializable object, Actor skills, friendly team and generation.
It autonomously selects living hostile targets and releases the supported recipe into the sole Core runtime.
Attached actors follow the owner; a repeated hit with the same valid skill/source refreshes that same actor's lifetime.
Refresh preserves attack generation, next scheduled attack, pending releases and valid in-flight contacts.
A different source skill replaces the attached actor; continuous owner attacks cannot starve its slower attack.
Occupying actors stay at their summon position.
Its inclusive radius-35 gate rejects a nearby summon without refreshing; a fourth distant summon retires the oldest.
Nearest-target selection and oldest replacement are explicit policies, not recovered official AI parity.
Exact native overhead attachment, inherited equipment appearance and support height-offset rendering remain unproven.
Real actor packets and model selection do not establish visual acceptance.

Every attack freezes real owner skill/weapon/support inputs at release.
Emitter is the archer; Owner and ReturnAnchor remain the player.
Proxy timing removes the owner's received action factor before applying the proxy's own allied Tailwind once.
It receives base 8% inside radius 60, without the owner's Gale self effect.
`SkillDamageAttribution.Mirage` sets CountsAsYourSkill, KillCreditToOwner and AllowCull true.
GrantOwnerLeech, GrantOwnerRage, GrantOwnerOnHit, GrantOwnerChargeOnHit and RecursiveMirage are false.
The shared `HitOwner`/`OwnerSkillHit` route consumes those flags, preserving Slayer kill/cull conditions.
Actor ID is not an attribution heuristic, and the proxy is not a minion.
Owner reflection is suppressed under the current proxy policy.
Expiry, source/passive/link/weapon removal, death, transfer and retirement invalidate queued work and contacts.
Send-gate generation/lease checks prevent ghost publication; target-reference checks protect reused object IDs.
Pause freezes the normal runtime; relog creates no restored ephemeral actor.

## Gale, Tailwind, Avidity and Barrage

`DeadeyeGaleState` observes actual Default Gathering Winds membership under the combat lock.
Successful owner uses grant once, including early-return marks, aura, instant, RF and warcry paths.
Failed payment/cooldown/recipe admission, movement, channel repeats and proxy/triggered uses grant nothing.
Each Gale has an independent four-second category-18 lifetime, cap ten.
At cap a use replaces only the oldest; it never refreshes all other stacks.
These lifetime/merge rules are explicit LocalExile policy; the hidden native merge field remains unrecovered.
Shared #339 `EffectClock` and `ActionSpeed` contracts are reused without a second general timing engine.
Recipient-rate changes republish native durations even if stack count is unchanged.
Source removal/death/retirement clears Gale; relog starts empty.
Transfer freezes remaining effect time for one destination ticket.
Failed loading resumes once without spending loading time.

Base Tailwind is 8%, radius 60, same team and identical explicit world/lease token.
It affects self and eligible allies; Gale's +10% effect per stack is self-only.
Tailwind sources do not stack; existing Tailwind is not granted another base -8.
A connection-local PlayerId is not global identity.
Cross-connection queries require a real shared world and rebased keys.
Native source/recipient buff output rechecks eligibility and generation at the send gate.
Native buffs are Tailwind 1094 and Gale Force 1095, with a separate timed instance per Gale.
Tailwind floors its base 8% after increased effect before shared action-speed composition.
Natural Gale expiry leaves base Tailwind active while Gathering Winds remains allocated.
Tailwind action timing composes multiplicatively with ailments once; attack/cast base milliseconds remain unscaled.
Movement uses the same final action factor beside ordinary movement speed; cooldown recovery is separate.
An explicit final `virtual_action_speed_+%` replacement, including zero, wins for native projection and execution.
Removing that replacement restores derived Tailwind/ailment composition.

Wind Ward applies -4% final damage taken per current Gale before accepted-hit loss.
Accepted blocked and zero-damage hits clear all stacks synchronously; evasion and stale/rejected contacts do not.
Incoming DoT, including Ignite's separate integral and Bleed's taken factor, gets mitigation without stack consumption.
Current Gale is sampled at the existing resource-writer cadence, not an exact continuous expiry-boundary integral.

`DeadeyeAvidityAccuracy` uses unrounded flat/increased inputs, including selected supports and offhand local deltas.
Actual held standard Frenzy from `ChargeSession.Effective`, including minima, supplies the distinct MORE count.
Affliction conversion suppresses standard Frenzy; maximums/display aliases are not held counts.
Wide intermediates saturate safely and floor once; explicit final main/offhand replacements, including zero, win.
Ordinary and supported release paths freeze that completed accuracy.
Target unique/close and evasion rules remain intact.

Core terrain chaining uses the frozen 30% source and an independent RNG stream.
It consumes exactly one existing remaining chain, choosing the nearest visible eligible enemy within both ranges.
Ties use actor ID/generation; no remaining chain means no terrain chain and no free reflection.
It preserves cumulative distance, expiry, hit ledger and split/pierce/fork/chain/return ordering.
The terrain decision precedes terminal areas, return turnaround and end emissions.
Failed chance/visibility/target/prohibition retains terminal behavior.
This budget/selection contract is explicit policy, not proof of official trajectory parity.
No pinned node grants old damage-per-remaining-chain mechanics.

Barrage classification uses native `projectiles_barrage` key 10702 or ordinary supported `Barrage`, never fan count.
Current Barrage has six base projectiles.
SupportBarrage grants four additional projectiles, level-1 -50% damage, 200% mana and 10% attack time per projectile.
Counts/support damage/cost resolve once; sequential emission groups allow normal repeat-target admission.
The contact delay plus half-action sequential span and seeded five-degree cone are bounded LocalExile policies.
Far Shot suppresses only this classified cone; ordinary simultaneous fans are unchanged.
Non-primary competing count recipes and lobbed conversions are rejected before payment or Gale admission.
The admission guard prevents a paid use from failing only later at release.

## Automated evidence and historical native qualification

The framing publication checkpoint is `6598d3b15b428214b0501246a6c078e1b495c675`.
It includes `0b997b37`'s non-recovery Headsman/Tailwind/Mark framing fix and independent row-flag/sentinel tests.
The preceding `f58a5b9a` built without warnings and passed the complete serial Gateway suite: 4026, zero skips.
For `f58a5b9a`, Core 434 and Data 34 passed on production-equivalent `22223d9d`.
The changes between those two revisions were Gateway-test-only; `6598d3b1` then changes four presenters.
Those passes precede the framing correction and cannot certify its native behavior or final publication readiness.
Default-parallel attempts retain cadence, overleech and recent-hit timing failures under host contention.
The final fixture corrections preserve captured projectile muzzle geometry and the actual ingress timestamp contract.
No production timing, damage bounds, test selections, skips or timeout extensions were used to conceal failures.
Post-framing docs descendant `bf45ce2f` has identical `src/` and `tests/` to `6598d3b1`.
Its local solution build has zero warnings/errors; 45 focused framing and shared-seam cases pass without skips.
Logs are `logs/publication-post-framing-build.log` and `logs/publication-post-framing-focus.log`.
The subsequent ground-admission input is `b3be9ba99b8b811e8b89e7b5d6f066ed6a60dd1e`.
Its #339 timing reconciliation is `cdcd472ff8656ee3b6985b612285f3191096b3d7`.
The shared weapon calculator already applies raw action speed, so ground dispatch divides by ailments only.
The solution builds without warnings; 16 focused ground/Harbinger cases pass, including two new real-dispatch
raw-action-plus-Chill controls and all seven coordinator ground admission cases.
Logs are `logs/publication-ground-build.log` and `logs/publication-ground-focus.log`.
Final local validation pins publication head `94d9d5bf82873df5901f78b5ebaf78b267d3c041` against snapshot `7476a054`.
The solution builds with zero warnings/errors; Core 434, Data 34 and complete Gateway 4037 pass with zero skips.
Gateway uses `logs/publication-final-authorized-serial.runsettings`: collections off and one thread.
The run has no filter, test selection, missing-data bypass or timeout extension.
Final logs are `logs/publication-final-authorized-build.log`, `-core.log`, `-data.log` and `-gateway-serial.log`.
TRX files retain every result; public data and compiled artifact hashes are in `logs/publication-*-sha256.txt`.
Any following documentation-only descendant must keep identical `src/` and `tests/` trees to that tested head.
This worker accessed no runtime and created no push, PR or comment.

The parent's recorded full-source checkpoint `a2c054c39438813741e235af89f4b11b6539be4e` has a solution build
with zero warnings/errors and Core 423, Data 34 and default-parallel Gateway 3757 passing.
The parent reports source `762fbe43` passing a warning-free solution build and Core 434, Data 34 and Gateway 3799.
That source retains the subsequent Mirage/Poacher, incoming Focal, action override and Barrage admission fixes.
Those checks certify the source revision, not the reconciled Harbinger publication candidate.
The parent's bounded native cull/refund/relog checks on `762fbe43` found missing Headsman buff presentation.
The publication candidate includes fix `7ac25737` and its tick hook; final native revalidation remains required.
No whole-class native acceptance follows from the partial run.
Earlier full-suite totals certify their own revisions, not changed publication code.

Current coverage includes pinned nodes, legal allocation/refund/reload, production release/contact provenance,
real Core continuations, marks/native buff construction, autonomous actors, Gale/accuracy and incoming hit/DoT paths.
The review fixtures exercise same-actor continuous-hit refresh, actual converted Poacher Ignite bases,
direct/area/Mirage ranges, live incoming mark factors and pre-payment Barrage rejection.
These automated checks do not establish client combat or visual parity.
Tests use shared `TestEnvironment`, isolated `TestSaves` and whitelisted public extraction files.
No per-test working-directory change, live database/key copy or client-archive write is part of this work.

Historical stages were independent `77517d98`, connected `fa9e336c` over `ba2a8509`, then combined `895d42dd`.
They progressed from distance helper to real release/contact wiring; their candidate build logs are history.
The isolated default Gateway AI timing failure and its successful serial rerun remain earlier evidence,
not a final acceptance result or a changed timing assertion.
Marks/Mirage and Gale handoff logs/policies remain in their engine documents.

Initial native revision `e09abff5040358011c1c6be19410ca3dba6f6905` added the startup argument-array fix.
Its `src/` and `tests/` matched the then-tested combined source; the native build embedded `e09abff5`.
Gateway SHA-256: `24A149FEF9652BCF15EC0E1CD21794553ABFAC0FDB86174000BAB99F31881AE8`.
The shared sixteen-check lifecycle smoke passed nine Slayer and seven Deadeye checks.
On a synthetic Ranger it observed class byte 130, Default graph without stat 21146, idempotent separate awards,
connected `28995 -> 45313`, five-point refund, reallocation and relog with retained budgets.
The client received distinct distance source 30, lost it on refund and regained it on relog.
This proves that revision's bounded identity/source lifecycle, not the new engines or actual combat formulas.
No final-source combat, death, restart, visual or whole-class acceptance follows from it.

Evidence: coordinator `logs/ordinary-ascendancies/native/smoke-20261031-011510-33680/`.
`result.json`, `native-events.jsonl` and adjacent provenance records pin assertions, source, DLLs and launch CWD.
Graph comparison used native resource `PassiveSkillTrees.Default.PassiveSkillGraph`, without `.psg` suffix.
Unsuccessful startup/process-inventory and incorrect-resource-stem attempts were retained as failures.
The parent used one client through `scripts/dev/live.ps1`, hot reload disabled and a synthetic database/account.
The reviewed #388 login JS was external test tooling, not copied production code.
The agreed runtime was restored and released after that initial smoke.
New native combat evidence belongs to the parent after final-source acceptance.

## Coordinator native subsets and their boundary

These are parent-reported actual-client observations on coordinator `0b997b37` and `b3be9ba9`.
They do not certify this reconciled main/#339 publication head; the parent owns a separate actual-tip native window.
The detailed Slayer subset is recorded in [Slayer's evidence](slayer.md#coordinator-native-subset).

Deadeye D1 run `031232` on `b3be9ba9` observed Far Shot damage 100/130 and paid-refund negative 100,
three emitted projectiles, and actual Focal Point contact damage 117/110/117 through refund/reallocation.
The Focal oracle measures real damage rather than an invalid native buff value-vector assumption.
Non-recovery buff packets contain no extra stat words and passed the actual client after the framing correction.

The D2/D3 run `032028` passed its complete automated subset with an intentional exit code 2.
Its helper explicitly leaves `WholeClassAccepted=false`.
It observed ten Gale stacks and native Gale buffs with one Tailwind buff, then incoming Chaos 100 -> 60
and stack removal after the accepted hit.
Burning Arrow dealt 280 owner damage: the raw DamageEffectiveness value 18000 is an offset, yielding 280%.
Mirage then dealt autonomous 168 damage without another owner attack, with native Life updates.
Moving Frenzy/accuracy, legal eight-point builds, paid refunds and relog controls also passed.

These results do not establish every manual matrix row, whole-class or official visual parity.
Mirage appearance, overhead/equipment/height presentation and untested interaction boundaries remain explicit.
Failed fixtures, the malformed non-recovery packets and all earlier unsuccessful attempts remain retained evidence.
The native windows were closed and the agreed runtime restored by the parent, never by this publication worker.

## Related integration and final acceptance work

The immutable current local prerequisite is `7476a054d0a026668f2ab1b6e99a6639a81db32c` on
`candidate/ordinary-prerequisite-main89b7-harbinger13aeea-3dbe8e19`.
Its parents are main `89b7aa908213ac8eb9b5ac5aa7ae54c14189c3e9` and
exact #339 `13aeea192edc10bc49cc88f76f77f01624a2f3a5`.
Its shared stat-source conflict retains stolen modifiers before companion and Temporal Chains layers.
The ordinary branch is `candidate/ordinary-publication-main89b7-harbinger13aeea-3dbe8e19`.
Its review base is that snapshot, not upstream main or the active Harbinger owner's branch.
It applies only the ordinary feature delta from `7cca5594` to `762fbe43`, with reviewed shared resolutions.
The #328/#333 class completions and private all-class integration commits are excluded.
#385 is already merged into pinned main; ItemCandidates, SocketedGemBonuses and support accuracy remain intact.
The current-main composition preserves #401's mod kill/XP/drop/tick hooks, rarity damage and stolen sources.
It retains staged loot, Ancient Orb and projectile contact-point/launch-height changes as baseline behavior.
The launcher keeps main's array construction, which already fixes single-DLL argument concatenation.
New regressions exercise rare hit damage on default/sweep/projectile paths and modifier theft on a Slayer cull.
The initial main `7cca5594` snapshot `b94deb1f` and ordinary `47280f4e` remain on their original local refs.
Their build/test logs remain separate; their evidence cannot certify the current-main composition.
Any later upstream or prerequisite movement again requires reconciliation and affected checks.

All refs remain local, with no push, PR, comment or runtime action from the publication worker.
The parent may publish both new refs and a contributor-fork draft against the snapshot after accepting the result.
Such a draft must explicitly remain blocked on upstream #328 -> #333 -> #339 serial integration.
It must not merge into the active `codex/harbinger-completion` branch.
The actual upstream ordinary PR depends on prerequisite merges and owner-coordinated revalidation.
Private sequence validation is separate evidence, never a publication branch or whole-class acceptance.
The parent retains private audit `64a567d6fd9d5fa29de27d70ed0a4c7fc119d08c` with the exact related-head graph.
Its reported 4174 serial Gateway cases apply to its own `412ed4b0` code and test-equivalent `643f0da5` fixture update.
It predates the framing fix and does not certify this publication candidate; none of its combined production was copied.

Preserve #328 Rampage/non-player ownership and one shared lethal callback, including owner Ignite/RF kills.
Preserve #333 selected accuracy/Blind snapshots, accepted-hit callbacks and Mana-recoup disposal.
The #339 reconciliation retains recipient clocks, charge expiry, ES bypass and source lifecycles.
Raw attack/cast calculators consume action speed once; runtime wrappers apply only the remaining ailment factor.
Mark casts and Mirage unwind the combined owner factor once; native virtual stats receive one final projection.
Explicit final action replacement remains final, and native freeze retains zero rather than the server timer floor.
Incoming Ignite/Bleed retain live Focal source callbacks and channel/ES-bypass treatment.
Recipient-rate changes accrue outgoing Ignite with the same Slayer multiplier as the subsequent burn drain.
Focused publication regressions cover these seams; inherited Harbinger tests retain the exact wire Single contract.
Related class order remains #328 `4c90a1f9`, then #333 `1aa9a177`, then #339 `13aeea19`.
Slayer-then-Deadeye common integration has one writer.
Juggernaut #152, Inquisitor #363 and selection #151 retain their owners.

The parent owns the following remaining acceptance, with exact source/DLL/data hashes and retained logs:

1. Retain the final publication build/Core/Data/Gateway evidence above and repeat affected checks after source changes.
2. Reserve the single-client window and launch only through `scripts/dev/live.ps1` using synthetic saves.
   Check all legal builds, source stats, separate budgets, paid/refused refunds, relog and coordinated restart.
   Projectile build: `5082 61627 56134 26067 28995 45313 53086 5443`.
   Gale/Mirage build: `59837 24848 22852 23169 55985 44482 1729 2872`.
   Alternate path: `28995 45313 64028 26067 62136 2872 5082 61627`.
3. Measure real hits at 10/35/52.5/70 cells: distinct factors 1/1/1.15/1.3 under this model.
   Repeat attack/spell, Point Blank/legacy Far Shot, melee negatives and in-flight paid refund.
   Validate all four Ignite ranges, actual DoT, critical/avoidance/immunity and target mitigation once.
4. Observe +2 emissions, +1 enemy chain, terrain chance/budget, bent continuations, return and recipe-owned areas.
   Exercise classified Barrage timing/spread, ordinary fan negatives and unsupported pre-payment rejection.
5. Exercise all four real mark casts/effects/quality grants, Focal refund/transfer and marked-enemy exclusion.
   Include hits and existing incoming Ignite/Bleed from attackers inside/outside 60 and stale-ID controls.
6. Observe attached continuous-hit refresh and autonomous damage, Occupying cap/radius/lifetime and source removal.
   Check proxy kill/cull/Slayer attribution with no owner leech/Rage/on-hit/charge/recursive grants.
   Review overhead/equipment/height rendering explicitly before claiming native visual parity.
7. Observe Gale use admission, staggered expiry/cap replacement, recipient timing, allied world/radius eligibility,
   Avidity held-charge accuracy, Wind Ward blocked/zero-hit loss and DoT mitigation without consumption.
   Include explicit final overrides, pause, death, successful/failed transfer and delayed send/source-removal controls.
8. Complete the Slayer combat matrix and verify shared leech, direct overkill, old Impale and lethal rewards once.
   Validate the exact related-head sequence and all manual shared resolutions before any readiness claim.
9. Preserve unsuccessful attempts and accepted evidence, restore/release the agreed runtime, then update status.
   Keep both classes Partly working until final native combat and integration acceptance are actually complete.

[pob-parser]: https://github.com/PathOfBuildingCommunity/PathOfBuilding/blob/16de4b8/src/Modules/ModParser.lua#L2654
[pob-offence]: https://github.com/PathOfBuildingCommunity/PathOfBuilding/blob/16de4b8/src/Modules/CalcOffence.lua#L1082
