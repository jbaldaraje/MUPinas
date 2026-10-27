#pragma once

#include <SDL3/SDL_video.h>

namespace Core::Platform
{

[[nodiscard]] constexpr SDL_WindowFlags BuildSDLWindowFlags(bool fullscreen, bool resizable)
{
#if defined(__APPLE__)
    // The current 2D and input paths use SDL window points. Until both are converted
    // consistently to drawable pixels, a Retina swapchain would misalign login UI.
    SDL_WindowFlags flags = 0;
#else
    SDL_WindowFlags flags = SDL_WINDOW_HIGH_PIXEL_DENSITY;
#endif
    if (fullscreen)
    {
        flags |= SDL_WINDOW_FULLSCREEN;
    }
    if (resizable)
    {
        flags |= SDL_WINDOW_RESIZABLE;
    }
    return flags;
}

[[nodiscard]] constexpr bool HasSDLWindowInputFocus(SDL_WindowFlags flags)
{
    return (flags & SDL_WINDOW_INPUT_FOCUS) != 0;
}

} // namespace Core::Platform
