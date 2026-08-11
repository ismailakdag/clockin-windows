using System.Globalization;

namespace Clockin.Windows;

public sealed record ProgressBadgeInfo(string Id, string Title, string Requirement, string Icon, bool Unlocked, string Progress);

public sealed record ProgressSnapshot(
    DateTime Now,
    double TotalHours,
    double BaseXp,
    int GoalDays,
    int DoubleGoalDays,
    int MonthlyGoalDays,
    int GoalBonusXp,
    int LongestStreak,
    int CurrentStreak,
    int StreakBonusXp,
    long Xp,
    int Level,
    double LevelProgress,
    int ActiveDays,
    double LongestSession,
    DateTime? BestDay,
    double BestDayDuration,
    string BestWeekday,
    string BestStartHour,
    double AverageSession,
    double Trend,
    double AverageDay,
    IReadOnlyDictionary<DateTime, double> DailyDurations,
    IReadOnlyList<ProgressBadgeInfo> Badges);

public static class ProgressCalculator
{
    public static ProgressSnapshot Calculate(ClockStore store, SettingStore settings, DateTime? at = null)
    {
        var now = at ?? DateTime.Now;
        var daily = new Dictionary<DateTime, double>();
        foreach (var session in store.Sessions) daily[session.Start.Date] = daily.GetValueOrDefault(session.Start.Date) + session.Duration;
        if (store.Running is { } running) daily[running.Start.Date] = daily.GetValueOrDefault(running.Start.Date) + running.Elapsed(now);

        var totalDuration = store.AllDuration(now);
        var totalHours = totalDuration / 3600d;
        var dailyGoal = settings.GetDouble("GoalDailyHours");
        var monthlyGoal = settings.GetDouble("GoalMonthlyHours");
        var goalDays = dailyGoal > 0 ? daily.Values.Count(value => value >= dailyGoal * 3600) : 0;
        var doubleGoalDays = dailyGoal > 0 ? daily.Values.Count(value => value >= dailyGoal * 7200) : 0;
        var monthlyGoalDays = monthlyGoal > 0
            ? daily.GroupBy(pair => new DateTime(pair.Key.Year, pair.Key.Month, 1)).Count(group => group.Sum(pair => pair.Value) >= monthlyGoal * 3600)
            : 0;
        var longestStreak = LongestStreak(daily.Keys);
        var currentStreak = CurrentStreak(daily.Keys, now);
        var goalBonus = goalDays * 100 + doubleGoalDays * 250 + monthlyGoalDays * 500;
        var streakBonus = new[] { (3, 100), (7, 250), (14, 500), (30, 1_000), (60, 2_000) }.Where(item => longestStreak >= item.Item1).Sum(item => item.Item2);
        var baseXp = totalHours * 100;
        // Match the Mac implementation: Int(totalHours * 100) truncates fractional XP.
        var xp = Math.Max(0L, (long)baseXp) + goalBonus + streakBonus;
        var level = (int)Math.Clamp(xp / 500 + 1, 1, int.MaxValue);

        var completed = store.Sessions.ToList();
        KeyValuePair<DateTime, double>? bestDay = daily.Count == 0 ? null : daily.MaxBy(pair => pair.Value);
        var weekdayTotals = completed.GroupBy(session => session.Start.DayOfWeek).ToDictionary(group => group.Key, group => group.Sum(session => session.Duration));
        var bestWeekday = weekdayTotals.Count == 0 ? "—" : weekdayTotals.MaxBy(pair => pair.Value).Key.ToString();
        var hourTotals = completed.GroupBy(session => session.Start.Hour).ToDictionary(group => group.Key, group => group.Sum(session => session.Duration));
        var bestHour = hourTotals.Count == 0 ? "—" : $"{hourTotals.MaxBy(pair => pair.Value).Key:00}:00";
        var recentCutoff = now.AddDays(-30);
        var recent = completed.Where(session => session.Start >= recentCutoff).Sum(session => session.Duration);
        var prior = completed.Where(session => session.Start >= now.AddDays(-60) && session.Start < recentCutoff).Sum(session => session.Duration);
        var trend = prior > 0 ? (recent - prior) / prior : recent > 0 ? 1 : 0;
        var earliest = daily.Keys.DefaultIfEmpty(now.Date).Min();
        var days = Math.Max(1, (now.Date - earliest).Days + 1);

        return new ProgressSnapshot(
            now, totalHours, baseXp, goalDays, doubleGoalDays, monthlyGoalDays, goalBonus, longestStreak, currentStreak, streakBonus,
            xp, level, (xp % 500) / 500d, daily.Count, completed.Select(session => session.Duration).DefaultIfEmpty().Max(),
            bestDay?.Key, bestDay?.Value ?? 0, bestWeekday, bestHour,
            completed.Count == 0 ? 0 : completed.Average(session => session.Duration), trend, totalDuration / 3600d / days,
            daily, BuildBadges(store, totalHours, xp, goalDays, doubleGoalDays, monthlyGoalDays, longestStreak, completed, daily.Count));
    }

