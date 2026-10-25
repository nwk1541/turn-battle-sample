using System;
using System.Collections.Generic;

namespace Noname.Battle
{
    /// <summary>
    /// 한 전투의 유닛 상태, 라운드와 난수 진행을 소유하며 호출마다 한 행동만 계산한다.
    /// </summary>
    public sealed class BattleRuler
    {
        private const int MAX_ROUNDS = 20;
        private const int LANE_COUNT = 3;

        private readonly List<BattleUnitState> _units;
        private readonly HashSet<int> _actedUnitIds = new HashSet<int>();
        private readonly Random _random;

        private BattleSide _nextSide;
        private int _nextUnitIndex = 0;

        public IReadOnlyList<BattleUnitState> Units { get; }
        public int Round { get; private set; } = 1;
        public bool IsFinished { get; private set; } = false;
        public BattleSide? Winner { get; private set; } = null;

        public bool IsDraw => IsFinished && !Winner.HasValue;
        public BattleUnitState? NextUnit => IsFinished ? (BattleUnitState?)null : _units[_nextUnitIndex];

        public BattleRuler(IEnumerable<BattleUnitState> units, int seed)
        {
            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            // 입력 편성과 분리된 상태를 소유하고 전투마다 seed로 난수열을 다시 시작한다.
            _units = new List<BattleUnitState>(units);
            Units = _units.AsReadOnly();
            _random = new Random(seed);
            ValidateUnits();
            UpdateResult();

            if (IsFinished)
            {
                return;
            }

            int myFirst = FindNextUnitIndex(BattleSide.MySide);
            int enemyFirst = FindNextUnitIndex(BattleSide.Enemies);
            _nextSide = _units[myFirst].Stats.Speed >= _units[enemyFirst].Stats.Speed ? BattleSide.MySide : BattleSide.Enemies;
            SelectNextUnit();
        }

        /// <summary>
        /// 현재 행동자의 피해와 사망, 승패를 반영한 뒤 다음 행동자를 정하고 이번 행동 결과를 반환한다.
        /// </summary>
        public DamageDesc ExecuteNextAction()
        {
            if (IsFinished)
            {
                throw new InvalidOperationException("종료된 전투에서는 행동할 수 없습니다.");
            }

            BattleUnitState attacker = _units[_nextUnitIndex];
            int targetIndex = ResolveTargetIndex(attacker);
            BattleUnitState target = _units[targetIndex];
            DamageDesc damage = DamageFormula.Calculate(attacker, target, _random.NextDouble());
            _units[targetIndex] = new BattleUnitState(target.Id, target.Side, target.Slot, target.Stats, damage.TargetFinalHealth, target.AttackType, target.UnitType);
            _actedUnitIds.Add(attacker.Id);
            _nextSide = OtherSide(attacker.Side);
            // 승패를 먼저 확정해야 마지막 라운드의 마지막 타격으로 얻은 승리가 무승부보다 우선한다.
            UpdateResult();

            if (!IsFinished)
            {
                SelectNextUnit();
            }

            return damage;
        }

        /// <summary>자기 라인부터 아래로 순환해 생존자가 있는 첫 라인에서 정면 또는 건너 뜀 대상을 선택한다.</summary>
        private int ResolveTargetIndex(BattleUnitState attacker)
        {
            // 슬롯은 같은 줄에서 1→4→7→10, 2→5→8→11, 3→6→9→12 순서로 뒤에 놓인다.
            int attackerLane = (attacker.Slot - 1) % LANE_COUNT;
            int selectedLaneOffset = LANE_COUNT;
            int firstIndex = -1;
            int secondIndex = -1;

            for (int i = 0; i < _units.Count; i++)
            {
                BattleUnitState unit = _units[i];
                if (unit.Side == attacker.Side || unit.IsDead)
                {
                    continue;
                }

                int lane = (unit.Slot - 1) % LANE_COUNT;
                int laneOffset = (lane - attackerLane + LANE_COUNT) % LANE_COUNT;
                if (laneOffset > selectedLaneOffset)
                {
                    continue;
                }

                // 더 가까운 라인을 찾으면 앞선 라인에서 모은 후보를 버린다.
                if (laneOffset < selectedLaneOffset)
                {
                    selectedLaneOffset = laneOffset;
                    firstIndex = -1;
                    secondIndex = -1;
                }

                // 입력 컬렉션 순서와 관계없이 슬롯이 작은 두 명을 앞뒤 후보로 유지한다.
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

            // 건너뛸 생존자가 없으면 다른 라인으로 바꾸지 않고 같은 라인의 첫 후보를 친다.
            return attacker.AttackType == AttackType.Skip && secondIndex >= 0 ? secondIndex : firstIndex;
        }

        /// <summary>진영별 미행동 생존자를 선택하고 양쪽에 후보가 없을 때만 라운드를 넘긴다.</summary>
        private void SelectNextUnit()
        {
            _nextUnitIndex = FindNextUnitIndex(_nextSide);
            if (_nextUnitIndex >= 0)
            {
                return;
            }

            // 한 진영이 먼저 행동을 마치면 같은 라운드에서 상대의 남은 행동을 소진한다.
            _nextUnitIndex = FindNextUnitIndex(OtherSide(_nextSide));
            if (_nextUnitIndex >= 0)
            {
                return;
            }

            // 양쪽에 미행동 생존자가 없을 때 제한을 검사해 마지막 라운드의 남은 행동까지 보장한다.
            if (Round >= MAX_ROUNDS)
            {
                IsFinished = true;
                Winner = null;
                return;
            }

            // _nextSide를 초기화하지 않아 라운드 경계에서도 진영 교대가 이어진다.
            _actedUnitIds.Clear();
            Round++;
            _nextUnitIndex = FindNextUnitIndex(_nextSide);
        }

        /// <summary>이번 라운드의 미행동 생존자 중 최고 속도를 선택하고 동률이면 작은 슬롯을 우선한다.</summary>
        private int FindNextUnitIndex(BattleSide side)
        {
            int selectedIndex = -1;

            for (int i = 0; i < _units.Count; i++)
            {
                BattleUnitState unit = _units[i];
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

        /// <summary>한쪽이 전멸하면 즉시 종료하며, 양쪽 모두 전멸한 입력은 승리 진영 없이 종료한다.</summary>
        private void UpdateResult()
        {
            bool mySideAlive = _units.Exists(unit => unit.Side == BattleSide.MySide && !unit.IsDead);
            bool enemiesAlive = _units.Exists(unit => unit.Side == BattleSide.Enemies && !unit.IsDead);
            IsFinished = !mySideAlive || !enemiesAlive;
            Winner = IsFinished && mySideAlive ? BattleSide.MySide : IsFinished && enemiesAlive ? (BattleSide?)BattleSide.Enemies : null;
        }

        private void ValidateUnits()
        {
            HashSet<int> ids = new HashSet<int>();
            HashSet<(BattleSide Side, int Slot)> slots = new HashSet<(BattleSide Side, int Slot)>();
            bool hasMySide = false;
            bool hasEnemies = false;

            foreach (BattleUnitState unit in _units)
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

        private static BattleSide OtherSide(BattleSide side)
        {
            return side == BattleSide.MySide ? BattleSide.Enemies : BattleSide.MySide;
        }
    }
}
