using System.Collections.Generic;

namespace Noname.Battle
{
    /// <summary>완료 시점의 승패와 유닛 상태를 복사해 보관한다. 이후 Ruler의 상태 변경에 영향받지 않는다.</summary>
    public sealed class BattleResult
    {
        public BattleSide? Winner { get; }
        public int Round { get; }
        public IReadOnlyList<BattleUnitState> Units { get; }

        public bool IsDraw => !Winner.HasValue;

        internal BattleResult(BattleRuler ruler)
        {
            Winner = ruler.Winner;
            Round = ruler.Round;
            Units = new List<BattleUnitState>(ruler.Units).AsReadOnly();
        }
    }
}
