#include "stdafx.h"
#include <doctest.h>
#include "RmlLayoutFixture.h"
#include "GameLogic/Testing/TestingSession.h"
#include "UI/MuHelper/TestingHelperWindow.h"
#include "UI/RmlBridge/RmlThemedView.h"
#include "UI/RmlBridge/RmlTheme.h"
#include <filesystem>

namespace
{
class TestingAssetsScope
{
public:
    explicit TestingAssetsScope(Rml::Context* context)
        : directory(std::filesystem::current_path()), theme(UI::RmlBridge::GetActiveThemeName())
    {
        const auto assets = std::filesystem::path(MU_RMLUI_DIR).parent_path().parent_path().parent_path();
        std::filesystem::current_path(assets);
        UI::RmlBridge::SetContextResolver([context](std::string_view) { return context; });
    }
    ~TestingAssetsScope()
    {
        UI::RmlBridge::SetContextResolver({});
        std::filesystem::current_path(directory);
        UI::RmlBridge::SetActiveThemeName(theme);
    }
private:
    std::filesystem::path directory;
    std::string theme;
};
} // namespace

TEST_CASE("testing helper retains field focus and edits across diagnostics updates [ui][testing-helper]")
{
    for (const auto* theme : {"legacy", "modern"})
    {
        for (const auto scale : {75, 100, 150})
        {
            CAPTURE(theme);
            CAPTURE(scale);
            UI::Tests::RmlLayoutFixture fixture;
            TestingAssetsScope assets(fixture.context);
            UI::RmlBridge::SetActiveThemeName(theme);
            fixture.Configure({1024, 768}, scale);
            mu::ui::window::CManager manager;
            mu::ui::window::CTestingHelperWindow window;
            REQUIRE(window.Create(&manager));
            // Exercise the real view/model without sending a login-dependent command.
            window.CObject::Show(true);
            GameLogic::Testing::ReceiveStatus(L"@LT1 status 1 10000 1000 800 65535 1");
            GameLogic::Testing::ReceiveStatus(L"@LT1 map 0 1 Lorencia");
            REQUIRE(window.Update());
            fixture.Refresh();
            auto* document = fixture.context->GetDocument(0);
            REQUIRE(document != nullptr);
            // Reveal the rates tab through its real RmlUi event binding.
            Rml::ElementList tabs;
            document->QuerySelectorAll(tabs, ".testing-tab");
            REQUIRE(tabs.size() == 6);
            tabs[1]->DispatchEvent("click", {});
            fixture.Refresh();
            auto* panel = document->GetElementById("panel");
            REQUIRE(panel != nullptr);
            const auto panelWidth = panel->GetBox().GetSize(Rml::BoxArea::Content).x;
            Rml::ElementList pages;
            document->QuerySelectorAll(pages, ".testing-page");
            REQUIRE(pages.size() == 6);
            CHECK(pages[1]->GetComputedValues().display() != Rml::Style::Display::None);
            INFO("rates page=" << pages[1]->GetBox().GetSize(Rml::BoxArea::Content).x << " scroll=" << pages[1]->GetParentNode()->GetBox().GetSize(Rml::BoxArea::Content).x);
            Rml::ElementList summaries;
            pages[1]->QuerySelectorAll(summaries, ".testing-summary");
            for (auto* summary : summaries)
            {
                if (summary->IsVisible())
                {
                    INFO("summary width=" << summary->GetBox().GetSize(Rml::BoxArea::Content).x << " panel=" << panelWidth);
                    CHECK(summary->GetBox().GetSize(Rml::BoxArea::Content).x > panelWidth * 0.7f);
                }
            }
            auto* speed = document->GetElementById("testing_speed");
            REQUIRE(speed != nullptr);
            // Focus must remain stable while the native update synchronizes visibility and metrics.
            speed->Focus();
            fixture.Refresh();
            CHECK(fixture.context->GetFocusElement() == speed);
            fixture.context->ProcessTextInput("1");
            fixture.Refresh();
            const auto edited = speed->GetAttribute<Rml::String>("value", "");
            CHECK(edited.find('1') != Rml::String::npos);
            GameLogic::Testing::ReceiveStatus(L"@LT1 metrics 3.25 1200 0.01 150");
            REQUIRE(window.Update());
            fixture.Refresh();
            CHECK(fixture.context->GetFocusElement() == speed);
            CHECK(speed->GetAttribute<Rml::String>("value", "") == edited);
            window.Show(false);
            CHECK(fixture.context->GetFocusElement() != speed);
            window.Release();
        }
    }
}
