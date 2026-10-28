#include "GameLogic/Testing/TestingSession.h"

#include <algorithm>
#include <cmath>
#include <sstream>

namespace GameLogic::Testing
{
namespace
{
constexpr std::wstring_view ProtocolPrefix = L"@LT1 ";
constexpr size_t MaximumNoticeLength = 241;
constexpr size_t MaximumRows = 100;
TestingSession state;

bool ReadSettings(std::wistringstream& input)
{
    int enabled = 0;
    int admin = 0;
    float xp = 0;
    double drop = 0;
    int speed = 0;
    int stat = 0;
    if (!(input >> enabled >> xp >> drop >> speed >> stat >> admin)
        || (enabled != 0 && enabled != 1) || (admin != 0 && admin != 1)
        || !std::isfinite(xp) || xp <= 0 || !std::isfinite(drop) || drop <= 0
        || speed < 1 || speed > 65535 || stat < 1 || stat > 65535)
        return false;
    ++state.settingsRevision;
    state.available = true;
    state.enabled = enabled != 0;
    state.administrator = admin != 0;
    state.savedXp = xp;
    state.savedDrop = drop;
    state.savedSpeed = speed;
    state.savedStat = stat;
    state.bots.clear();
    state.maps.clear();
    state.availableMaps.clear();
    state.error.clear();
    state.feedback.clear();
    return true;
}

void AppendBounded(std::vector<std::string>& rows, std::wstring_view text)
{
    if (rows.size() < MaximumRows)
        rows.emplace_back(text.begin(), text.end());
}
} // namespace

const TestingSession& Session() { return state; }
void ResetSession() { state = {}; }
void BeginPreview() { state.maps.clear(); ++state.revision; }
void BeginRequest() { state.feedback.clear(); state.error.clear(); }

bool ReceiveStatus(std::wstring_view notice)
{
    if (!notice.starts_with(ProtocolPrefix))
        return false;
    if (notice.size() > MaximumNoticeLength)
        return true;
    std::wistringstream input{std::wstring(notice.substr(ProtocolPrefix.size()))};
    std::wstring kind;
    input >> kind;
    if (kind == L"status")
    {
        if (!ReadSettings(input))
            return true;
    }
    else if (kind == L"effective")
    {
        auto next = state;
        if (!(input >> next.effectiveXp >> next.masterXp >> next.effectiveSpeed >> next.effectiveStat
                    >> next.players >> next.monsterTarget >> next.group >> next.respawn >> next.spacing)
            || !std::isfinite(next.effectiveXp) || !std::isfinite(next.masterXp))
            return true;
        state = std::move(next);
    }
    else if (kind == L"metrics")
    {
        auto next = state;
        if (input >> next.serverCpu >> next.serverMemory >> next.maintenanceMs >> next.generatedMonsters
            && std::isfinite(next.serverCpu) && next.serverCpu >= 0 && next.serverCpu <= 100
            && std::isfinite(next.serverMemory) && next.serverMemory >= 0
            && std::isfinite(next.maintenanceMs) && next.maintenanceMs >= 0 && next.generatedMonsters >= 0)
        {
            next.metricsAvailable = true;
            state = std::move(next);
        }
    }
    else if (kind == L"map")
    {
        TestingSession::MapOption option;
        int eligible = 0;
        if (input >> option.number >> eligible && option.number >= 0 && option.number <= 255
            && state.availableMaps.size() < MaximumRows)
        {
            option.eligible = eligible == 1;
            std::getline(input >> std::ws, option.name);
            state.availableMaps.push_back(std::move(option));
        }
    }
    else if (kind == L"resetinfo")
    {
        auto next = state;
        if (input >> next.resetLevel >> next.resetZen >> next.resetItems >> next.resets >> next.resetLimit)
        {
            std::getline(input >> std::ws, next.resetItemName);
            state = std::move(next);
        }
    }
    else if (kind == L"drop")
    {
        double factor = 0;
        int groups = 0;
        int capped = 0;
        if (input >> factor >> groups >> capped && std::isfinite(factor) && factor >= 1 && groups >= 0 && capped >= 0 && capped <= groups)
        {
            state.effectiveDrop = factor;
            state.dropGroups = groups;
            state.cappedDropGroups = capped;
        }
    }
    else if (kind == L"crowd")
    {
        int requested = 0;
        int actual = 0;
        if (input >> requested >> actual && requested >= 0 && requested <= 100 && actual >= 0 && actual <= 100)
        {
            state.crowdRequested = requested;
            state.crowdActual = actual;
        }
    }
    else if (kind == L"population" || kind == L"preview")
        AppendBounded(state.maps, notice.substr(ProtocolPrefix.size()));
    else if (kind == L"bot")
        AppendBounded(state.bots, notice.substr(ProtocolPrefix.size() + kind.size() + 1));
    else if (kind == L"feedback")
    {
        std::getline(input >> std::ws, state.feedback);
        state.error.clear();
    }
    else if (kind == L"error")
    {
        std::wstring code;
        input >> code;
        state.error.assign(code.begin(), code.end());
        if (code == L"unavailable") state.available = false;
        if (code == L"unavailable" || code == L"denied") state.administrator = false;
    }
    ++state.revision;
    return true;
}
} // namespace GameLogic::Testing
