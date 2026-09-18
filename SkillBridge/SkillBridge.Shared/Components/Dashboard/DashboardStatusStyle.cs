namespace SkillBridge.Shared.Components.Dashboard;

// One shared mapping from a raw status string (course Mode, material Status, application
// Stage — all different enums, all rendered the same visual way) to a small fixed set of pill
// colors, so StatusPill's color logic lives in one place instead of once per dashboard.
public static class DashboardStatusStyle
{
    public static string VariantFor(string status) => status switch
    {
        "Live" or "Published" or "Interview" or "Offer" => "positive",
        "Self-paced" or "New" => "info",
        "Draft slots open" or "Screening" => "warning",
        _ => "neutral",
    };
}
