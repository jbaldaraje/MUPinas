#include "stdafx.h"
#include "Engine/Object/Movement/MovementBones.h"

#include "Engine/Object/Movement/FootPlacement.h"
#include "Engine/Object/Movement/PoseFade.h"
#include "Engine/Object/ZzzCharacter.h"
#include "Engine/Object/ZzzObject.h"
#include <cstdint>

namespace Engine::Characters
{
namespace
{
int MountIndex(const OBJECT* object)
{
    const auto address = reinterpret_cast<std::uintptr_t>(object);
    const auto first = reinterpret_cast<std::uintptr_t>(Mounts);
    if (address < first)
    {
        return -1;
    }
    const auto offset = address - first;
    if (offset >= sizeof(OBJECT) * MAX_MOUNTS || offset % sizeof(OBJECT) != 0)
    {
        return -1;
    }
    return static_cast<int>(offset / sizeof(OBJECT));
}
} // namespace

void AnimateBones(OBJECT* o, BMD& model, float (*bones)[3][4], bool translate, short currentAction)
{
    const CHARACTER* character = CharacterOfObject(o);
    vec3_t angle = {o->Angle[0], o->Angle[1],
                    o->Angle[2] + (character != nullptr ? character->Motion.SnapTurnOffset : 0.f)};
    const float turnLag = character != nullptr ? character->Motion.TurnLag : 0.f;
    const int mount = MountIndex(o);
    const float headTurn = mount >= 0 ? MountHeadTurn[mount] : 0.f;
    const float headRoll = mount >= 0 ? MountHeadRoll[mount] : 0.f;
    PoseBlend blend;
    model.Animation(bones, o->AnimationFrame, o->PriorAnimationFrame, o->PriorAction, angle, o->HeadAngle, false,
                    translate, nullptr, currentAction, MAX_BONES, turnLag, headTurn, headRoll,
                    CharacterPoseFade(character, blend));
    if (character != nullptr && !translate)
    {
        PlaceFeet(character, bones);
    }
}
} // namespace Engine::Characters
