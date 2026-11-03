# Harbinger module candidate

## Delivery boundary

Private implementation candidate for #339, based on main `ec522f1876690c0b203636bf78b508f308365163`.
The sole publisher integrates this candidate after the actual Whisperer #333 successor of `a8afe49e`.
Order: Whisperer, Harbinger, ordinary Inquisitor, ordinary Slayer/Deadeye.
No original branch push, PR edit, client launch or shared runtime access belongs to this candidate.
Harbinger is deliberately absent from `PhreciaFamilyPolicy.ImplementedClasses` until integration is complete.
This is implemented feature code with pending shared wiring, not an independently ready sibling PR.
Idols remains separate.

## Migrated implementation

`Modules/PhreciaAscendancies/Classes/Harbinger` owns the descriptor, metadata binding, roster, cycles,
native actor profile, session adapter, recipient clocks, defence arithmetic and Temporal Chains feature state.
The descriptor identifies only Phrecia `Occultist` revision `3.29.3b`, with the existing read-only graph verifier.
Ordinary Occultist and unsupported revisions do not match it.
The catalog accepts the current connection's mode-specific `SkillCatalog`; production paths remain `data/`.
It resolves all three companions from existing extracted tables without new generators or archive writes.

| Node | Consumer retained in candidate |
|---|---|
| 20105 | Arcane: varieties, two native buffs, source stats and count stat |
| 60867 | Time: native action speed buff and separate action-speed arithmetic |
| 18859 | Focus: a current paid skill/action/channel generation admits a cast |
| 41042 | Remaining BASE recipient budgets and category-specific elapsed-rate calculation |
| 39702 | Extra ES from final Life/Mana; Focus reduction and non-chaos bypass arithmetic |
| 34822 | Temporal Chains source duration/effect, reflection, aura membership and suppression state |
| 62155 | Source remains the current mode node vector; shared Mana/ES recovery wiring is still required |
| 3702 | Source remains the current mode node vector; shared owner cooldown wiring is still required |

Arcane binds `HarbingerArcaneSurgeUber` and `HarbingerArcaneSurgeUber2`; Time and Focus bind their own native effects.
GrantedEffects.SkillID and AddBuffToTargetVarieties supply the recipient definitions and ordered fixed/source values.
No guessed Arcane damage or Shock effect is added.
Minion recovery comes exclusively from its own metadata; owner recovery never changes the minion's cooldown.
Initial cooldown, metadata casting order, 700 ms cast phase and cooldown suspension during that source's buff survive.
An already received Focus buff survives channel termination; starting another cast requires a current paid receipt.
The native actor profile supplies one maximum Life, immortality flags, non-pathing skills and minion flags.
The caller must additionally register it as a non-hostile, non-corpse owned minion with no attack AI.

The roster retains character transfer leases, pause boundaries, passive-source removal, replacement and retirement.
Private fix `28d2f9ac` settles unobserved completed casts at the previous recipient rate before changing it.
This prevents retroactive rate changes or resurrection after expiry; this fix is newer than old PR head `13aeea19`.
Stat/publication reads also settle scheduled completions without starting a cast, before the next minion tick.
The session checks epoch, lease, living owner, pause, source node and current native actor at publication.
Native stats precede AddBuff's remaining BASE duration in the same writer packet.
Publication resamples after building those stats; crossing expiry retains zero clears without adding an expired buff.
Source refund is observed before recipient stat collection, even without a minion tick.
Following beyond twelve cells remains a caller policy; official server AIS was never recovered.

Temporal Chains retains one source snapshot per hostile identity and one reflected self curse.
Identical maintained aura snapshots retain their instance; source changes remove and replace it.
Object-id reuse cannot receive an old recipient's removal.
Hexproof, ignore-Hexproof and immunity use the target's fixed/rolled flags.
Self suppression leaves its clock running, so removing suppression exposes only its remaining budget.
Generic and curse duration increases add before support more-duration and self duration.
The other-effects term excludes Temporal Chains itself and floors at the native recipient-rate minimum.
Party propagation and strongest-of-multiple-casters curse arbitration remain outside this executor.

## Shared integration owned by the parent after Whisperer

### Private combined resource integration

