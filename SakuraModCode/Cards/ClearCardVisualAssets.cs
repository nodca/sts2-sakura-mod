using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Extensions;
using System.Text;

namespace SakuraMod.SakuraModCode.Cards;

internal static class ClearCardVisualAssets
{
    public static string ArtPath(Type cardType) =>
        ArtFileName(cardType).ClearCardAssetPath();

    public static string EnglishName(CardModel card) =>
        card is SakuraOptionCard optionCard
            ? optionCard.EnglishName
            : Path.GetFileNameWithoutExtension(ArtFileName(card.GetType())).Replace('_', ' ');

    private static string ArtFileName(Type cardType)
    {
        var name = cardType.Name;
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var character = name[i];
            if (i > 0 && char.IsUpper(character) && char.IsLower(name[i - 1]))
                builder.Append('_');

            builder.Append(char.ToUpperInvariant(character));
        }

        builder.Append(".png");
        return builder.ToString();
    }
}