    private static IReadOnlyList<ProgressBadgeInfo> BuildBadges(ClockStore store, double totalHours, long xp, int goalDays, int doubleGoalDays, int monthlyGoalDays, int longestStreak, IReadOnlyList<WorkSession> sessions, int activeDays)
    {
        var longestSession = sessions.Select(session => session.Duration).DefaultIfEmpty().Max();
        var earlyBird = sessions.Count(session => session.Start.Hour < 8);
        var nightOwl = sessions.Count(session => session.Start.Hour >= 22);
        var weekendDays = sessions.Where(session => session.Start.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday).Select(session => session.Start.Date).Distinct().Count();
        string Current(double value, string target) => $"{value:0.#} / {target}";
        string Count(long value, string target) => $"{value} / {target}";
        return [
            new("first", "First session", "Complete your first session", "🏁", sessions.Count > 0, $"{sessions.Count} sessions"),
            new("ten", "10-hour club", "Work 10 total hours", "⏱", totalHours >= 10, Current(totalHours, "10h")),
            new("fifty", "Half-century", "Work 50 total hours", "🔥", totalHours >= 50, Current(totalHours, "50h")),
            new("hundred", "Century", "Work 100 total hours", "⚡", totalHours >= 100, Current(totalHours, "100h")),
            new("quarter", "Quarter kilo", "Work 250 total hours", "👑", totalHours >= 250, Current(totalHours, "250h")),
            new("fivehundred", "Half-thousand", "Work 500 total hours", "👑", totalHours >= 500, Current(totalHours, "500h")),
            new("sevenfifty", "Three-quarter legend", "Work 750 total hours", "🏅", totalHours >= 750, Current(totalHours, "750h")),
            new("thousand", "Thousand-hour", "Work 1,000 total hours", "🏆", totalHours >= 1_000, Current(totalHours, "1,000h")),
            new("titan", "Time titan", "Work 1,500 total hours", "💎", totalHours >= 1_500, Current(totalHours, "1,500h")),
            new("streak", "On a roll", "Keep a 3-day streak", "🔥", longestStreak >= 3, Count(longestStreak, "3 days")),
            new("weekstreak", "Weekly fire", "Keep a 7-day streak", "📅", longestStreak >= 7, Count(longestStreak, "7 days")),
            new("monthstreak", "Unstoppable", "Keep a 30-day streak", "♾", longestStreak >= 30, Count(longestStreak, "30 days")),
            new("streak14", "Fortnight fire", "Reach a 14-day streak", "✨", longestStreak >= 14, Count(longestStreak, "14 days")),
            new("streak60", "Seasoned", "Reach a 60-day streak", "⛰", longestStreak >= 60, Count(longestStreak, "60 days")),
            new("week", "Weekly finisher", "Log 7 sessions", "🗓", sessions.Count >= 7, Count(sessions.Count, "7 sessions")),
            new("sessions25", "Session collector", "Log 25 sessions", "📚", sessions.Count >= 25, Count(sessions.Count, "25 sessions")),
            new("marathon", "Marathon", "Complete a 4-hour session", "🏃", longestSession >= 4 * 3600, DurationText.Compact(longestSession)),
            new("ultra", "Ultra focus", "Complete an 8-hour session", "⚡", longestSession >= 8 * 3600, DurationText.Compact(longestSession)),
            new("xp", "XP engine", "Earn 10,000 XP", "⭐", xp >= 10_000, Count(xp, "10,000 XP")),
            new("goal", "Goal setter", "Complete a daily goal", "🎯", goalDays >= 1, Count(goalDays, "1 goal day")),
            new("doublegoal", "Double down", "Reach 2× a daily goal", "↗", doubleGoalDays >= 1, Count(doubleGoalDays, "1 double-goal day")),
            new("monthgoal", "Month finisher", "Complete a monthly goal", "🗓", monthlyGoalDays >= 1, Count(monthlyGoalDays, "1 goal month")),
            new("xp25", "Quarter XP", "Earn 25,000 XP", "🏵", xp >= 25_000, Count(xp, "25,000 XP")),
            new("active5", "Getting steady", "Work on 5 different days", "📅", activeDays >= 5, Count(activeDays, "5 active days")),
            new("active25", "Calendar regular", "Work on 25 different days", "✅", activeDays >= 25, Count(activeDays, "25 active days")),
            new("active100", "Daily craft", "Work on 100 different days", "🗓", activeDays >= 100, Count(activeDays, "100 active days")),
            new("earlybird", "Early bird", "Start 5 sessions before 08:00", "🌅", earlyBird >= 5, Count(earlyBird, "5 early starts")),
            new("nightowl", "Night owl", "Start 5 sessions after 22:00", "🌙", nightOwl >= 5, Count(nightOwl, "5 late starts")),
            new("weekend", "Weekend warrior", "Work on 4 weekend days", "☀", weekendDays >= 4, Count(weekendDays, "4 weekend days")),
            new("sessions50", "Deep archive", "Log 50 sessions", "📚", sessions.Count >= 50, Count(sessions.Count, "50 sessions")),
            new("sessions100", "Century sessions", "Log 100 sessions", "🏛", sessions.Count >= 100, Count(sessions.Count, "100 sessions")),
            new("sessions200", "Archive master", "Log 200 sessions", "🗃", sessions.Count >= 200, Count(sessions.Count, "200 sessions")),
            new("sessions500", "Session institution", "Log 500 sessions", "🏢", sessions.Count >= 500, Count(sessions.Count, "500 sessions")),
            new("ultra12", "Iron focus", "Complete a 12-hour session", "⌛", longestSession >= 12 * 3600, DurationText.Compact(longestSession)),
            new("ultra15", "Deep dive", "Complete a 15-hour session", "🌊", longestSession >= 15 * 3600, DurationText.Compact(longestSession)),
            new("goal7", "Goal rhythm", "Complete daily goals on 7 days", "🔖", goalDays >= 7, Count(goalDays, "7 goal days")),
            new("goal30", "Goal machine", "Complete daily goals on 30 days", "🎯", goalDays >= 30, Count(goalDays, "30 goal days")),
            new("month3", "Quarter planner", "Complete 3 monthly goals", "🗓", monthlyGoalDays >= 3, Count(monthlyGoalDays, "3 goal months")),
            new("month12", "Year planner", "Complete 12 monthly goals", "📆", monthlyGoalDays >= 12, Count(monthlyGoalDays, "12 goal months")),
            new("streak90", "Season streak", "Reach a 90-day streak", "🔥", longestStreak >= 90, Count(longestStreak, "90 days")),
            new("streak180", "Half-year fire", "Reach a 180-day streak", "☀", longestStreak >= 180, Count(longestStreak, "180 days")),
            new("streak365", "Year-round", "Reach a 365-day streak", "🌍", longestStreak >= 365, Count(longestStreak, "365 days")),
            new("active250", "Always on", "Work on 250 different days", "📅", activeDays >= 250, Count(activeDays, "250 active days")),
            new("active500", "Permanent practice", "Work on 500 different days", "🗓", activeDays >= 500, Count(activeDays, "500 active days")),
            new("xp50", "XP architect", "Earn 50,000 XP", "🌟", xp >= 50_000, Count(xp, "50,000 XP")),
            new("xp100", "XP legend", "Earn 100,000 XP", "✨", xp >= 100_000, Count(xp, "100,000 XP"))
        ];
    }

    public static int LongestStreak(IEnumerable<DateTime> dates)
    {
        var values = dates.Select(date => date.Date).Distinct().OrderBy(date => date).ToArray();
        if (values.Length == 0) return 0;
        var current = 1; var best = 1;
        for (var i = 1; i < values.Length; i++)
        {
            current = (values[i] - values[i - 1]).TotalDays == 1 ? current + 1 : 1;
            best = Math.Max(best, current);
        }
        return best;
    }

    public static int CurrentStreak(IEnumerable<DateTime> dates, DateTime now)
    {
        var available = dates.Select(date => date.Date).ToHashSet();
        var cursor = now.Date;
        if (!available.Contains(cursor)) cursor = cursor.AddDays(-1);
        var count = 0;
        while (available.Contains(cursor)) { count++; cursor = cursor.AddDays(-1); }
        return count;
    }
}
