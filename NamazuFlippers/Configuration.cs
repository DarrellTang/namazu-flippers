using Dalamud.Configuration;

namespace NamazuFlippers;

/// <summary>
/// Dalamud-facing half of the plugin configuration: the <see cref="IPluginConfiguration"/> marker
/// so Dalamud persists it via
/// <see cref="Dalamud.Plugin.IDalamudPluginInterface.GetPluginConfig"/> and
/// <see cref="Dalamud.Plugin.IDalamudPluginInterface.SavePluginConfig"/>. Every actual setting,
/// its default, and the snapshot/restore/reset logic live in the Dalamud-free partial
/// (ConfigurationSettings.cs) so they are unit-testable. <c>Version</c> — the interface's only
/// member — is declared there. Corresponding requirements: CONF-01 through CONF-09.
/// </summary>
public partial class Configuration : IPluginConfiguration
{
}
