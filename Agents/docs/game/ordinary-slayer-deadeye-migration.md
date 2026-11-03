# Private Slayer and Deadeye residual implementation

Base: `ec522f1876690c0b203636bf78b508f308365163`, fetched from origin/main.
Historical source: fork PR2 `30c2f4eefc410bb6ee86d21d5d9cc6fbb0e42ad8`, compared with
`candidate/ordinary-prerequisite-main89b7-harbinger13aeea-3dbe8e19`.
This private candidate reconstructs residual behavior in `Modules/Core/Game/Ascendancy/Ordinary`.
It does not activate gameplay: all shared orchestration remains with the publisher.
The original branch, draft and shared runtime are untouched.
Required actual publication order: **#333 -> #339 -> Inquisitor -> Slayer/Deadeye**.
Inquisitor reference `65b99d17` is a predecessor candidate, not included or independently certified here.

## Executable behavior

- `OrdinaryResidualSession.Kill` rejects noncommitted deaths, phase transitions, already-dead victims,
  reflection and duplicate area/event identities before recording recent kills and Headsman grants.
  Owner DoT and explicitly owner-credited proxies record kill history; only owner direct hits add overkill leech.
  The overkill basis is post-mitigation direct damage minus pre-hit Life, excluding cull and reflected Impale.
- `SlayerRules` supplies independent unique/recent-kill more buckets, capped nearby critical multiplier,
  capped recent-kill area, while-leeching attack/incoming/stun/bleed conditions and Masterful Form equality.
  The overload taking the existing `LeechSession` checks actual Life/Mana/ES instances.
  Standard template39/buff1343 reduces nearby hostile source critical multiplier by 30 within radius60.
  Admission uses qualified passive buff templates, since Ruthless has no template39.
- `SlayerKillState` tracks four-second world-time history and separate twenty-second recipient-clock grants.
  Source refund removes the corresponding grant when observed, without retrospective regrant on reallocation.
- `DeadeyeProjectileDamage` freezes ordinary Far Shot sources at release and evaluates cumulative distance
  at contact: no bonus through 35, linear ramp to full at 70, independent ailment basis.
  Point Blank additionally scales projectile attack hits only; reflected Impale is never scaled here.
- `DeadeyeGaleState` supplies ten independently expiring four-second grants after accepted owner admission.
  The session deduplicates area/use identities and rejects failed payment, triggers, repeats and owned actors.
  Incoming-hit observation returns the factor before Wind Ward consumes stacks; blocks count, evasion does not.
  `DeadeyeGaleAura` selects one same-world/team radius60 source, scales Gale on self only, and supplies
  recipient action-speed input and generation/lease-scoped native Tailwind presentation.
- `DeadeyeMarkRules` owns one target identity/generation, scaled gem amounts and recipient category14 clock.
  Transfer retains the original clock. Standard Focal Point excludes the marked attacker from nearby mitigation.
  Ruthless independently consumes `deadeye_damage_taken_+%_final_from_marked_enemy` (-20) and the nearby
  nonmarked-attacker bucket (+10); it does not inherit Standard's -25 or +75 mark effect.
- `DeadeyeMirageLifecycle` deduplicates the accepted release at its first qualifying direct owner bow hit.
  It owns detached proximity/max-count rules, attached lifetime refresh, equipment signature invalidation,
  autonomous attack deadlines and generation-bound attack tokens. Proxy hits cannot recurse.
  It does not implement object creation, equipment visuals, target selection or projectile release.
- `DeadeyeResidualProjectile` freezes terrain-chain chance and no-Barrage-spread input, leaving generic count,
  chain, collision and sequence execution with their existing owners. No historical half-action timing restored.

All getters receive final qualified sources for the selected ordinary graph and league mode.
No engine here independently requalifies equipment or reads mutable sources during an in-flight contact.
The existing Overwhelm base-crit, Impact proximity, cull, leech and projectile engines are retained unchanged.

