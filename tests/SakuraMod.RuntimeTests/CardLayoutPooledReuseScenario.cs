using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Pooling;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.TestProtocol;
using System.Globalization;
using System.Reflection;

namespace SakuraMod.RuntimeTests;

/// <summary>
/// Cycles one pooled <see cref="NCard"/> through Clear, Classic, and vanilla models and
/// requires every step to render exactly like a card node that only ever showed that model.
/// </summary>
internal static class CardLayoutPooledReuseScenario
{
    private const int MaxReportedDifferences = 12;

    private static readonly StringName[] ThemeColorNames =
    [
        new("font_color"),
        new("default_color"),
        new("font_outline_color"),
        new("font_shadow_color")
    ];

    private static readonly StringName[] ThemeConstantNames =
    [
        new("outline_size"),
        new("shadow_offset_x"),
        new("shadow_offset_y"),
        new("shadow_outline_size")
    ];

    private static readonly StringName[] ThemeFontSizeNames =
    [
        new("font_size"),
        new("normal_font_size")
    ];

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;

        var clear = combat.CreateCard<Repair>(player);
        var classic = combat.CreateCard<ClowShield>(player);
        var vanilla = combat.CreateCard<DefendIronclad>(player);
        (string Label, CardModel Model)[] sequence =
        [
            ("clear", clear),
            ("classic", classic),
            ("vanilla", vanilla),
            ("clear_again", clear),
            ("vanilla_after_clear", vanilla),
            ("classic_after_vanilla", classic),
            ("clear_after_classic", clear),
            ("vanilla_final", vanilla)
        ];

