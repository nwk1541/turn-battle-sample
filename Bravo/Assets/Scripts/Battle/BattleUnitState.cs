using System;

namespace Noname.Battle
{
    /// <summary>
    /// 한 시점의 유닛 상태. 상태 변경 시 새 값을 만들어 이전 결과와 입력을 보존한다.
    /// </summary>
    public readonly struct BattleUnitState
    {
        public int Id { get; }
        public BattleSide Side { get; }
        public int Slot { get; }
        public UnitStats Stats { get; }
        public AttackType AttackType { get; }
        public int CurrentHealth { get; }
        public bool IsDead => CurrentHealth <= 0;

        public BattleUnitState(int id, BattleSide side, int slot, UnitStats stats, int currentHealth, AttackType attackType = AttackType.Front)
        {
            if (side != BattleSide.MySide && side != BattleSide.Enemies)
            {
                throw new ArgumentOutOfRangeException(nameof(side));
            }
            if (slot < 1 || slot > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }
            if (stats.MaxHealth <= 0)
            {
                throw new ArgumentException("초기화된 능력치가 필요합니다.", nameof(stats));
            }
            if (currentHealth > stats.MaxHealth)
            {
                throw new ArgumentOutOfRangeException(nameof(currentHealth));
            }
            if (attackType != AttackType.Front && attackType != AttackType.Skip)
            {
                throw new ArgumentOutOfRangeException(nameof(attackType));
            }

            Id = id;
            Side = side;
            Slot = slot;
            Stats = stats;
            AttackType = attackType;
            CurrentHealth = currentHealth;
        }
    }
}
