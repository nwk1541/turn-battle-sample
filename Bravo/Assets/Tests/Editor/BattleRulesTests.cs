using System;
using System.Linq;
using NUnit.Framework;

namespace Noname.Battle.Tests
{
    public sealed class BattleRulesTests
    {
        private const int TEST_HEALTH = 10;
        private const int TEST_SEED = 123;
        // 종료 경계의 기대값이 구현 상수와 함께 바뀌지 않도록 독립적으로 둔다.
        private const int EXPECTED_ROUND_LIMIT = 20;

        [TestCase(0f, 0d, 7, false)]
        [TestCase(1f, 0.99d, 12, true)]
        [TestCase(0.5f, 0.49d, 12, true)]
        [TestCase(0.5f, 0.5d, 7, false)]
        [Description("피해 계산 예시와 치명타 확률 경계")]
        public void DamageMatchesRuleExamples(float criticalRate, double roll, int expectedDamage, bool expectedCritical)
        {
            // 공격력 11, 방어력 4: 일반 피해 7, 치명타 피해 floor(16.5 - 4) = 12.
            BattleUnitState attacker = Unit(1, BattleSide.MySide, attack: 11, critical: criticalRate);
            BattleUnitState target = Unit(2, BattleSide.Enemies, health: 20, defense: 4);

            DamageDesc result = DamageFormula.Calculate(attacker, target, roll);

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
            BattleUnitState target = Unit(2, BattleSide.Enemies, defense: 1);
            BattleUnitState blockedAttacker = Unit(1, BattleSide.MySide, attack: 0);
            BattleUnitState lethalAttacker = Unit(1, BattleSide.MySide, attack: target.CurrentHealth + target.Stats.Defense + 1);

            DamageDesc blocked = DamageFormula.Calculate(blockedAttacker, target, 0d);
            DamageDesc lethal = DamageFormula.Calculate(lethalAttacker, target, 0d);

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
            BattleUnitState myFast = Unit(1, BattleSide.MySide, speed: 3);
            BattleUnitState mySlow = Unit(2, BattleSide.MySide, slot: 4, speed: 1);
            BattleUnitState enemyFast = Unit(3, BattleSide.Enemies, speed: 4);
            BattleUnitState enemySlow = Unit(4, BattleSide.Enemies, slot: 4, speed: 2);
            BattleRuler ruler = new BattleRuler(new[] { mySlow, enemySlow, myFast, enemyFast }, TEST_SEED);
            int initialRound = ruler.Round;

            foreach (BattleUnitState expected in new[] { enemyFast, myFast, enemySlow, mySlow })
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
            BattleUnitState myFront = Unit(1, BattleSide.MySide);
            BattleUnitState myRear = Unit(2, BattleSide.MySide, slot: 4);
            BattleUnitState enemy = Unit(3, BattleSide.Enemies);
            BattleRuler ruler = new BattleRuler(new[] { myRear, enemy, myFront }, TEST_SEED);
            int initialRound = ruler.Round;

            foreach (BattleUnitState expected in new[] { myFront, enemy, myRear })
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
            BattleUnitState victim = Unit(2, BattleSide.Enemies, speed: 2, health: 1);
            BattleUnitState survivor = Unit(3, BattleSide.Enemies, slot: 2);
            BattleUnitState attacker = Unit(1, BattleSide.MySide, speed: 3, attack: victim.CurrentHealth);
            BattleRuler ruler = new BattleRuler(new[] { victim, attacker, survivor }, TEST_SEED);

            DamageDesc lethal = ruler.ExecuteNextAction();
            DamageDesc enemyAction = ruler.ExecuteNextAction();
            DamageDesc nextAttack = ruler.ExecuteNextAction();

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
            BattleUnitState attacker = Unit(1, BattleSide.MySide, slot: 8, speed: 2, attackType: attackType);
            BattleUnitState dead = Unit(2, BattleSide.Enemies, slot: 2, health: 0);
            BattleUnitState front = Unit(3, BattleSide.Enemies, slot: 5);
            BattleUnitState rear = Unit(4, BattleSide.Enemies, slot: 11);
            BattleUnitState otherLane = Unit(5, BattleSide.Enemies, slot: 3);
            BattleRuler ruler = new BattleRuler(new[] { otherLane, rear, dead, attacker, front }, TEST_SEED);
            BattleUnitState expected = attackType == AttackType.Front ? front : rear;

            Assert.That(ruler.ExecuteNextAction().TargetId, Is.EqualTo(expected.Id));
        }

        [TestCase(1, 2, 3)]
        [TestCase(2, 3, 1)]
        [TestCase(3, 1, 2)]
        [Description("빈 자기 라인 다음에는 아래 라인 우선, 맨 아래 다음은 맨 위")]
        public void EmptyLaneSearchesDownwardAndWraps(int ownSlot, int nextSlot, int lastSlot)
        {
            BattleUnitState attacker = Unit(1, BattleSide.Enemies, slot: ownSlot, speed: 2);
            BattleUnitState dead = Unit(2, BattleSide.MySide, slot: ownSlot, health: 0);
            BattleUnitState next = Unit(3, BattleSide.MySide, slot: nextSlot);
            BattleUnitState last = Unit(4, BattleSide.MySide, slot: lastSlot);
            BattleRuler ruler = new BattleRuler(new[] { last, attacker, dead, next }, TEST_SEED);

            Assert.That(ruler.ExecuteNextAction().TargetId, Is.EqualTo(next.Id));
        }

