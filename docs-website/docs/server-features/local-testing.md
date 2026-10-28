---
title: Local testing preset
description: Reversible rates, editable caps, monster spots and simulated crowds.
---

# Local testing preset

Back up the database and configuration before deployment. This opt-in feature
needs no schema migration or new database. Existing characters, inventory and
natural spawns remain in place. Deploying new assemblies requires restarting
the server process; editing these plugin settings does not.

## Admin controls

Open **Game configuration → Plugins** (`/plugins`), search for **macOS local
testing preset**, activate it and open its gear button. Keep the plugin active
and use **Enabled** to apply or restore the preset. Settings and its rollback
snapshot persist in the existing plugin configuration.

| Field | Default and meaning |
|---|---|
| Enabled | Off until explicitly enabled |
| NormalExperienceRate | 10000, relative to the engine's 1x formula |
| ItemDropMultiplier | 1000, ordinary drop-group probability multiplier |
| MaximumAttackSpeed | 800, attack **and magic** speed cap, editable 1–65535 |
| MaximumStat | 65535, five base and total stat caps, editable 1–65535 |
| EligibleMaps | `0,1,2,3,4,7,8,10,33,37,38,51,56,57,63,80,81` |
| MonstersPerMap / MonstersPerSpot | 150 / 3, bounded to 100–200 / 2–3 |
| SpotSpacing / RespawnSeconds | 12 tiles / 3 seconds |
| ActiveMonsterMaps | Empty until applied, selected map numbers |
| GlobalMonsterLimit | 3000 across the process's game servers |
| MapActivationIntervalSeconds | 5, one additional map per stage |
| MaximumWorkingSetMegabytes | 6144, pauses additional spawns above this process memory limit |
| PopulationServerId | Owning game server; helper actions select the current server |
| CrowdCount / MaximumCrowdBots | 0 / 100, target and global limit |
| CrowdMap / CrowdX / CrowdY / CrowdRadius | 0 / 135 / 125 / 8 tiles |
| CrowdBehavior | `idle`, `wander`, or `combat` |
| CrowdLevel / CrowdLifetimeSeconds | 100 for newly generated characters / 300 seconds |

Caps are limits, not stat grants. Low-agility characters do not automatically
gain 800 speed. Lowering a cap preserves stored stats and blocks further
allocation above the limit. Raising it reveals the retained value. Verify
combat animations for the chosen classes and equipment at unusually high speeds.
The stored stat getter remains uncapped for persistence, while effective
attribute reads apply the current cap. Saving or reconnecting therefore does not
overwrite an existing higher earned value with a temporarily lower limit.

Only **GameMaster** characters may edit testing settings or preview populations
through `/localtest`. The server enforces this independently of UI visibility.
Normal characters may read status and use normal reset/market actions. Privileged
requests and results appear as `LocalTesting audit` entries in server logs.
Retain `/app/logs` across container recreation.

## Rates

Normal XP combines base kill XP, game/server multipliers, character experience
and bonus attributes, map multipliers, experience plugins and random variation.
Party sharing/bonuses and premium/event rules remain in force. Master XP keeps
its separate rate. The preset enables existing overflow protection; intermediate
conversions saturate at `int.MaxValue` instead of wrapping negative. It does not
multiply the previous configured rate (for example 9999) by 10000.

Ordinary item-group probability becomes `min(1, originalProbability × multiplier)`.
For example 0.0001 becomes 0.1; 0.01 becomes 1, an effective 100x gain because
probability cannot exceed 1. The helper reports groups hitting this cap. Drop
selection order and competing groups still affect observed loot. Item counts
are unchanged. Money, quest-item groups, item-open rewards and monster-specific
reward groups are excluded. The installed engine's **Zen amount formula uses
gained experience**, so increasing XP can indirectly increase Zen amounts even
though this feature does not multiply Zen tables.

Original values are captured and persisted before application. Reapplying uses
that baseline. Disable restores only owned rates, probabilities and caps, not
an old database snapshot or unrelated configuration.

