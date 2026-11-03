# Private ordinary Slayer and Deadeye gameplay candidate

Input rules: `0831ee7f75f3963c7c3dcb96284a33b72b54b21a`, originally based on main `ec522f18`.
The root combined integration reconciles remaining adapters `987b84b1`, marks `64d9d97e`,
Inquisitor `75e49a1f`, Harbinger `f9793c84` and Whisperer `1d87a4f3` on main `ec522f18`.
The integration includes the accepted-transfer and rejected-attack corrections plus the ranged-clock regression fix.
Private combined base: `e3587919`, containing exact Whisperer `1d87a4f3`, Harbinger `5318b7b9`,
and Inquisitor `65b99d17`, followed by the input rules.
The private merge `009ec47d` retains Whisperer quest transactions, phase-type output, held movement,
freeze acknowledgement and current-generation publication while admitting Harbinger typed resource routing.
The test class remains partial and retains both predecessors' grouped transaction regressions.
The feature implementation is in `OrdinaryGameplay.cs`; shared consumers receive small hooks.
Nothing was published, pushed, marked ready, or installed into the running native environment.
The root remains sole shared integrator and publisher, in order **#333 -> #339 -> Inquisitor -> Slayer/Deadeye**.
Combined native acceptance remains incomplete; automated gameplay checks do not certify native art or timings.

## Active production consumers

- The existing synchronous owner-damage transaction records credited lethal outcomes before awaited rewards.
  Its Life result excludes phase floors, threshold encounters, invulnerability and already-dead callbacks.
  The common hook records kill history only; direct-hit overkill is handled separately before reflection and cull.
  Owner and owner-credited proxy/DoT routes reaching that transaction share history.
- Direct-hit leech adds post-mitigation overkill to the same Life amount before the single shared session call.
  Existing instance caps, instant fractions, Ghost Reaver, target prohibitions and full-pool behavior remain intact.
  Phase-bearing encounters before their completed stage conservatively receive no overkill addition.
- Default melee, supported area hits and owner projectiles consume actual-unique and recent-kill more buckets.
  Nearby living hostile counts add Overwhelm critical multiplier before target restrictions and new Impale storage.
  Standard template39 reduces the nearby enemy critical multiplier through the existing incoming critical owner.
- Raw character sources receive Headsman movement/attack speed, recent-kill area and actual-session leech conditions.
  Pool rebuilds, movement and client stats refresh through the existing charge ticker when those inputs change.
  The incoming increased-taken bucket and existing stun consumer receive the corresponding leech conditions.
  Bleeding-unaffected projects its flag; there is no claim of a new player-bleeding damage engine.
- Masterful Form changes the shared standard Endurance maximum consumed by the panel and charge session.
  Explicit Frenzy replacements, including zero, precede the Endurance mirror in both consumers.
  A final explicit Endurance replacement retains precedence over that mirror.
- Projectile release captures ordinary Far Shot independently of legacy Far Shot and Point Blank.
  Contact reads cumulative runtime distance, and separately scales the independent ignite settings.
  Slayer unique/recent-kill more also scales projectile ignite settings independently of direct-hit leech.
  Existing chain/fork/return child payload propagation retains the release inputs.
- Paid manual gem admission and accepted ordinary attack admission record one Gale use per area/action identity.
  Failed payment, channel ticks, repeats and owned actor contacts do not call that hook.
  Owner Tailwind joins the existing action-speed reduction bucket once; a final developer override retains precedence.
  Accepted incoming monster hits capture Wind Ward mitigation before consuming stacks; blocks count, evasion does not.
- Headsman buffs1341/1342 and owner Tailwind1094 publish under the current charge sender's cipher gate.
  Shown ids and deadlines live on the host, so replaced Game assemblies do not independently allocate duplicate buffs.
- A neutral JSON payload lives on the host connection, retaining kill history and original finite effect clocks.
  Accepted transfer tickets carry neutral payloads and rebase epochs with remaining time frozen during loading.
  Tickets are character-bound, expire and are consumed once; failed transfers never create them.
  Death and closing clear the state. Mark/actor identities intentionally do not cross replacement or travel.
- Avidity accuracy scales the existing accuracy consumer using current effective Frenzy charges.
  Its moving Frenzy source already uses the shared periodic charge timer; no duplicate charge engine was added.
  Timer progress and its last sampled world time now live with the host charge state across Game reloads.
  Death clears both; a fresh connection starts a fresh timer rather than inheriting another character's progress.
- Tailwind selects self or a living, current, nearby published player in the same instance-presence cohort.
  The cohort is the existing allied-player model; no party membership or PvP policy exists to extend it.
  Peer selection reads immutable published poses and a neutral qualified source flag, without peer combat locks.
  Gale scales only its owner; the recipient's Tailwind-effect stat scales the selected nonstacking receipt once.
  The current cipher gate owns Tailwind source/value changes and independent row1095 Gale grant clocks.
  Ordinary finite clocks delegate category rates to the shared `RecipientEffectTime` contract.
- Aura activation, accepted links, Vaal costs and instant utility uses join paid manual-use Gale admission.
  Instant utility identities are separate from the interrupted ordinary action; aura deactivation grants nothing.
- Released ordinary/gem projectiles retain terrain-chain policy across refund and child propagation.
  The existing simulator checks a reachable hostile and chain budget before asking the production adapter to roll.
  A terrain chain consumes that budget and resets only the movement-instance range, retaining total distance.
  The gem release adapter suppresses both native and server spread for Barrage/`projectiles_barrage` sources.
  It does not add a missing Barrage skill recipe or change unsupported-skill admission.

