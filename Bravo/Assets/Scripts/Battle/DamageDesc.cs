namespace Noname.Battle
{
    /// <summary>
    /// 한 번의 타격 결과. Damage는 남은 체력까지 적용된 실제 피해량이다.
    /// </summary>
    public readonly struct DamageDesc
    {
        public int AttackerId { get; }
        public int TargetId { get; }
        public int Damage { get; }
        public bool IsCritical { get; }
        public int TargetFinalHealth { get; }
        public bool IsTargetDead => TargetFinalHealth <= 0;

        public DamageDesc(int attackerId, int targetId, int damage, bool isCritical, int targetFinalHealth)
        {
            AttackerId = attackerId;
            TargetId = targetId;
            Damage = damage;
            IsCritical = isCritical;
            TargetFinalHealth = targetFinalHealth;
        }
    }
}
