#pragma once

#include <SDL3/SDL_events.h>

namespace Rml
{
class Context;
}

namespace Render::RmlUi::Input
{
int KeyModifiers(SDL_Keymod modifiers);
bool ProcessMouseButton(Rml::Context& context, const SDL_Event& event);
void CancelMousePress(Rml::Context& context);
bool ProcessKey(Rml::Context& context, const SDL_Event& event);
} // namespace Render::RmlUi::Input
