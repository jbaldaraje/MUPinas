#pragma once

namespace Engine::Characters
{
// Only a world click (or a held world click) supplies a new heading. An accepted turn
// finishes toward its last clicked heading after release, without following hover.
// Returns true once when that turn finishes and its facing should be sent to the server.
bool UpdateHeroCursorTurn(CHARACTER* hero, bool& turningToCursor, bool mayTurn, bool worldClick, float clickedAngle);
} // namespace Engine::Characters
