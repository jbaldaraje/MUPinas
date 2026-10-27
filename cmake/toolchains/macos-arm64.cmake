# Toolchain for native macOS arm64 builds
#
# This is a NATIVE toolchain (macOS host -> macOS target).
# Native macOS arm64 build using Homebrew LLVM (story 7-6-1).
# Full compilation of game client targets succeeds.
#
# Note: Setting CMAKE_SYSTEM_NAME causes CMAKE_CROSSCOMPILING=TRUE even for
# native builds. This is acceptable — the preset condition restricts to Darwin
# hosts, and no CMake logic in this project checks CMAKE_CROSSCOMPILING.

set(CMAKE_SYSTEM_NAME Darwin)
set(CMAKE_SYSTEM_PROCESSOR arm64)
set(CMAKE_OSX_ARCHITECTURES arm64)

# Target macOS 12 Monterey as minimum deployment target
set(CMAKE_OSX_DEPLOYMENT_TARGET "12.0")

# Prefer Homebrew LLVM unless the caller supplied a compiler explicitly.
if(EXISTS "/opt/homebrew/opt/llvm/bin/clang")
    set(_mu_default_c_compiler "/opt/homebrew/opt/llvm/bin/clang")
    set(_mu_default_cxx_compiler "/opt/homebrew/opt/llvm/bin/clang++")
else()
    set(_mu_default_c_compiler clang)
    set(_mu_default_cxx_compiler clang++)
endif()
if(NOT DEFINED CMAKE_C_COMPILER)
    set(CMAKE_C_COMPILER "${_mu_default_c_compiler}")
endif()
if(NOT DEFINED CMAKE_CXX_COMPILER)
    set(CMAKE_CXX_COMPILER "${_mu_default_cxx_compiler}")
endif()

# Detect macOS SDK path at configure time
execute_process(
    COMMAND xcrun --sdk macosx --show-sdk-path
    OUTPUT_VARIABLE CMAKE_OSX_SYSROOT
    OUTPUT_STRIP_TRAILING_WHITESPACE
    RESULT_VARIABLE _xcrun_result
)
if(NOT _xcrun_result EQUAL 0)
    message(WARNING "xcrun failed (exit ${_xcrun_result}). Install Xcode Command Line Tools: xcode-select --install")
endif()

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
