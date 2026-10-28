#pragma once

#include <string_view>

namespace UI::Testing
{
// Keyboard fallback for systems that reserve the F12 key.
bool HandleConsoleCommand(std::wstring_view command);
}
