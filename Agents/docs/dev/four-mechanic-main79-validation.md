# Four-mechanic integration on main 79cc0337

The four PRs keep one mechanic each: increased attributes, mana-to-armour conversion, spell damage restriction,
and recent-hit double-armour chance.
Phrecia class admission remains gated; full classes follow their shared prerequisites.

The tested base is `79cc0337b331b69e49b3c35ea3e34c67e7d547ce`.
The accepted source order is **#290 -> #294 -> #296 -> #300**.
Each published feature branch contains one implementation commit and one evidence-only documentation commit.

| PR | Tested implementation | Published head including documentation |
|---|---|---|
| #290 | `00b246f2a481fe4d680b614e64bfb2bb6aa3605b` | `e47b7403875557d1267d4dc933e13753a15afb30` |
| #294 | `e444b15e1d5fba247a4af746c221424f2fb441f2` | `bbd6755068e48b175e0a4c4a73a027f8eaee8333` |
| #296 | `8ca9f3950fbc4b3051c5a01c40bf9d547fe0164d` | `42e5d81605d9a693567ca85b8e5863cbf08743b3` |
| #300 | `c9e840e9893bd1117b5444b9244919f05607b72b` | `de957213f747e4499318dc47cbf9f73dad567546` |

The independently tested/native source is `c9cd1864d802bfb929e15072105dc5a22670721e`.
Later validation-branch merges add only documentation and retain all four final published heads.
Their complete non-documentation trees match that tested source.
The obsolete PR300 candidates are excluded from this composition's ancestry.

## Shared-code resolutions

The independent feature PRs target main and can still conflict with each other when combined.
This validation branch contains the reviewed resolutions and joint regressions; it is not another feature PR.
Use these resolutions for the proposed merge block rather than selecting one side wholesale.

- `CharacterStats.Bonus` retains both attribute modifiers and the conversion flag.
  Conversion reads rounded final Strength and final Intelligence, preserving odd Intelligence's half mana.
  Strength-granted mana enters the flat bucket once; ordinary clamps and converted BigInteger arithmetic remain.
- `/set_stat` retains attribute rebuilds and both mana rebuild inputs, with source removal and no-refill behavior.
- Spell restriction keeps the current supported-attack/Rage path and the existing aura/resource behavior.
- Recent-hit defence uses one temporary armour decision for the whole physical/enabled-chaos packet.
  Full snapshots acknowledge the last encoded count/chance pair in wire order.
  Fortification builds its deferred tail after the aura prefix.

## Automated verification

Every individual solution build completed with zero warnings and errors.

| PR/source above | Core | Data | Gateway |
|---|---:|---:|---:|
| #290 | 523 | 18 | 3104 |
| #294 | 523 | 18 | 3075 |
| #296 | 523 | 18 | 3100 |
| #300 | 523 | 18 | 3173 |
| Ordered joint source | 523 | 18 | 3308 |

Joint tests use actual command/wire paths for final Strength groups, odd Intelligence, reservation and conversion.
They also exercise spell restriction beside converted pools and mixed physical/chaos contacts after attribute changes.
Decoded-wire regressions cover Rage, own auras, Mageblood and Fortification snapshots crossing recent-hit expiry.
Gateway runs use four xUnit workers; existing automated assertions and timeouts were retained.
Only public copied catalogues were used in the clean verification checkout; no runtime pointers were deleted.

## Native verification and limits

A fresh level-one unascended Marauder passed **28 controls** at the joint source above.
The client was already running; only the isolated Gateway was restarted through `scripts/dev/live.ps1`.
Gateway/Core/Storage/Host embedded revisions, hashes, listening DLL and actual working directory were checked.

| Artifact | SHA256 |
|---|---|
| Gateway | `AD45C6A8EB18ACB3EEF14D931822830CE4C04EE94B0425C4071FF406096FF512` |
| Core | `047E4628944A37DE32EF2FE2D1F2FE96D38A5F2C2C3F6674DB42EC7B0C4CA192` |
| Storage | `E868D09248B91E45BB9699996610C78DD44824943F6B44B919DE972579A83ACC` |
| Host | `0EFAF584CE2D8DAAF58A34561C0D5D1B1F6476CD020F41011887AE95437FDCFC` |

- Eleven attribute/mana/requirement controls and eight conversion controls read native final stats.
  Final Strength 16 / Intelligence 7 retains B=75.5 until conversion gives 15 mana / 140 Armour with the vest.
- Fireball removes 200 target Life, zero with the flag, then 200 after clearing it.
  An attack still removes 100 while flagged; two existing synthetic accuracy modifiers guarantee its hit roll.
- An incoming physical 100 removes 67 Life at permanent Armour 251, or 50 with guaranteed doubling.
  The permanent rating remains 251; accepted contacts, source changes and idle expiry update the native counter.
  One contact with raw per-hit 40 proves the capped 30 contribution plus generic 20, giving chance 50.
  A full stat refresh followed by idle expiry clears the counter and preserves generic chance 20.

An earlier trial passed 26 controls, then timed out on a four-simultaneous-contact witness.
Its exact cause remains unproven; its complete bundle and evidence are preserved locally.
The successful narrowed cap control changes no game clock, deadline or production algorithm.
Four-contact timing and forced full-writer deadline races have automated coverage, not fresh native proof here.
Historical original-head checks retain their original attribution in each mechanic's documentation.
Official-server fractional parity, complete Phrecia classes and broader native cases remain separate work.

Owned selection/logout, observer unload and detach completed; the saved normal Gateway was restored and verified.
No client/archive writes, extra client, extraction, changes to existing accounts or shared-root builds occurred.
