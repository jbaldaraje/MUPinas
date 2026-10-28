#include "stdafx.h"
#include "UI/MuHelper/TestingHelperCommand.h"
#include "UI/Core/WindowSystem.h"
#include "Scenes/SceneCore.h"

bool UI::Testing::HandleConsoleCommand(std::wstring_view command)
{
    if (command != L"$testing") return false;
    if (SceneFlag == MAIN_SCENE && g_pNewUISystem)
        g_pNewUISystem->Toggle(mu::ui::window::INTERFACE_TESTING_HELPER);
    return true;
}
