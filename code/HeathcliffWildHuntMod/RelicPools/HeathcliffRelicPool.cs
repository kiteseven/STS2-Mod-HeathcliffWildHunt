using System.Collections.Generic;
using Godot;
using HeathcliffWildHuntMod.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.RelicPools;

/// <summary>狂猎希斯克利夫专属遗物池。</summary>
public sealed class HeathcliffRelicPool : RelicPoolModel
{
    public override string EnergyColorName => "ironclad";

    public override Color LabOutlineColor => StsColors.red;

    protected override IEnumerable<RelicModel> GenerateAllRelics()
    {
        return new RelicModel[]
        {
            ModelDb.Relic<CatherineCoffinRelic>(),
            ModelDb.Relic<CleanAllCathyRelic>(),
        };
    }
}
