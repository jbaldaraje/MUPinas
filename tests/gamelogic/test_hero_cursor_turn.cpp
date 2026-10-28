#include "stdafx.h"
#include "doctest.h"

#include "Engine/Object/EnhancedMovement.h"
#include "Engine/Object/Movement/HeroCursorTurn.h"

namespace
{
class CursorTurnFixture
{
public:
    CursorTurnFixture() : m_previousEnhanced(Engine::Characters::EnhancedMovementEnabled())
    {
        hero.Initialize();
        Engine::Characters::SetEnhancedMovementEnabled(true);
    }

    ~CursorTurnFixture() { Engine::Characters::SetEnhancedMovementEnabled(m_previousEnhanced); }

    bool Update(bool worldClick, float angle, bool mayTurn = true)
    {
        return Engine::Characters::UpdateHeroCursorTurn(&hero, turning, mayTurn, worldClick, angle);
    }

    CHARACTER hero;
    bool turning = false;

private:
    bool m_previousEnhanced;
};
} // namespace

TEST_CASE("hover never starts a standing turn even behind the hero [gamelogic][cursor-turn]")
{
    CursorTurnFixture fixture;
    constexpr int HoverFrames = 1000;
    for (int frame = 0; frame < HoverFrames; ++frame)
        CHECK_FALSE(fixture.Update(false, 180.f));

    CHECK_FALSE(fixture.turning);
    CHECK_FALSE(fixture.hero.TurnPending);
    CHECK(fixture.hero.Object.Angle[2] == 0.f);
}

TEST_CASE("a world click starts a smooth turn without a hover delay [gamelogic][cursor-turn]")
{
    CursorTurnFixture fixture;
    constexpr float ClickedAngle = 45.f;
    CHECK_FALSE(fixture.Update(true, ClickedAngle));
    CHECK(fixture.turning);
    CHECK(fixture.hero.TurnPending);
    CHECK(fixture.hero.MoveHeading == ClickedAngle);
    CHECK(fixture.hero.Object.Angle[2] == 0.f);
}

TEST_CASE("release and hover keep the clicked heading until the turn finishes [gamelogic][cursor-turn]")
{
    CursorTurnFixture fixture;
    constexpr float ClickedAngle = 90.f;
    fixture.Update(true, ClickedAngle);
    for (const float hoveredAngle : {180.f, 270.f, 0.f})
    {
        CHECK_FALSE(fixture.Update(false, hoveredAngle));
        CHECK(fixture.hero.MoveHeading == ClickedAngle);
    }

    fixture.hero.Object.Angle[2] = ClickedAngle;
    fixture.hero.TurnPending = false;
    CHECK(fixture.Update(false, 270.f));
    CHECK_FALSE(fixture.Update(false, 270.f));
    CHECK_FALSE(fixture.turning);
    CHECK_FALSE(fixture.hero.TurnPending);
    CHECK(fixture.hero.Object.Angle[2] == ClickedAngle);
}

TEST_CASE("holding or clicking again can deliberately change the turn target [gamelogic][cursor-turn]")
{
    CursorTurnFixture fixture;
    fixture.Update(true, 90.f);
    CHECK_FALSE(fixture.Update(true, 180.f));
    CHECK(fixture.hero.MoveHeading == 180.f);
    CHECK(fixture.hero.TurnPending);

    fixture.hero.Object.Angle[2] = 180.f;
    fixture.hero.TurnPending = false;
    CHECK(fixture.Update(true, 270.f));
    CHECK(fixture.hero.TurnPending);
    CHECK(fixture.hero.MoveHeading == 270.f);
}

TEST_CASE("an action which forbids turning ignores clicks and interrupts cursor ownership [gamelogic][cursor-turn]")
{
    CursorTurnFixture fixture;
    CHECK_FALSE(fixture.Update(true, 90.f, false));
    CHECK_FALSE(fixture.hero.TurnPending);

    fixture.Update(true, 90.f);
    CHECK_FALSE(fixture.Update(true, 180.f, false));
    CHECK_FALSE(fixture.turning);
    CHECK(fixture.hero.MoveHeading == 90.f);
}
