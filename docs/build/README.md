# Build Guide

This workspace currently supports **macOS arm64 (Apple Silicon)** with SDL GPU/Metal,
editor and control socket disabled. Browser support is planned; no browser preset
or playable browser build exists yet.

Follow [macOS terminal setup](macos/console.md), then run:

```sh
./scripts/build-macos.sh
./scripts/run-macos.sh
```

Windows/Linux presets and CI build jobs have been removed. Their existing guides
and shared platform implementation are retained as upstream reference material;
they do not describe supported builds of this workspace. In particular, the shared
bootstrap still lives in `App/Platform/Windows/Winmain.cpp` and is used by macOS.

CI validates macOS Release with tests and uploads a no-data runtime archive on
main-branch pushes. There is no automatic release publishing or updater in this
workspace. Local builds include assets; CI artifacts require compatible `Data/`
and `fonts/` directories staged into `Main.app/Contents/MacOS/` before running.

## Common concepts

The supported client target in this workspace is macOS arm64.

### Dependencies and first configuration

Use CMake 3.25 or newer, the .NET 10 SDK, and the compiler and Homebrew packages listed
in the [macOS guide](macos/console.md).
Initial configuration needs network access unless dependency sources are already cached.

CMake initializes missing Git submodules under `src/ThirdParty/`: SDL3 and RmlUi for
all builds, and ImGui when `ENABLE_EDITOR=ON`. RmlUi uses the
[nitoygo/RmlUi fork](https://github.com/nitoygo/RmlUi), with the revision pinned by
the repository's submodule entry, for the SDL GPU renderer integration.

If automatic initialization fails, run from the repository root:

```sh
git submodule update --init -- src/ThirdParty/SDL src/ThirdParty/RmlUi

# For editor builds:
git submodule update --init -- src/ThirdParty/imgui
```

CMake downloads miniaudio, GLM, spdlog, SDL3_ttf and SDL3_image through `FetchContent`.
Audio uses miniaudio. SDL3_ttf supplies the vendored FreeType used by RmlUi; neither
SDL_mixer nor a separate FreeType submodule is required.

### Build options

| CMake option | Values | Meaning |
|--------------|--------|---------|
| `CMAKE_BUILD_TYPE` | `Release` / `Debug` / `RelWithDebInfo` | Build configuration for single-config generators. With Ninja Multi-Config, select it through a build preset or `--config` instead. |
| `ENABLE_EDITOR` | `ON` / `OFF` | Builds the in-app ImGui editor (admin tooling). When `OFF`, nothing under `src/MuEditor/` is compiled in - enforced by the `editor_leak` test. |
| `ENABLE_CONTROL_SOCKET` | `ON` / `OFF` | Builds the developer control socket that lets test scripts drive the client (see [`control-socket.md`](../control-socket.md)). Default `OFF`; never for player builds. When `OFF`, nothing under `src/source/App/Control/` and no local-socket transport is compiled in, and the `MU_CONTROL_SOCKET` variable is ignored - enforced by the `control_socket_leak` test. Enable it explicitly for developer builds. |
| `BUILD_TESTING` | `ON` / `OFF` | Builds and registers the unit tests (run with `ctest`). |
| `RMLUI_SDL_GPU_NUM_MSAA_SAMPLES` | `1` / `2` / `4` / `8` | RmlUi UI-layer sample count. Default `1` disables MSAA to avoid reported NVIDIA/Vulkan device loss. Higher values are opt-in for diagnostics; see [rendering parity](rendering-parity.md#rmlui-multisampling). |
| `MU_COPY_RUNTIME_ASSETS` | `ON` / `OFF` | Copies `Data/` and `fonts/` beside the executable. Defaults to `ON` for local runnable builds. |


If configuration reports that SDL_ttf or RmlUi cannot find FreeType after removing
cached dependencies, reconfigure the same build directory with
`-DFETCHCONTENT_FULLY_DISCONNECTED=OFF`. SDL_ttf supplies the vendored FreeType;
disconnected mode requires its sources to already exist. Let configuration finish
successfully before building or running the tests.

### The network library

The client talks to OpenMU through `ClientLibrary/`, a C# .NET 10 project
compiled with Native AOT for `osx-arm64`. The build copies
`MUnique.Client.Library.dylib` beside `Main`. This is required for networking;
it is separate from the optional Windows launcher. Use a native macOS .NET SDK.

### Output layout

A post-build step copies the game assets (from `src/bin/`) and the network
library next to the executable, so the build output directory is runnable:

```text
out/build/macos-arm64/src/Release/Main.app/Contents/MacOS/
  Main
  MUnique.Client.Library.dylib
  Data/
  fonts/
  shaders/
  config.ini
```

Run the client from that directory so it finds its assets and the library.

## Reference

### NuGet cache

The default is `<repo>/.nuget` (ignored by Git). Override with
`-DMU_NUGET_CACHE_DIR=<path>`.

### Tests

The macOS preset enables `BUILD_TESTING`. Run:

```sh
ctest --test-dir out/build/macos-arm64 -C Release --output-on-failure --parallel 1
```

See the [macOS guide](macos/console.md#tests-and-current-limits) for known test
limitations and the [test reference](reference-tests.md) for adding a module.
