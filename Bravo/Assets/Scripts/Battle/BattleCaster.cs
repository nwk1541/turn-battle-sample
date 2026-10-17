using Cysharp.Threading.Tasks;
using Noname.Asset;

namespace Noname.Battle
{
    /// <summary>
    /// 전투 한 판의 준비, 진행, 정리를 담당한다.
    /// 로직 초기화, 프리팹 로드, UI 연결과 행동 및 연출 순서를 조율한다.
    /// UI와 모션 핸들이 없으면 표현과 연출 대기를 생략하고 전투를 진행한다.
    /// </summary>
    public sealed class BattleCaster
    {
        /// <summary>
        /// 전투 진입을 준비한다. 현재는 전투 씬 로드 단계까지 수행한다.
        /// </summary>
        public async UniTask StartBattleAsync()
        {
            await AssetManager.LoadSceneAsync(AssetAddresses.BATTLE_SCENE);
        }
    }
}
