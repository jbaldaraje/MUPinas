#include "stdafx.h"
#include "UI/MuHelper/TestingHelperWindow.h"

#include "I18N/All.h"
#include "Core/Globals/InterfaceList.h"
#include "Core/Utilities/StringUtils.h"
#include "Engine/Object/ZzzCharacter.h"
#include "World/MapInfra/MapManager.h"
#include "UI/Placement/WindowPlacement.h"
#include "GameLogic/Commands/ChatCommandCatalog.h"
#include "GameLogic/Testing/TestingSession.h"
#include "Render/Textures/ZzzOpenglUtil.h"
#include "UI/Core/WindowCommon.h"
#include "UI/Core/WindowAccess.h"
#include "UI/RmlBridge/RmlDocumentVisibility.h"
#include "UI/RmlBridge/RmlDraggable.h"
#include "UI/RmlBridge/RmlNumericInputFilter.h"
#include "UI/RmlBridge/RmlPointer.h"

#include <RmlUi/Core/Context.h>
#include <SDL3/SDL_timer.h>
#include <algorithm>
#include <sstream>
#include <iomanip>

namespace mu::ui::window
{
namespace
{
constexpr uint64_t ResponseTimeoutMilliseconds = 60000;
constexpr uint64_t MetricsRefreshMilliseconds = 250;
constexpr size_t MaximumNumericDigits = 6;
constexpr size_t MaximumCommandLength = 120;
constexpr double MillisecondsPerSecond = 1000.0;
std::wstring Wide(const Rml::String& value) { return std::wstring(value.begin(), value.end()); }
} // namespace

CTestingHelperWindow::~CTestingHelperWindow() { Release(); }

bool CTestingHelperWindow::Create(CManager* manager)
{
    if (!manager) return false;
    m_manager = manager;
    manager->AddUIObj(INTERFACE_TESTING_HELPER, this);
    UI::Placement::RegisterName(INTERFACE_TESTING_HELPER, "testing_helper");
    m_view.Ensure();
    Show(false);
    return true;
}

void CTestingHelperWindow::Release()
{
    if (m_manager) m_manager->RemoveUIObj(this);
    m_manager = nullptr;
    m_view.Release();
    GameLogic::Testing::ResetSession();
}

void CTestingHelperWindow::BindModel(Rml::DataModelConstructor& c, TestingHelperRmlModel& model)
{
    c.RegisterArray<std::vector<Rml::String>>();
    auto option = c.RegisterStruct<TestingHelperRmlModel::MapOption>();
    option.RegisterMember("value", &TestingHelperRmlModel::MapOption::value);
    option.RegisterMember("label", &TestingHelperRmlModel::MapOption::label);
    c.RegisterArray<std::vector<TestingHelperRmlModel::MapOption>>();
    c.Bind("map_options", &model.mapOptions);
    c.Bind("reset_info", &model.resetInfo);
    c.Bind("drop_info", &model.dropInfo);
    c.Bind("title", &model.title);
    c.Bind("has_title", &model.hasTitle);
    c.Bind("positioned", &model.positioned);
    c.Bind("root_x", &model.rootX);
    c.Bind("root_y", &model.rootY);
    c.Bind("tab", &model.tab);
    c.Bind("busy", &model.busy);
    c.Bind("administrator", &model.administrator);
    c.Bind("available", &model.available);
    c.Bind("text", &model.text);
    c.Bind("rows", &model.rows);
    c.Bind("bots", &model.bots);
    c.Bind("feedback", &model.feedback);
    c.Bind("saved", &model.saved);
    c.Bind("effective", &model.effective);
    c.Bind("diagnostics", &model.diagnostics);
    c.Bind("server_metrics", &model.serverMetrics);
    c.Bind("xp", &model.xp);
    c.Bind("drop", &model.drop);
    c.Bind("speed", &model.speed);
    c.Bind("stat", &model.stat);
    c.Bind("map", &model.map);
    c.Bind("target", &model.target);
    c.Bind("group", &model.group);
    c.Bind("respawn", &model.respawn);
    c.Bind("spacing", &model.spacing);
    c.Bind("count", &model.count);
    c.Bind("behavior", &model.behavior);
    c.Bind("x", &model.x);
    c.Bind("y", &model.y);
    c.Bind("radius", &model.radius);
    c.Bind("lifetime", &model.lifetime);
    c.Bind("level", &model.level);
    c.Bind("bot_name", &model.botName);
    SetLabels(model);
    c.BindEventCallback("testing_action", [this](Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
    {
        if (!args.empty()) RunAction(args[0].Get<Rml::String>());
    });
    c.BindEventCallback("testing_tab", [this](Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
    {
        if (args.empty()) return;
        const auto tab = args[0].Get<int>(-1);
        if (tab >= 0 && tab < 6)
        {
            m_view.GetModel().tab = tab;
            m_view.MarkDirty("tab");
        }
    });
}

void CTestingHelperWindow::SetLabels(TestingHelperRmlModel& model)
{
    model.text = {
        I18N::Testing::Title,
        I18N::Testing::Reset,
        I18N::Testing::Rates,
        I18N::Testing::Spots,
        I18N::Testing::Crowd,
        I18N::Testing::Market,
        I18N::Testing::Diagnostics,
        I18N::Testing::Refresh,
        I18N::Testing::Close,
        I18N::Testing::Stay,
        I18N::Testing::Lorencia,
        I18N::Testing::ResetNote,
        I18N::Testing::Xp,
        I18N::Testing::Drop,
        I18N::Testing::Speed,
        I18N::Testing::Stat,
        I18N::Testing::SaveRates,
        I18N::Testing::SaveCaps,
        I18N::Testing::Enable,
        I18N::Testing::Disable,
        I18N::Testing::Map,
        I18N::Testing::Target,
        I18N::Testing::Group,
        I18N::Testing::Respawn,
        I18N::Testing::Spacing,
        I18N::Testing::Preview,
        I18N::Testing::Apply,
        I18N::Testing::Cleanup,
        I18N::Testing::SpotNote,
        I18N::Testing::Count,
        I18N::Testing::Behavior,
        I18N::Testing::Idle,
        I18N::Testing::Wander,
        I18N::Testing::Combat,
        I18N::Testing::Radius,
        I18N::Testing::Lifetime,
        I18N::Testing::Level,
        I18N::Testing::Spawn,
        I18N::Testing::RemoveAll,
        I18N::Testing::RemoveSelected,
        I18N::Testing::BotName,
        I18N::Testing::CrowdNote,
        I18N::Testing::EnterMarket,
        I18N::Testing::OpenShop,
        I18N::Testing::MarketNote,
        I18N::Testing::Stop,
        I18N::Testing::Waiting,
        I18N::Testing::Unavailable,
        I18N::Testing::Ready,
        I18N::Testing::Denied,
        I18N::Testing::Invalid,
        I18N::Testing::Failed,
        I18N::Testing::TimedOut,
        I18N::Testing::AdminNote,
        I18N::Testing::Saved,
        I18N::Testing::Effective,
        I18N::Testing::Master,
        I18N::Testing::Actual,
        I18N::Testing::FrameTime,
        I18N::Testing::NoRestart,
        I18N::Testing::Location,
        I18N::Testing::DropNote,
        I18N::Testing::PreviewHeader,
        I18N::Testing::AllMaps,
        I18N::Testing::MapChoice,
        I18N::Testing::CenterX,
        I18N::Testing::CenterY,
        I18N::Testing::SavedOrder,
        I18N::Testing::EffectiveOrder,
        I18N::Testing::SaveSpots,
        I18N::Testing::ResetInfoOrder,
        I18N::Testing::DropEffective,
        I18N::Testing::DiagnosticsOrder,
        I18N::Testing::ServerMetricsOrder,
    };
    model.title = I18N::Testing::Title;
}

void CTestingHelperWindow::AfterBuild()
{
    auto* doc = m_view.Document();
    if (!doc) return;
    UI::RmlBridge::AttachNumericInputFilter(doc);
    UI::RmlBridge::MakeDraggable(doc->GetElementById("window_shell_header"), doc->GetElementById("panel"));
}

void CTestingHelperWindow::Show(bool visible)
{
    if (visible == IsVisible()) return;
    CObject::Show(visible);
    if (!visible)
    {
        if (auto* doc = m_view.Document())
        {
            if (auto* focus = doc->GetContext()->GetFocusElement(); focus && focus->GetOwnerDocument() == doc)
                focus->Blur();
        }
        UI::RmlBridge::SyncDocumentVisibility(m_view.Document(), false);
        return;
    }
    m_view.Ensure();
    auto& model = m_view.GetModel();
    model.x = std::to_string(Hero->PositionX);
    model.y = std::to_string(Hero->PositionY);
    model.map = std::to_string(gMapManager.WorldActive);
    for (const auto* field : {"x", "y", "map"}) m_view.MarkDirty(field);
    UI::RmlBridge::SyncDocumentVisibilityInFront(m_view.Document(), true);
    Send(L"/localtest status");
}

void CTestingHelperWindow::Close() { Show(false); }
void CTestingHelperWindow::Toggle() { Show(!IsVisible()); }

bool CTestingHelperWindow::UpdateKeyEvent()
{
    if (IsPress(VK_F12)) { Toggle(); return false; }
    if (IsVisible() && IsPress(VK_ESCAPE)) { Close(); return false; }
    return true;
}

bool CTestingHelperWindow::UpdateMouseEvent()
{
    return !IsVisible() || !UI::RmlBridge::IsPointerOver(m_view.Document());
}

bool CTestingHelperWindow::Update()
{
    m_view.Ensure();
    UI::RmlBridge::SyncDocumentVisibilityInFront(m_view.Document(), IsVisible());
    if (!IsVisible()) return true;
    SyncServerState();
    SyncDiagnostics();
    auto& model = m_view.GetModel();
    if (model.busy && SDL_GetTicks() - m_requestTick >= ResponseTimeoutMilliseconds)
    {
        model.busy = false;
        model.feedback = I18N::Testing::TimedOut;
        m_view.MarkDirty("busy");
        m_view.MarkDirty("feedback");
    }
    return true;
}

bool CTestingHelperWindow::ValidFields(std::initializer_list<Rml::String> values) const
{
    return std::all_of(values.begin(), values.end(), [](const auto& value)
    {
        return !value.empty() && value.size() <= MaximumNumericDigits
            && std::all_of(value.begin(), value.end(), [](char c) { return c >= '0' && c <= '9'; });
    });
}

void CTestingHelperWindow::Send(const std::wstring& command)
{
    if (command.size() > MaximumCommandLength) return;
    GameLogic::Testing::BeginRequest();
    auto& model = m_view.GetModel();
    model.busy = true;
    model.feedback = I18N::Testing::Waiting;
    m_requestTick = SDL_GetTicks();
    m_revision = GameLogic::Testing::Session().revision;
    m_view.MarkDirty("busy");
    m_view.MarkDirty("feedback");
    GameLogic::Commands::Catalog().Execute(command);
}

void CTestingHelperWindow::RunAction(const Rml::String& action)
{
    if (action == "close") { Close(); return; }
    auto& m = m_view.GetModel();
    if (action == "refresh") { Send(L"/localtest status"); return; }
    if (action.starts_with("count_"))
    {
        const auto count = action.substr(6);
        if (count == "10" || count == "25" || count == "50" || count == "100")
        {
            m.count = count;
            m_view.MarkDirty("count");
        }
        return;
    }
    if (m.busy && action != "stop") return;
    if (action == "stay") { Send(L"/reset stay"); return; }
    if (action == "lorencia") { Send(L"/reset lorencia"); return; }
    if (action == "market") { Send(L"/market"); return; }
    if (action == "shop")
    {
        Close();
        UI::Windows::Show(INTERFACE_MYSHOP_INVENTORY);
        return;
    }
    if (!m.available || !m.administrator) return;
    std::wstring command;
    if (action == "enable") command = L"/localtest enabled 1";
    else if (action == "disable") command = L"/localtest enabled 0";
    else if (action == "stop") command = L"/localtest stop";
    else if (action == "rates" && ValidFields({m.xp, m.drop}))
        command = L"/localtest rates " + Wide(m.xp) + L" " + Wide(m.drop);
    else if (action == "caps" && ValidFields({m.speed, m.stat}))
        command = L"/localtest caps " + Wide(m.speed) + L" " + Wide(m.stat);
    else if (action == "spots" && ValidFields({m.target, m.group, m.respawn, m.spacing}))
        command = L"/localtest spots " + Wide(m.target) + L" " + Wide(m.group) + L" " + Wide(m.respawn) + L" " + Wide(m.spacing);
    else if ((action == "preview" || action == "apply" || action == "cleanup") && (m.map == "all" || ValidFields({m.map})))
    {
        if (action == "preview") GameLogic::Testing::BeginPreview();
        command = L"/localtest " + Wide(action) + L" " + Wide(m.map);
    }
    else if (action == "crowd" && ValidFields({m.count, m.map, m.x, m.y, m.radius, m.lifetime, m.level})
        && (m.behavior == "idle" || m.behavior == "wander" || m.behavior == "combat"))
        command = L"/localtest crowd " + Wide(m.count) + L" " + Wide(m.map) + L" " + Wide(m.x) + L" " + Wide(m.y)
            + L" " + Wide(m.radius) + L" " + Wide(m.behavior) + L" " + Wide(m.lifetime) + L" " + Wide(m.level);
    else if (action == "remove_all") command = L"/localtest remove all";
    else if (action == "remove" && m.botName.size() <= 10 && !m.botName.empty()
        && std::all_of(m.botName.begin(), m.botName.end(), [](unsigned char c) { return std::isalnum(c); }))
        command = L"/localtest remove " + Wide(m.botName);
    if (!command.empty()) Send(command);
    else
    {
        m.feedback = I18N::Testing::Invalid;
        m_view.MarkDirty("feedback");
    }
}

void CTestingHelperWindow::SyncServerState()
{
    const auto& state = GameLogic::Testing::Session();
    if (state.revision == m_revision) return;
    m_revision = state.revision;
    auto& m = m_view.GetModel();
    m.busy = false;
    m.available = state.available;
    m.administrator = state.administrator;
    m.feedback = state.error.empty() ? I18N::Testing::Ready :
        state.error == "denied" ? I18N::Testing::Denied : state.error == "unavailable" ? I18N::Testing::Unavailable :
        state.error == "invalid" ? I18N::Testing::Invalid : I18N::Testing::Failed;
    if (!state.feedback.empty()) m.feedback = StringUtils::WideToNarrow(state.feedback.c_str());
    if (m_settingsRevision != state.settingsRevision)
    {
        m_settingsRevision = state.settingsRevision;
        m.xp = std::to_string(static_cast<int>(state.savedXp));
        m.drop = std::to_string(static_cast<int>(state.savedDrop));
        m.speed = std::to_string(state.savedSpeed);
        m.stat = std::to_string(state.savedStat);
        m.target = std::to_string(state.monsterTarget);
        m.group = std::to_string(state.group);
        m.respawn = std::to_string(state.respawn);
        m.spacing = std::to_string(state.spacing);
    }
    std::ostringstream saved;
    saved << state.savedXp << " / " << state.savedDrop << " / " << state.savedSpeed << " / " << state.savedStat;
    m.saved = saved.str();
    std::ostringstream effective;
    effective << state.effectiveXp << " / " << state.masterXp << " / " << state.effectiveSpeed << " / " << state.effectiveStat;
    m.effective = effective.str();
    m.mapOptions.clear();
    for (const auto& option : state.availableMaps)
    {
        if (option.eligible || option.number == 79)
            m.mapOptions.push_back({std::to_string(option.number), std::to_string(option.number) + " — " + StringUtils::WideToNarrow(option.name.c_str())});
    }
    std::ostringstream reset;
    reset << state.resetLevel << " / " << state.resetZen << " / " << state.resetItems << " / " << state.resets << " / " << state.resetLimit;
    if (!state.resetItemName.empty()) reset << " (" << StringUtils::WideToNarrow(state.resetItemName.c_str()) << ")";
    m.resetInfo = reset.str();
    std::ostringstream drops;
    drops << state.effectiveDrop << " / " << state.cappedDropGroups << " / " << state.dropGroups;
    m.dropInfo = drops.str();
    m.rows.assign(state.maps.begin(), state.maps.end());
    m.bots.assign(state.bots.begin(), state.bots.end());
    for (const auto* field : {"busy", "available", "administrator", "feedback", "xp", "drop", "speed", "stat", "target",
        "group", "respawn", "spacing", "saved", "effective", "diagnostics", "rows", "bots", "map_options", "reset_info", "drop_info"})
        m_view.MarkDirty(field);
}
void CTestingHelperWindow::SyncDiagnostics()
{
    const auto now = SDL_GetTicks();
    if (now - m_metricsTick < MetricsRefreshMilliseconds) return;
    m_metricsTick = now;
    const auto& state = GameLogic::Testing::Session();
    auto& model = m_view.GetModel();
    std::ostringstream client;
    client << state.players << " / " << state.crowdActual << " / ";
    if (FPS_AVG > 0) client << std::fixed << std::setprecision(2) << MillisecondsPerSecond / FPS_AVG;
    else client << "—";
    model.diagnostics = client.str();
    model.serverMetrics = "—";
    if (state.metricsAvailable)
    {
        std::ostringstream server;
        server << std::fixed << std::setprecision(2) << state.serverCpu << " / " << state.serverMemory << " / " << state.maintenanceMs << " / " << state.generatedMonsters;
        model.serverMetrics = server.str();
    }
    m_view.MarkDirty("diagnostics");
    m_view.MarkDirty("server_metrics");
}

} // namespace mu::ui::window
