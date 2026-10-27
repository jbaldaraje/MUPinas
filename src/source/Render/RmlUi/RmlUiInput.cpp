#include "RmlUiInput.h"

#include <RmlUi/Core/Context.h>
#include <RmlUi/Core/Input.h>
#include <RmlUi_Platform_SDL.h>

namespace Render::RmlUi::Input
{
int KeyModifiers(SDL_Keymod modifiers)
{
    int result = 0;
    if (modifiers & SDL_KMOD_CTRL)
        result |= Rml::Input::KM_CTRL;
    if (modifiers & SDL_KMOD_SHIFT)
        result |= Rml::Input::KM_SHIFT;
    if (modifiers & SDL_KMOD_ALT)
        result |= Rml::Input::KM_ALT;
    if (modifiers & SDL_KMOD_NUM)
        result |= Rml::Input::KM_NUMLOCK;
    if (modifiers & SDL_KMOD_CAPS)
        result |= Rml::Input::KM_CAPSLOCK;
#if defined(__APPLE__)
    // RmlUi text editing uses Control for shortcuts, including select/copy/paste.
    if (modifiers & SDL_KMOD_GUI)
        result |= Rml::Input::KM_CTRL;
#endif
    return result;
}

bool ProcessMouseButton(Rml::Context& context, const SDL_Event& event)
{
    const int modifiers = KeyModifiers(SDL_GetModState());
    // A tap or a queued press need not have a preceding motion event. The release
    // position also decides whether RmlUi should click the pressed control.
    context.ProcessMouseMove(static_cast<int>(event.button.x), static_cast<int>(event.button.y), modifiers);
    const int button = RmlSDL::ConvertMouseButton(event.button.button);
    if (event.type == SDL_EVENT_MOUSE_BUTTON_DOWN)
        return context.ProcessMouseButtonDown(button, modifiers);
    return context.ProcessMouseButtonUp(button, modifiers);
}

bool ProcessKey(Rml::Context& context, const SDL_Event& event)
{
    const int modifiers = KeyModifiers(event.key.mod);
    const auto key = RmlSDL::ConvertKey(event.key.key);
    if (event.type == SDL_EVENT_KEY_UP)
        return context.ProcessKeyUp(key, modifiers);
    bool propagates = context.ProcessKeyDown(key, modifiers);
    if (event.key.key == SDLK_RETURN || event.key.key == SDLK_KP_ENTER)
        propagates = context.ProcessTextInput('\n') && propagates;
    return propagates;
}

void CancelMousePress(Rml::Context& context)
{
    // Clear hover first so releasing the active element cannot dispatch a click.
    context.ProcessMouseLeave();
    context.ProcessMouseButtonUp(0, KeyModifiers(SDL_GetModState()));
    // Finishing a drag can refresh hover internally while handling the release.
    context.ProcessMouseLeave();
}
} // namespace Render::RmlUi::Input
