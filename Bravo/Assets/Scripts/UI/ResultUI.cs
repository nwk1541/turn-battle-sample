using Noname.Battle;
using TMPro;
using UnityEngine;

namespace Noname.UI
{
    public class ResultUI : UIBase
    {
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private UnityEngine.UI.Button _returnButton;

        public override void Init()
        {
            BindButton(_returnButton, OnClickReturn);
        }

        public void SetResult(BattleResult result)
        {
            _resultText.text = result.IsDraw ? "Draw" : result.Winner == BattleSide.MySide ? "Victory" : "Defeat";
        }

        private void OnClickReturn()
        {
            _returnButton.interactable = false;
            BattleManager.Instance.ReturnToTitle();
        }
    }
}
