using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.addons.mega_text;
using SakuraMod.SakuraModCode.Cards;
using STS2RitsuLib.Patching;
using System.Reflection;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraVanillaCardVisualRestorer
{
    private static readonly FieldInfo? EnergyIconField = PrivateAccess.DeclaredField(typeof(NCard), "_energyIcon");
    private static readonly FieldInfo? EnergyLabelField = PrivateAccess.DeclaredField(typeof(NCard), "_energyLabel");

    public static void RestoreCurrentModelCostIfVanillaRoute(NCard card)
    {
        if (!ShouldRestoreCurrentModelCost(card.Model))
            return;

        RestoreCurrentModelCost(card, card.Model!);
    }

    private static bool ShouldRestoreCurrentModelCost(CardModel? model) =>
        model is not null
        && SakuraCardVisualFamilies.Layout(model) == SakuraCardVisualLayout.None;

    private static void RestoreCurrentModelCost(NCard card, CardModel model)
    {
        RestoreEnergyIcon(FieldValue<TextureRect>(EnergyIconField, card), model.EnergyIcon);
        RestoreEnergyLabel(FieldValue<MegaLabel>(EnergyLabelField, card));
    }

    private static void RestoreEnergyIcon(TextureRect? icon, Texture2D? texture)
    {
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(icon))
            return;

        SakuraCardVisualInfrastructure.SetTextureIfDifferent(icon, texture);
    }

    private static void RestoreEnergyLabel(MegaLabel? label)
    {
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(label))
            return;

        if (!label!.Visible)
            label.Visible = true;
        if (label.Modulate != Colors.White)
            label.Modulate = Colors.White;
        if (label.SelfModulate != Colors.White)
            label.SelfModulate = Colors.White;
    }

    private static T? FieldValue<T>(FieldInfo? field, object instance)
        where T : class =>
        field?.GetValue(instance) as T;
}
