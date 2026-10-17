using Cysharp.Threading.Tasks;
using Noname.Battle;

/// <summary>
/// 외부에 전투 시작 등의 제어 API를 제공하고 요청과 데이터를 Caster에 전달하는 진입점.
/// Caster의 완료 결과를 받아 외부에 전달한다.
/// 전투 내부 클래스는 이 싱글톤을 참조하지 않는다.
/// </summary>
public sealed class BattleManager
{
    public static BattleManager Instance { get; } = new BattleManager();

    private readonly BattleCaster _battleCaster = new BattleCaster();

    private BattleManager()
    {
    }

    /// <summary>
    /// 소유한 Caster에 전투 시작 요청을 전달한다.
    /// </summary>
    public void StartBattle()
    {
        StartBattleAsync().Forget();
    }

    private async UniTask StartBattleAsync()
    {
        await _battleCaster.StartBattleAsync();
    }
}