All sources use the selected normal graph and existing Standard/Ruthless qualification.
Ordinary production transactions are tested with only the Game league module enabled.

## Explicit remaining implementation

This is a partial gameplay candidate, not completion of both ascendancies or an independently ready PR.
The following requested behavior remains unimplemented or uncertified:

- Mark admission and Focal Point now have production adapters in `DeadeyeMarks.cs` and `DeadeyeMarkContacts.cs`.
  The four extracted ordinary gem identities use shared requirements, cooldown/payment and manual-use acceptance.
  Target admission rejects ground, friendly, dead, distant and reused objects before payment.
  Cast completion rechecks the action, source gem and target object; cancellation retains the paid cost.
  Native rows74/77/78/80 carry ordered qualified values, source skill and recipient-rate finite clocks.
  Source removal, death and Game retirement clear identities; neutral area/reload payloads never carry marks.
  Accepted lethal transfer selects a living nearby hostile only when the current source grants transfer.
  It keeps the original clock and never repeats payment; Standard and Ruthless use their separate Focal vectors.
  Supported owner melee/area/projectile contacts consume mark critical, damage-taken, physical reduction,
  added-base, recovery, leech and stun inputs; projectiles retain their frozen hitter inputs across refund.
  Sniper split feeds the existing simulator through a default-zero target query, without a new projectile engine.
  Native art/timing, knockback and legacy Sniper flask-charge behavior still need separate certification.
- Mirage now has production actor allocation, source validation, target selection, projectile attacks and retirement.
  An accepted owner arrow release carries its area/action/source receipt; only a qualifying hit admits the actor.
  Autonomous arrows use the common projectile damage calculation, including distance and target mark modifiers.
  They do not debit owner mana, leech for the owner or grant owner direct-hit charges, Rage or critical flask effects.
  Reflected non-damaging ailments do not target the owner through the proxy's contact.
  Detached actors validate each source independently; equipment changes and replacement sessions retire stale actors.
  Native appearance, equipment packets, attachment and animation timing remain uncertified.
- All-source direct/DoT/proxy provenance certification across every historical damage route.
- Player bleeding damage suppression, complete Slayer DoT/ailment coverage outside the supported projectile ignite path,
  and real-client timing/art/equipment verification.

The underlying rule objects retain pinned source comments and Standard/Ruthless node-vector tests.
No new generic cull, leech, critical, proximity, projectile, ranged, recovery or charge engine was introduced.

## Verification

The input's two Gateway Facts are extended with production transactions rather than adding new Fact methods.
Coverage includes actual mark gem payment/rejection, replacement, source removal, finite expiry, native cleanup,
clock-preserving lethal transfer, ordinary non-Deadeye use and Standard/Ruthless Focal incoming damage/refund.
It also includes ordinary-only admission, actual lethal/duplicate/phase outcomes, overkill in the shared leech session,
refund without retrospective regrant, native owner Tailwind output, accepted Gale deduplication and Wind Ward consumption,
real projectile release freezing across refund, and accepted neutral transfer with the original remaining lifetime.
The bounded adapter build and required suites are recorded against the private committed candidate in ignored
`logs/ordinary-finish/`, including `source.txt`, `build.log`, `core.log`, `data.log`, `gateway.log`.
The earlier mark-wiring candidate's logs remain separate in `logs/ordinary-wiring/` in its originating checkout.
The predecessor Inquisitor timing assertion sampled before its first one-second charge boundary.
The root correction samples the actual accepted-use boundary; the exact combined predecessor passes all suites.
No Core/Data tests, sleeps, production catalog path changes or per-test working-directory changes were added.
Prepared nonsecret data is physically copied from `t3-f62fbadf`; the existing vendor-stock file is present.
The predecessor graph-admission tests also require its nonsecret `game-files.zip` fixture.
A first full Gateway run lacked that fixture and failed two predecessor admission checks; the rerun includes it.

## Root native replay checklist

1. Verify the final accepted predecessor heads and build hashes before taking the existing native window.
2. On normal Slayer, allocate/refund Headsman, Bane, Overwhelm, Endless Hunger, Brutal Fervour and Masterful Form.
   Check actual lethal/phase/cull/Impale separation, leech instances, nearby critical behavior and timed expiry.
3. On normal Deadeye, check Far Shot at35/70 cells, independent ignite, return/chain distance and source refund in flight.
4. Check accepted/rejected manual uses, repeated contacts, ten Gale stacks, blocked/evaded hits and current native Tailwind.
5. Check timed stat/buff expiry while idle, paid refund, death, logout, Game reload and successful/failed area travel.
6. Repeat admitted Standard/Ruthless vectors. Do not claim mark/Mirage completion from helper tests or class selection.
7. With two supported same-instance peers, verify60-cell Tailwind entry/exit, source refund/death/leave and source swaps.
   Confirm Gale stacks affect only their owner, independent stack expiry, rate changes and native row1095 display.
8. Verify aura on/off, link, Vaal and instant utility admission/rejection without duplicate Gale on repeats.
   Check terrain chains consume the existing budget and Barrage spread suppression on an admitted executable recipe.
9. Replace Game midway through Avidity's moving period and verify the remaining interval, then stop/move and die.
   Set virtual Frenzy maximum to zero and seven with Masterful Form; compare the charge session and native panels.

The running client and its logs belong to the root; this private writer did not access or rebuild them.
