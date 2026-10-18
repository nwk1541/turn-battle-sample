using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Noname.Battle.Tests
{
    public sealed class BattleRulesTests
    {
        // 밸런스 자산과 독립적인 테스트 입력이다. 행동 순서와 대상 선택 테스트는 피해 없이 진행한다.
        private const int TEST_HEALTH = 10;
        private const int TEST_SEED = 123;
        // 구현의 상수를 읽지 않고, 요구사항인 20라운드 경계를 독립적으로 검증한다.
        private const int EXPECTED_ROUND_LIMIT = 20;

        private static BattleUnitState Unit(int id, BattleSide side, int slot = 1, int speed = 1, int health = TEST_HEALTH, int attack = 0, int defense = 0, float critical = 0f, AttackType attackType = AttackType.Front)
        {
            var stats = new UnitStats(Math.Max(1, health), defense, attack, critical, speed);
            return new BattleUnitState(id, side, slot, stats, health, attackType);
        }

        [TestCase(0f, 0d, 7, false)]
        [TestCase(1f, 0.99d, 12, true)]
        [TestCase(0.5f, 0.49d, 12, true)]
        [TestCase(0.5f, 0.5d, 7, false)]
        [Description("피해 계산 예시와 치명타 확률 경계")]
        public void DamageMatchesRuleExamples(float criticalRate, double roll, int expectedDamage, bool expectedCritical)
        {
            // 공격력 11, 방어력 4: 일반 피해 7, 치명타 피해 floor(16.5 - 4) = 12.
            // 기대값을 DamageFormula로 다시 계산하지 않아 계산 순서와 배율 변경을 탐지한다.
            var attacker = Unit(1, BattleSide.MySide, attack: 11, critical: criticalRate);
            var target = Unit(2, BattleSide.Enemies, health: 20, defense: 4);

            var result = DamageFormula.Calculate(attacker, target, roll);

            Assert.That(result.AttackerId, Is.EqualTo(attacker.Id));
            Assert.That(result.TargetId, Is.EqualTo(target.Id));
            Assert.That(result.Damage, Is.EqualTo(expectedDamage));
            Assert.That(result.IsCritical, Is.EqualTo(expectedCritical));
            Assert.That(result.TargetFinalHealth, Is.EqualTo(target.CurrentHealth - expectedDamage));
        }

        [Test]
        [Description("피해는 음수가 되거나 남은 체력을 초과하지 않음")]
        public void DamageStaysWithinRemainingHealth()
        {
            var target = Unit(2, BattleSide.Enemies, defense: 1);
            var blockedAttacker = Unit(1, BattleSide.MySide, attack: 0);
            var lethalAttacker = Unit(1, BattleSide.MySide, attack: target.CurrentHealth + target.Stats.Defense + 1);

            var blocked = DamageFormula.Calculate(blockedAttacker, target, 0d);
            var lethal = DamageFormula.Calculate(lethalAttacker, target, 0d);

            Assert.That(blocked.Damage, Is.Zero);
            Assert.That(blocked.TargetFinalHealth, Is.EqualTo(target.CurrentHealth));
            Assert.That(lethal.Damage, Is.EqualTo(target.CurrentHealth));
            Assert.That(lethal.TargetFinalHealth, Is.Zero);
            Assert.That(lethal.IsTargetDead, Is.True);
        }

        [Test]
        [Description("최고 속도 선공, 진영 교대와 진영 내 속도순 행동 후 다음 라운드 시작")]
        public void SpeedOrderAdvancesRoundAfterEachUnitActs()
        {
            var myFast = Unit(1, BattleSide.MySide, speed: 3);
            var mySlow = Unit(2, BattleSide.MySide, slot: 4, speed: 1);
            var enemyFast = Unit(3, BattleSide.Enemies, speed: 4);
            var enemySlow = Unit(4, BattleSide.Enemies, slot: 4, speed: 2);
            var ruler = new BattleRuler(new[] { mySlow, enemySlow, myFast, enemyFast }, TEST_SEED);
            var initialRound = ruler.Round;

            foreach (var expected in new[] { enemyFast, myFast, enemySlow, mySlow })
            {
                Assert.That(ruler.Round, Is.EqualTo(initialRound));
                Assert.That(ruler.ExecuteNextAction().AttackerId, Is.EqualTo(expected.Id));
            }

            Assert.That(ruler.Round, Is.EqualTo(initialRound + 1));
            Assert.That(ruler.NextUnit.Value.Id, Is.EqualTo(enemyFast.Id));
        }

        [Test]
        [Description("속도 동률은 아군과 작은 슬롯 우선, 인원이 많은 진영의 남은 행동 후 라운드 전환")]
        public void SpeedTiesAndUnequalSidesKeepTurnOrder()
        {
            var myFront = Unit(1, BattleSide.MySide);
            var myRear = Unit(2, BattleSide.MySide, slot: 4);
            var enemy = Unit(3, BattleSide.Enemies);
            var ruler = new BattleRuler(new[] { myRear, enemy, myFront }, TEST_SEED);
            var initialRound = ruler.Round;

            foreach (var expected in new[] { myFront, enemy, myRear })
            {
                Assert.That(ruler.Round, Is.EqualTo(initialRound));
                Assert.That(ruler.ExecuteNextAction().AttackerId, Is.EqualTo(expected.Id));
            }

            Assert.That(ruler.Round, Is.EqualTo(initialRound + 1));
            Assert.That(ruler.NextUnit.Value.Id, Is.EqualTo(enemy.Id));
        }

        [Test]
        [Description("행동 전 사망한 적을 제외하고, 비어진 라인 대신 다음 라인을 탐색")]
        public void DeathRemovesActionAndChangesTargetLane()
        {
            var victim = Unit(2, BattleSide.Enemies, speed: 2, health: 1);
            var survivor = Unit(3, BattleSide.Enemies, slot: 2);
            var attacker = Unit(1, BattleSide.MySide, speed: 3, attack: victim.CurrentHealth);
            var ruler = new BattleRuler(new[] { victim, attacker, survivor }, TEST_SEED);

            var lethal = ruler.ExecuteNextAction();
            var enemyAction = ruler.ExecuteNextAction();
            var nextAttack = ruler.ExecuteNextAction();

            Assert.That(lethal.TargetId, Is.EqualTo(victim.Id));
            Assert.That(lethal.IsTargetDead, Is.True);
            Assert.That(enemyAction.AttackerId, Is.EqualTo(survivor.Id));
            Assert.That(enemyAction.TargetId, Is.EqualTo(attacker.Id));
            Assert.That(nextAttack.AttackerId, Is.EqualTo(attacker.Id));
            Assert.That(nextAttack.TargetId, Is.EqualTo(survivor.Id));
        }

        [TestCase(AttackType.Front)]
        [TestCase(AttackType.Skip)]
        [Description("자기 라인에서 정면과 건너 뜀 적용, 사망 유닛과 빈칸은 제외")]
        public void AttackTypeSelectsLivingTargetInOwnLane(AttackType attackType)
        {
            var attacker = Unit(1, BattleSide.MySide, slot: 8, speed: 2, attackType: attackType);
            var dead = Unit(2, BattleSide.Enemies, slot: 2, health: 0);
            var front = Unit(3, BattleSide.Enemies, slot: 5);
            var rear = Unit(4, BattleSide.Enemies, slot: 11);
            var otherLane = Unit(5, BattleSide.Enemies, slot: 3);
            var ruler = new BattleRuler(new[] { otherLane, rear, dead, attacker, front }, TEST_SEED);
            var expected = attackType == AttackType.Front ? front : rear;

            Assert.That(ruler.ExecuteNextAction().TargetId, Is.EqualTo(expected.Id));
        }

        [TestCase(1, 2, 3)]
        [TestCase(2, 3, 1)]
        [TestCase(3, 1, 2)]
        [Description("빈 자기 라인 다음에는 아래 라인 우선, 맨 아래 다음은 맨 위")]
        public void EmptyLaneSearchesDownwardAndWraps(int ownSlot, int nextSlot, int lastSlot)
        {
            var attacker = Unit(1, BattleSide.Enemies, slot: ownSlot, speed: 2);
            var dead = Unit(2, BattleSide.MySide, slot: ownSlot, health: 0);
            var next = Unit(3, BattleSide.MySide, slot: nextSlot);
            var last = Unit(4, BattleSide.MySide, slot: lastSlot);
            var ruler = new BattleRuler(new[] { last, attacker, dead, next }, TEST_SEED);

            Assert.That(ruler.ExecuteNextAction().TargetId, Is.EqualTo(next.Id));
        }

        [Test]
        [Description("피격 후 건너 뜀 유형을 유지하고, 자기 라인에 적이 한 명이면 다른 라인으로 바꾸지 않음")]
        public void DamagedSkipUnitKeepsTypeAndFallsBackInOwnLane()
        {
            var front = Unit(1, BattleSide.MySide, speed: 3, attack: 1);
            var otherFront = Unit(2, BattleSide.MySide, slot: 2);
            var otherRear = Unit(3, BattleSide.MySide, slot: 5);
            var skipUnit = Unit(4, BattleSide.Enemies, speed: 2, health: front.Stats.AttackPower + 1, attackType: AttackType.Skip);
            var ruler = new BattleRuler(new[] { otherFront, otherRear, skipUnit, front }, TEST_SEED);

            ruler.ExecuteNextAction();
            var result = ruler.ExecuteNextAction();

            Assert.That(ruler.Units.Single(unit => unit.Id == skipUnit.Id).AttackType, Is.EqualTo(skipUnit.AttackType));
            Assert.That(result.AttackerId, Is.EqualTo(skipUnit.Id));
            Assert.That(result.TargetId, Is.EqualTo(front.Id));
        }

        [Test]
        [Description("20라운드의 마지막 행동까지 진행한 뒤 무승부 종료")]
        public void RoundLimitWaitsForAllRemainingActions()
        {
            var units = new[] { Unit(1, BattleSide.MySide), Unit(2, BattleSide.MySide, slot: 4), Unit(3, BattleSide.Enemies) };
            var ruler = new BattleRuler(units, TEST_SEED);
            var totalActions = EXPECTED_ROUND_LIMIT * units.Length;

            for (var action = 0; action < totalActions; action++)
            {
                Assert.That(ruler.IsFinished, Is.False);
                Assert.That(ruler.Round, Is.EqualTo(action / units.Length + 1));
                ruler.ExecuteNextAction();
            }

            Assert.That(ruler.Round, Is.EqualTo(EXPECTED_ROUND_LIMIT));
            Assert.That(ruler.IsDraw, Is.True);
            Assert.That(ruler.Winner, Is.Null);
            Assert.That(ruler.NextUnit, Is.Null);
            Assert.Throws<InvalidOperationException>(() => ruler.ExecuteNextAction());
        }

        [Test]
        [Description("마지막 라운드의 마지막 행동으로 승패가 나면 무승부보다 우선")]
        public void VictoryOnFinalActionTakesPriorityOverDraw()
        {
            var enemy = Unit(2, BattleSide.Enemies, attack: 1);
            var mine = Unit(1, BattleSide.MySide, speed: enemy.Stats.Speed + 1, health: EXPECTED_ROUND_LIMIT * enemy.Stats.AttackPower);
            var units = new[] { mine, enemy };
            var ruler = new BattleRuler(units, TEST_SEED);
            var totalActions = EXPECTED_ROUND_LIMIT * units.Length;

            for (var action = 0; action < totalActions - 1; action++)
            {
                ruler.ExecuteNextAction();
            }
            Assert.That(ruler.IsFinished, Is.False);
            var result = ruler.ExecuteNextAction();

            Assert.That(result.IsTargetDead, Is.True);
            Assert.That(ruler.Round, Is.EqualTo(EXPECTED_ROUND_LIMIT));
            Assert.That(ruler.IsFinished, Is.True);
            Assert.That(ruler.IsDraw, Is.False);
            Assert.That(ruler.Winner, Is.EqualTo(enemy.Side));
            Assert.That(ruler.NextUnit, Is.Null);
            Assert.Throws<InvalidOperationException>(() => ruler.ExecuteNextAction());
        }

        [Test]
        [Description("전투별 상태는 독립적이며 같은 입력과 시드는 같은 결과를 생성")]
        public void IndependentBattlesReproduceResults()
        {
            var mine = Unit(1, BattleSide.MySide, attack: 2, critical: 0.5f);
            var enemy = Unit(2, BattleSide.Enemies, attack: 1, critical: 0.5f);
            var input = new[] { mine, enemy };
            var first = new BattleRuler(input, TEST_SEED);
            var second = new BattleRuler(input, TEST_SEED);
            input[0] = Unit(99, BattleSide.MySide);

            var firstResult = first.ExecuteNextAction();

            Assert.That(firstResult.AttackerId, Is.EqualTo(mine.Id));
            Assert.That(second.Units.Single(unit => unit.Id == enemy.Id), Is.EqualTo(enemy));
            Assert.That(second.ExecuteNextAction(), Is.EqualTo(firstResult));
            for (var action = 0; action < input.Length && !first.IsFinished; action++)
            {
                Assert.That(second.ExecuteNextAction(), Is.EqualTo(first.ExecuteNextAction()));
            }
            Assert.That(first.IsFinished, Is.EqualTo(second.IsFinished));
        }

        [Test]
        [Description("설정 자산을 바꿔도 이미 만든 전투 상태의 능력치와 공격 유형은 유지")]
        public void UnitConfigCreatesIndependentState()
        {
            var config = ScriptableObject.CreateInstance<UnitConfig>();
            try
            {
                using (var serialized = new SerializedObject(config))
                {
                    serialized.FindProperty("_attackType").intValue = (int)AttackType.Skip;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    var expectedStats = config.CreateStats();
                    var expectedType = config.AttackType;
                    var state = config.CreateState(1, BattleSide.MySide, 4);

                    serialized.FindProperty("_attackType").intValue = (int)AttackType.Front;
                    serialized.FindProperty("_maxHealth").intValue = expectedStats.MaxHealth + 1;
                    serialized.ApplyModifiedPropertiesWithoutUndo();

                    Assert.That(state.Stats, Is.EqualTo(expectedStats));
                    Assert.That(state.CurrentHealth, Is.EqualTo(expectedStats.MaxHealth));
                    Assert.That(state.AttackType, Is.EqualTo(expectedType));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        [Description("유닛 식별자와 같은 진영의 슬롯 중복은 초기화 시 거부")]
        public void DuplicateIdentityOrSlotIsRejected()
        {
            var mine = Unit(1, BattleSide.MySide);
            var enemy = Unit(2, BattleSide.Enemies);
            var duplicateId = Unit(mine.Id, enemy.Side);
            var duplicateSlot = Unit(3, mine.Side, slot: mine.Slot);

            Assert.Throws<ArgumentException>(() => new BattleRuler(new[] { mine, duplicateId }, TEST_SEED));
            Assert.Throws<ArgumentException>(() => new BattleRuler(new[] { mine, enemy, duplicateSlot }, TEST_SEED));
        }
    }
}
