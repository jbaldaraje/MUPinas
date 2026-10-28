# macOS local testing setup

Implemented and verified on 28 October 2026. The native client opens a six-tab
testing helper with **F12** (or **`$testing`** in chat). **Esc** closes it.
The existing HUD and inventory are retained.

## Use it

- **Reset:** choose stay in place or Lorencia. Existing level, Zen, item, stat,
  and reset-limit rules still apply. These explicit destinations do not log out.
- **Rates:** view saved and effective values; a GameMaster can save normal XP,
  ordinary drop multiplier, attack/magic speed cap, and stat cap.
- **Spots:** select one/all eligible maps, preview, save population settings,
  apply, or clean up owned monsters. Natural spawns are retained.
- **Crowds:** request 10/25/50/100 or a bounded custom count, map/tile/radius,
  idle/wander/combat behavior, lifetime and level. Remove a named test bot or all.
- **Market:** travel between Lorencia and Loren Market, or open personal shop
  and inventory through the existing shop interface.
- **Diagnostics:** see connection, population and measured performance snapshots.
  The fixed footer provides emergency stop even while another request is pending.

Testing mutations require **GameMaster** character status on the server.
Normal players retain reset/market access under their normal rules and may read
status. No reset requirement bypass was introduced.

The live preset currently uses **normal XP 10000, ordinary drop multiplier 1000,
attack and magic speed cap 800, stat cap 65535**. Change caps at any time in
**F12 → Rates → Save caps**, or as a GM:

```text
/localtest caps 800 65535
```

These are effective limits, not grants of stats or speed. Lowering them preserves
the higher raw values in storage; raising the cap reveals those values again.
The limit also prevents allocating more points above the cap. Combat animation
behavior at unusual speeds should be checked for each class/equipment combination.

## Admin panel and persistence

