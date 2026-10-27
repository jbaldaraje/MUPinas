# macOS - Terminal

Native Apple Silicon build using SDL GPU with Metal and the `osx-arm64`
.NET Native AOT networking library.

## Prerequisites

- Apple Silicon Mac with Xcode Command Line Tools selected
- .NET 10 SDK
- Homebrew packages:

```bash
brew install cmake ninja pkg-config cryptopp python@3.13 expat
```

The local build script uses Xcode Clang explicitly. Homebrew LLVM 23 emitted
unresolved Objective-C class-message symbols when linking against the macOS 26
SDK on this machine.

## Configure and build

From the repository root:

```bash
./scripts/build-macos.sh
```

On macOS, the client currently uses a 1x drawable so its window-point-based
2D rendering and mouse coordinates stay aligned on Retina displays. This is a
workaround for upstream issue [#869](https://github.com/sven-n/MuMain/issues/869);
macOS scales the window to the display. Re-enable high-density drawables only
after converting the 2D rendering and input paths together.

The script configures the `macos-arm64` CMake preset and builds
`macos-arm64-release`. The Python wrapper loads Homebrew Expat for Python's XML
packet-size generator; this avoids the system Expat symbol mismatch seen with
Homebrew Python on this machine.

The output is a runnable directory:

```text
out/build/macos-arm64/src/Release/Main.app/Contents/MacOS/
```

It contains `Main`, `MUnique.Client.Library.dylib`, `Data/`, `fonts/`,
`config.ini`, and compiled shaders. Run with:

```bash
./scripts/run-macos.sh
```

The script changes to the runtime directory before launching so relative asset
and library loading works.

The default is Release. For a Debug build, use the explicit pair:

```sh
./scripts/build-macos.sh --debug
./scripts/run-macos.sh --debug
```

To clean the selected configuration before rebuilding and launch Debug:

```sh
./scripts/build-macos.sh --debug --clean && ./scripts/run-macos.sh --debug
```

Both app bundles are named `Main.app`; macOS may resolve the name "MU Online"
to either one. The run script prints the exact configuration and executable
path so an older build is not mistaken for the updated client. Rebuild the
configuration you actually launch after source or dependency fixes. A process
already running continues to use its old code until it exits and is restarted.

The 2026-10-27 recording of grainy Enhanced rendering showed the old Debug app
switching to 4x MSAA, while recent fixes had been built only in Release. The SDL
Metal upload-stride patch is also needed for the small mip levels Enhanced
generates, even when full-size textures look correct in Classic.

## Mouse and text input

Buttons and text fields use the position of each press and release, including
trackpad taps without an intervening mouse movement. A release outside the
pressed control does not click that control. Native pointer tracking is updated
even when the interface consumes the click. Releasing a game press over a UI
control cancels the game's held click. Switching away from the app while a
button is held cancels the pending UI press, so the next tap works after returning.

Text fields support macOS Command shortcuts, including Cmd+A to select all and
Cmd+C, Cmd+X and Cmd+V for clipboard editing. The modifier keys recorded with
each key event are used even if the key has already been released by the time
the event is processed. Restart an already-running client to load input fixes.

## Local server configuration

The generated `config.ini` uses `ServerIP=localhost` and
`ServerPort=44406`. This workspace's existing OpenMU container currently has
listeners on ports `44405`, `44406`, `45901`-`45904`, and `45980`; the server
configuration and the role of each listener were not changed. Confirm the
connect-server endpoint and advertised game-server addresses before changing
the client endpoint. At the configured `localhost:44406`, the client connected
and received a server list. Login and game-server entry have not been verified.
The existing Docker image is unpinned, so its exact OpenMU source version and
protocol configuration remain unknown. Do not put account credentials in this
file or in logs.

MuMain packages its own `Data/` and `fonts/` from the exact client revision.
After each build, the build script overlays the complete existing
`MU_S6E3/Data` package into the generated runtime `Data/` directory. Files
from the local package take precedence when paths overlap; MuMain-only files
(including its RmlUi theme and data definitions) remain in place. The
workspace `MU_S6E3/` and `data/` directories are preserved.

The current data has four remaining model gaps at startup:
`Data/Skill/ManyFlag.bmd`, `Data/Effect/shockwave03.bmd`,
`Data/Effect/wind_spin01.bmd`, and `Data/Monster/Monster50.bmd`. These files
are absent from both MuMain's packaged data and the local S6E3 package; the
client still reaches the login scene, but the affected skill/effect/monster
models are unavailable.

## Launcher and patch status

The supplied Windows launcher 0.9.6 is not required. Its runtime configuration
requires .NET 9 `Microsoft.WindowsDesktop.App`, and `launcher.config` points to
`main.exe`. It cannot launch the native macOS app. Its placeholder
`127.127.127.127` hosts and game-server ports are not this deployment's settings.
Use the direct launch script, optionally overriding the connection for one run:

```sh
./scripts/run-macos.sh connect /u127.0.0.1 /p44406
```

On 2026-10-27, GitHub's `nolt/MuMain` main branch still pointed to
[`18888d0`](https://github.com/nolt/MuMain/commit/18888d0193ed6526d6423314a208f7c994028985),
the revision imported here. That confirms the imported revision is current at the
time of checking; it does not mean Season 6 is feature complete or that all local
assets and server behavior are compatible. Lucky Items remain listed as upstream
work, four model files are absent, and account login/gameplay need verification.
Installing the launcher does not resolve those gaps.

The workspace now exposes only macOS configure/build presets and macOS client CI.
Obsolete MonoGame build folders, the duplicate `docker-compse.yml`, upstream-only
publishing/bot workflows, and the Windows release-contract check were removed.
The active `ClientLibrary/`, shared engine/bootstrap, optional editor sources,
local assets, and Docker deployment are retained.

## Revision and local changes

- Enhanced lighting from open [PR #808](https://github.com/sven-n/MuMain/pull/808),
  pinned at `dbdb1f2de5123f6f9f045ef1fa5fc2d72dd515fd`, is applied on top of
  the imported main revision. This includes its compiled Metal shaders and
  uniform-buffer binding correction. Its prerequisite PR #807 was already
  present in the imported source.
- macOS configuration automatically patches SDL's Metal texture-upload stride
  handling to fix scrambled UI sprites; see the [GPU verification notes](../rendering-parity.md#bitmap-upload-staging-and-upscaled-textures).
- Upstream: `nolt/MuMain`, branch `main`, commit
  `18888d0193ed6526d6423314a208f7c994028985`.
- Submodules: SDL `d9d5536704d585616d4db3c8ba3c4ff6fc2757e1`,
  RmlUi `afc25fecb70d1eff7753b15fc801835a4c0e155f`, and ImGui
  `b1bcb12a624af7509894c8e77dd47416997777fa`.
- Local source fix: include `<charconv>` directly where `std::from_chars` is
  used.
- Local toolchain fix: honor explicit C/C++ compiler paths while retaining
  Homebrew LLVM as the default when no compiler is supplied.
- The original deployment, database, server configuration, and local game
  assets remain outside the replacement.

## Tests and current limits

The focused SDL window-flags test passes:

```bash
./out/build/macos-arm64/tests/platform/Release/sdl_window_flags_tests
```

Run the upstream test suite with:

```bash
ctest --test-dir out/build/macos-arm64 -C Release --output-on-failure --parallel 1
```

The macOS Release build succeeds with PR #808 applied. The full serial CTest
run discovered 1,289 checks: 1,288 passed and the opt-in GPU integration check
was skipped. That GPU upload/readback regression was run separately on Metal
and passed with the SDL stride fix, including 4,800 uploads across two device
lifetimes, small mip levels and banded textures.

The patched app was launched with Enhanced enabled, `Antialiasing=0`, and
EnhancedLighting, DynamicLights, LightMood and PostProcessing set to `1` in the
runtime config. It loaded the Metal shaders, created 252 HDR scene pipelines,
received the server list and produced a frame with clean server-selection UI.
The capture is at `out/verification/pr808-enhanced-metal.png`. AO is enabled by
the shipped look profile and requires single-sample rendering in this PR.
Login and gameplay lighting still need verification with a test account; no
account login was performed during this check.

Run the suite serially: Rml themed-view and texture-pack fixtures share temporary
directories, and parallel runs can interfere. Managed network tests need local
socket access allowed by the execution environment.

The local Homebrew libraries were built for macOS 26, and the linker warns about
the configured macOS 12 deployment target. Running this local build on older
macOS versions has not been verified. Live login and gameplay still require a
test account and server protocol compatibility confirmation.

Browser support remains future work: the native SDL GPU client and its .NET
Native AOT library are not browser targets. No browser code or server changes
were included in this replacement.

## Rollback

The pre-replacement snapshot is at
`/Users/exodus/muonline-backups/mumain-pre-replacement-20261027/`. It contains a
Git bundle, the pre-existing tracked-worktree patch, and an archive of the
untracked browser/deployment files. To reconstruct the previous tracked tree
and those untracked files in a separate directory without overwriting this
workspace:

```bash
git clone /Users/exodus/muonline-backups/mumain-pre-replacement-20261027/workspace-history.bundle \
  /tmp/muonline-pre-mumain
cd /tmp/muonline-pre-mumain
git apply /Users/exodus/muonline-backups/mumain-pre-replacement-20261027/worktree-changes.patch
tar -xzf /Users/exodus/muonline-backups/mumain-pre-replacement-20261027/untracked-preserved-files.tar.gz \
  -C /tmp/muonline-pre-mumain
```

The local `MU_S6E3/`, lowercase `data/`, Docker configuration, and database
volumes remain in the original workspace and were not part of the replacement.
