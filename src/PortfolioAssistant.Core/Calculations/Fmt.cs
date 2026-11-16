using System.Globalization;

namespace PortfolioAssistant.Core.Calculations;

/// <summary>
/// עיצוב מספרים ותאריכים במקום אחד. הכלים מחזירים מספרים כבר מעוצבים,
/// כדי שהמודל רק יעתיק ולא יעגל או ינסח מספרים בעצמו.
/// </summary>
public static class Fmt
{
    private static readonly CultureInfo C = CultureInfo.InvariantCulture;

    public static string Usd(decimal value) => PortfolioCalculator.Round2(value).ToString("#,0.##", C);
    public static string Percent(decimal value) => PortfolioCalculator.Round2(value).ToString("0.##", C) + "%";
    public static string Quantity(decimal value) => value.ToString("#,0.########", C);
    public static string Date(DateTimeOffset value) => value.UtcDateTime.ToString("dd.MM.yyyy", C);
    public static string Date(DateOnly value) => value.ToString("dd.MM.yyyy", C);
    public static string DateTime(DateTimeOffset value) => value.UtcDateTime.ToString("dd.MM.yyyy HH:mm", C) + " UTC";
}
