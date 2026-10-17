using UnityEngine;
using UnityEngine.UI;

namespace Noname.UI
{
    public sealed class TitleUI : UIBase
    {
        [SerializeField] private Button _playButton;

        public override void Init()
        {
            BindButton(_playButton, OnPlayClicked);
        }

        private void OnPlayClicked()
        {
            // 기존 씬을 교체하기 전에 타이틀의 콜백과 인스턴스를 반환한다.
            Release();

            // 전투 진입 및 씬 교체 요청은 후속 작업에서 이 지점에 연결한다.
        }
    }
}
