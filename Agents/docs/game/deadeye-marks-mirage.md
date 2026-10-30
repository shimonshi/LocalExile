# Deadeye marks and Mirage Archer execution candidate

This is the exclusive marks/Mirage handoff for ordinary Deadeye issue #392.
The parent owns shared integration, general status/roadmap, native runtime and PR publication.
New engine files are callable and exercised directly; shared hooks are deliberately not installed in this candidate.
These checks do not establish native gameplay acceptance or whole-class completion.

## Revisions and ownership

The allocated checkout was clean at `864da152f451a7fe90024c57ec4d9aba3cfaaca3`.
Its original `t3/deadeye-marks-mirage` branch remains unchanged.
New branch: `candidate/ordinary-deadeye-marks-mirage-392-1031a`.
Exact starting commit: `f15a3abf5ebf841159d264fd43bfb259fa256feb`, containing upstream `5fc27fa2`.
Only this checkout was modified.

Explicit prerequisites were cherry-picked in this order:

| Original immutable source | Local equivalent | Purpose |
|---|---|---|
| `37aa00194b472377cd27d98626bf56f601b3f527` | `ef137517` | byte-identical #339 clocks |
| `ef8ab11975af64f5a11e5e8d24b60069f5222bfd` | `6029f8fa` | Core terrain chain and target split |
| `24052c98bc97a45af5306dd6c07f0d576bb578e3` | `959f26fc` | Gale/accuracy/projectile adapters |
| `865ba96304f35ff56300ffee977b9ce5e6a0a948` | `f2cd324c` | world-scoped allied Tailwind |

The parent already accepted these prerequisites; integration must not duplicate their source.
Marks/Mirage feature commits are `e0a8a69a`, `50fe2ea7` and `f21e1068`.
Exact frozen production revision: `f21e10680aac611874cd886b329f42231d844496`.
Only new feature production/tests and this new document belong to this worker.
No existing shared production/test/doc file was edited by these three feature commits.

Related exact diffs were inspected offline: #328 `4c90a1f9`, #333 `1aa9a177`, #339 `13aeea19`, #385 `ad1e7e32`.
They remain separately owned, with #328 then #333 then #339 ordering unchanged.
The #339 clocks were extracted byte-equivalently, rather than replaced by a second timing engine.
Whisperer blind/accuracy, Behemoth actor attribution and item-support eligibility must survive common integration.
Juggernaut #152, Inquisitor #363 and selection #151 remain outside this worker's ownership.

## Pinned evidence

Game tables are read from exact client 3.29.3b public extraction under repository-relative `data/`.
The Default graph source SHA-256 is `82D82CD3CD2DAD81606AF0DA9D18FCD53B2E92CF006550947D65A4F7E102DC32`.
The public positioned graph JSON SHA-256 is `CB0B73E546F2FA1D19097741C1B67AFACC0585BF7B48F37C900BD7E4DC4460F5`.
The graph has 797 normal groups and seven orbits; the copied hideout catalog has 14 radii.
No Phrecia graph gate, extraction manifest or generated data output changed.

