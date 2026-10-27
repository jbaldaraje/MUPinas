#include <doctest.h>

#include "RmlLayoutFixture.h"
#include "Render/RmlUi/RmlUiInput.h"

#include <RmlUi/Core/Elements/ElementFormControlInput.h>
#include <RmlUi/Core/EventListener.h>

namespace
{
struct ClickCounter : Rml::EventListener
{
    int count = 0;
    void ProcessEvent(Rml::Event&) override { ++count; }
};

constexpr char kMarkup[] = R"(
<rml><head><style>
body { width: 640px; height: 480px; }
input { position: absolute; left: 100px; top: 100px; width: 200px; height: 40px; }
</style></head><body><input id="field" type="text"/></body></rml>)";
constexpr int kFieldX = 150;
constexpr int kFieldY = 120;
constexpr int kOutsideX = 500;
constexpr int kOutsideY = 400;

SDL_Event Button(SDL_EventType type, int x, int y)
{
    SDL_Event event{};
    event.type = type;
    event.button.button = SDL_BUTTON_LEFT;
    event.button.x = static_cast<float>(x);
    event.button.y = static_cast<float>(y);
    return event;
}
} // namespace

TEST_CASE("a click focuses and types at its own coordinates without a motion event [ui][input]")
{
    UI::Tests::RmlLayoutFixture fixture;
    auto* document = fixture.LoadMarkup(kMarkup);
    auto* field = document->GetElementById("field");
    fixture.context->ProcessMouseMove(kOutsideX, kOutsideY, 0);
    Render::RmlUi::Input::ProcessMouseButton(*fixture.context, Button(SDL_EVENT_MOUSE_BUTTON_DOWN, kFieldX, kFieldY));
    Render::RmlUi::Input::ProcessMouseButton(*fixture.context, Button(SDL_EVENT_MOUSE_BUTTON_UP, kFieldX, kFieldY));
    REQUIRE(fixture.context->GetFocusElement() == field);
    fixture.context->ProcessTextInput("input-check");
    CHECK(static_cast<Rml::ElementFormControlInput*>(field)->GetValue() == "input-check");
}

TEST_CASE("a release outside the control updates the hover target [ui][input]")
{
    UI::Tests::RmlLayoutFixture fixture;
    fixture.LoadMarkup(kMarkup);
    fixture.context->ProcessMouseMove(kFieldX, kFieldY, 0);
    Render::RmlUi::Input::ProcessMouseButton(*fixture.context, Button(SDL_EVENT_MOUSE_BUTTON_DOWN, kFieldX, kFieldY));
    Render::RmlUi::Input::ProcessMouseButton(*fixture.context, Button(SDL_EVENT_MOUSE_BUTTON_UP, kOutsideX, kOutsideY));
    CHECK(fixture.context->GetHoverElement() == fixture.context->GetElementAtPoint({kOutsideX, kOutsideY}));
}

TEST_CASE("text selection uses the modifiers attached to the key event [ui][input]")
{
    UI::Tests::RmlLayoutFixture fixture;
    auto* document = fixture.LoadMarkup(kMarkup);
    auto* field = static_cast<Rml::ElementFormControlInput*>(document->GetElementById("field"));
    field->Focus();
    fixture.context->ProcessTextInput("original");
    SDL_Event key{};
    key.type = SDL_EVENT_KEY_DOWN;
    key.key.key = SDLK_A;
#if defined(__APPLE__)
    key.key.mod = SDL_KMOD_GUI;
#else
    key.key.mod = SDL_KMOD_CTRL;
#endif
    Render::RmlUi::Input::ProcessKey(*fixture.context, key);
    fixture.context->ProcessTextInput("replacement");
    CHECK(field->GetValue() == "replacement");
}

TEST_CASE("losing window focus cancels a UI press and allows the next click [ui][input][focus]")
{
    ClickCounter clicks;
    UI::Tests::RmlLayoutFixture fixture;
    auto* document = fixture.LoadMarkup(kMarkup);
    auto* field = document->GetElementById("field");
    field->AddEventListener("click", &clicks);
    Render::RmlUi::Input::ProcessMouseButton(*fixture.context, Button(SDL_EVENT_MOUSE_BUTTON_DOWN, kFieldX, kFieldY));
    REQUIRE(field->IsPseudoClassSet("active"));

    Render::RmlUi::Input::CancelMousePress(*fixture.context);
    CHECK_FALSE(field->IsPseudoClassSet("active"));
    CHECK_FALSE(fixture.context->IsMouseInteracting());
    CHECK(clicks.count == 0);
    // The OS may never deliver the old release after an app switch. A new tap
    // must work without needing that missing event first.
    Render::RmlUi::Input::ProcessMouseButton(*fixture.context, Button(SDL_EVENT_MOUSE_BUTTON_DOWN, kFieldX, kFieldY));
    Render::RmlUi::Input::ProcessMouseButton(*fixture.context, Button(SDL_EVENT_MOUSE_BUTTON_UP, kFieldX, kFieldY));
    CHECK_FALSE(field->IsPseudoClassSet("active"));
    CHECK(fixture.context->GetFocusElement() == field);
    CHECK(clicks.count == 1);
    fixture.context->ProcessTextInput("after-focus-change");
    CHECK(static_cast<Rml::ElementFormControlInput*>(field)->GetValue() == "after-focus-change");
}
