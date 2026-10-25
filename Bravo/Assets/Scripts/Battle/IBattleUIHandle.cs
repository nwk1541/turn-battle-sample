namespace Noname.Battle
{
    /// <summary>이미 계산된 행동 결과를 화면에 표시한다. 규칙 상태를 변경하지 않는다.</summary>
    public interface IBattleUIHandle
    {
        void ShowAction(DamageDesc damage);
    }
}
