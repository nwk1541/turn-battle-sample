using UnityEngine;

namespace Noname.Battle
{
    public enum UnitType
    {
        [InspectorName("검병")] Swordsman = 0,
        [InspectorName("창병")] Spearman = 1,
        [InspectorName("기병")] Cavalry = 2,
        [InspectorName("궁병")] Archer = 3
    }
}