The secondary model reference is PoB `16de4b82d57f1c0de6eb40f37143c32d4da36a02`.
[Mirage support data][pob-support] identifies the bow compatibility, duration and proxy penalties.
[Sniper skill data][pob-dex] identifies target projectile hit damage taken and target split count.
The shipped native tables, rather than an old wiki version, determine all actual stat values here.
[Mark_GGG's blocked-hit explanation][blocked] distinguishes attacker stat effects and defender effects.
It allows skill-specific exceptions; this candidate's no-mark-trigger-on-block rule is explicit LocalExile policy.

[pob-support]: https://github.com/PathOfBuildingCommunity/PathOfBuilding/blob/16de4b82/src/Data/Skills/sup_dex.lua
[pob-dex]: https://github.com/PathOfBuildingCommunity/PathOfBuilding/blob/16de4b82/src/Data/Skills/act_dex.lua#L11635
[blocked]: https://www.reddit.com/r/pathofexile/comments/h0z19u/comment/funpjve/?context=10

## Assigned nodes and execution

| Default node | Native sourced effect | Real engine consumer |
|---|---|---|
| 53086 | mark cast speed +25% | paid native mark cast wait and proposed native skill timing |
| 5443 | mark effect +75% | live per-level/quality mark values and native guarded refresh |
| 5443 | nearby other-enemy damage taken -25% final | incoming attacker-position factor |
| 5443 | mark death transfer 100% | synchronous lethal transfer preserving caster/source |
| 1729 | Mirage duration +15% | actual owned actor category-18 lifetime |
| 62136 | Mirage duration +15% | same lifetime, additive with the first small |
| 2872 | Mirage does not attach | stationary owned actor instead of owner-following actor |
| 2872 | additional maximum Mirage Archers +2 | actual three-actor roster cap |
| 2872 | no summon within radius 35 | inclusive actor-position admission gate |

All assigned small nodes also grant projectile damage +10% through existing damage source calculations.
The nodes do not summon archers without a real compatible linked support and an admitted player bow hit.
Gathering Winds/Gale and the other projectile nodes remain with the separate Deadeye worker.
The Mirage adapter consumes that worker's world-scoped ally Tailwind query.

## Native marks

`DeadeyeMarkCatalog` joins real GrantedEffects and BuffDefinitions, preserving native buff value ordering.
Supported ordinary effects are SnipersMark 1214/buff 78, AssassinsMark 1205/buff 77,
PoachersMark 4044/buff 80 and WarlordsMark 1176/buff 74.
Item paths retain the historical ProjectileWeakness and CriticalWeakness names for the first two gems.
No server-only fake marked flag is used.

`TryDeadeyeMarkCast(session,skill,target)` validates the real selected instance and source requirements.
It rejects town, missing/friendly/dead targets, invalid sources, pause, death and active hit recovery.
It uses the actual native 500 ms cast, caster/support cast speed and mark-specific cast speed.
Native mana cost comes from the supported skill, paid once after admission.
Repeated input cannot pay again during the current cast window.
Source/action invalidation cancels the pending application; the accepted cast's payment remains spent.
Parent integration must intercept object-mark casting before generic action, cooldown and payment ownership.
Ground targeting needs the common parsed-action owner's target selector or a no-target rejection before payment.

Each connection owns one active mark; another successful cast replaces its prior mark.
The mark retains original caster, skill ID, effect, level, quality, source gem identity and target reference.
Ordinary marks have no duration in these native rows and persist until replacement or lifecycle removal.
Any positive sourced duration uses the existing category-14 recipient clock.
Caster death, transfer, close or source removal retires the mark; relog does not restore ephemeral enemy marks.
The current server has connection-local combat worlds; this is not a new cross-connection shared-monster registry.

Focal Point reads the markcaster's current effect sources, including native quality, at the consumer.
Its refund changes current mechanics immediately and republishes changed native values atomically at the send gate.
The hitter's raw damage source is a separate release-frozen snapshot.
It is never recomputed from the markcaster or from the mark's selected gem.

Sniper's increased projectile HIT damage taken joins the target's existing increased/reduced taken bucket.
It follows resistance/armour and the combined physical reduction bucket; it never scales independent Ignite DPS.
Its split query runs before terminal-area selection and retains explicit recipe damage provenance.
Core max-merges the count with existing split, respects CannotSplit and discards provisional split on miss/block.
No eligible neighbouring target means no split; SplitWithoutTargets is not enabled globally.
The legacy flask-charge and knockback buff slots are zero in these real ordinary skill rows.
They are serialized as zero and are not falsely claimed as old Sniper mechanics.

Assassin's Mark adds native permyriad base critical chance and native critical multiplier against its target.
Its death grants restore Life/Mana within current pool caps and enqueue the real Power charge grant.
Poacher's flat physical source enters the actual hitter's raw skill source before effectiveness and conversion.
It then traverses the same increased/more/support calculations as the skill's other added damage.
Its physical reduction modifier joins the target's existing raw reduction before the same clamp.
Its attack hit gains restore actual Life/Mana; its death grant enqueues Frenzy.
Warlord's attack leech coefficients join actual per-type Life/Mana leech sources for direct-player attacks.
Its stun effect extends an accepted existing LocalExile flinch, and its death grant enqueues Endurance.
LocalExile policy grants native Rage immediately on that accepted stun; exact official delivery timing is unrecovered.
Native quality on Assassin/Poacher/Warlord grants Power/Frenzy/Endurance on admitted hits, respectively.
At quality 20, native 5% chance becomes 8% after Focal's integer scaling.

Transfer and nearby defence use a documented LocalExile radius of 60 cells, inclusive, centre to centre.
The marked enemy itself never supplies Focal's nearby-other-enemy incoming reduction.
Friendlies, dead targets and enemies outside the radius are excluded.
Death transfer chooses the nearest living eligible hostile, then object ID for a distance tie.
It retains the original caster/source and does not recast, repay mana or create a second mark.
It must run at the synchronous lethal commit, before generic dead-mark refresh, once per real death.
Boss phase interception is not a death; stale callbacks and reused object IDs fail identity checks.

## Mirage Archer

The support is native GrantedEffects 9076, SupportMirageArcher.
The existing GemSupports resolver determines actual linked compatibility from native ActiveSkillTypes.
It requires MirageArcherCanUse and excludes Vaal, totems, traps, mines and minions.
Current bow, active skill instance, support link and requirements are revalidated on actor work.
The support's 4000 ms base duration becomes 4600 ms with one duration small, or 5200 ms with both.
The duration uses the shared category-18 EffectClock, independently of attack timing and cooldowns.
Attack speed uses the native -60% final support penalty.
Damage uses the actual support level's native less-damage coefficient, not a minion multiplier.

The actor uses the exact native RangerMirage variety and its serializable MonsterObject components.
It has an independent object ID/generation, native Actor skills, friendly team, creation and wake-zone packets.
It is not inserted into hostile monster AI and cannot trigger Mirage recursion.
The actor autonomously selects living hostile targets and releases the original supported projectile recipe.
Core performs actual flight, continuation and contact handling; there is no additional projectile simulator.
Detached actors remain at their summon position; attached actors follow the owner's current position.
LocalExile presentation uses the native RangerMirage model at the actor's world position.
Exact official overhead attachment, inherited equipment appearance and support height-offset rendering
await native review.
The engine's real actor/object presentation must not be reported as visually verified before that review.

Occupying's radius-35 gate compares the owner's attempted summon position to that owner's living archers.
The inclusive boundary rejects the summon without refreshing an existing actor.
A fourth valid distant summon retires the oldest actor and keeps three.
An ordinary attached archer is replaced/refreshed rather than multiplied by repeated accepted hits.
These replacement and nearest-target policies are coherent LocalExile decisions, not recovered official AI parity.

Every attack snapshots real owner skill/weapon/support damage at release.
Its Emitter is the archer; Owner and ReturnAnchor remain the original player.
Proxy action timing removes the owner's recipient action factor from the shared attack-duration calculation.
It then applies the proxy's own received ally Tailwind once: 8% within 60, with no owner Gale self scaling.
The query requires the same world token and explicit distinct proxy key.
Separate connection PlayerId namespaces must never be combined without a shared world and rebased unique keys.

Explicit agreed attribution: CountsAsYourSkill, KillCreditToOwner and AllowCull are true.
Owner leech, Rage gain, on-hit effects, charge-on-hit effects and recursive Mirage are false.
This is an explicit LocalExile proxy policy where official secondary details remain unrecovered.
It is never inferred from a separate actor ID and never classifies the proxy as a minion.
Existing quest/XP/reward and boss interception paths remain common.
The parent replaces the temporary new-file contact adapter with the shared attributed damage contract.
It must preserve Slayer Bane/spatial/kill state, direct-versus-Impale/cull amounts and any shared reflection rules.

Expiry, source/support/weapon/active-set removal, relevant passive refund, death and transfer invalidate actor work.
Queued casts and in-flight contacts validate source/lease/generation; retired actors cannot cause ghost attacks.
The normal runtime pump freezes under pause; transfer/loading/relog retire ephemeral actors by policy.
Native output is guarded at the actual send gate, including source removal while output is delayed.
RemoveBuff also checks the original target reference so a reused object ID receives no stale removal.

## Integration artifacts and checks

Ignored handoff artifacts live under `logs/deadeye-marks-mirage/` in this checkout.
`integration-hooks.patch` is an unapplied baseline extraction and passes `git apply --check`.
`contact-order.txt` specifies the exact shared source, mitigation, on-hit, Impale, cull and death order.
The patch is not a wholesale replacement for the parent's newer Slayer common hooks.
Parent integration must fold targeted modifiers into existing MonsterDamage/MonsterTakenComponents.
It must replace temporary DeadeyeMarkedDamage/DeadeyeMirageHit contact copies with shared attributed execution.
All projectile visual reads must unwrap the Mirage payload's original ProjectileOffense for correct art.

The 41 feature cases execute parsed real metadata, production adapters and real Core trajectories.
They cover native cast cost/timing/buff values, all four mark consumers, quality charge queue execution,
Focal scaling/refund/native refresh, other-enemy defence, transfer and independent mark ownership.
Poacher's raw source is compared with an independently rebuilt actual supported skill after conversion/increases.
Sniper tests include mixed physical/fire mitigation, the existing taken bucket and unqualified-hit controls.
Actual split trajectories cover neighbours, no neighbours, CannotSplit, miss and block.
Mirage tests exercise native actor packets, autonomous release, actual contact damage, source attribution,
linked support/weapon negatives, real duration smalls, cap/radius/following, lifecycle removal and delayed publication.
The bridge forwards actual Core contacts into the production proxy adapter while shared dispatch is uninstalled.
Direct-player shared mark hooks and final shared proxy damage integration remain separate parent-stage evidence.

Build/test outputs stay in this worktree; no production paths or per-test process directory changed.
TestSaves supplies isolated databases.
Only the explicit public extraction whitelist was copied; no keys, accounts, saves, databases or stamps were copied.
The exact `f21e1068` solution build passes with zero warnings/errors.
Core 423, Data 34 and Gateway 3645 pass on that exact revision, with zero failures or reported skips.
The full Gateway suite uses its default parallel execution.
Final logs are `build-quality.log`, `core-quality-final.log`, `data-quality-final.log`
and `gateway-quality-final.log` under the ignored handoff directory.
Earlier full Gateway results: 3638 on `e0a8a69a`, and 3642 on `50fe2ea7`, default parallel execution, no skips.
Those earlier results do not certify a later changed source.
No client or shared Gateway was started, stopped, attached, restarted or commanded by this worker.

## Parent native acceptance matrix

1. Allocate 53086; equip each real mark gem; compare paid cast time and mana with/refund without the small.
2. Allocate/refund 5443; verify native buff values and actual projectile/critical/recovery effects change together.
3. Mark one enemy, take hits from it and from enemies at 60/61; confirm the marked enemy is excluded.
4. Kill the marked enemy through direct hit, cull, Ignite and RF; verify one nearest eligible transfer or removal.
5. Reject friendly/dead/missing mark targets and cancelled/source-removed casts without a late buff.
6. Exercise Assassin critical/death grants, Poacher conversion/PDR/gains and Warlord leech/accepted stun/Rage.
7. Set quality 20 on the three charge marks; observe actual admitted-hit charge events and blocked controls.
8. Link native Mirage support to an eligible bow skill; verify real actor model, projectile art and actual damage.
9. Remove the link, bow, skill requirements or active weapon set during windup/flight; require no ghost contact.
10. Check 4.0/4.6/5.2-second lifetime, attached following, Occupying stationary actors, cap three and radius 35/36.
11. Place detached archers inside/outside Tailwind 60; confirm proxy +8%, without owner's Gale-scaled self effect.
12. Verify proxy cull/Slayer your-skills/recent-kill attribution, with no owner leech/Rage/onhit/recursive summons.
13. Pause, transfer, relog, die and retire while attacks/native sends wait;
    require correct teardown and no old callbacks.
14. Validate native RangerMirage equipment/height/attachment presentation against the chosen LocalExile model.
15. Verify all combined Slayer/Deadeye nodes after exact related-head integration,
    before any readiness/publication claim.

These are prepared scenarios, not fabricated native observations.
The parent alone reserves the one-client window through `scripts/dev/live.ps1` and owns final shutdown.
