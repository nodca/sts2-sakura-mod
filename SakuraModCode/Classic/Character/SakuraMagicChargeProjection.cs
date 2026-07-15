using SakuraMod.SakuraModCode.Classic.Cards;

namespace SakuraMod.SakuraModCode.Classic.Character;

internal enum SakuraMagicChargeHudState
{
    Zero,
    Low,
    ResonantReady,
    ResonantSpent,
    FullReady,
    FullLocked,
}

internal readonly record struct SakuraMagicChargeProjection(
    int Amount,
    SakuraMagicChargeHudState State)
{
    internal static SakuraMagicChargeProjection From(
        int amount,
        bool hasOpportunity,
        bool canActivateExtra)
    {
        amount = Math.Max(0, amount);
        if (amount == 0)
            return new(amount, SakuraMagicChargeHudState.Zero);

        return ClassicSakuraMagic.BandFor(amount) switch
        {
            ClassicMagicChargeBand.Low => new(amount, SakuraMagicChargeHudState.Low),
            ClassicMagicChargeBand.Resonant when hasOpportunity =>
                new(amount, SakuraMagicChargeHudState.ResonantReady),
            ClassicMagicChargeBand.Resonant =>
                new(amount, SakuraMagicChargeHudState.ResonantSpent),
            ClassicMagicChargeBand.Full when canActivateExtra =>
                new(amount, SakuraMagicChargeHudState.FullReady),
            ClassicMagicChargeBand.Full =>
                new(amount, SakuraMagicChargeHudState.FullLocked),
            _ => throw new ArgumentOutOfRangeException(nameof(amount)),
        };
    }
}