This checkout combines exact inputs Whisperer `566f4e8dd811bef8ec0bd7d933932e671892c5b3`
and Harbinger `b914f0b93bf5b3c84dbf9ea442da3d07bfc72121`, in that order,
on `ec522f1876690c0b203636bf78b508f308365163`.
The local replay commits are `53463c92` and `fdfffd48`; neither input was replaced with an older PR tree.
The shared resource changes below are private, incomplete integration, not class readiness.
The separate correction writer still owns QuestKillEffects and FreezeShown/HeldMove retirement.
Neither correction is included here and neither quest nor LivingCollision was edited by this integration.

`IncomingResourceDefences` is a small Gateway arithmetic contract usable by both Game and the optional feature.
The candidate's HarbingerDefences delegates to it so component and production arithmetic cannot diverge.
The current typed writer applies Focus to each mitigated component before Ward,
then non-chaos bypass before ES, then physical-only defensive Mana.
Mana Recoup still observes the reduced post-Ward components, independent of actual Life damage.
Flask recovery and its accepted payment/copy machinery are unchanged.
Extra ES uses freshly computed final Life/Mana and ES increases once in BuildPoolsFor.
Rebuilding a pool never treats previously derived ES as a new base.
Ignite and Righteous Fire route non-chaos DoT through the same bypass helper.
Fractional DoT reduction/bypass carry lives in neutral connection data and clears on death/close.
This does not yet establish live received-Focus or Temporal source reconciliation at every resource boundary.

The real initial channel admission and repeated paid stages now issue a host-owned receipt with exact
skill, effect, action, generation and rule-lifetime identity.
PaidChannelCurrent rejects changed actions, stale rules, expired silence, pause, blocked actions,
removed/replaced gems and failed current requirements.
Zero-cost successful payment is still payment admission; an unpaid action never creates a receipt.
Focus's session adapter is not yet attached to this production receipt consumer.

The existing two Harbinger Facts retain component checks and add combined production transactions through
the existing Whisperer harness; no new Core/Data test or individual working-directory setup was added.
They cover Focus/Ward/typed ES/physical Mana/Recoup composition, an ordinary control,
source-derived extra ES without feedback, a payable non-channel skill rejection and death cleanup.
They do not establish companion actor acceptance or native class behavior.

Remaining implementation is concrete: attach the roster/session to neutral lifetime/transfer state,
route accepted summon completions to registered owned actors and native cast/follow/buff publication,
feed their live recipient stats into pool/stat sources, attach Temporal self/aura/reflection/suppression,
and integrate category-specific BASE budgets with charge/flask/fortification/ailment/Ignite/taunt clocks.
Time/Temporal native and raw server pacing still need one shared action-factor integration.
The descriptor remains absent until these consumers are complete.
No native client, shared runtime, public push or PR state was touched.

Do not cherry-pick the old 77-file PR tree or its generic foundations over current main.
Do not register the descriptor before these steps; the isolated tests do not establish whole-class readiness.
The following are the minimal behavioral integration points, not permission to replace the containing methods.

| Current location | Required integration |
|---|---|
| `PhreciaFamilyPolicy.cs` | Add Harbinger descriptor only when the remaining hooks and acceptance pass |
| Game `Passive/PassiveGrantedSkills.Rules.cs`, `Combat/Combat.cs` | Reuse passive grant ids/levels and normal payment/cooldown/requirements; call session Activate only at current accepted cast completion |
| Game `Combat/Combat.cs` channel-payment gate | Supply receipt after successful payment; validate exact skill/action/generation and requirements at Focus cast start |
| Game `Monster/PlayerMinions.cs`, `Monster/Monsters.cs`, `Player/Connection.cs` | Attach session/roster under current provider generation; spawn through common owner registry and writer acceptance; bind ids, tick Observe, publish cast phases/following and prune actors |
| Game `Player/ClientState.cs`, `Player/Player.cs`, `Passive/Progression.cs` | Store opaque feature state in the current generation-owned contract; clear on death/logout/provider retirement, transfer lease on travel, reconcile only after a successful saved refund |
| Game `Skill/Skills.cs`, `Item/ItemDefences.cs` | Merge current recipient stats/counts before final derived pools and caching; add extra ES once, without feeding derived ES back into its own base |
| Game `Skill/Skills.cs`, `Skill/Gem/GemSupports.cs`, `Combat/Resources/OwnAuras.cs` | Dispatch Temporal Chains and Blasphemy through current aura machinery, real reservation and source validation; apply free reservation only while its actual source is present |
| Game `Combat/Combat.cs`, `Monster/Monsters.cs`, `Monster/MonsterIgnites.cs`, `Flask/FlaskGrounds.cs` | At accepted curse changes, reconcile old Ignite/taunt budgets before replacing/removing the source; multiply existing monster walk roll and capture swing duration at accepted start |
| Shared incoming/resource writers, Game `Player/Player.cs` | Compose Focus before typed Ward, non-chaos bypass after Ward, fractional DoT bypass; keep Whisperer physical Mana, recoup and unscaled flask-to-Life copying, and synchronous Hardcore death |
| Game `Combat/Resources/Charges.cs`, charge/fortify/ailment/flask states | Keep category-specific BASE budgets, carry and native stats-prefix ordering; retain `SetExpiryRate` before charge Step and `moving: conn.Walk.Moving(now)` |
| Game skill timing/panels/projectile dispatch | Feed Time/Temporal action factor once into raw server timers and movement; preserve Chill projection, native caches, dual wield and every newer main dispatch path; leave projectile flight and cooldown rate independent |
| Gateway `Connection/Program.cs` | Any residual startup/loop hook belongs here, never recreate old root Program.cs |

