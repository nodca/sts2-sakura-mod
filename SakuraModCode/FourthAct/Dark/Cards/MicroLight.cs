using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Dark.Powers;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.FourthAct.Dark.Cards;

public sealed class MicroLight() :
    ModCardTemplate(1, CardType.Status, CardRarity.Basic, TargetType.Self, showInCardLibrary: false),
    ISakuraClearLayoutCard,
    ISakuraForgottenImmune
{
    public CardType DescriptionShapeCardType => CardType.Skill;
    public override CardPoolModel Pool => ModelDb.CardPool<ClassicSakuraCardPool>();
    public override bool CanBeGeneratedInCombat => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];
    public override string CustomPortraitPath => CardModel.MissingPortraitPath;
    public override string PortraitPath => CardModel.MissingPortraitPath;
    public override string BetaPortraitPath => CardModel.MissingPortraitPath;
    public override Material? CustomFrameMaterial => SakuraCardFrameVisuals.PlainFrameMaterial;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<DarknessPower>(-1)];
    protected override IEnumerable<string> ExtraRunAssetPaths => SakuraCardFrameVisuals.RunAssetPaths(this);

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        DarkMicroLightCoordinator.ApplyMicroLight(choiceContext, Owner, 1);
}
