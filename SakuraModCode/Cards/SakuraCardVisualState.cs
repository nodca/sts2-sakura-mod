using Godot;
using MegaCrit.Sts2.addons.mega_text;
using System.Runtime.CompilerServices;

namespace SakuraMod.SakuraModCode.Cards;

internal enum SakuraCardRendererId
{
    Clear,
    Classic
}

[Flags]
internal enum SakuraControlProperty
{
    None = 0,
    Anchors = 1 << 0,
    Position = 1 << 1,
    Size = 1 << 2,
    CustomMinimumSize = 1 << 3,
    Scale = 1 << 4,
    PivotOffset = 1 << 5,
    Visibility = 1 << 6,
    Modulate = 1 << 7,
    SelfModulate = 1 << 8,
    ZIndex = 1 << 9,
    MouseFilter = 1 << 10,
    TextureExpandMode = 1 << 11,
    TextureStretchMode = 1 << 12,
    HorizontalAlignment = 1 << 13,
    VerticalAlignment = 1 << 14,
    AutowrapMode = 1 << 15,
    FontBounds = 1 << 16,
    RichTextLayout = 1 << 17,
}

internal static class SakuraCardMutationLedgers
{
    private static readonly ConditionalWeakTable<GodotObject, SakuraCardMutationLedger> Ledgers = new();

    public static SakuraCardMutationLedger For(GodotObject owner) =>
        Ledgers.GetOrCreateValue(owner);
}

internal sealed class SakuraCardMutationLedger
{
    private readonly List<ISakuraCardMutation> _mutations = [];
    private readonly HashSet<SakuraMutationKey> _keys = new(SakuraMutationKeyComparer.Instance);
    private readonly Dictionary<Control, Vector2> _positionBaselines = new(ReferenceEqualityComparer.Instance);
    private SakuraCardRendererId? _renderer;
    private bool _isApplied;

    public bool IsApplied(SakuraCardRendererId renderer) =>
        _renderer == renderer && _isApplied;

    public void Begin(SakuraCardRendererId renderer)
    {
        if (_renderer == renderer)
            return;

        RestoreCurrent();
        _renderer = renderer;
    }

    public void Borrow(Control? control, SakuraControlProperty properties)
    {
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(control))
            return;

        if (properties.HasFlag(SakuraControlProperty.Anchors))
        {
            Capture(
                control!,
                SakuraMutationProperty.Anchors,
                new SakuraAnchorState(
                    control!.AnchorLeft,
                    control.AnchorTop,
                    control.AnchorRight,
                    control.AnchorBottom),
                static (target, value) =>
                {
                    target.AnchorLeft = value.Left;
                    target.AnchorTop = value.Top;
                    target.AnchorRight = value.Right;
                    target.AnchorBottom = value.Bottom;
                });
        }

        // Godot clamps Size against CustomMinimumSize. Restore the constraint first so
        // the original size is not silently retained at the borrowed minimum.
        CaptureIf(
            control!,
            properties,
            SakuraControlProperty.CustomMinimumSize,
            SakuraMutationProperty.CustomMinimumSize,
            static target => target.CustomMinimumSize,
            static (target, value) => target.CustomMinimumSize = value);
        CaptureIf(
            control!,
            properties,
            SakuraControlProperty.Position,
            SakuraMutationProperty.Position,
            static target => target.Position,
            static (target, value) => target.Position = value);
        CaptureIf(
            control!,
            properties,
            SakuraControlProperty.Size,
            SakuraMutationProperty.Size,
            static target => target.Size,
            static (target, value) => target.Size = value);
        CaptureIf(
            control!,
            properties,
            SakuraControlProperty.Scale,
            SakuraMutationProperty.Scale,
            static target => target.Scale,
            static (target, value) => target.Scale = value);
        CaptureIf(
            control!,
            properties,
            SakuraControlProperty.PivotOffset,
            SakuraMutationProperty.PivotOffset,
            static target => target.PivotOffset,
            static (target, value) => target.PivotOffset = value);

