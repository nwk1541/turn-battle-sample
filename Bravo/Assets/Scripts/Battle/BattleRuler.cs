using System;
using System.Collections.Generic;

namespace Noname.Battle
{
    /// <summary>
    /// 진영을 교대하며 각 라운드에 생존 유닛이 속도순으로 한 번씩 행동한다.
    /// 자기 라인부터 아래로 순환하며 공격할 라인과 피격자를 결정하고 전투 결과를 처리한다.
    /// </summary>
    public sealed class BattleRuler
    {
        private const int MAX_ROUNDS = 20;
        private const int LANE_COUNT = 3;

        private readonly List<BattleUnitState> _units;
        private readonly HashSet<int> _actedUnitIds = new HashSet<int>();
        private readonly Random _random;
        private BattleSide _nextSide;
        private int _nextUnitIndex;

        public IReadOnlyList<BattleUnitState> Units { get; }
        public int Round { get; private set; } = 1;
        public bool IsFinished { get; private set; }
        public bool IsDraw => IsFinished && !Winner.HasValue;
        public BattleSide? Winner { get; private set; }
        public BattleUnitState? NextUnit => IsFinished ? (BattleUnitState?)null : _units[_nextUnitIndex];

        public BattleRuler(IEnumerable<BattleUnitState> units, int seed)
        {
            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            _units = new List<BattleUnitState>(units);
            Units = _units.AsReadOnly();
            _random = new Random(seed);
            ValidateUnits();
            UpdateResult();

            if (IsFinished)
            {
                return;
            }

            var myFirst = FindNextUnitIndex(BattleSide.MySide);
            var enemyFirst = FindNextUnitIndex(BattleSide.Enemies);
            _nextSide = _units[myFirst].Stats.Speed >= _units[enemyFirst].Stats.Speed ? BattleSide.MySide : BattleSide.Enemies;
            SelectNextUnit();
        }

        /// <summary>
        /// 자기 라인부터 아래로 순환하며 생존한 적이 있는 첫 라인을 선택한다.
        /// 정면은 첫 번째, 건너 뜀은 두 번째 생존 유닛을 공격하며 한 명뿐이면 그 유닛을 공격한다.
        /// </summary>
        public DamageDesc ExecuteNextAction()
        {
            if (IsFinished)
            {
                throw new InvalidOperationException("종료된 전투에서는 행동할 수 없습니다.");
            }

            var attacker = _units[_nextUnitIndex];
            var targetIndex = ResolveTargetIndex(attacker);
            var target = _units[targetIndex];
            var damage = DamageFormula.Calculate(attacker, target, _random.NextDouble());
            _units[targetIndex] = new BattleUnitState(target.Id, target.Side, target.Slot, target.Stats, damage.TargetFinalHealth, target.AttackType);
            _actedUnitIds.Add(attacker.Id);
            _nextSide = OtherSide(attacker.Side);
            UpdateResult();

            if (!IsFinished)
            {
                SelectNextUnit();
            }

            return damage;
        }

        private int ResolveTargetIndex(BattleUnitState attacker)
        {
            // 슬롯은 같은 줄에서 1→4→7→10, 2→5→8→11, 3→6→9→12 순서로 뒤에 놓인다.
            var attackerLane = (attacker.Slot - 1) % LANE_COUNT;
            var selectedLaneOffset = LANE_COUNT;
            var firstIndex = -1;
            var secondIndex = -1;

            for (var i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit.Side == attacker.Side || unit.IsDead)
                {
                    continue;
                }

                var lane = (unit.Slot - 1) % LANE_COUNT;
                var laneOffset = (lane - attackerLane + LANE_COUNT) % LANE_COUNT;
                if (laneOffset > selectedLaneOffset)
                {
                    continue;
                }

                if (laneOffset < selectedLaneOffset)
                {
                    selectedLaneOffset = laneOffset;
                    firstIndex = -1;
                    secondIndex = -1;
                }

                if (firstIndex < 0 || unit.Slot < _units[firstIndex].Slot)
                {
                    secondIndex = firstIndex;
                    firstIndex = i;
                }
                else if (secondIndex < 0 || unit.Slot < _units[secondIndex].Slot)
                {
                    secondIndex = i;
                }
            }

            return attacker.AttackType == AttackType.Skip && secondIndex >= 0 ? secondIndex : firstIndex;
        }

        private void ValidateUnits()
        {
            var ids = new HashSet<int>();
            var slots = new HashSet<(BattleSide Side, int Slot)>();
            var hasMySide = false;
            var hasEnemies = false;

            foreach (var unit in _units)
            {
                if (unit.Stats.MaxHealth <= 0)
                {
                    throw new ArgumentException("초기화되지 않은 유닛 상태입니다.");
                }
                if (!ids.Add(unit.Id))
                {
                    throw new ArgumentException("전투 내 유닛 ID가 중복되었습니다.");
                }
                if (!slots.Add((unit.Side, unit.Slot)))
                {
                    throw new ArgumentException("같은 진영의 슬롯이 중복되었습니다.");
                }

                hasMySide |= unit.Side == BattleSide.MySide;
                hasEnemies |= unit.Side == BattleSide.Enemies;
            }

            if (!hasMySide || !hasEnemies)
            {
                throw new ArgumentException("양쪽 진영에 유닛이 필요합니다.");
            }
        }

        private void SelectNextUnit()
        {
            _nextUnitIndex = FindNextUnitIndex(_nextSide);
            if (_nextUnitIndex >= 0)
            {
                return;
            }

            _nextUnitIndex = FindNextUnitIndex(OtherSide(_nextSide));
            if (_nextUnitIndex >= 0)
            {
                return;
            }

            if (Round >= MAX_ROUNDS)
            {
                IsFinished = true;
                Winner = null;
                return;
            }

            _actedUnitIds.Clear();
            Round++;
            _nextUnitIndex = FindNextUnitIndex(_nextSide);
        }

        private int FindNextUnitIndex(BattleSide side)
        {
            var selectedIndex = -1;

            for (var i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                if (unit.Side != side || unit.IsDead || _actedUnitIds.Contains(unit.Id))
                {
                    continue;
                }

                if (selectedIndex < 0 || unit.Stats.Speed > _units[selectedIndex].Stats.Speed || (unit.Stats.Speed == _units[selectedIndex].Stats.Speed && unit.Slot < _units[selectedIndex].Slot))
                {
                    selectedIndex = i;
                }
            }

            return selectedIndex;
        }

        private void UpdateResult()
        {
            var mySideAlive = _units.Exists(unit => unit.Side == BattleSide.MySide && !unit.IsDead);
            var enemiesAlive = _units.Exists(unit => unit.Side == BattleSide.Enemies && !unit.IsDead);
            IsFinished = !mySideAlive || !enemiesAlive;
            Winner = IsFinished && mySideAlive ? BattleSide.MySide : IsFinished && enemiesAlive ? (BattleSide?)BattleSide.Enemies : null;
        }

        private static BattleSide OtherSide(BattleSide side)
        {
            return side == BattleSide.MySide ? BattleSide.Enemies : BattleSide.MySide;
        }
    }
}
