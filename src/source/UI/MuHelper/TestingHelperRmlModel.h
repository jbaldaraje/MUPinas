#pragma once

#include <RmlUi/Core/Types.h>
#include <string>
#include <vector>

namespace mu::ui::window
{
struct TestingHelperRmlModel
{
    struct MapOption
    {
        Rml::String value;
        Rml::String label;
    };
    std::vector<MapOption> mapOptions;
    Rml::String resetInfo;
    Rml::String dropInfo;
    Rml::String title;
    bool hasTitle = true;
    bool positioned = false;
    float rootX = 0;
    float rootY = 0;
    int tab = 0;
    bool busy = false;
    bool administrator = false;
    bool available = false;
    std::vector<Rml::String> text;
    std::vector<Rml::String> rows;
    std::vector<Rml::String> bots;
    Rml::String feedback;
    Rml::String saved;
    Rml::String effective;
    Rml::String diagnostics;
    Rml::String serverMetrics;
    Rml::String xp;
    Rml::String drop;
    Rml::String speed;
    Rml::String stat;
    Rml::String map = "0";
    Rml::String target;
    Rml::String group;
    Rml::String respawn;
    Rml::String spacing;
    Rml::String count = "10";
    Rml::String behavior = "idle";
    Rml::String x;
    Rml::String y;
    Rml::String radius = "8";
    Rml::String lifetime = "300";
    Rml::String level = "100";
    Rml::String botName;
};
} // namespace mu::ui::window
