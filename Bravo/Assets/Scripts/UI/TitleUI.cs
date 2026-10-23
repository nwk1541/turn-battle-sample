using UnityEngine;
using UnityEngine.UI;

namespace Noname.UI
{
    public sealed class TitleUI : UIBase
    {
        [SerializeField] private Button _playButton;

        public override void Init()
        {
            BindButton(_playButton, OnClickPlay);
        }

        private void OnClickPlay()
        {
            // 기존 씬을 교체하기 전에 타이틀의 콜백과 인스턴스를 반환한다.
            Release();

            BattleManager.Instance.StartBattle();
        }
    }
}
