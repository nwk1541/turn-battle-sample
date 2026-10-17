namespace Noname.Battle
{
    /// <summary>
    /// 전투 상태와 결과를 UI에 표현하기 위한 연결 규약.
    /// 전투 시작 같은 외부 제어 요청은 BattleManager의 API를 통해 전달한다.
    /// 표현 메서드는 후속 구현에서 정의한다.
    /// </summary>
    public interface IBattleUIHandle
    {
    }
}
