namespace RaceDay.Web.Helpers;

public static class FormatHelper
{
    /// <summary>1 -> "1st", 2 -> "2nd", 11 -> "11th", 47 -> "47th".</summary>
    public static string Ordinal(int number)
    {
        var lastTwo = number % 100;
        if (lastTwo is >= 11 and <= 13)
        {
            return $"{number}th";
        }

        return (number % 10) switch
        {
            1 => $"{number}st",
            2 => $"{number}nd",
            3 => $"{number}rd",
            _ => $"{number}th"
        };
    }

    /// <summary>Formats a finish time as HH:MM:SS (hours may exceed 23 for ultra events).</summary>
    public static string FinishTime(TimeSpan time) =>
        $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";

    /// <summary>"47th out of 312 finishers", or "Not placed" when no position was recorded.</summary>
    public static string Standing(int? position, int totalFinishers) =>
        position is null
            ? "Not placed"
            : $"{Ordinal(position.Value)} out of {totalFinishers} finisher{(totalFinishers == 1 ? "" : "s")}";

    /// <summary>Bootstrap background class for an enrolment status badge.</summary>
    public static string BadgeClass(string status) => status switch
    {
        "Confirmed" => "bg-success",
        "Pending" => "bg-warning text-dark",
        "Cancelled" => "bg-secondary",
        _ => "bg-info text-dark"
    };
}