        if (properties.HasFlag(SakuraControlProperty.Visibility))
            BorrowVisibility(control);
        if (properties.HasFlag(SakuraControlProperty.Modulate))
        {
            Capture(
                control!,
                SakuraMutationProperty.Modulate,
                control!.Modulate,
                static (target, value) => target.Modulate = value);
        }
        if (properties.HasFlag(SakuraControlProperty.SelfModulate))
        {
            Capture(
                control!,
                SakuraMutationProperty.SelfModulate,
                control!.SelfModulate,
                static (target, value) => target.SelfModulate = value);
        }
        if (properties.HasFlag(SakuraControlProperty.ZIndex))
        {
            Capture(
                control!,
                SakuraMutationProperty.ZIndex,
                control!.ZIndex,
                static (target, value) => target.ZIndex = value);
        }
        if (properties.HasFlag(SakuraControlProperty.MouseFilter))
        {
            Capture(
                control!,
                SakuraMutationProperty.MouseFilter,
                control!.MouseFilter,
                static (target, value) => target.MouseFilter = value);
        }

        if (control is TextureRect textureRect)
            BorrowTextureRectLayout(textureRect, properties);
        if (properties.HasFlag(SakuraControlProperty.HorizontalAlignment))
            BorrowHorizontalAlignment(control!);
        if (properties.HasFlag(SakuraControlProperty.VerticalAlignment))
            BorrowVerticalAlignment(control!);
        if (properties.HasFlag(SakuraControlProperty.AutowrapMode))
            BorrowAutowrapMode(control!);
        if (properties.HasFlag(SakuraControlProperty.FontBounds))
            BorrowFontBounds(control!);
        if (properties.HasFlag(SakuraControlProperty.RichTextLayout)
            && control is MegaRichTextLabel richTextLabel)
        {
            Capture(
                richTextLabel,
                SakuraMutationProperty.RichTextLayout,
                new SakuraRichTextLayoutState(
                    richTextLabel.ScrollActive,
                    richTextLabel.FitContent,
                    richTextLabel.IsHorizontallyBound,
                    richTextLabel.IsVerticallyBound),
                static (target, value) =>
                {
                    target.ScrollActive = value.ScrollActive;
                    target.FitContent = value.FitContent;
                    target.IsHorizontallyBound = value.IsHorizontallyBound;
                    target.IsVerticallyBound = value.IsVerticallyBound;
                });
        }
    }

    public void BorrowPositionBaseline(Control? control)
    {
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(control))
            return;

        _positionBaselines.TryAdd(control!, control!.Position);
        Borrow(control, SakuraControlProperty.Position);
    }

    public bool TryGetPositionBaseline(Control? control, out Vector2 position)
    {
        if (control is not null && _positionBaselines.TryGetValue(control, out position))
            return true;

        position = default;
        return false;
    }

    public void BorrowVisibility(CanvasItem? item)
    {
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(item))
            return;

        Capture(
            item!,
            SakuraMutationProperty.Visibility,
            item!.Visible,
            static (target, value) => target.Visible = value);
    }

    public void BorrowTexture(TextureRect? textureRect)
    {
        if (!SakuraCardVisualInfrastructure.TryGetTexture(textureRect, out var texture))
            return;

        var resource = SakuraCardTextureResource.Capture(texture);
        Capture(
            textureRect!,
            SakuraMutationProperty.Texture,
            resource,
            static (target, value) =>
            {
                if (value.TryResolve(out var resolved))
                    SakuraCardVisualInfrastructure.SetTextureIfDifferent(target, resolved);
            });
    }

    public void BorrowThemeColor(Control? control, StringName name)
    {
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(control))
            return;

        var state = new SakuraThemeColorState(
            control!.HasThemeColorOverride(name),
            control.HasThemeColorOverride(name) ? control.GetThemeColor(name) : default);
        Capture(
            control,
            SakuraMutationProperty.ThemeColor,
            state,
            static (target, value, qualifier) =>
            {
                var themeName = new StringName(qualifier);
                if (value.HadOverride)
                    SakuraCardVisualInfrastructure.ApplyThemeColorOverride(target, themeName, value.Value);
                else
                    SakuraCardVisualInfrastructure.RemoveThemeColorOverride(target, themeName);
            },
            name.ToString());
    }

    public void BorrowThemeConstant(Control? control, StringName name)
    {
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(control))
            return;

        var state = new SakuraThemeIntState(
            control!.HasThemeConstantOverride(name),
            control.HasThemeConstantOverride(name) ? control.GetThemeConstant(name) : default);
        Capture(
            control,
            SakuraMutationProperty.ThemeConstant,
            state,
            static (target, value, qualifier) =>
            {
                var themeName = new StringName(qualifier);
                if (value.HadOverride)
                    SakuraCardVisualInfrastructure.ApplyThemeConstantOverride(target, themeName, value.Value);
                else
                    SakuraCardVisualInfrastructure.RemoveThemeConstantOverride(target, themeName);
            },
            name.ToString());
    }

    public void BorrowThemeFontSize(Control? control, StringName name)
    {
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(control))
            return;

        var state = new SakuraThemeIntState(
            control!.HasThemeFontSizeOverride(name),
            control.HasThemeFontSizeOverride(name) ? control.GetThemeFontSize(name) : default);
        Capture(
            control,
            SakuraMutationProperty.ThemeFontSize,
            state,
            static (target, value, qualifier) =>
            {
                var themeName = new StringName(qualifier);
                if (value.HadOverride)
                    SakuraCardVisualInfrastructure.ApplyThemeFontSizeOverride(target, themeName, value.Value);
                else
                    SakuraCardVisualInfrastructure.RemoveThemeFontSizeOverride(target, themeName);
            },
            name.ToString());
    }

    public void BorrowViewportSize(SubViewport? viewport)
    {
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(viewport))
            return;

        Capture(
            viewport!,
            SakuraMutationProperty.ViewportSize,
            viewport!.Size,
            static (target, value) => target.Size = value);
    }

    public void YieldVisibilityToNative(CanvasItem? item)
    {
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(item))
            return;

        Add(
            new SakuraMutationKey(item!, SakuraMutationProperty.Visibility, null),
            new SakuraNativeRepaintMutation(item!));
    }

    public void YieldShaderStateToNative(CanvasItem? item)
    {
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(item))
            return;

        Add(
            new SakuraMutationKey(item!, SakuraMutationProperty.ShaderState, null),
            new SakuraNativeRepaintMutation(item!));
    }

    public void Own(CanvasItem? item)
    {
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(item))
            return;

        Add(
            new SakuraMutationKey(item!, SakuraMutationProperty.OwnedVisibility, null),
            new SakuraOwnedMutation(item!));
    }

    public void MarkApplied(SakuraCardRendererId renderer)
    {
        if (_renderer != renderer)
            throw new InvalidOperationException($"Cannot mark {renderer} applied while {_renderer?.ToString() ?? "no renderer"} owns the ledger.");

        _isApplied = true;
    }

    public void Restore(SakuraCardRendererId renderer)
    {
        if (_renderer != renderer)
            return;

        RestoreCurrent();
    }

    private void RestoreCurrent()
    {
        foreach (var mutation in _mutations)
            mutation.Restore();

        _mutations.Clear();
        _keys.Clear();
        _positionBaselines.Clear();
        _renderer = null;
        _isApplied = false;
    }

    private void BorrowTextureRectLayout(TextureRect textureRect, SakuraControlProperty properties)
    {
        if (properties.HasFlag(SakuraControlProperty.TextureExpandMode))
        {
            Capture(
                textureRect,
                SakuraMutationProperty.TextureExpandMode,
                textureRect.ExpandMode,
                static (target, value) => target.ExpandMode = value);
        }
        if (properties.HasFlag(SakuraControlProperty.TextureStretchMode))
        {
            Capture(
                textureRect,
                SakuraMutationProperty.TextureStretchMode,
                textureRect.StretchMode,
                static (target, value) => target.StretchMode = value);
        }
    }

    private void BorrowHorizontalAlignment(Control control)
    {
        switch (control)
        {
            case MegaLabel label:
                Capture(
                    label,
                    SakuraMutationProperty.HorizontalAlignment,
                    label.HorizontalAlignment,
                    static (target, value) => target.HorizontalAlignment = value);
                break;
            case Label label:
                Capture(
                    label,
                    SakuraMutationProperty.HorizontalAlignment,
                    label.HorizontalAlignment,
                    static (target, value) => target.HorizontalAlignment = value);
                break;
        }
    }

    private void BorrowVerticalAlignment(Control control)
    {
        switch (control)
        {
            case MegaLabel label:
                Capture(
                    label,
                    SakuraMutationProperty.VerticalAlignment,
                    label.VerticalAlignment,
                    static (target, value) => target.VerticalAlignment = value);
                break;
            case Label label:
                Capture(
                    label,
                    SakuraMutationProperty.VerticalAlignment,
                    label.VerticalAlignment,
                    static (target, value) => target.VerticalAlignment = value);
                break;
        }
    }

    private void BorrowAutowrapMode(Control control)
    {
        switch (control)
        {
            case MegaLabel label:
                Capture(
                    label,
                    SakuraMutationProperty.AutowrapMode,
                    label.AutowrapMode,
                    static (target, value) => target.AutowrapMode = value);
                break;
            case Label label:
                Capture(
                    label,
                    SakuraMutationProperty.AutowrapMode,
                    label.AutowrapMode,
                    static (target, value) => target.AutowrapMode = value);
                break;
        }
    }

    private void BorrowFontBounds(Control control)
    {
        switch (control)
        {
            case MegaLabel label:
                Capture(
                    label,
                    SakuraMutationProperty.FontBounds,
                    new SakuraFontBounds(label.MinFontSize, label.MaxFontSize),
                    static (target, value) =>
                    {
                        target.MinFontSize = value.Minimum;
                        target.MaxFontSize = value.Maximum;
                    });
                break;
            case MegaRichTextLabel label:
                Capture(
                    label,
                    SakuraMutationProperty.FontBounds,
                    new SakuraFontBounds(label.MinFontSize, label.MaxFontSize),
                    static (target, value) =>
                    {
                        target.MinFontSize = value.Minimum;
                        target.MaxFontSize = value.Maximum;
                    });
                break;
        }
    }

    private void CaptureIf<TValue>(
        Control control,
        SakuraControlProperty properties,
        SakuraControlProperty flag,
        SakuraMutationProperty property,
        Func<Control, TValue> read,
        Action<Control, TValue> restore)
    {
        if (properties.HasFlag(flag))
            Capture(control, property, read(control), restore);
    }

    private void Capture<TTarget, TValue>(
        TTarget target,
        SakuraMutationProperty property,
        TValue value,
        Action<TTarget, TValue> restore,
        string? qualifier = null)
        where TTarget : GodotObject
    {
        Add(
            new SakuraMutationKey(target, property, qualifier),
            new SakuraRestoreMutation<TTarget, TValue>(target, value, restore));
    }

    private void Capture<TTarget, TValue>(
        TTarget target,
        SakuraMutationProperty property,
        TValue value,
        Action<TTarget, TValue, string> restore,
        string qualifier)
        where TTarget : GodotObject
    {
        Add(
            new SakuraMutationKey(target, property, qualifier),
            new SakuraQualifiedRestoreMutation<TTarget, TValue>(target, value, qualifier, restore));
    }

    private void Add(SakuraMutationKey key, ISakuraCardMutation mutation)
    {
        if (!_keys.Add(key))
            return;

        _mutations.Add(mutation);
    }

    private enum SakuraMutationProperty
    {
        Anchors,
        Position,
        Size,
        CustomMinimumSize,
        Scale,
        PivotOffset,
        Visibility,
        Modulate,
        SelfModulate,
        ZIndex,
        MouseFilter,
        TextureExpandMode,
        TextureStretchMode,
        HorizontalAlignment,
        VerticalAlignment,
        AutowrapMode,
        FontBounds,
        RichTextLayout,
        Texture,
        ThemeColor,
        ThemeConstant,
        ThemeFontSize,
        ViewportSize,
        ShaderState,
        OwnedVisibility,
    }

    private interface ISakuraCardMutation
    {
        void Restore();
    }

    private sealed class SakuraRestoreMutation<TTarget, TValue>(
        TTarget target,
        TValue value,
        Action<TTarget, TValue> restore) : ISakuraCardMutation
        where TTarget : GodotObject
    {
        public void Restore()
        {
            if (SakuraCardVisualInfrastructure.IsGodotInstanceUsable(target))
                restore(target, value);
        }
    }

    private sealed class SakuraQualifiedRestoreMutation<TTarget, TValue>(
        TTarget target,
        TValue value,
        string qualifier,
        Action<TTarget, TValue, string> restore) : ISakuraCardMutation
        where TTarget : GodotObject
    {
        public void Restore()
        {
            if (SakuraCardVisualInfrastructure.IsGodotInstanceUsable(target))
                restore(target, value, qualifier);
        }
    }

    private sealed class SakuraNativeRepaintMutation(GodotObject target) : ISakuraCardMutation
    {
        public void Restore()
        {
            _ = target;
        }
    }

    private sealed class SakuraOwnedMutation(CanvasItem target) : ISakuraCardMutation
    {
        public void Restore()
        {
            if (SakuraCardVisualInfrastructure.IsGodotInstanceUsable(target))
                target.Visible = false;
        }
    }

    private readonly record struct SakuraMutationKey(
        GodotObject Target,
        SakuraMutationProperty Property,
        string? Qualifier);

    private sealed class SakuraMutationKeyComparer : IEqualityComparer<SakuraMutationKey>
    {
        public static readonly SakuraMutationKeyComparer Instance = new();

        public bool Equals(SakuraMutationKey left, SakuraMutationKey right) =>
            ReferenceEquals(left.Target, right.Target)
            && left.Property == right.Property
            && string.Equals(left.Qualifier, right.Qualifier, StringComparison.Ordinal);

        public int GetHashCode(SakuraMutationKey key) =>
            HashCode.Combine(
                RuntimeHelpers.GetHashCode(key.Target),
                key.Property,
                key.Qualifier is null ? 0 : StringComparer.Ordinal.GetHashCode(key.Qualifier));
    }

    private readonly record struct SakuraAnchorState(float Left, float Top, float Right, float Bottom);
    private readonly record struct SakuraFontBounds(int Minimum, int Maximum);
    private readonly record struct SakuraRichTextLayoutState(
        bool ScrollActive,
        bool FitContent,
        bool IsHorizontallyBound,
        bool IsVerticallyBound);
    private readonly record struct SakuraThemeColorState(bool HadOverride, Color Value);
    private readonly record struct SakuraThemeIntState(bool HadOverride, int Value);
}

