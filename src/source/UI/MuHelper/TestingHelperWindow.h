#pragma once

#include "UI/Core/WindowManager.h"
#include "UI/MuHelper/TestingHelperRmlModel.h"
#include "UI/RmlBridge/RmlStackingOrder.h"
#include "UI/RmlBridge/RmlThemedView.h"

namespace mu::ui::window
{
// A centered, draggable, scrollable RmlUi testing panel; RCSS owns size and responsive layout.
class CTestingHelperWindow : public CObject
{
public:
    ~CTestingHelperWindow() override;
    bool Create(CManager* manager);
    void Release();
    void Show(bool visible) override;
    bool UpdateMouseEvent() override;
    bool UpdateKeyEvent() override;
    bool Update() override;
    bool Render() override { return true; }
    float GetLayerDepth() override { return UI::RmlBridge::ForegroundPanelLayerDepth; }
    float GetKeyEventOrder() override { return 10.f; }
    bool TakesTypingFrom(const Rml::ElementDocument* document) const override
    {
        return IsVisible() && document == m_view.Document();
    }

private:
    void BindModel(Rml::DataModelConstructor& constructor, TestingHelperRmlModel& model);
    void AfterBuild();
    void SetLabels(TestingHelperRmlModel& model);
    void Toggle();
    void Close();
    void RunAction(const Rml::String& action);
    void SaveDamageStyle();
    void Send(const std::wstring& command);
    void SyncServerState();
    void SyncDiagnostics();
    bool ValidFields(std::initializer_list<Rml::String> values) const;

    CManager* m_manager = nullptr;
    unsigned m_revision = 0;
    unsigned m_settingsRevision = 0;
    uint64_t m_metricsTick = 0;
    uint64_t m_requestTick = 0;
    UI::RmlBridge::ThemedView<TestingHelperRmlModel> m_view{"testing_helper",
        [this](Rml::DataModelConstructor& c, TestingHelperRmlModel& model) { BindModel(c, model); },
        {{"Data/Interface/RmlUi/testing_helper.rml"}}, {.afterBuild = [this] { AfterBuild(); }}};
};
} // namespace mu::ui::window