The live panel is [http://127.0.0.1:52407/](http://127.0.0.1:52407/).
The verified local administrator credentials are stored only in
`out/staging/openmu-local-testing/admin-login.private.txt` (private file;
do not commit it). Browser sign-in was confirmed in the server authentication log.

Open **Game configuration → Plugins**, search **macOS local testing preset**,
activate it, and open its gear button. Edit **MaximumAttackSpeed** and
**MaximumStat** there for the same dynamic caps. Keep the plugin active and use
**Enabled** to apply/restore the preset. Its original-value snapshot and settings
persist in the existing plugin configuration. New assemblies need a process
restart; these settings are applied while running.

The existing **Reset Feature** exposes **Destination** (class home, Lorencia,
stay), retaining legacy MoveHome/LogOut semantics when no explicit choice exists.
The **Enter Loren Market** chat plugin provides `/market`.

All plugin fields, command examples, probability rules, ownership, exclusions and
cleanup behavior are documented in
[the server feature guide](../server/OpenMU-installed/docs-website/docs/server-features/local-testing.md).

## What was inspected and retained

The running server image was matched against OpenMU source commit
`a1d63fef0774597239c2e6d627d35a3f0aa38483`: **1157 handwritten source checksums
matched** its portable PDBs. The newer checkout at `server/OpenMU` had unrelated
concurrent edits and was preserved. The compatible implementation is in
`server/OpenMU-installed`, branch `codex/local-testing-installed`.
The main MUPinas checkout links that exact server commit as a submodule; clone
with `--recurse-submodules` or run `git submodule update --init` to obtain it.
The server source history is on the `codex/openmu-server` branch of MUPinas.

Installed OpenMU already supplied reset rules, plugin configuration and admin
forms, experience/drop pipelines, bot generation, map warps and personal shops.
The changes add the reversible testing feature, validated reset destinations,
market chat access and native helper; they do not upgrade the whole server.
The comparison/evidence is in `out/inspection/openmu/`, including
`source-version-verification.json` and `upstream-since-installed.txt`.

The client is the CMake/SDL GPU native MuMain source under `src/`, based on the
nolt MuMain tree. The .NET network bridge and extended protocol remain in use.
Client fixes also distinguish connect-server encryption explicitly and handle
same-map live character-stat packets without recreating the hero or losing UI/input.

Deployment retains PostgreSQL 18, database `openmu`, volumes `muonline_dbdata`
and `muonline_adminpanel-keys`, game ports 44405/44406, 45901–45906 and 45980,
and the original startup/configuration behavior. Only `openmu-startup` was
recreated. The developer actor endpoint remains disabled on the live server.
Logs persist in `out/server-logs`. No player database was reinitialized or restored.

## Build and deploy

From `/Users/exodus/muonline`:

```sh
cmake --preset macos-arm64
cmake --build --preset macos-arm64-release --target Main
sh scripts/run-macos.sh
```

The ready native executable is
`out/build/macos-arm64/src/Release/Main.app/Contents/MacOS/Main`.
The isolated QA app under `out/testing-helper-runtime` uses different test ports;
use the normal run script for your live setup.

Build the compatible server image, then perform a planned game-server restart:

```sh
sh server/OpenMU-installed/docker/local-testing/build.sh
sh scripts/run-local-testing-server.sh
```

The build pins the original image digest and replaces only GameLogic and
AttributeSystem DLL/PDB files. The wrapper uses the original Compose file plus
`docker-compose.local-testing.yml` and the ignored private environment file
`out/staging/openmu-local-testing/live.private.env`. It preserves the database
and keys volumes, mounts backed-up configuration read-only, and does not restart
PostgreSQL. Do not replace that private file with example/test credentials.

## Backups and rollback

Backups are in `out/backups/admin-testing-20261028/` and
`out/backups/admin-testing-cutover-20261028/`. They include custom-format database
dumps, configuration, container inspection, data-protection keys and old logs.
The first dump was restored to the separate verification database; subsequent
dump contents were validated with `pg_restore -l`. These directories are private.

For normal rollback, first use **Emergency stop**, **Enabled = false**, or:

```text
/localtest stop
```

Verify owned populations are zero and the captured values are restored. In this
installation that baseline is normal XP 9999, master XP 999, speed cap 200 and
uncapped base stats. Restoration does not roll back unrelated settings or player
progress. Then switch the image while keeping the same configuration/volumes:

```sh
OPENMU_TESTING_IMAGE=mupinas/openmu:before-local-testing-20261028 \
  sh scripts/run-local-testing-server.sh
```

The original image is also pinned by
`munique/openmu@sha256:79c280b271091555b801e08aa4428d254e2405536e7d7b420c48acbb5ca94a97`.
Do not run `docker compose down -v`, initialize a new database, or restore an old
dump over current player progress for an ordinary feature rollback.

## Verification and practical limits

- **Preservation:** exact before/after row hashes matched across deployment for
  20 real accounts, 76 characters, 4658 items and their stored stats. The final
  stat fix was backed up and checked again through a coordinated restart.
- **Automated server:** 28 focused reset/permission/rate/spawn/market cases;
  all 46 AttributeSystem tests passed. Release build: zero warnings/errors.
- **Native:** helper layout/input tests passed 138 assertions across both themes
  and 75/100/150% scales; parser tests passed 12 assertions; the live-stat packet
  regression passed 15 assertions. The Release game was built and exercised.
- **Reset persistence:** separate synthetic characters exercised stay and
  Lorencia, paid their configured costs, saved, and reconnected. The native
  client reset stayed at Loren Market tile 126,142, changed level 400 to 10 and
  persisted reset count 1 without logout.
- **Stat safety:** a synthetic character retained raw Strength 128226,
  Vitality 120020 and Energy 65918 after cap changes, save and reconnect;
  effective health changed with caps 32000/65535.
- **Spots:** all 17 eligible maps were activated in stages on live game server 0.
  Fifteen fit 150 each; Icarus fits 105 and Kanturu III 63 at the safe default
  spacing: **2418 owned monsters**. Shortfalls are reported. Repeat application
  and cleanup were checked on the isolated database.
- **Crowds:** actual 10/25/50/100 simulated entities and seven class families
  were verified, including expiry and cleanup. Live crowd target is zero.
  These are server-side simulations, not 100 connected clients. Reusable IsBot
  account templates remain in the verification database after active cleanup.
- **Measured samples:** the isolated idle crowd test sampled Docker CPU
  2.08/1.03/1.06/1.32% and memory 1.259/1.332/1.456/1.503 GiB for 10/25/50/100.
  Native diagnostics while rendering 100 showed a 30.29 ms frame-time snapshot.
  These are snapshots, not averages or a hardware comparison; feature maintenance
  time is not the full game-loop time. Full measurements are in
  `out/inspection/openmu/crowd-benchmark.json` and the native diagnostics image.
- **Market:** two synthetic characters completed entry/return; native travel
  and the personal-shop/inventory shortcut were verified. A completed two-client
  purchase transaction was not tested. There is no new auction/global listing system.
- The inherited base image prints a nonfatal missing Kerberos shared-library
  diagnostic at startup; game/database listeners still start successfully.

![Native helper with editable caps](../out/inspection/openmu/native-helper-verified-small.png)

![Native reset without logout](../out/inspection/openmu/native-reset-stay-verified-small.png)

![Native crowd diagnostics](../out/inspection/openmu/native-diagnostics-crowd100-small.png)

The original checkout's unrelated changes remain untouched. Source change lists
and local commit identifiers are recorded in `out/inspection/openmu/change-manifest.json`.