internal sealed class SakuraCardTextureResource
{
    private readonly string? _path;
    private readonly Func<Texture2D>? _factory;
    private readonly bool _representsNull;
    private Texture2D? _instance;

    private SakuraCardTextureResource(
        string? path,
        Func<Texture2D>? factory,
        Texture2D? instance,
        bool representsNull)
    {
        _path = path;
        _factory = factory;
        _instance = instance;
        _representsNull = representsNull;
    }

    public static SakuraCardTextureResource FromPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new SakuraCardTextureResource(path, null, null, representsNull: false);
    }

    public static SakuraCardTextureResource FromFactory(Func<Texture2D> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new SakuraCardTextureResource(null, factory, null, representsNull: false);
    }

    public static SakuraCardTextureResource Capture(Texture2D? texture)
    {
        if (texture is null)
            return new SakuraCardTextureResource(null, null, null, representsNull: true);
        if (!SakuraCardVisualInfrastructure.IsGodotInstanceUsable(texture))
            return new SakuraCardTextureResource(null, null, null, representsNull: false);

        var path = texture.ResourcePath;
        return string.IsNullOrEmpty(path)
            ? new SakuraCardTextureResource(null, null, null, representsNull: false)
            : new SakuraCardTextureResource(path, null, texture, representsNull: false);
    }

    public bool TryResolve(out Texture2D? texture)
    {
        if (_representsNull)
        {
            texture = null;
            return true;
        }
        if (SakuraCardVisualInfrastructure.IsGodotInstanceUsable(_instance))
        {
            texture = _instance;
            return true;
        }

        _instance = null;
        if (_path is not null)
        {
            if (!ResourceLoader.Exists(_path))
            {
                texture = null;
                return false;
            }

            _instance = ResourceLoader.Load<Texture2D>(_path, null, ResourceLoader.CacheMode.Reuse);
        }
        else if (_factory is not null)
        {
            _instance = _factory();
        }

        texture = _instance;
        return SakuraCardVisualInfrastructure.IsGodotInstanceUsable(texture);
    }

    public Texture2D ResolveRequired(string description)
    {
        if (TryResolve(out var texture) && texture is not null)
            return texture;

        var source = _path is null ? description : $"{description}: {_path}";
        throw new InvalidOperationException($"Could not resolve card visual texture {source}.");
    }
}
