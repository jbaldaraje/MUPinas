#include "doctest.h"

#include "Core/Input/KeyState.h"
#include "Core/Input/UiInputRouter.h"
#include "Core/Platform/WinCompat.h"
#include <SDL3/SDL.h>

extern bool MouseLButton;
extern bool MouseLButtonPush;
extern bool MouseLButtonPop;
extern bool MouseLButtonDBClick;
extern bool MouseRButton;
extern bool MouseRButtonPush;
extern bool MouseRButtonPop;
extern bool MouseMButton;
extern bool MouseMButtonPush;
extern bool MouseMButtonPop;

// The physical SDL button adapter used by the native event loop.
bool RouteMouseButton(SDL_Event& event, bool synthetic);

namespace
{
class MouseInputFixture : public Core::Input::IUiInputConsumer
{
public:
    MouseInputFixture() : m_previous(Core::Input::ActiveUiInputConsumer())
    {
        ResetButtons();
        Core::Input::SetUiInputConsumer(this);
    }
    ~MouseInputFixture() override
    {
        Core::Input::SetUiInputConsumer(m_previous);
        ResetButtons();
    }
    bool ProcessSdlEvent(SDL_Event&, SDL_Window*) override { return !consume; }
    void CancelSyntheticMousePress(unsigned char, SDL_Window*) override {}
    bool IsMouseOverUI() const override { return consume; }

    bool consume = false;

private:
    static void ResetButtons()
    {
        MouseLButton = MouseLButtonPush = MouseLButtonPop = false;
        MouseLButtonDBClick = false;
        MouseRButton = MouseRButtonPush = MouseRButtonPop = false;
        MouseMButton = MouseMButtonPush = MouseMButtonPop = false;
        Core::Input::ClearLeftMouseButtonPressEdge();
    }
    Core::Input::IUiInputConsumer* m_previous;
};

SDL_Event Button(SDL_EventType type)
{
    SDL_Event event{};
    event.type = type;
    event.button.button = SDL_BUTTON_LEFT;
    event.button.x = 100.f;
    event.button.y = 100.f;
    return event;
}
} // namespace

TEST_CASE("A physical release consumed by UI cancels the game's held click [core][input][mouse-release]")
{
    MouseInputFixture ui;
    auto button = Button(SDL_EVENT_MOUSE_BUTTON_DOWN);
    button.button.clicks = 2;
    CHECK(RouteMouseButton(button, false));
    CHECK(MouseLButton);
    CHECK(MouseLButtonPush);
    CHECK(MouseLButtonDBClick);

    ui.consume = true;
    button.type = SDL_EVENT_MOUSE_BUTTON_UP;
    CHECK_FALSE(RouteMouseButton(button, false));
    CHECK_FALSE(MouseLButton);
    CHECK_FALSE(MouseLButtonPush);
    CHECK_FALSE(MouseLButtonPop);
    CHECK_FALSE(MouseLButtonDBClick);
    CHECK_FALSE(Core::Input::IsKeyDown(VK_LBUTTON));
}

TEST_CASE("A physical game click retains its press and release edges [core][input][mouse-release]")
{
    MouseInputFixture ui;
    auto button = Button(SDL_EVENT_MOUSE_BUTTON_DOWN);
    CHECK(RouteMouseButton(button, false));
    button.type = SDL_EVENT_MOUSE_BUTTON_UP;
    CHECK(RouteMouseButton(button, false));
    CHECK_FALSE(MouseLButton);
    CHECK(MouseLButtonPush);
    CHECK(MouseLButtonPop);
    CHECK(Core::Input::IsKeyDown(VK_LBUTTON));
}

TEST_CASE("A UI click does not create a game click [core][input][mouse-release]")
{
    MouseInputFixture ui;
    ui.consume = true;
    auto button = Button(SDL_EVENT_MOUSE_BUTTON_DOWN);
    CHECK_FALSE(RouteMouseButton(button, false));
    button.type = SDL_EVENT_MOUSE_BUTTON_UP;
    CHECK_FALSE(RouteMouseButton(button, false));
    CHECK_FALSE(MouseLButton);
    CHECK_FALSE(MouseLButtonPush);
    CHECK_FALSE(MouseLButtonPop);
    CHECK_FALSE(Core::Input::IsKeyDown(VK_LBUTTON));
}

TEST_CASE("UI releases clear held right and middle buttons [core][input][mouse-release]")
{
    const struct ButtonState
    {
        Uint8 button;
        bool* held;
        bool* pressed;
        bool* released;
    } buttons[] = {{SDL_BUTTON_RIGHT, &MouseRButton, &MouseRButtonPush, &MouseRButtonPop},
                   {SDL_BUTTON_MIDDLE, &MouseMButton, &MouseMButtonPush, &MouseMButtonPop}};
    for (const auto& state : buttons)
    {
        CAPTURE(state.button);
        MouseInputFixture ui;
        auto button = Button(SDL_EVENT_MOUSE_BUTTON_DOWN);
        button.button.button = state.button;
        CHECK(RouteMouseButton(button, false));
        CHECK(*state.held);
        CHECK(*state.pressed);
        ui.consume = true;
        button.type = SDL_EVENT_MOUSE_BUTTON_UP;
        CHECK_FALSE(RouteMouseButton(button, false));
        CHECK_FALSE(*state.held);
        CHECK_FALSE(*state.pressed);
        CHECK_FALSE(*state.released);
    }
}
