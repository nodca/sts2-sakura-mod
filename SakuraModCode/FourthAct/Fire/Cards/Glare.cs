using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Fire.Cards;

// 灼目: Light's non-Ethereal Dazed. Light grants Retain, so it lingers and swells Judgment until paid off.
public sealed class Glare() :
    ModCardTemplate(1, CardType.Status, CardRarity.Status, TargetType.None, showInCardLibrary: false),
    ISakuraClearLayoutCard
{
    public CardType DescriptionShapeCardType => CardType.Skill;
    public override CardPoolModel Pool => ModelDb.CardPool<ClassicSakuraCardPool>();
    public override bool CanBeGeneratedInCombat => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public override string CustomPortraitPath => CardModel.MissingPortraitPath;
    public override string PortraitPath => CardModel.MissingPortraitPath;
    public override string BetaPortraitPath => CardModel.MissingPortraitPath;
    public override Material? CustomFrameMaterial => SakuraCardFrameVisuals.PlainFrameMaterial;
    protected override IEnumerable<string> ExtraRunAssetPaths => SakuraCardFrameVisuals.RunAssetPaths(this);

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) => Task.CompletedTask;
}