## Concrete publisher insertion recipes (unapplied)

Use these after the predecessors are accepted; do not take a side wholesale at their shared seams.

1. Store one session per connection, created with the active Game generation, behind the owner combat lock.
   Add a host-owned **string** slot for its neutral JSON payload and area/use/death sequence counters.
   Do not store a Game-defined type or delegate on the host across module replacement.
   CWTs may cache the live session but cannot be its only reload persistence.
2. At `ApplyMonsterHitLocked` / the common lethal transaction reached by `CommitProjectileKill`, capture
   pre-hit Life and separate direct damage before mutation. After the first actual lethal commit, synchronously call
   `state.Kill(now, new(areaEpoch, deathSequence, credit, true, false, rareOrUnique, lifeBefore, direct), Get)`.
   Preserve the same identity through duplicate reward/contact callbacks. Phase transitions do not call it.
   DoT passes `OwnerDamageOverTime`; Mirage passes `OwnedProxy`; reflection never becomes owner direct damage.
   Merge returned `OverkillLife` into the existing Life leech amount **before the single LeechSession.Hit**.
   Do not record another kill in awaited reward/presentation work or call a second leech engine.
3. After common successful payment and cooldown admission, call
   `state.Gale.Observe(now, qualifiedGatheringWindsTemplate44, Get, alive)` and
   `state.Use(now, new(areaEpoch, useSequence, true, true, triggered, repeat, ownedActor))` once.
   Zero-cost successfully admitted skills count as paid. Failed requests and repeated contacts reuse/reject identities.
4. At final-stat projection, read `state.Slayer.Read(now, Get)` once for movement/attack/area increases.
   At target contact multiply the existing source hit/ailment more bucket by
   `SlayerRules.DamageMultiplier(Get, actualUnique, snapshot.KilledRecently)` exactly once.
   Add `CriticalMultiplierBonus` before the existing target critical restrictions and new Impale storage.
   Use living hostile nearby counts, excluding owned actors; the historical 70-unit offensive count is a model.
   Feed `WhileLeeching(Get, existingLeech, now)` into the increased-taken bucket and stun/bleed consumers.
   Bleeding remains present while unaffected; do not remove it or change leech/recovery caps.
   Project `EnemyCriticalMultiplierDelta` into incoming enemy source crit before final target no-extra-crit handling.
5. In `ChargePanelStats` (or the accepted charge resolver), after ordinary Frenzy and its explicit replacement,
   set completed Endurance to `SlayerRules.EnduranceMaximum(Get, endurance, frenzy)`; then resolve special
   mirrors/minima and apply the final explicit Endurance override. Keep the existing charge session/presentation.
6. Carry `DeadeyeProjectileDamage.Capture(Get)` and `DeadeyeResidualProjectile.Capture(Get)` in the existing
   release-frozen offense payload through chains, forks and child stages, with explicit actor provenance.
   On eligible projectile contact use cumulative travel, not movement-instance displacement:
   scale the direct source packet by `HitMultiplier(distance, attack)` and the separate ailment basis via
   `IgniteAt(releaseSettings, distance)` or `AilmentMultiplier`. Keep reflected Impale and leech separate.
   Existing terrain collision owner calls `ChainsFromTerrain` only if its normal chain/prohibition/budget rules allow.
   Apply `ApplySpread(profile, actualBarrage)` after generic count resolution. No count/sequence replacement.
7. `CommitMonsterHitLocked` captures `state.Gale.IncomingHit(now, acceptedHit)` before the existing incoming
   mitigation; only accepted hits consume. Feed Tailwind's one selected `Received.ActionSpeedIncrease` into
   the accepted #339 action-speed composition, replacing duplicate Tailwind contribution, before final bounds.
   Preserve Unstoppable, chill/freeze and recipient-clock policy. Do not multiply a completed factor twice.
