using Noname.Battle;
using TMPro;
using UnityEngine;

namespace Noname.UI
{
    public class ResultUI : UIBase
    {
        [SerializeField] private TMP_Text _resultText = null;
        [SerializeField] private UnityEngine.UI.Button _returnButton = null;
        [SerializeField] private UnityEngine.UI.Button _backButton = null;

        #region Override

        public override void Init()
        {
            BindButton(_returnButton, OnClickReturn);
            BindButton(_backButton, OnClickBack);
        }

        #endregion

        public void SetResult(BattleResult result)
        {
            _resultText.text = result.IsDraw ? "Draw" : result.Winner == BattleSide.MySide ? "Victory" : "Defeat";
        }

        private void OnClickReturn()
        {
            BattleManager.Instance.ReturnToTitle();
        }

        private void OnClickBack()
        {
            UIManager.Instance.Pop();
        }
    }
}
