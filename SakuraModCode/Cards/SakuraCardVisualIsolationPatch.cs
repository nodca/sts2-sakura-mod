using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Classic.Cards;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Cards;

[HarmonyPatch(typeof(NCard))]
internal static class SakuraCardVisualIsolationPatch
{
    [HarmonyPriority(Priority.First)]
    [HarmonyPrefix]
    [HarmonyPatch(nameof(NCard.UpdateVisuals))]
    public static void UpdateVisualsPrefix(NCard __instance)
    {
        SakuraCardVisualDiagnostics.LogUpdateVisuals("before", __instance);

        var family = SakuraCardVisualFamilies.Family(__instance);

        if (family != SakuraCardVisualFamily.Clear)
            ClearCardLayout.RestoreCardIfTracked(__instance);
        if (family != SakuraCardVisualFamily.Classic)
            ClassicSakuraCardLayout.RestoreCardIfTracked(__instance);

        if (family is not SakuraCardVisualFamily.Clear and not SakuraCardVisualFamily.Classic)
            SakuraNonClearFrameApplier.RestoreTrackedAndCurrentModelVisuals(__instance);

        SakuraCardVisualDiagnostics.LogUpdateVisuals("after-isolation", __instance);
    }

    [HarmonyPriority(Priority.Last)]
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCard.UpdateVisuals))]
    public static void UpdateVisualsPostfix(NCard __instance)
    {
        SakuraCardVisualDiagnostics.LogUpdateVisuals("after", __instance);
    }
}

internal static class SakuraCardVisualDiagnostics
{
    public const string EnabledEnvironmentVariable = "SAKURA_CARD_VISUAL_DIAGNOSTICS";

    private static readonly FieldInfo? PortraitField = AccessTools.Field(typeof(NCard), "_portrait");
    private static readonly FieldInfo? AncientPortraitField = AccessTools.Field(typeof(NCard), "_ancientPortrait");
    private static readonly FieldInfo? FrameField = AccessTools.Field(typeof(NCard), "_frame");
    private static readonly FieldInfo? AncientBorderField = AccessTools.Field(typeof(NCard), "_ancientBorder");
    private static readonly FieldInfo? BannerField = AccessTools.Field(typeof(NCard), "_banner");
    private static readonly FieldInfo? EnergyIconField = AccessTools.Field(typeof(NCard), "_energyIcon");
    private static readonly FieldInfo? UnplayableEnergyIconField = AccessTools.Field(typeof(NCard), "_unplayableEnergyIcon");

    private static readonly ConditionalWeakTable<NCard, CardVisualTraceState> States = new();
    private static readonly string? EnabledEnvironmentValue = System.Environment.GetEnvironmentVariable(EnabledEnvironmentVariable);
    private static readonly bool LogAllUpdates = EnabledEnvironmentValue?.Equals("all", StringComparison.OrdinalIgnoreCase) == true;

    public static bool Enabled { get; set; } = IsEnabledValue(EnabledEnvironmentValue);

    public static void LogUpdateVisuals(string stage, NCard card)
    {
        if (!Enabled || card.Model is not { } model)
            return;

        var family = SakuraCardVisualFamilies.Family(model);
        var snapshot = CardVisualSnapshot.Capture(stage, card, model, family);
        var state = States.GetOrCreateValue(card);

        if (LogAllUpdates || snapshot.IsSuspicious || state.WasSuspicious || state.Family != family)
        {
            MainFile.Logger.Info(snapshot.ToLogString(state.Family, state.LastStage));
            state.WasSuspicious = snapshot.IsSuspicious;
            state.Family = family;
            state.LastStage = stage;
        }
    }

