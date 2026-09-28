namespace FindIt.Api.Services;

public static class BusinessDayDeadline
{
    private static readonly TimeZoneInfo Brasilia =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static DateTime FiveBusinessDaysAfter(DateTime approvedAtUtc)
    {
        if (approvedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("O instante de aprovação deve estar em UTC.", nameof(approvedAtUtc));
        }

        var localDate = TimeZoneInfo.ConvertTimeFromUtc(approvedAtUtc, Brasilia).Date;
        var businessDays = 0;
        while (businessDays < 5)
        {
            localDate = localDate.AddDays(1);
            if (localDate.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                businessDays++;
            }
        }

        // A aprovação vale até o fim do quinto dia útil no horário de Brasília.
        var endOfDay = DateTime.SpecifyKind(localDate.AddDays(1), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(endOfDay, Brasilia);
    }
}
