using System.Globalization;

namespace Piggy3.Shared;

public static class Money
{
    private const NumberStyles Styles =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public static bool TryParse(string? input, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var text = input.Trim()
            .Replace("€", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("\u00A0", string.Empty);

        if (text.Contains(',') && text.Contains('.'))
            return false;

        text = text.Replace(',', '.');
        if (text.Count(c => c == '.') > 1)
            return false;

        var digits = text.TrimStart('+', '-');
        if (digits.StartsWith('.') || digits.EndsWith('.'))
            return false;

        if (!decimal.TryParse(text, Styles, CultureInfo.InvariantCulture, out var parsed))
            return false;

        amount = Round(parsed);
        return true;
    }

    public static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static long Cents(decimal value) =>
        (long)Math.Round(value * 100m, MidpointRounding.AwayFromZero);

    public static string Format(decimal amount) =>
        amount.ToString("N2", CultureInfo.GetCultureInfo("de-DE")) + " €";
}
