using System;
using System.Collections.Generic;

namespace Noname.Battle
{
    /// <summary>편성과 난수 시드를 묶은 입력. 원본 컬렉션이 바뀌어도 생성 시점의 편성을 유지한다.</summary>
    public sealed class BattleStartData
    {
        public IReadOnlyList<BattleUnitState> Units { get; }
        public int Seed { get; }

        public BattleStartData(IEnumerable<BattleUnitState> units, int seed)
        {
            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            Units = new List<BattleUnitState>(units).AsReadOnly();
            Seed = seed;
        }
    }
}
