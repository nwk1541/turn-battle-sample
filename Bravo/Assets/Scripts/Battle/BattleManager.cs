namespace TurnBattleSample
{
    /// <summary>
    /// 아웃게임의 전투 시작 요청과 데이터를 전달하고 종료 결과를 반환하는 진입점.
    /// 전투 내부 클래스는 이 싱글톤을 참조하지 않는다.
    /// </summary>
    public sealed class BattleManager
    {
        public static BattleManager Instance { get; } = new BattleManager();

        private BattleManager()
        {
        }
    }
}
