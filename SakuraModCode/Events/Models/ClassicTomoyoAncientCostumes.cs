using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Relics;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.Events.Models;

public sealed class ClassicTomoyoAncientCostumes : ModAncientEventTemplate
{
    private const string BackgroundScenePath =
        MainFile.ResPath + "/scenes/events/tomoyo_ancient_costumes_background.tscn";

    private const string AncientIconPath =
        MainFile.ResPath + "/images/events/tomoyo_ancient_icon.png";

    private const string AncientIconOutlinePath =
        MainFile.ResPath + "/images/events/tomoyo_ancient_icon_outline.png";

    public override string? CustomBackgroundScenePath => BackgroundScenePath;

    public override AncientEventPresentationAssetProfile AncientPresentationAssetProfile =>
        new(
            MapIconPath: AncientIconPath,
            MapIconOutlinePath: AncientIconOutlinePath,
            RunHistoryIconPath: AncientIconPath,
            RunHistoryIconOutlinePath: AncientIconOutlinePath);

    public override bool IsAllowed(IRunState runState) =>
        SakuraStarterCompatibility.IsKinomotoSakuraRun(runState);

    public override IEnumerable<EventOption> AllPossibleOptions =>
    [
        CreateCostumeRelicOption<ClassicRedCapeRelic>(),
        CreateCostumeRelicOption<ClassicPinkTransformationCostumeRelic>(),
        CreateCostumeRelicOption<ClassicFrogRaincoatRelic>()
    ];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        CreateCostumeRelicOption<ClassicRedCapeRelic>(),
        CreateCostumeRelicOption<ClassicPinkTransformationCostumeRelic>(),
        CreateCostumeRelicOption<ClassicFrogRaincoatRelic>()
    ];

    private EventOption CreateCostumeRelicOption<T>() where T : RelicModel
    {
        var relic = ModelDb.Relic<T>().ToMutable();
        if (Owner is not null)
            relic.Owner = Owner;

        return new EventOption(
                this,
                () => ObtainCostumeRelic(relic),
                ModOptionKey("INITIAL", relic.Id.Entry),
                HoverTipFactory.FromRelic(relic))
            .WithRelic(relic);
    }

    private async Task ObtainCostumeRelic(RelicModel relic)
    {
        var player = Owner
            ?? throw new InvalidOperationException($"Ancient '{Id.Entry}' had no owner when a costume was chosen.");
        relic.Owner = player;
        await RelicCmd.Obtain(relic, player);
        Done();
    }
}