        [Test]
        [Description("피격 후 건너 뜀 유형을 유지하고, 자기 라인에 적이 한 명이면 다른 라인으로 바꾸지 않음")]
        public void DamagedSkipUnitKeepsTypeAndFallsBackInOwnLane()
        {
            BattleUnitState front = Unit(1, BattleSide.MySide, speed: 3, attack: 1);
            BattleUnitState otherFront = Unit(2, BattleSide.MySide, slot: 2);
            BattleUnitState otherRear = Unit(3, BattleSide.MySide, slot: 5);
            BattleUnitState skipUnit = Unit(4, BattleSide.Enemies, speed: 2, health: front.Stats.AttackPower + 1, attackType: AttackType.Skip);
            BattleRuler ruler = new BattleRuler(new[] { otherFront, otherRear, skipUnit, front }, TEST_SEED);

            ruler.ExecuteNextAction();
            DamageDesc result = ruler.ExecuteNextAction();

            Assert.That(ruler.Units.Single(unit => unit.Id == skipUnit.Id).AttackType, Is.EqualTo(skipUnit.AttackType));
            Assert.That(result.AttackerId, Is.EqualTo(skipUnit.Id));
            Assert.That(result.TargetId, Is.EqualTo(front.Id));
        }

        [Test]
        [Description("20라운드의 마지막 행동까지 진행한 뒤 무승부 종료")]
        public void RoundLimitWaitsForAllRemainingActions()
        {
            BattleUnitState[] units = new[] { Unit(1, BattleSide.MySide), Unit(2, BattleSide.MySide, slot: 4), Unit(3, BattleSide.Enemies) };
            BattleRuler ruler = new BattleRuler(units, TEST_SEED);
            int totalActions = EXPECTED_ROUND_LIMIT * units.Length;

            for (int action = 0; action < totalActions; action++)
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
            BattleUnitState enemy = Unit(2, BattleSide.Enemies, attack: 1);
            BattleUnitState mine = Unit(1, BattleSide.MySide, speed: enemy.Stats.Speed + 1, health: EXPECTED_ROUND_LIMIT * enemy.Stats.AttackPower);
            BattleUnitState[] units = new[] { mine, enemy };
            BattleRuler ruler = new BattleRuler(units, TEST_SEED);
            int totalActions = EXPECTED_ROUND_LIMIT * units.Length;

            for (int action = 0; action < totalActions - 1; action++)
            {
                ruler.ExecuteNextAction();
            }
            Assert.That(ruler.IsFinished, Is.False);
            DamageDesc result = ruler.ExecuteNextAction();

            Assert.That(result.IsTargetDead, Is.True);
            Assert.That(ruler.Round, Is.EqualTo(EXPECTED_ROUND_LIMIT));
            Assert.That(ruler.IsFinished, Is.True);
            Assert.That(ruler.IsDraw, Is.False);
            Assert.That(ruler.Winner, Is.EqualTo(enemy.Side));
            Assert.That(ruler.NextUnit, Is.Null);
            Assert.Throws<InvalidOperationException>(() => ruler.ExecuteNextAction());
        }

        [Test]
        [Description("유닛 식별자와 같은 진영의 슬롯 중복은 초기화 시 거부")]
        public void DuplicateIdentityOrSlotIsRejected()
        {
            BattleUnitState mine = Unit(1, BattleSide.MySide);
            BattleUnitState enemy = Unit(2, BattleSide.Enemies);
            BattleUnitState duplicateId = Unit(mine.Id, enemy.Side);
            BattleUnitState duplicateSlot = Unit(3, mine.Side, slot: mine.Slot);

            Assert.Throws<ArgumentException>(() => new BattleRuler(new[] { mine, duplicateId }, TEST_SEED));
            Assert.Throws<ArgumentException>(() => new BattleRuler(new[] { mine, enemy, duplicateSlot }, TEST_SEED));
        }

        // 순서와 대상 선택 테스트는 기본 공격력을 0으로 두어 사망이 시나리오를 바꾸지 않게 한다.
        private static BattleUnitState Unit(int id, BattleSide side, int slot = 1, int speed = 1, int health = TEST_HEALTH, int attack = 0, int defense = 0, float critical = 0f, AttackType attackType = AttackType.Front)
        {
            // 사망 유닛도 최대 체력은 양수여야 하므로 현재 체력과 별도로 보정한다.
            UnitStats stats = new UnitStats(Math.Max(1, health), defense, attack, critical, speed);
            return new BattleUnitState(id, side, slot, stats, health, attackType);
        }
    }
}
