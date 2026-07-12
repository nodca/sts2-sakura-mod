using System.Text;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.Events;

public abstract class SakuraModEventTemplate : ModEventTemplate
{
    protected EventOption Option(Func<Task> onChosen, params IHoverTip[] tips) =>
        new(this, onChosen, InitialOptionKey(OptionKeyFromDelegate(onChosen)), tips);

    protected EventOption Option(Func<Task> onChosen, IEnumerable<IHoverTip> tips) =>
        new(this, onChosen, InitialOptionKey(OptionKeyFromDelegate(onChosen)), tips);

    protected EventOption LockedOption(string optionKey, params IHoverTip[] tips) =>
        new(this, null, InitialOptionKey(optionKey), tips);

    protected EventOption LockedOption(string optionKey, IEnumerable<IHoverTip> tips) =>
        new(this, null, InitialOptionKey(optionKey), tips);

    private static string OptionKeyFromDelegate(Delegate optionHandler) =>
        ToUpperSnakeCase(optionHandler.Method.Name);

    private static string ToUpperSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (i > 0 && char.IsUpper(current) && !char.IsUpper(name[i - 1]))
                builder.Append('_');

            builder.Append(char.ToUpperInvariant(current));
        }

        return builder.ToString();
    }
}
