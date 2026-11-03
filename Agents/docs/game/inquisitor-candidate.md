# Private ordinary Inquisitor implementation candidate

Base: `ec522f1876690c0b203636bf78b508f308365163` (freshly fetched `origin/main`).
Historical source: fork PR1 `930b21fa9ba46ba2db146b04229aff44af14c7b2`, originally based on
`candidate/pr318-main361-admission`.
This candidate replaces no original branch and has no publication or readiness claim.
The parent is the sole publisher and shared-integration writer.
Actual merge order remains **#333 -> #339 -> Inquisitor -> Slayer/Deadeye**.
Whisperer/Harbinger shared integration must be retained when the parent applies these hooks.

## Implemented independent behavior

`Modules/Core/Game/Inquisitor/` contains executable ordinary helpers, independent of Phrecia.
No helper installs itself into shared orchestration in this private candidate.
The two grouped Gateway tests call the new projections using real ordinary node stats and WeaponTypes data.

- Providence derives its boolean grant from the lower final qualified Strength/Intelligence, once.
  Attack/spell projections call the current shared critical engines, retaining cannot-crit and skill modifiers.
- Justice uses main-hand local critical base, before shared added-base/increased/final critical calculations.
  Empty main-hand is zero, including an off-hand-only context.
- Battlemage copies the qualified main-hand `LocalDamage` packet, with legacy physical/added fallback.
  Added effectiveness scales that packet once before current spell increases and conversion.
  It never consumes global attack damage or reapplies local mod effect.
  #573 is not an ancestor of this base; its item-local resolver remains the upstream authority when integrated.
- Virtue retains eight-second accepted manual non-instant spell history, capped at 30% more attack damage.
  Accepted-use provenance excludes triggers/repeats and records attacks before periodic Fanatic charge observation.
  Charge gains occur on the pausable one-second schedule, never retroactively for a later attack.
  Fanaticism magnitude/cost projection retains current generic engines and receives buff effect explicitly.
- `InquisitorRecipients` owns recipient intervals only, never a ground, owner buff or regeneration source.
  Sanctuary enemy linger is separate per living hostile ID, and Pious mirroring clips exact owner expiry.
  The mirror consumes actual Life regeneration before Life-only recovery scaling, applies ES recovery rate once,
  and respects Life regeneration and ES recovery prohibitions.
- Nearby Augury yields additive typed increased-taken contributions (16 Standard / 8 Ruthless within 60 cells)
  and a separate .92 incoming elemental factor; physical/chaos are excluded.

Five-second Fanaticism comes from the historical pinned 3.29.3b stat-description investigation in
`Agents/docs/game/inquisitor.md` at source `930b21fa`, not the legacy `for_4_seconds` stat name.
No client/archive was accessed to re-prove that description in this task.
Duration is an explicit clock input for the parent's accepted #339 duration/recipient policy.

## Concrete shared hook patch for the publisher

These are insertion/replacement recipes against this exact base, deliberately not concurrent shared-file edits.
They require the actual accepted predecessors before integration and combined testing.
`inquisitor-supported-raw.patch` is the concrete supported-spell raw-input slice of hook 2.
It is kept unapplied for the sole shared writer and is not a complete gameplay installation patch.

1. In `Passive/Progression.cs`, immediately after `CharacterStats.For` produces final qualified pools, compute
   `p = InquisitorProvidence(RawCritical, pools.Strength, pools.Intelligence)`.
   Add `p` to both `CriticalStrikeChancePct` and `SpellCriticalStrikeChancePct` before recomputing displayed chances.
   Use `InquisitorOffense.SpellBase(base, activeWeapon.Unarmed ? null : activeWeapon.Critical, RawCritical)`
   for the pooled spell reference.
   In `Skill/Skills.cs` raw stat assembly add the same `p` to `critical_strike_chance_+%`, like overcap crit.
   Consumers using the stored pooled increases must NOT add Providence a second time through the convenience helpers.
   Pure observers must not call equipment qualification publishers.
