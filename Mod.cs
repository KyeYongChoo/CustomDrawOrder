using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using ModToggle;

namespace CustomDrawOrder;

[ModInitializer(nameof(Init))]
internal static class Mod
{
    private static void Init()
    {
        ToggleSettings.Load();
        new Harmony("KyeYongChoo.CustomDrawOrder").PatchAll(typeof(Mod).Assembly);
        BaseLibBridge.RegisterWhenModsLoaded();
    }
}
