using System;
using System.Collections.Generic;
using UnityEngine;

namespace Noname.Battle
{
    /// <summary>
    /// 모든 병종의 공격 유형, 기본 능력치와 프리팹 설정. 전투 중 변경되는 상태는 별도로 관리한다.
    /// </summary>
    [CreateAssetMenu(fileName = "UnitConfig", menuName = "Noname/Battle/Unit Config")]
    public sealed class UnitConfig : ScriptableObject
    {
        [SerializeField, Tooltip("병종별 기본 능력치와 프리팹 설정")] private UnitDefinition[] _units = Array.Empty<UnitDefinition>();

        public IReadOnlyList<UnitDefinition> Units => _units;

        public UnitDefinition Get(UnitType unitType)
        {
            foreach (var unit in _units)
            {
                if (unit.UnitType == unitType)
                {
                    return unit;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(unitType), unitType, "설정된 병종이 없습니다.");
        }

        [Serializable]
        public sealed class UnitDefinition
        {
            [Header("유닛 분류")]
            [SerializeField, Tooltip("병종")] private UnitType _unitType = UnitType.Swordsman;
            [SerializeField, Tooltip("공격 유형")] private AttackType _attackType = AttackType.Front;
            [SerializeField, Tooltip("이 병종에 사용할 프리팹의 Addressables 주소")] private string _prefabAddress;

            [Header("기본 스탯")]
            [SerializeField, Min(1), Tooltip("최대 체력")] private int _maxHealth = 1;
            [SerializeField, Min(0), Tooltip("방어력")] private int _defense = 0;
            [SerializeField, Min(0), Tooltip("공격력")] private int _attackPower = 1;
            [SerializeField, Range(0f, 1f), Tooltip("치명타율. 0~1 범위이며 0.1은 10%를 의미한다.")] private float _criticalRate = 0f;
            [SerializeField, Min(0), Tooltip("속도")] private int _speed = 1;

            public UnitType UnitType => _unitType;
            public AttackType AttackType => _attackType;
            public string PrefabAddress => _prefabAddress;
            public int MaxHealth => _maxHealth;
            public int Defense => _defense;
            public int AttackPower => _attackPower;
            public float CriticalRate => _criticalRate;
            public int Speed => _speed;

            public UnitStats CreateStats()
            {
                return new UnitStats(_maxHealth, _defense, _attackPower, _criticalRate, _speed);
            }

            public BattleUnitState CreateState(int id, BattleSide side, int slot)
            {
                return new BattleUnitState(id, side, slot, CreateStats(), _maxHealth, _attackType, _unitType);
            }
        }
    }
}
