using Noname.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Noname.UI
{
    public sealed class TitleUI : UIBase
    {
        [SerializeField] private Button _playButton = null;

        #region Override

        public override void Init()
        {
            BindButton(_playButton, OnPlayClicked);
        }

        #endregion

        private void OnPlayClicked()
        {
            // 씬 전환 중 추가 입력을 받지 않도록 버튼 바인딩을 먼저 해제한다.
            Release();

            BattleManager.Instance.StartBattle();
        }
    }
}