## Spots

Preview is read-only. Only hosted ordinary maps in the allowlist qualify;
restricted/event/boss maps and maps without suitable natural ordinary monsters
are excluded with reasons. Spacing, wall clearance, safe zones, gates and NPC
interaction areas are checked. Shortfalls are reported instead of forcing tiles.

Generated monsters use cloned spawn settings and owned runtime references.
Natural spawn lists remain unchanged. Reapplication retains owned populations;
changed parameters replace only those owned populations. Cleanup cancels their
respawns. Requested, allocated and alive counts are distinct during deaths.

The installed terrain at default spacing fits 150 on fifteen maps, 105 on
Icarus and 63 on Kanturu III: 2418 total. Different terrain can change this.
The memory ceiling pauses later stages without deleting natural entities.
**Stop** disables the preset, restores its baseline and removes owned entities.

## Crowds and measurements

Crowds reuse the bot factory and offline-player lifecycle. Supported classes are
randomly selected, visible names begin with `Test`, and accounts use the separate
`tc<server><index>` namespace. Real accounts are never repurposed. Starter gear
comes from the normal factory. Idle stays put, wander uses bounded adjacent
steps, and combat uses existing MU Helper AI. Ordinary death remains possible.

Selected/all cleanup removes active entities. A persisted lifetime deadline
survives restart. Reusable **IsBot account/character templates remain stored**,
so database account counts need not decrease after cleanup.

These are simulations, not 100 network connections. They exercise entities/AI,
persistence, area-of-interest updates and observer rendering/network traffic.
They do not reproduce 100 clients' socket, encryption or input costs.

For a repeatable isolated test, provide an already spawned GM actor on a
loopback-only developer endpoint:

```sh
python3 tools/local_testing_crowd.py --actor bot0001 \
  --container openmu-localtest-verify --output crowd-results.json
```

It checks 10/25/50/100 actual entities and class distribution, samples real Docker
CPU/memory and cleans up in `finally`. Request elapsed time includes persistence;
it is not game-loop timing. The helper's maintenance time covers this feature
only; CPU is normalized to available processors. Native frame time derives from
measured client FPS. The server script reports no invented FPS. Never expose
the unauthenticated developer actor endpoint publicly.

## Reset and market

Activate the existing **Reset Feature** and retain its level, Zen, item tiers,
stat rules and limits. **Destination** supports class home, Lorencia, or staying
on the current map. Configurations without this choice retain legacy MoveHome
behavior. `/reset stay` and `/reset lorencia` request live travel without logout;
unqualified `/reset` retains configured logout behavior. No requirement bypass
is added. Death, event states, invalid tiles, trade/open shops and simultaneous
reset requests are rejected. Successful live resets save progress before the
completion acknowledgement.

Activate **Enter Loren Market** if disabled. `/market` travels between Lorencia
and Loren Market through configured hosted gates. Other maps, death, events,
trade and open personal shops block travel. Existing personal shops and their
purchase validation are reused. This does **not** add global listings or auctions.

## Native helper and chat

Open the compatible native helper with **F12**, or type **`$testing`** in chat
if the OS reserves that function key. Esc closes it and releases focus. Six
tabs cover reset, rates/caps, spots, crowds, market and diagnostics, using shared
UI scale and both themes. Refresh requests a snapshot; diagnostics do not replace
unsaved edits. Unpatched servers time out instead of showing invented results.

```text
/localtest status
/localtest enabled 1
/localtest caps 800 65535
/localtest rates 10000 1000
/localtest spots 150 3 3 12
/localtest preview all
/localtest apply all
/localtest cleanup all
/localtest crowd 25 0 135 125 8 idle 300 100
/localtest remove TestName
/localtest remove all
/localtest stop
/reset stay
/reset lorencia
/market
```

Replace `all` with a selected map number. To roll back, disable first and verify
zero owned entities and restored values, then restart on the original pinned
image. Keep current database/volumes; do not overwrite newer player progress
with an old database restore.
