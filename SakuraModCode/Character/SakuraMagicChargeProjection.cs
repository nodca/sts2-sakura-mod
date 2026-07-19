using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Character;

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

        return SakuraMagicCharge.BandFor(amount) switch
        {
            SakuraMagicChargeBand.Low => new(amount, SakuraMagicChargeHudState.Low),
            SakuraMagicChargeBand.Resonant when hasOpportunity =>
                new(amount, SakuraMagicChargeHudState.ResonantReady),
            SakuraMagicChargeBand.Resonant =>
                new(amount, SakuraMagicChargeHudState.ResonantSpent),
            SakuraMagicChargeBand.Full when canActivateExtra =>
                new(amount, SakuraMagicChargeHudState.FullReady),
            SakuraMagicChargeBand.Full =>
                new(amount, SakuraMagicChargeHudState.FullLocked),
            _ => throw new ArgumentOutOfRangeException(nameof(amount)),
        };
    }
}
