using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Character;

internal enum SakuraMagicChargeHudState
{
    Zero,
    Low,
    FullReady,
    FullLocked,
}

internal readonly record struct SakuraMagicChargeProjection(
    int Amount,
    SakuraMagicChargeHudState State)
{
    internal static SakuraMagicChargeProjection From(int amount, bool canActivateExtra)
    {
        amount = Math.Max(0, amount);
        var state = amount switch
        {
            0 => SakuraMagicChargeHudState.Zero,
            < SakuraMagicCharge.ExtraEffectCost => SakuraMagicChargeHudState.Low,
            _ when canActivateExtra => SakuraMagicChargeHudState.FullReady,
            _ => SakuraMagicChargeHudState.FullLocked,
        };
        return new(amount, state);
    }
}
