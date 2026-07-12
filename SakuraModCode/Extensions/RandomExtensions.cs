using MegaCrit.Sts2.Core.Random;

namespace SakuraMod.SakuraModCode.Extensions;

public static class RandomExtensions
{
    public static T? NextItem<T>(this Rng rng, IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(items);

        return items.Count == 0 ? default : items[rng.NextInt(items.Count)];
    }

    public static T? NextItem<T>(this Rng rng, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(items);

        var list = items as IReadOnlyList<T> ?? items.ToList();
        return rng.NextItem(list);
    }
}