        var cardSteps = await CycleBareCard(sequence, assertions);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "card_layout_pooled_card_cycled",
            "One pooled NCard matched a single-model reference after every layout change.");

        var holderSteps = await CycleGridHolder(sequence, assertions);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "card_layout_pooled_holder_cycled",
            "One pooled grid holder matched a single-model reference after every layout change.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["card_steps"] = cardSteps,
            ["holder_steps"] = holderSteps
        };
    }

    private static async Task<Dictionary<string, object>> CycleBareCard(
        (string Label, CardModel Model)[] sequence,
        RuntimeAssertionCollector assertions)
    {
        var results = new Dictionary<string, object>(StringComparer.Ordinal);
        NCard? pooled = null;
        try
        {
            pooled = CreateAttachedCard(sequence[0].Model);
            foreach (var (label, model) in sequence)
            {
                pooled.Model = model;
                pooled.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
                await Settle(pooled);

                var reference = CreateFreshAttachedCard(model);
                try
                {
                    await Settle(reference);
                    AssertSakuraFace($"card_{label}", label, pooled, assertions);
                    results[label] = Compare($"card_{label}", Snapshot(reference), Snapshot(pooled), assertions);
                }
                finally
                {
                    FreeFresh(reference);
                }
            }
        }
        finally
        {
            ReleaseCard(pooled);
        }

        return results;
    }

    private static async Task<Dictionary<string, object>> CycleGridHolder(
        (string Label, CardModel Model)[] sequence,
        RuntimeAssertionCollector assertions)
    {
        var results = new Dictionary<string, object>(StringComparer.Ordinal);
        NGridCardHolder? pooled = null;
        try
        {
            pooled = CreateAttachedHolder(sequence[0].Model);
            foreach (var (label, model) in sequence)
            {
                var card = pooled.CardNode
                    ?? throw new InvalidOperationException("Grid holder lost its card node.");
                card.Model = model;
                card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
                await Settle(card);

                var reference = CreateFreshAttachedHolder(model);
                try
                {
                    await Settle(reference);
                    AssertSakuraFace($"holder_{label}", label, card, assertions);
                    results[label] = Compare($"holder_{label}", Snapshot(reference), Snapshot(pooled), assertions);
                }
                finally
                {
                    FreeFresh(reference);
                }
            }
        }
        finally
        {
            ReleaseHolder(pooled);
        }

        return results;
    }

    /// <summary>
    /// Both sides go through the same UpdateVisuals hooks, so a Sakura layout that never ran
    /// would still match; require the step's own face node to be visible with a texture.
    /// </summary>
    private static void AssertSakuraFace(string step, string label, NCard card, RuntimeAssertionCollector assertions)
    {
        var expected = label.StartsWith("clear", StringComparison.Ordinal) ? ClearCardLayout.ArtNodeName
            : label.StartsWith("classic", StringComparison.Ordinal) ? ClassicCardLayout.FaceNodeName
            : null;
        foreach (var name in new[] { ClearCardLayout.ArtNodeName, ClassicCardLayout.FaceNodeName })
        {
            var face = card.FindChild(name, recursive: true, owned: false) as TextureRect;
            var shown = face is { } rect && rect.IsVisibleInTree() && rect.Texture is not null;
            assertions.Equal($"{step}_{name}_shown", name == expected, shown);
        }
    }

    /// <summary>
    /// Nodes present only on the pooled side are Sakura-owned leftovers; they may stay as
    /// children but must not be visible. Every shared node must match the reference exactly.
    /// </summary>
    private static object Compare(
        string step,
        IReadOnlyDictionary<string, string> reference,
        IReadOnlyDictionary<string, string> pooled,
        RuntimeAssertionCollector assertions)
    {
        var differences = new List<string>();
        foreach (var (key, expected) in reference)
        {
            if (!pooled.TryGetValue(key, out var actual))
                differences.Add($"{key}: missing on pooled node (expected {expected})");
            else if (!string.Equals(expected, actual, StringComparison.Ordinal))
                differences.Add($"{key}: expected {expected} but was {actual}");
        }

        var leakedVisible = pooled
            .Where(entry => !reference.ContainsKey(entry.Key)
                && entry.Key.EndsWith("#visible", StringComparison.Ordinal)
                && entry.Value == "True")
            .Select(entry => $"{entry.Key}: extra visible node")
            .ToList();
        differences.AddRange(leakedVisible);

        assertions.True(
            $"{step}_matches_single_model_reference",
            differences.Count == 0,
            differences.Count == 0
                ? string.Empty
                : $"{differences.Count} difference(s): {string.Join("; ", differences.Take(MaxReportedDifferences))}");

        return new
        {
            compared = reference.Count,
            differences = differences.Take(MaxReportedDifferences).ToArray(),
            pooled_only_nodes = pooled
                .Where(entry => !reference.ContainsKey(entry.Key)
                    && entry.Key.EndsWith("#visible", StringComparison.Ordinal))
                .Select(entry => $"{entry.Key[..^"#visible".Length]}={entry.Value}")
                .ToArray()
        };
    }

    private static Dictionary<string, string> Snapshot(Node root)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        Visit(root, ".");
        return values;

        void Visit(Node node, string path)
        {
            if (node is CanvasItem item)
                Record(item, path);

            var anonymousOrdinals = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var child in node.GetChildren())
                Visit(child, $"{path}/{StableName(child, anonymousOrdinals)}");
        }

        // Godot names unnamed nodes "@Type@<instance id>", which differs between two otherwise identical trees.
        static string StableName(Node child, Dictionary<string, int> anonymousOrdinals)
        {
            // A pooled card keeps whatever name its first owner gave it.
            if (child is NCard)
                return "<card>";

            var name = child.Name.ToString();
            if (!name.StartsWith('@'))
                return name;

            var type = child.GetType().Name;
            var ordinal = anonymousOrdinals.GetValueOrDefault(type);
            anonymousOrdinals[type] = ordinal + 1;
            return $"@{type}[{ordinal}]";
        }

        void Record(CanvasItem item, string path)
        {
            values[$"{path}#visible"] = item.Visible.ToString();
            values[$"{path}#modulate"] = Format(item.Modulate);
            values[$"{path}#self_modulate"] = Format(item.SelfModulate);
            values[$"{path}#z_index"] = item.ZIndex.ToString(CultureInfo.InvariantCulture);
            values[$"{path}#material"] = ResourceIdentity(item.Material);

            if (item is Control control)
            {
                values[$"{path}#position"] = Format(control.Position);
                values[$"{path}#size"] = Format(control.Size);
                values[$"{path}#scale"] = Format(control.Scale);
                values[$"{path}#pivot"] = Format(control.PivotOffset);
                values[$"{path}#min_size"] = Format(control.CustomMinimumSize);
                values[$"{path}#anchors"] = string.Join(
                    ",",
                    new[] { control.AnchorLeft, control.AnchorTop, control.AnchorRight, control.AnchorBottom }
                        .Select(Format));
                RecordThemeOverrides(control, path);
            }

            switch (item)
            {
                case TextureRect textureRect:
                    values[$"{path}#texture"] = ResourceIdentity(textureRect.Texture);
                    values[$"{path}#expand"] = textureRect.ExpandMode.ToString();
                    values[$"{path}#stretch"] = textureRect.StretchMode.ToString();
                    break;
                case Label label:
                    values[$"{path}#text"] = label.Text;
                    values[$"{path}#h_align"] = label.HorizontalAlignment.ToString();
                    values[$"{path}#v_align"] = label.VerticalAlignment.ToString();
                    values[$"{path}#autowrap"] = label.AutowrapMode.ToString();
                    break;
                case RichTextLabel richText:
                    values[$"{path}#text"] = richText.Text;
                    break;
            }
        }

        void RecordThemeOverrides(Control control, string path)
        {
            foreach (var name in ThemeColorNames)
            {
                values[$"{path}#theme_color_{name}"] = control.HasThemeColorOverride(name)
                    ? Format(control.GetThemeColor(name))
                    : "-";
            }

            foreach (var name in ThemeConstantNames)
            {
                values[$"{path}#theme_constant_{name}"] = control.HasThemeConstantOverride(name)
                    ? control.GetThemeConstant(name).ToString(CultureInfo.InvariantCulture)
                    : "-";
            }

            foreach (var name in ThemeFontSizeNames)
            {
                values[$"{path}#theme_font_size_{name}"] = control.HasThemeFontSizeOverride(name)
                    ? control.GetThemeFontSize(name).ToString(CultureInfo.InvariantCulture)
                    : "-";
            }
        }
    }

    private static string ResourceIdentity(Resource? resource) =>
        resource switch
        {
            null => "null",
            { ResourcePath: { Length: > 0 } path } => path,
            _ => resource.GetType().Name
        };

    private static string Format(float value) =>
        value.ToString("F2", CultureInfo.InvariantCulture);

    private static string Format(Vector2 vector) =>
        $"({Format(vector.X)},{Format(vector.Y)})";

    private static string Format(Color color) =>
        $"({Format(color.R)},{Format(color.G)},{Format(color.B)},{Format(color.A)})";

    private static async Task Settle(Node node)
    {
        await node.AwaitProcessFrame();
        await node.AwaitProcessFrame();
    }

    private static NCard CreateAttachedCard(CardModel model)
    {
        var card = NCard.Create(model)
            ?? throw new InvalidOperationException($"Could not create NCard for {model.Id}.");
        SceneRoot().AddChild(card);
        card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        return card;
    }

    private static NGridCardHolder CreateAttachedHolder(CardModel model)
    {
        var card = NCard.Create(model)
            ?? throw new InvalidOperationException($"Could not create NCard for {model.Id}.");
        var holder = NGridCardHolder.Create(card)
            ?? throw new InvalidOperationException($"Could not create a grid holder for {model.Id}.");
        SceneRoot().AddChild(holder);
        card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        return holder;
    }

    /// <summary>
    /// The reference must never come from <see cref="NodePool"/>: a recycled node carries the
    /// same leak as the pooled node under test and the comparison would pass vacuously.
    /// </summary>
    private static NCard CreateFreshCard(CardModel model)
    {
        var card = PreloadManager.Cache.GetScene(NCard.AssetPaths.First())
            .Instantiate<NCard>(PackedScene.GenEditState.Disabled);
        card.OnInstantiated();
        card.Model = model;
        return card;
    }

    private static NCard CreateFreshAttachedCard(CardModel model)
    {
        var card = CreateFreshCard(model);
        SceneRoot().AddChild(card);
        card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        return card;
    }

    // Replays NGridCardHolder.Create on a freshly instantiated holder scene.
    private static NGridCardHolder CreateFreshAttachedHolder(CardModel model)
    {
        var card = CreateFreshCard(model);
        var holder = PreloadManager.Cache.GetScene(NGridCardHolder.AssetPaths.First())
            .Instantiate<NGridCardHolder>(PackedScene.GenEditState.Disabled);
        holder.OnInstantiated();
        InvokeHolder(holder, "SetCard", card);
        InvokeHolder(holder, "UpdateCardModel");
        InvokeHolder(holder, "UpdateName");
        holder.Scale = holder.SmallScale;
        SceneRoot().AddChild(holder);
        card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        return holder;
    }

    private static void InvokeHolder(NGridCardHolder holder, string name, params object[] arguments)
    {
        var method = typeof(NGridCardHolder).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                arguments.Select(static argument => argument.GetType()).ToArray())
            ?? throw new MissingMethodException(nameof(NGridCardHolder), name);
        method.Invoke(holder, arguments);
    }

    private static void FreeFresh(Node node)
    {
        node.GetParent()?.RemoveChild(node);
        node.QueueFree();
    }

    private static Window SceneRoot() =>
        Engine.GetMainLoop() is SceneTree tree
            ? tree.Root
            : throw new InvalidOperationException("Godot main loop is not a SceneTree.");

    private static void ReleaseCard(NCard? card)
    {
        if (card is null)
            return;

        card.GetParent()?.RemoveChild(card);
        NodePool.Free(card);
    }

    private static void ReleaseHolder(NGridCardHolder? holder)
    {
        if (holder is null)
            return;

        var card = holder.CardNode;
        card?.GetParent()?.RemoveChild(card);
        ReleaseCard(card);
        holder.GetParent()?.RemoveChild(holder);
        NodePool.Free(holder);
    }
}
