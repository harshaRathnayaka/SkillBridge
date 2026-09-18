using SkillBridge.ApiService.Data;

namespace SkillBridge.ApiService.Dashboard;

// Small presentation helpers shared by the dashboard endpoint's DTO mapping, so Razor
// components on the client stay display-only rather than re-implementing currency/date
// formatting per role.
internal static class DashboardFormatting
{
    public static string FormatMode(CourseMode mode) => mode switch
    {
        CourseMode.Live => "Live",
        CourseMode.SelfPaced => "Self-paced",
        CourseMode.DraftSlots => "Draft slots open",
        _ => mode.ToString(),
    };

    public static string FormatMoney(decimal amount, string currency, string? unitLabel) =>
        currency == "USD"
            ? $"${amount:N0}{unitLabel}"
            : $"{currency} {amount:N0}{unitLabel}";

    public static string FormatRelative(DateTimeOffset at)
    {
        var delta = DateTimeOffset.UtcNow - at;
        if (delta < TimeSpan.FromHours(1))
        {
            var minutes = Math.Max(1, (int)delta.TotalMinutes);
            return $"{minutes} minute{(minutes == 1 ? "" : "s")} ago";
        }

        if (delta < TimeSpan.FromDays(1))
        {
            var hours = (int)delta.TotalHours;
            return $"{hours} hour{(hours == 1 ? "" : "s")} ago";
        }

        var days = (int)delta.TotalDays;
        return days switch
        {
            0 => "Today",
            1 => "Yesterday",
            _ => $"{days} days ago",
        };
    }
}
