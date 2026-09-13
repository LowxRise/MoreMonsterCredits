using BepInEx.Configuration;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;

namespace LocalTweaks
{
    internal static class TweakSettings
    {
        internal static ConfigEntry<int> Slider(ConfigFile file, string section, string name,
            int value, int min, int max, string description, string guid, string modName)
        {
            var entry = file.Bind(section, name, value,
                new ConfigDescription(description, new AcceptableValueRange<int>(min, max)));
            ModSettingsManager.AddOption(new IntSliderOption(entry,
                new IntSliderConfig { min = min, max = max }), guid, modName);
            return entry;
        }

        internal static ConfigEntry<bool> Checkbox(ConfigFile file, string section, string name,
            bool value, string description, string guid, string modName)
        {
            var entry = file.Bind(section, name, value, description);
            ModSettingsManager.AddOption(new CheckBoxOption(entry), guid, modName);
            return entry;
        }
    }
}
