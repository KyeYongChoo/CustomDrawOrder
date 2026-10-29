namespace ModToggle;

/// <summary>This mod's identity for the shared toggle code in Shared/ModToggle.</summary>
public static class ModInfo
{
    public const string Id = "CustomDrawOrder";
    public const string Title = "Custom Draw Order";
    public const string LogPrefix = "[CustomDrawOrder]";

    public const string ToggleDescription =
        "When ticked, every draw opens a screen where you choose the cards to draw. Untick to draw normally. " +
        "Takes effect immediately, even mid-combat.";
}
