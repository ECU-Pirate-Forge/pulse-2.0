namespace Pulse.Shared.Models;

public static class ChoiceLabelFormatter
{
    public static string ForIndex(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        var label = string.Empty;
        do
        {
            label = (char)('A' + index % 26) + label;
            index = index / 26 - 1;
        } while (index >= 0);

        return label;
    }
}
