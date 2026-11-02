using System.Globalization;

namespace PenielPlaceMinistriesCSharp.ViewModels;

/// <summary>Display helpers so every screen formats money, dates and percentages the same way.</summary>
public static class Fmt
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Money(decimal? value) =>
        value.HasValue ? "R " + value.Value.ToString("N2", Inv) : "-";

    public static string Date(DateTime? value) =>
        value.HasValue ? value.Value.ToString("d MMM yyyy", Inv) : "-";

    public static string Month(DateTime value) => value.ToString("MMM yyyy", Inv);

    /// <summary>Whole-number percentage (0-100) used for bar widths. Never divides by zero.</summary>
    public static int Pct(decimal part, decimal whole) =>
        whole <= 0 ? 0 : (int)Math.Round(part / whole * 100m, MidpointRounding.AwayFromZero);
}