Use HarbingerActor's profile with current spawn ownership callbacks, not an old SpawnMonster replacement.
Never restore the deleted `CollisionPosition` initializer.
Common fighting-minion/aura/provider contracts belong to the Whisperer integration owner.
HarbingerSession intentionally owns no task, cipher, shared cooldown, common provider registration or Game worker.
The parent must bind callbacks to the currently active host-issued lifetime and retire them on reload.
It must publish removals/replacements using its common buff-instance ledger and current writer gate.
If building a stat prefix crosses expiry, preserve the already consumed stats clears and suppress the expired add.
Promote feature-local clock/action arithmetic into an existing shared engine where appropriate; never install a second hook.

The old diff's admission/graph changes map to the optional descriptor; companion files map to this directory.
Old action/charge/taunt/flask/fortify/ailment/recovery/Ignite changes map to the shared integrations above.
Old connection/death/refund/item changes map to ownership retirement and fresh source reconciliation.
Old common documentation expansions and all historical test files are excluded from this candidate.
Historical candidates including `candidate/harbinger-339-main-d415-09d013a5` remain read-only sources, not bases.

## Validation and remaining acceptance

### Combined resource candidate checks

The private combined resource candidate builds the full solution with zero warnings/errors.
Core passes 434, Data passes 45 and Gateway passes 96, including the existing two Harbinger Facts.
The exact committed combined head and pinned inputs are recorded in `logs/private-harbinger/revisions.txt`
in the prepared checkout; suite outputs are alongside it.
The first Gateway attempt passed 94 and failed two installed-graph checks because this checkout lacked
`data/game-files.zip`; after copying an existing extracted archive privately, all 96 passed.
Existing extracted tables/catalogs were copied from the Harbinger candidate checkout.
No generated data, archives, saves or credentials enter the commit.
No native window was requested or used, so these automated results do not establish class acceptance.

Solution build: zero warnings/errors.
Core: 434 passing; Data: 45 passing; Gateway: 94 passing, including exactly two new Facts.
Gateway uses the existing TestEnvironment module initializer; no individual test changes the working directory.
The prepared checkout initially lacked extracted data, producing missing-input failures.
Validation uses private copies of existing tables/catalog JSON and the unchanged vendor-stock generator's output.
No keys, saves, credentials, client archives or generated data are committed.
The two tests cover native catalog binding, delayed recipient rates, minion cooldown isolation, pause/lease/refund,
paid Focus receipts, queue rejection, retirement, defence arithmetic and curse source/suppression/identity boundaries.
These are component tests, not combined Whisperer/Harbinger or native acceptance.

After #333 changes/merges, fetch its actual main successor and integrate the hooks above in the declared order.
Repeat the solution build and Core/Data/Gateway, combined typed defence/recovery/channel/clock regressions,
provider reload and ordinary controls against the exact resulting heads.
The parent owns the future native window: all three actors and buffs, paid Focus, Time/raw/native timing,
Temporal self/aura/reflection/suppression, recipient slopes, refund, pause, travel, death and reload need fresh logs.
Old PR339 smoke at `ccad428e` and older native runs certify only those historical sources.
They cannot establish readiness for this module migration.
