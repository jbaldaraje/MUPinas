#include "stdafx.h"
#include "Engine/Object/Movement/HeroCursorTurn.h"

#include "Engine/AI/ZzzAI.h"

namespace Engine::Characters
{
bool UpdateHeroCursorTurn(CHARACTER* hero, bool& turningToCursor, bool mayTurn, bool worldClick, float clickedAngle)
{
    if (!mayTurn)
    {
        turningToCursor = false;
        return false;
    }
    const bool completed = turningToCursor && !hero->TurnPending;
    if (completed)
        turningToCursor = false;
    if (!worldClick)
        return completed;

    if (turningToCursor)
        hero->MoveHeading = clickedAngle;
    else
    {
        StartStandingTurn(hero, clickedAngle);
        turningToCursor = hero->TurnPending;
    }
    return completed;
}
} // namespace Engine::Characters
