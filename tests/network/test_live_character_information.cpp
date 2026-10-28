#include "stdafx.h"
#include <doctest.h>
#include "Engine/Object/ZzzCharacter.h"
#include "Engine/Object/ZzzInfomation.h"
#include "Engine/Object/ZzzInterface.h"
#include "GameLogic/Skills/SkillManager.h"
#include "Network/Server/WSclient.h"
#include "Render/Textures/ZzzOpenglUtil.h"
#include "Scenes/SceneCore.h"
#include "World/MapInfra/MapManager.h"

extern int HeroIndex;
BOOL ReceiveJoinMapServer(std::span<const BYTE> packet);

namespace
{
struct LiveCharacterScope
{
    CHARACTER hero;
    CHARACTER_MACHINE machine{};
    CHARACTER* previousHero = Hero;
    CHARACTER_MACHINE* previousMachine = CharacterMachine;
    CHARACTER_ATTRIBUTE* previousAttributes = CharacterAttribute;
    EGameScene previousScene = SceneFlag;
    int previousProtocol = CurrentProtocolState;
    int previousWorld = gMapManager.WorldActive;
    int previousLoading = LoadingWorld;
    int previousIndex = HeroIndex;
    bool previousMouse = MouseLButton;

    LiveCharacterScope()
    {
        Hero = &hero;
        CharacterMachine = &machine;
        CharacterAttribute = &machine.Character;
        SceneFlag = MAIN_SCENE;
        CurrentProtocolState = RECEIVE_JOIN_MAP_SERVER;
        gMapManager.WorldActive = WD_0LORENCIA;
        hero.Object.Live = true;
    }
    ~LiveCharacterScope()
    {
        Hero = previousHero;
        CharacterMachine = previousMachine;
        CharacterAttribute = previousAttributes;
        SceneFlag = previousScene;
        CurrentProtocolState = previousProtocol;
        gMapManager.WorldActive = previousWorld;
        LoadingWorld = previousLoading;
        HeroIndex = previousIndex;
        MouseLButton = previousMouse;
        gSkillManager.InvalidateSkillAttributeRequirementsCache();
    }
};
}

TEST_CASE("Live character information refresh preserves world and input [network][character-stats]")
{
    LiveCharacterScope scope;
    scope.hero.PositionX = 127;
    scope.hero.PositionY = 121;
    scope.hero.Object.Angle[2] = 45.f;
    scope.machine.Equipment[0].Type = ITEM_SWORD;
    scope.machine.Character.AbilityTime[0] = 123;
    LoadingWorld = 0;
    HeroIndex = 7;
    MouseLButton = true;

    PRECEIVE_JOIN_MAP_SERVER_EXTENDED packet{};
    packet.Map = WD_0LORENCIA;
    packet.PositionX = 130;
    packet.PositionY = 125;
    packet.Strength = 65535;
    packet.AttackSpeed = 700;
    packet.MaxAttackSpeed = 799;
    packet.Resets = 4;
    packet.Gold = 12345;
    const auto bytes = std::span<const BYTE>(reinterpret_cast<const BYTE*>(&packet), sizeof(packet));
    REQUIRE(ReceiveJoinMapServer(bytes));
    CHECK(CharacterAttribute->Strength == 65535);
    CHECK(CharacterAttribute->AttackSpeed == 700);
    CHECK(CharacterAttribute->MaxAttackSpeed == 799);
    CHECK(CharacterAttribute->Resets == 4);
    CHECK(CharacterMachine->Gold == 12345);
    CHECK(Hero == &scope.hero);
    CHECK(HeroIndex == 7);
    CHECK(Hero->PositionX == 127);
    CHECK(Hero->PositionY == 121);
    CHECK(Hero->Object.Angle[2] == 45.f);
    CHECK(CharacterMachine->Equipment[0].Type == ITEM_SWORD);
    CHECK(CharacterAttribute->AbilityTime[0] == 123);
    CHECK(LoadingWorld == 0);
    CHECK(MouseLButton);
}
