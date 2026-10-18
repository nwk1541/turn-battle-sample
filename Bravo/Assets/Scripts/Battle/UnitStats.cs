using System;

namespace Noname.Battle
{
    /// <summary>
    /// 설정 자산에서 복사한 전투용 능력치. 치명타율은 0~1 범위이다.
    /// </summary>
    public readonly struct UnitStats
    {
        public int MaxHealth { get; }
        public int Defense { get; }
        public int AttackPower { get; }
        public float CriticalRate { get; }
        public int Speed { get; }

        public UnitStats(int maxHealth, int defense, int attackPower, float criticalRate, int speed)
        {
            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            }
            if (defense < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(defense));
            }
            if (attackPower < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attackPower));
            }
            if (float.IsNaN(criticalRate) || criticalRate < 0f || criticalRate > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(criticalRate));
            }
            if (speed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(speed));
            }

            MaxHealth = maxHealth;
            Defense = defense;
            AttackPower = attackPower;
            CriticalRate = criticalRate;
            Speed = speed;
        }
    }
}
