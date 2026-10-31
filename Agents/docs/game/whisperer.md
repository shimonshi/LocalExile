# Whisperer

Whisperer is the Phrecia `Deadeye@3.29.3b` capability in
`Modules/PhreciaAscendancies/Classes/Whisperer`.
The actual Ascendancy catalog row is 4, Characters row 3 (Ranger), BaseClass 2, ClassNo 2, start 56856.
The displayed name is not its stable ID; normal Deadeye remains a separate family.
Admission requires `PhreciaAscendancies` in the league and `Game_VERIFIED_WHISPERER=1`.
The existing policy verifies the reviewed ordinary/combined graph hashes in `data/game-files.zip` read-only.
That extracted pack attests its captured bytes, not a subsequently modified native installation.
No runtime GGPK decoder, client directory, archive installation or archive repair is introduced.
Idols remains independent.

## Ownership and consumers

The optional module supplies the exact descriptor and mode-aware allocated node stats through the current policy.
It owns no autonomous class worker.
Reusable Mana, Recoup, spending and Blind rules live in `Modules/Core/Game` and also accept ordinary/item sources.
Gateway only exposes an opaque transfer-capable resource slot, a keyed lifetime lease, synchronous cleanup,
a flat pool input and a synchronous continuous-flask observer.
There are no Phrecia class IDs or opt-in branches in those generic layers.

The resource slot is independent of Rampage's owned actors, jobs and worker.
Both Game and optional source-provider host lifetimes own callback-free Gateway retirement resources.
Death, close and retirement clear transient state synchronously; current-generation checks reject stale mutation/output.
Only primitive buff-removal receipts survive cleanup.
An ordinary connection's resources do not depend on the optional provider's lifetime.
Current module reload policy closes affected optional areas; durable class progress remains available on re-entry.
Already active areas register a Gateway identity for current Game replacement to resume player updates.
A separate Game lifetime lease claims the current area loops, independently of Rampage and resources.
It starts recovery, the slower player tick, AI, monster regeneration, collisions, Charges, Fortification, Rage and flask charges.
Retirement cancels old roles, and completion cannot release a replacement's claim.
Gateway server frames retain their existing single framework loop.
Tour is a finite guarded continuation; its normal completion does not stop the area owner.
Pending boss stages resume their existing deadline, and old projectile runtimes retire with the host lifetime.
Send-gate generation checks reject queued old output; old full stat/skill refreshes cannot overwrite current Blind.
Ordinary connections keep their area and resume recovery and Blind publication without a new character action.
Retired monster contacts are refused before hit bookkeeping, and retired Rage publishers stop before stat construction.

| Notable | Consumer |
|---|---|
| 4174 an infinite pursuit | Final Dexterity adds flat Mana before increases; Intelligence's inherent Mana is removed |
| 22720 we know all | Existing maximum-Mana attack lightning; signed -100% cost reduction doubles the supported cost |
| 26415 don't panic yet | Ward, then typed ES, then physical-only defensive Mana; 20% post-Ward hit damage recouped as Mana |
| 53715 breathe deep | Accepted continuous Mana flask recovery also earns Life; independent queues and enduring Mana remain |
| 2266 there is more | Existing maximum/minimum Power and Frenzy charge consumers |
| 863 wait a little | Actual accepted Mana spending grants 10 Rage per 200, retaining the remainder |
| 23799 have you forgotten? | Existing attack-only fork and additional-fork projectile parameters |
| 50932 your mind is destroyed | Existing spell increases/reductions applied to attacks at 150% |
| 5888 not a word | Blind immunity, 100% Blind on hit and 50% increased Blind effect |

The nine small nodes each supply 6% increased maximum Mana and 10% increased attack damage.
Three connected eight-point builds cover every small and notable:

| Build | Nodes |
|---|---|
| Mana/damage | 17123, 4174, 1116, 22720, 27553, 50932, 15390, 26415 |
| Recovery/charges | 30419, 53715, 21095, 2266, 540, 863, 9982, 5888 |
| Fork/Blind | 17123, 4174, 1116, 22720, 24726, 23799, 9982, 5888 |

## Resource and contact boundaries

Recoup uses four game seconds and keeps fractional earnings until whole Mana can be published.
Its basis includes post-Ward damage absorbed by ES or defensive Mana; blocked hits, costs and DoT create no grants.
Full-Mana settlement discards earned fractions while preserving future grants and spending progress.
Only accepted skill/Vaal payment counts as spending; defensive loss, reservation, clamps and flask removal do not.
The existing Vaal admission transaction retains its readiness, captured payment, once-only deadline and executor guards.
The current supported Vaal Ground Slam catalog has zero Mana cost; a positive captured-plan test is an engine control.

The recovery writer preserves Life, Zealot's Oath, RF and leech ordering.
Lethal Ignite clears transient resources/Blind before any await and prevents Mana recovery in the same frame.
Continuous flask Life copying uses the accepted use's recipient snapshot across a paid node refund.
Instant and at-end Mana do not copy; the original Mana queue and duration remain independent of the Life recipient.
Recovery-flask payment commits instant pools, continuous queues, captured Life recipient and Adjacent/LifeFlask edges
under the same flask-bar transaction, before any inventory-send await.
The accepted recipient is neutral effect data, retained over Game replacement without an old Game object.
The flask-bar gate rechecks the admitting owner and item/cell before spending, so queued retired uses cannot charge.
Current publication drains retained inventory/use-sound/buff receipts once and samples current pools at its send gate.
Game retirement preserves paid effects; explicit/source/death/connection retirement retains copied-effect cleanup.
Life copying from a Mana flask does not turn that use into a LifeFlask trigger.
Utility-flask payment installs the accepted stat snapshot, pause-clock deadline and output receipts in one transaction.
The current resource writer owns expiry; pending End edges and presentation data survive Game replacement.
End reuse checks the accepted activation, original item/cell and current enchant again at payment, so old expiry,
manual refresh, moved/replaced items and removed enchants cannot remove or reuse a newer effect.
Smoke, consecrated ground and taunt updates share the current area claim and keep pending output across replacement.
SavageHit uses actual Life lost after Ward, ES and defensive Mana; Block and ailment-transition triggers are retained.