8. Mark admission/payment stays with one shared skill owner. On accepted completed cast call `Marks.Apply`.
   Resolve `Marks.Current` by exact target and source signature before every contact/target query; route
   `Amount` into the existing target added-base/crit/leech/split/taken consumers at their normal ordering.
   Added physical damage is pre-effectiveness/conversion, Sniper increased-hit-taken is post-mitigation.
   Apply `Incoming` to incoming hit and supported DoT routes using actual attacker/mark positions.
   On death choose the nearest eligible living hostile within radius60 (id breaks distance ties), then `Transfer`
   if the qualified transfer chance succeeds. Remove old buff and create the new one under one presentation owner.
9. On the first qualifying paid owner bow contact call `Mirages.OwnerHit` with the complete equipment/gem/link
   signature and immutable release sequence. Allocate native object ids only for newly returned actor identities.
   For each autonomous attack use `TryAttack`; queued work must check `Current` plus actual lifetime/source validity
   under the combat lock immediately before release. Use the existing projectile runtime, actor equipment and recipe.
   Proxy kills can be credited to owner, but grant no owner leech/rage/on-hit/charge-on-hit or recursive Mirage.
   Parent supplies actor target selection, equipment/native packets and exact retirement/removal.
10. At cipher gate check the active Game generation and `state.CanPublish(currentGeneration)` plus world/lease
    and current grant/mark/actor identity. Batch Headsman (buff1341/1342), Gale and mark/actor changes once.
    Do not mutate shown-state before that gate. Old queued work must fail after retire or source refund.
11. On reload/transfer capture `state.Capture(now)` on the host before `Retire`; restore into the new Game
    generation using `Restore(json, generation, now, gathering, qualified)` after acceptance.
    The pausable owner clock must retain its epoch across transfer/loading; a different epoch needs explicit rebasing
    by the parent. Finite clocks preserve absolute deadlines and settled elapsed time across reload, never a new grant.
    Failed transfer restores the captured state once; death/logout clears it. Mark/actor native identities intentionally
    retire rather than survive reload/transfer; remove old objects/buffs and cancel their callbacks.
    Full mark/actor recreation across reload is remaining integration work, not claimed persistence.

`OrdinaryEffectClock` is a private feature-local clock port pending the actual accepted #339 contract.
At integration replace it with that common clock if its carry API preserves original deadline/rate/remaining/sample.
Do not add another generic clock engine or replace #339's recipient policy.
Event deduplication sets last for the session; the parent may prune only after all callbacks from an area retire.

## Validation and limits

Solution build: zero warnings/errors. Core: 434 passed. Data: 45 passed. Gateway: 94 passed.
All three report zero skips; existing tests can early-return when optional client assets are unavailable.
Exactly two new Gateway Fact methods group Standard/Ruthless extracted node-vector scenarios and lifecycle rules.
No new Core/Data tests, individual test CWD changes, production path changes or real-time sleeps.
Data is a private physical copy of the prepared Inquisitor candidate's tables/catalogs, with vendor-stock present.
No client, running Gateway, shared production data, account store or archive was accessed.

Earlier build attempts exposed a missing shell PATH SDK and historical ActionSpeed/profile fields absent on main.
The installed SDK was invoked explicitly; the final code projects inputs into the existing owners instead.
An intermediate stronger regression expected Far Shot's populated hardmode column (20), then failed against
the correctly admitted 30: its HasHardmodeAlternate flag is false. The corrected assertion pins production admission.
The complete shared stack, native presentation and active gameplay still require publisher validation.
Moving Frenzy/Avidity, full mark catalog/contact adapters, Mirage visuals/equipment/attacks and cross-generation
mark/actor recreation are not complete here. This candidate must not be marked independently ready.
Parent should update the existing ordinary status/roadmap entries when hooks activate, linking this document;
shared summaries are deliberately untouched while Whisperer/Harbinger integration owns those files.
