using System.Buffers.Binary;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Relics;
using STS2RitsuLib.Networking.ManagedActions;

namespace SakuraMod.SakuraModCode.Character;

internal readonly record struct SealedWandChargeRecipient(ulong PlayerNetId, int Amount);

internal readonly record struct SealedWandChargeActionPayload(
    uint CombatId,
    SealedWandChargeRecipient[] Recipients);

internal static class SakuraSealedWandChargeAction
{
    private const string ActionKey = "sealed_wand_charge";
    private const string DeferredActionKey = "sealed_wand_charge_player_turn";
    private const int HeaderBytes = sizeof(uint) + sizeof(int);
    private const int RecipientBytes = sizeof(ulong) + sizeof(int);

    internal static readonly RitsuLibManagedNetActionDescriptor<SealedWandChargeActionPayload> Descriptor =
        CreateDescriptor(ActionKey, GameActionType.Any, applyDeferredTurnCharge: false);

    internal static readonly RitsuLibManagedNetActionDescriptor<SealedWandChargeActionPayload> DeferredDescriptor =
        CreateDescriptor(DeferredActionKey, GameActionType.CombatPlayPhaseOnly, applyDeferredTurnCharge: true);

    private static RitsuLibManagedNetActionDescriptor<SealedWandChargeActionPayload> CreateDescriptor(
        string actionKey,
        GameActionType actionType,
        bool applyDeferredTurnCharge) =>
        new(
            MainFile.ModId,
            actionKey,
            Serialize,
            Deserialize,
            async context =>
            {
                var runState = SakuraRunHooks.ActiveRunState ?? context.Player.RunState;
                if (applyDeferredTurnCharge)
                    await ApplyDeferred(runState, context.Message);
                else
                    Apply(runState, context.Message);
            },
            actionType);

    internal static byte[] Serialize(SealedWandChargeActionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload.Recipients);
        var bytes = new byte[HeaderBytes + checked(payload.Recipients.Length * RecipientBytes)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0, sizeof(uint)), payload.CombatId);
        BinaryPrimitives.WriteInt32LittleEndian(
            bytes.AsSpan(sizeof(uint), sizeof(int)),
            payload.Recipients.Length);
        var offset = HeaderBytes;
        foreach (var recipient in payload.Recipients)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset, sizeof(ulong)), recipient.PlayerNetId);
            BinaryPrimitives.WriteInt32LittleEndian(
                bytes.AsSpan(offset + sizeof(ulong), sizeof(int)),
                recipient.Amount);
            offset += RecipientBytes;
        }

        return bytes;
    }

    internal static SealedWandChargeActionPayload Deserialize(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderBytes)
            throw new InvalidDataException("Sealed Wand charge action payload is truncated.");

        var combatId = BinaryPrimitives.ReadUInt32LittleEndian(bytes[..sizeof(uint)]);
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(sizeof(uint), sizeof(int)));
        int expectedLength;
        try
        {
            expectedLength = checked(HeaderBytes + checked(count * RecipientBytes));
        }
        catch (OverflowException ex)
        {
            throw new InvalidDataException("Sealed Wand charge action payload is too large.", ex);
        }

        if (count < 0 || bytes.Length != expectedLength)
            throw new InvalidDataException("Sealed Wand charge action payload has an invalid recipient count.");

        var recipients = new SealedWandChargeRecipient[count];
        var offset = HeaderBytes;
        for (var index = 0; index < count; index++)
        {
            recipients[index] = new SealedWandChargeRecipient(
                BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(offset, sizeof(ulong))),
                BinaryPrimitives.ReadInt32LittleEndian(
                    bytes.Slice(offset + sizeof(ulong), sizeof(int))));
            offset += RecipientBytes;
        }

        return new SealedWandChargeActionPayload(combatId, recipients);
    }

    internal static void Apply(IRunState runState, SealedWandChargeActionPayload payload)
    {
        foreach (var recipient in payload.Recipients)
        {
            var wand = ResolveWand(runState, recipient.PlayerNetId);
            wand.ApplySynchronizedCharge(payload.CombatId, recipient.Amount);
        }
    }

    private static async Task ApplyDeferred(IRunState runState, SealedWandChargeActionPayload payload)
    {
        foreach (var recipient in payload.Recipients)
        {
            var wand = ResolveWand(runState, recipient.PlayerNetId);
            await wand.ApplyDeferredSynchronizedCharge(payload.CombatId, recipient.Amount);
        }
    }

    private static ClassicSealedWandRelic ResolveWand(IRunState runState, ulong playerNetId)
    {
        var player = runState.GetPlayer(playerNetId)
            ?? throw new InvalidOperationException(
                $"Sealed Wand charge recipient player {playerNetId} is missing.");
        return player.GetRelic<ClassicSealedWandRelic>()
            ?? throw new InvalidOperationException(
                $"Sealed Wand charge recipient player {playerNetId} has no Sealed Wand.");
    }
}
