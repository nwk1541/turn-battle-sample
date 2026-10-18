using System;

namespace Noname.Battle
{
    /// <summary>
    /// 상태를 변경하지 않는 피해 계산기. 난수는 호출자가 전달하여 결과를 재현한다.
    /// </summary>
    public static class DamageFormula
    {
        private const double CRITICAL_MULTIPLIER = 1.5;

        /// <summary>
        /// criticalRoll은 0 이상 1 미만이다. 치명타 배율 적용 후 방어력을 빼고 소수점을 버린다.
        /// 피해는 0 이상이며, 대상의 남은 체력을 초과하지 않는다.
        /// </summary>
        public static DamageDesc Calculate(BattleUnitState attacker, BattleUnitState target, double criticalRoll)
        {
            if (attacker.IsDead)
            {
                throw new ArgumentException("사망한 유닛은 공격할 수 없습니다.", nameof(attacker));
            }
            if (target.IsDead)
            {
                throw new ArgumentException("사망한 유닛은 피격 대상이 될 수 없습니다.", nameof(target));
            }
            if (double.IsNaN(criticalRoll) || criticalRoll < 0d || criticalRoll >= 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(criticalRoll));
            }

            var isCritical = criticalRoll < attacker.Stats.CriticalRate;
            var attackPower = attacker.Stats.AttackPower * (isCritical ? CRITICAL_MULTIPLIER : 1d);
            var calculatedDamage = Math.Max(0d, Math.Floor(attackPower - target.Stats.Defense));
            var damage = (int)Math.Min(target.CurrentHealth, calculatedDamage);

            return new DamageDesc(attacker.Id, target.Id, damage, isCritical, target.CurrentHealth - damage);
        }
    }
}
