namespace TermRecall.Domain.Algorithms;

/// <summary>
/// Перевод интервала SM-2 в календарную дату.
/// Момент повторения хранится в UTC. Срок наступает в местную полночь даты,
/// полученной прибавлением целого числа дней к местной дате ответа.
/// Пропуск дней не увеличивает интервал и не помечает термин изученным.
/// </summary>
public static class ReviewCalendar
{
    public static DateTime NextReviewUtc(DateTime reviewedAtUtc, int intervalDays, TimeZoneInfo timeZone)
    {
        if (intervalDays < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalDays));
        }

        var reviewed = EnsureUtc(reviewedAtUtc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(reviewed, timeZone);
        var dueLocal = DateTime.SpecifyKind(local.Date.AddDays(intervalDays), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(dueLocal, timeZone);
    }

    public static DateOnly LocalDate(DateTime utc, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(utc), timeZone);
        return DateOnly.FromDateTime(local);
    }

    public static bool IsDue(DateTime? nextReviewUtc, DateTime nowUtc, TimeZoneInfo timeZone)
    {
        if (nextReviewUtc is null)
        {
            return false;
        }

        return LocalDate(nextReviewUtc.Value, timeZone) <= LocalDate(nowUtc, timeZone);
    }

    public static bool IsOverdue(DateTime? nextReviewUtc, DateTime nowUtc, TimeZoneInfo timeZone)
    {
        if (nextReviewUtc is null)
        {
            return false;
        }

        return LocalDate(nextReviewUtc.Value, timeZone) < LocalDate(nowUtc, timeZone);
    }

    public static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
