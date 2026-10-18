using UnityEngine;

namespace Noname.Battle
{
    /// <summary>
    /// 유닛의 병종, 공격 유형과 기본 능력치. 전투 중 변경되는 상태는 별도로 관리한다.
    /// </summary>
    [CreateAssetMenu(fileName = "UnitConfig", menuName = "Noname/Battle/Unit Config")]
    public sealed class UnitConfig : ScriptableObject
    {
        [Header("유닛 분류")]
        [SerializeField, Tooltip("병종")] private UnitType _unitType = UnitType.Swordsman;
        [SerializeField, Tooltip("공격 유형")] private AttackType _attackType = AttackType.Front;

        [Header("기본 스탯")]
        [SerializeField, Min(1), Tooltip("최대 체력")] private int _maxHealth = 1;
        [SerializeField, Min(0), Tooltip("방어력")] private int _defense = 0;
        [SerializeField, Min(0), Tooltip("공격력")] private int _attackPower = 1;
        [SerializeField, Range(0f, 1f), Tooltip("치명타율. 0~1 범위이며 0.1은 10%를 의미한다.")] private float _criticalRate = 0f;
        [SerializeField, Min(0), Tooltip("속도")] private int _speed = 1;

        public UnitType UnitType => _unitType;
        public AttackType AttackType => _attackType;
        public int MaxHealth => _maxHealth;
        public int Defense => _defense;
        public int AttackPower => _attackPower;
        public float CriticalRate => _criticalRate;
        public int Speed => _speed;

        public UnitStats CreateStats()
        {
            return new UnitStats(_maxHealth, _defense, _attackPower, _criticalRate, _speed);
        }
    }
}
