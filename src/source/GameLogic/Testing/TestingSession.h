#pragma once

#include <string>
#include <string_view>
#include <vector>

namespace GameLogic::Testing
{
// State advertised by the authenticated server; client-side settings never grant authority.
struct TestingSession
{
    struct MapOption
    {
        int number = 0;
        bool eligible = false;
        std::wstring name;
    };
    bool available = false;
    bool enabled = false;
    bool administrator = false;
    float savedXp = 0;
    double savedDrop = 0;
    int savedSpeed = 0;
    int savedStat = 0;
    float effectiveXp = 0;
    float masterXp = 0;
    int effectiveSpeed = 0;
    int effectiveStat = 0;
    int players = 0;
    int monsterTarget = 150;
    int group = 3;
    int respawn = 3;
    int spacing = 12;
    int crowdRequested = 0;
    int crowdActual = 0;
    std::string error;
    std::wstring feedback;
    std::vector<MapOption> availableMaps;
    double effectiveDrop = 1;
    int cappedDropGroups = 0;
    int dropGroups = 0;
    int resetLevel = 0;
    long long resetZen = 0;
    int resetItems = 0;
    std::wstring resetItemName;
    int resets = 0;
    int resetLimit = 0;
    std::vector<std::string> maps;
    std::vector<std::string> bots;
    bool metricsAvailable = false;
    double serverCpu = 0;
    double serverMemory = 0;
    double maintenanceMs = 0;
    int generatedMonsters = 0;
    unsigned settingsRevision = 0;
    unsigned revision = 0;
};

const TestingSession& Session();
void ResetSession();
// Called only for server BlueNormal notices. Unknown ordinary notices are left to chat.
bool ReceiveStatus(std::wstring_view notice);
void BeginPreview();
void BeginRequest();
} // namespace GameLogic::Testing
