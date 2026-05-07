using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace NamazuFlippers.UI;

/// <summary>
/// Settings window. Stub created in plan 04-01 so the entry point compiles.
/// Body — snapshot/dirty/save/discard, all CONF-01..09 widgets, Reset modal — lands in plan 04-03.
/// </summary>
public class ConfigWindow : Window
{
    private readonly NamazuFlippers plugin;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;

    public ConfigWindow(NamazuFlippers plugin, IDalamudPluginInterface pluginInterface, IPluginLog log)
        : base("Namazu Flippers — Settings", ImGuiWindowFlags.None)
    {
        this.plugin = plugin;
        this.pluginInterface = pluginInterface;
        this.log = log;
        Size = new Vector2(400, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void Draw()
    {
        ImGui.TextWrapped("ConfigWindow placeholder — body lands in plan 04-03.");
    }
}