2. In `Skill/Gem/GemSupports.Rules.cs` spell raw assembly, replace `plan.SpellBase` with
   `plan.SpellBase.Add(InquisitorOffense.WeaponDamage(WeaponIn(conn, ActiveMainHand(conn)), Get).Scale(effectiveness))`
   inside `DamageStats.RawAdded`; retain that function's existing added effectiveness for other added sources.
   In `SupportedCriticalChanceFor`, pass Justice's selected base into existing `SpellCriticalStrike.Chance`,
   with the active qualified weapon's local crit, never pooled main-hand attack chance.
   Apply the same raw/base projection in `Passive/PassiveGrantedSkills.Rules.cs`, retaining hit/flat ailment parity.
   `SpellHit.Resolve` needs an explicit optional local weapon input for callers without a connection.
3. After accepted payment/admission in `Skill/Skills.cs`, notify `Used(now, inputs, provenance)` once.
   Rejected requests never call it; triggers/repeats never become another owner use.
   Apply snapshot `AttackDamageFactor` only to attack more buckets, and `Fanaticism` only to self-cast spells.
   Cost projection belongs before Lifetap/#543 split/additional Life cost and final pool rounding.
   Compose cast speed with existing action speed; area goes in the additive area bucket.
4. Reuse `FlaskGrounds` and its existing `Walk.Moving(now)` lifecycle as sole ground/buff authority.
   Observe actual owner/enemy coverage at both sides of accepted movement/source changes, not a lingered marker.
   At natural arrival use the actual arrival instant only for unchanged sources previously observed moving.
   Feed `ObserveEnemy` per live object ID and add `TakenIncrease` to hit AND supported DoT increased-taken buckets.
   Feed Augury allocation 40059 plus actual current hostility/life/range; incoming elemental hit/ignite/DoT routes
   consume `IncomingFactor` before critical ignite basis is captured.
5. Before every recovery/source/coverage transition, integrate the old segment through `MirroredRecovery`.
   Supply the shared recovery engine's actual piecewise regeneration, including temporary source expiry boundaries.
   Never feed post-Life-recovery rate, Flask, Leech, Recoup, or ES regeneration into the copy.
   Publish the corresponding current rate without making a second recovery writer.
6. Keep one connection-owned session and one generation-bound presentation writer, under the combat/send gates.
   Death clears recipients/history; successful transfer captures combat only and retires the old owner.
   Failed transfer restores the captured payload once under the parent's ownership token.
   Capture/restore neutral BCL dictionaries across Game reload, using original remaining durations, frozen loading time.
   The connection/ticket owner must enforce character/generation identity and single consumption.
   Do not store Game-local `Carried` records or CWT/static tickets in the host; they cannot survive assembly replacement.
   Ground recipients are area-local and clear on transfer; do not transfer enemy IDs or art/buff ownership.

## Validation and limits

SDK: existing `C:/Users/solba/LocalExile/Tools/dotnet10/dotnet.exe` 10.0.402 (not on this shell's PATH).
Production data paths remain relative `data/`; the shared test initializer is unchanged.
Existing extracted tables and selected non-secret catalogs were copied read-only into this worktree's ignored data.
No shared data writes, runtime/client access, branch publication or PR edits occurred.
No extractor output, manifest, item-local scaling or generic ground/resistance/recovery engine was changed.

Initial solution build: zero warnings/errors; focused Gateway regressions: 2 passed, zero skips.
Core: 434 passed; Data: 45 passed, zero reported skips in either suite.
First full Gateway: 92 passed / 2 failed / zero reported skips.
Both failures are existing `VendorItemLevelTests` requiring absent `data/vendor-stock.json`.
The failure was repeated in a focused normal-verbosity run; no vendor production code was changed.
The generated file is also absent from the shared source data, and generating it is outside this candidate's scope.
Final code verification repeated the solution build (zero warnings/errors), Core (434 passed), Data (45 passed),
and full Gateway (92 passed / the same 2 vendor failures), with zero reported skips.
The focused two-test Inquisitor run also passed after adding the independent attack-critical projection.
`git diff --check` and `git apply --check Agents/docs/game/inquisitor-supported-raw.patch` pass.
The checks cover the committed code; subsequent edits to this receipt and status links change documentation only.
No native parity, actual predecessor composition, presentation, or installed gameplay acceptance is claimed.