    private static bool IsEnabledValue(string? value) =>
        value is not null
        && (value == "1"
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase)
            || value.Equals("all", StringComparison.OrdinalIgnoreCase));

    private static TextureRect? TextureRectField(FieldInfo? field, NCard card) =>
        field?.GetValue(card) as TextureRect;

    private static string TextureInfo(TextureRect? rect)
    {
        if (!IsGodotInstanceUsable(rect))
            return "null";

        var texture = rect!.Texture;
        return string.Join(
            ':',
            rect.Name,
            $"visible={rect.Visible}",
            $"texture={ResourceInfo(texture)}",
            $"material={ResourceInfo(rect.Material)}");
    }

    private static string ResourceInfo(Resource? resource)
    {
        if (!IsGodotInstanceUsable(resource))
            return "null";

        var path = resource!.ResourcePath;
        return string.IsNullOrEmpty(path)
            ? $"{resource.GetType().Name}@{resource.GetInstanceId()}"
            : path;
    }

    private static bool IsSuspiciousNonTargetVisual(SakuraCardVisualFamily family, params string[] values)
    {
        if (family is SakuraCardVisualFamily.Clear or SakuraCardVisualFamily.Classic)
            return false;

        return values.Any(IsSakuraVisualValue);
    }

    private static bool IsSakuraVisualValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Contains("Sakura", StringComparison.OrdinalIgnoreCase)
            || value.Contains("sakura", StringComparison.OrdinalIgnoreCase)
            || value.Contains("clear", StringComparison.OrdinalIgnoreCase)
            || value.Contains("classic", StringComparison.OrdinalIgnoreCase)
            || value.Contains("clow", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGodotInstanceUsable(GodotObject? instance)
    {
        try
        {
            return instance is not null
                && GodotObject.IsInstanceValid(instance)
                && (instance is not Node node || !node.IsQueuedForDeletion());
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private sealed class CardVisualTraceState
    {
        public SakuraCardVisualFamily Family { get; set; }
        public string? LastStage { get; set; }
        public bool WasSuspicious { get; set; }
    }

    private readonly record struct CardVisualSnapshot(
        string Stage,
        int NodeId,
        string NodeName,
        string CardId,
        string ModelType,
        SakuraCardVisualFamily Family,
        string PortraitPath,
        string CustomPortraitPath,
        string BetaPortraitPath,
        string PoolType,
        string VisualPoolType,
        string ModelPortrait,
        string ModelFrame,
        string ModelEnergyIcon,
        string PortraitNode,
        string AncientPortraitNode,
        string FrameNode,
        string AncientBorderNode,
        string BannerNode,
        string EnergyIconNode,
        string UnplayableEnergyIconNode,
        string BodyChildren,
        bool IsSuspicious)
    {
        public static CardVisualSnapshot Capture(
            string stage,
            NCard card,
            CardModel model,
            SakuraCardVisualFamily family)
        {
            var portraitNode = TextureInfo(TextureRectField(PortraitField, card));
            var ancientPortraitNode = TextureInfo(TextureRectField(AncientPortraitField, card));
            var frameNode = TextureInfo(TextureRectField(FrameField, card));
            var ancientBorderNode = TextureInfo(TextureRectField(AncientBorderField, card));
            var bannerNode = TextureInfo(TextureRectField(BannerField, card));
            var energyIconNode = TextureInfo(TextureRectField(EnergyIconField, card));
            var unplayableEnergyIconNode = TextureInfo(TextureRectField(UnplayableEnergyIconField, card));
            var bodyChildren = BodyChildrenInfo(card);
            var poolType = model.Pool.GetType().FullName ?? model.Pool.GetType().Name;
            var visualPoolType = model.VisualCardPool.GetType().FullName ?? model.VisualCardPool.GetType().Name;
            var modelPortrait = ResourceInfo(model.Portrait);
            var modelFrame = ResourceInfo(model.Frame);
            var modelEnergyIcon = ResourceInfo(model.EnergyIcon);
            var customPortraitPath = StringProperty(model, "CustomPortraitPath");

            var suspicious = IsSuspiciousNonTargetVisual(
                family,
                model.PortraitPath,
                customPortraitPath,
                model.BetaPortraitPath,
                poolType,
                visualPoolType,
                modelPortrait,
                modelFrame,
                modelEnergyIcon,
                portraitNode,
                ancientPortraitNode,
                frameNode,
                ancientBorderNode,
                bannerNode,
                energyIconNode,
                unplayableEnergyIconNode,
                bodyChildren);

            return new CardVisualSnapshot(
                stage,
                (int)card.GetInstanceId(),
                card.Name,
                model.Id.ToString(),
                model.GetType().FullName ?? model.GetType().Name,
                family,
                model.PortraitPath,
                customPortraitPath,
                model.BetaPortraitPath,
                poolType,
                visualPoolType,
                modelPortrait,
                modelFrame,
                modelEnergyIcon,
                portraitNode,
                ancientPortraitNode,
                frameNode,
                ancientBorderNode,
                bannerNode,
                energyIconNode,
                unplayableEnergyIconNode,
                bodyChildren,
                suspicious);
        }

        private static string StringProperty(CardModel model, string propertyName)
        {
            var property = model.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property?.GetValue(model) as string ?? "<unavailable>";
        }

        private static string BodyChildrenInfo(NCard card)
        {
            if (!IsGodotInstanceUsable(card.Body))
                return "null";

            var children = new List<string>();
            foreach (var child in card.Body.GetChildren())
            {
                if (child is not Node node || !IsGodotInstanceUsable(node))
                    continue;
                if (!IsInterestingBodyChild(node))
                    continue;

                children.Add(NodeInfo(node));
            }

            return children.Count == 0 ? "none" : string.Join('|', children);
        }

        private static bool IsInterestingBodyChild(Node node) =>
            IsSakuraVisualValue(node.Name)
            || node is TextureRect textureRect && IsSakuraVisualValue(ResourceInfo(textureRect.Texture))
            || node is CanvasItem canvasItem && (IsSakuraVisualValue(ResourceInfo(canvasItem.Material)) || !canvasItem.Visible);

        private static string NodeInfo(Node node)
        {
            var info = $"{node.Name}:{node.GetType().Name}";
            if (node is CanvasItem canvasItem)
                info += $":visible={canvasItem.Visible}:z={canvasItem.ZIndex}:material={ResourceInfo(canvasItem.Material)}";
            if (node is Control control)
                info += $":pos={control.Position}:size={control.Size}:scale={control.Scale}";
            if (node is TextureRect textureRect)
                info += $":texture={ResourceInfo(textureRect.Texture)}";

            return info;
        }

        public string ToLogString(SakuraCardVisualFamily previousFamily, string? previousStage) =>
            string.Join(
                ' ',
                $"CardVisualDiagnostics stage={Stage}",
                $"previousStage={previousStage ?? "none"}",
                $"nodeId={NodeId}",
                $"nodeName={NodeName}",
                $"cardId={CardId}",
                $"modelType={ModelType}",
                $"family={Family}",
                $"previousFamily={previousFamily}",
                $"suspicious={IsSuspicious}",
                $"portraitPath={PortraitPath}",
                $"customPortraitPath={CustomPortraitPath}",
                $"betaPortraitPath={BetaPortraitPath}",
                $"pool={PoolType}",
                $"visualPool={VisualPoolType}",
                $"modelPortrait={ModelPortrait}",
                $"modelFrame={ModelFrame}",
                $"modelEnergyIcon={ModelEnergyIcon}",
                $"portraitNode={PortraitNode}",
                $"ancientPortraitNode={AncientPortraitNode}",
                $"frameNode={FrameNode}",
                $"ancientBorderNode={AncientBorderNode}",
                $"bannerNode={BannerNode}",
                $"energyIconNode={EnergyIconNode}",
                $"unplayableEnergyIconNode={UnplayableEnergyIconNode}",
                $"bodyChildren={BodyChildren}");
    }
}
