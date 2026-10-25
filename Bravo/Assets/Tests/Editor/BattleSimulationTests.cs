using System.Linq;
using NUnit.Framework;

namespace Noname.Battle.Tests
{
    public sealed class BattleSimulationTests
    {
        private const int TEST_SEED = 123;

        [TestCase(BattleSide.MySide)]
        [TestCase(BattleSide.Enemies)]
        public void DataBattleReturnsVictoryAndFinalHealth(BattleSide winningSide)
        {
            BattleSide losingSide = winningSide == BattleSide.MySide ? BattleSide.Enemies : BattleSide.MySide;
            BattleUnitState winner = Unit(1, winningSide, attack: 5, speed: 2);
            BattleUnitState loser = Unit(2, losingSide, attack: 0);
            BattleStartData data = new BattleStartData(new[] { winner, loser }, TEST_SEED);

            BattleResult result = new BattleCaster().Simulate(data);

            Assert.That(result.Winner, Is.EqualTo(winningSide));
            Assert.That(result.IsDraw, Is.False);
            Assert.That(result.Round, Is.EqualTo(1));
            Assert.That(result.Units.Single(unit => unit.Id == winner.Id).CurrentHealth, Is.EqualTo(winner.CurrentHealth));
            Assert.That(result.Units.Single(unit => unit.Id == loser.Id).CurrentHealth, Is.Zero);
            Assert.That(data.Units, Is.EqualTo(new[] { winner, loser }));
        }

        [Test]
        public void DataBattleReturnsDrawAfterTwentyCompleteRounds()
        {
            BattleUnitState[] units = new[] { Unit(1, BattleSide.MySide, attack: 0), Unit(2, BattleSide.Enemies, attack: 0) };

            BattleResult result = new BattleCaster().Simulate(new BattleStartData(units, TEST_SEED));

            Assert.That(result.IsDraw, Is.True);
            Assert.That(result.Winner, Is.Null);
            Assert.That(result.Round, Is.EqualTo(20));
            Assert.That(result.Units, Is.EqualTo(units));
        }

        // 서로 다른 종료 라운드와 생존 체력을 기대해 난수 고정과 시드 무시 오류를 탐지한다.
        [TestCase(123, 3, 0, 7)]
        [TestCase(7, 2, 9, 3)]
        [Description("고정 시드의 예상 결과, 반복 실행 재현성과 입력 및 병종 보존")]
        public void SeededScenarioMatchesExpectedResultAndPreservesInput(int seed, int expectedRound, int expectedSwordsmanHealth, int expectedArcherHealth)
        {
            BattleUnitState[] input = new[]
            {
                new BattleUnitState(1, BattleSide.MySide, 1, new UnitStats(12, 1, 5, 0.5f, 4), 12, AttackType.Front, UnitType.Swordsman),
                new BattleUnitState(2, BattleSide.MySide, 2, new UnitStats(10, 0, 4, 0.5f, 2), 10, AttackType.Skip, UnitType.Archer),
                new BattleUnitState(3, BattleSide.Enemies, 1, new UnitStats(12, 1, 4, 0.5f, 3), 12, AttackType.Front, UnitType.Spearman),
                new BattleUnitState(4, BattleSide.Enemies, 2, new UnitStats(10, 0, 3, 0.5f, 1), 10, AttackType.Front, UnitType.Cavalry)
            };
            BattleStartData data = new BattleStartData(input, seed);
            BattleUnitState[] snapshot = data.Units.ToArray();
            // 원본 편성을 바꿔도 BattleStartData는 생성 시점의 편성을 유지해야 한다.
            input[0] = Unit(99, BattleSide.MySide);

            int[] expectedHealth = new[] { expectedSwordsmanHealth, expectedArcherHealth, 0, 0 };
            BattleUnitState[] expectedUnits = snapshot.Select((unit, index) => new BattleUnitState(unit.Id, unit.Side, unit.Slot, unit.Stats, expectedHealth[index], unit.AttackType, unit.UnitType)).ToArray();
            BattleCaster caster = new BattleCaster();

            BattleResult first = caster.Simulate(data);
            BattleResult second = caster.Simulate(data);

            foreach (BattleResult result in new[] { first, second })
            {
                Assert.That(result.Winner, Is.EqualTo(BattleSide.MySide));
                Assert.That(result.IsDraw, Is.False);
                Assert.That(result.Round, Is.EqualTo(expectedRound));
                Assert.That(result.Units, Is.EqualTo(expectedUnits));
            }
            Assert.That(data.Seed, Is.EqualTo(seed));
            Assert.That(data.Units, Is.EqualTo(snapshot));
        }

        private static BattleUnitState Unit(int id, BattleSide side, int health = 5, int attack = 1, int speed = 1)
        {
            return new BattleUnitState(id, side, 1, new UnitStats(health, 0, attack, 0f, speed), health);
        }
    }
}
