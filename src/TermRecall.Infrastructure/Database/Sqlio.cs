using System.Globalization;
using Microsoft.Data.Sqlite;

namespace TermRecall.Infrastructure.Database;

internal static class Sqlio
{
    public static string GuidText(Guid id) => id.ToString("D");

    public static string Time(DateTime value) => ReviewUtc(value).ToString("O", CultureInfo.InvariantCulture);

    public static DateTime ReviewUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public static DateTime ReadTime(SqliteDataReader reader, int ordinal)
        => DateTime.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static DateTime? ReadTimeOrNull(SqliteDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : ReadTime(reader, ordinal);

    public static Guid ReadGuid(SqliteDataReader reader, int ordinal)
        => Guid.Parse(reader.GetString(ordinal));

    public static Guid? ReadGuidOrNull(SqliteDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : Guid.Parse(reader.GetString(ordinal));

    public static object Db(string? value) => string.IsNullOrEmpty(value) ? value ?? string.Empty : value;

    public static object Db(DateTime? value) => value is null ? DBNull.Value : Time(value.Value);

    public static object Db(Guid? value) => value is null ? DBNull.Value : GuidText(value.Value);

    public static object Db(int? value) => value is null ? DBNull.Value : value.Value;
}
