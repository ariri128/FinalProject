public class NumberFormatter
{
    public static string Format(float value)
    {
        if (value >= 1_000_000_000_000f)
            return (value / 1_000_000_000_000f).ToString("F2") + "T";
        if (value >= 1_000_000_000f)
            return (value / 1_000_000_000f).ToString("F2") + "B";
        if (value >= 1_000_000f)
            return (value / 1_000_000f).ToString("F2") + "M";
        if (value >= 1_000f)
            return (value / 1_000f).ToString("F2") + "K";

        return value.ToString("F1");
    }
}