Successful travel uses the checkpoint captured at `TryBeginAreaChange`, before the Auth await.
Destination loading freezes remaining Recoup and player Blind duration until area activation.
A refused transfer resumes the live draft and separately discards loading recovery; it does not restore that checkpoint.
Transfer tickets are character-bound, one-use and tied to both host generations.

Timed Blind, smoke and Rampage Blind share one strongest Accuracy/Evasion reduction, applied once.
Immunity, target identity and removal apply to combat and publication, including owned-actor contacts.
Native timed-buff renewal tracks its own duration independently of the merged strongest stat value.
Publication captures sources once per pass; target count does not multiply full player-stat construction.
Selected and off-hand Accuracy retain their own/local scope before Blind.
Released projectiles retain independent Blind and Ignite snapshots; spell prediction keeps main's spell-only rule.
Send gates refresh current sources and lifetime checks after awaits and paid refunds.
The generic `/blind [seconds]` development control uses normal immunity/liveness and current Blind publication.
Its default is four seconds and accepted explicit durations are finite values in `(0, 60]`.
Slash dispatch rebuilds the current league's command table through the existing Gateway factory before execution.
A completed Game/Commands reload therefore affects the next command in the same inbox batch as well as later packets.
Gateway Send/Reply sinks and dynamic mod fallback remain in that factory; retired Game publication guards stay active.
This dispatch boundary does not promise that every mid-command file-watcher race is safe beyond the existing guards.

## Validation limits

This candidate starts directly at actual main `33ea01513d38a839da09c0e77900e106540459dc`.
It reconstructs the feature-only changes from frozen `918fbc6e`, originally based on merged main `825b9711`.
Original PR head `1aa9a177` and private candidates `33c9a0c2`/`918fbc6e` remain historical references.
Their obsolete Gateway foundations, fixtures and historical native results do not establish this port's readiness.
Two new deterministic Gateway Facts exercise the actual policy/catalog, legal node sources, paid refunds,
resources, shared Blind, transfer and retirement through production helpers without real-time waits.
Combined assertions retain actual-Life SavageHit and accepted recovery-trigger behavior from the current main.
The existing Behemoth admission Fact adds a positive captured-payment/once-only spending control.
The replacement regression drives the actual admitted-area loops and Game-replacement bridge with manual scheduling.
It checks one replacement writer, Life/Mana/ES/flask/new Recoup progress, current Blind expiry and late old output/completion.
An additional control in that same Fact loads two distinct Game assemblies sharing Gateway.
It drives the replacement AI's actual 100-damage contact and current Rage publication under Blind and a held send gate.
Retired contact/Rage callbacks cannot accept zero-damage hits, clear Blind rows or acknowledge current publication state.
Exact-head build, suite, review and native results are recorded in PR #333 and ignored validation receipts.
At `a8cb0ee6`, the recorded full suites passed Core 436, Data 45 and Gateway 15.
Fourteen partial native controls covered legal A/B nodes and resource/outgoing-Blind behavior, then stopped at missing `/blind`.
Incoming immunity/refund, reload and build C were not accepted by that run.
The reload fix and restored command require their own current-head acceptance; older passes do not establish it.
The `8c8898e9` full suites also passed, but its native harness did not execute and review found the retained AI/Rage bugs.
At `106ac5a0`, full suites passed Core 436, Data 45 and Gateway 15; R3 review resolved the earlier findings but found
that active utility expiry still lost End automation on reload. Its partial native V3 checks did not include an enchanted
utility fixture and cannot certify that behavior. The utility correction extends the same Fact with distinct Game
assemblies, actual accepted catalog effects, a held send gate, reload-gap expiry and the real current drainer/payment.
It checks remaining duration, pause, exact End/removal identity, once-only reuse and charge spending, stale item/cell/enchant
rejection, current smoke/consecration expiry and late old completion. Removing the expiry ownership guard makes it fail.
At `b26a1aec`, full suites passed Core 436, Data 45 and Gateway 15; independent R4 separately confirmed current
utility End reuse but found that recovery flasks could pay and lose their use during a blocked inventory send.
The recovery correction extends the same Fact with distinct Game assemblies and actual catalog admission for ordinary
50-Mana recovery, copied Life, instant Life/Mana, at-end/Enduring Mana and independent hybrid durations.
It drives current production recovery, verifies one payment/Adjacent classification and native sound/buff output,
and checks full-Mana leech/Recoup settlement plus retained pre-payment source-stop refusal.
The `b26a1aec` native V4 run was partial (29 checks); source triage confirmed stale cached Commands closures after
ordinary Game reload. The dispatch correction extends the same Fact with distinct actual Game and Commands assemblies
and their actual builders. A manually scheduled isolated dependency wave drives production slash dispatch in one batch,
then a later packet, requiring current strength publication while the old cached closure remains unable to send.
It also checks unchanged area/connection, current league filtering and Gateway sinks/mod-fallback wiring.
This deterministic control does not replace the unchanged native same-area reload acceptance flow.
Current full-suite, independent-review and native acceptance require their own exact-head PR receipts;
these deterministic/build checks and historical full/native passes do not establish whole-class readiness.
