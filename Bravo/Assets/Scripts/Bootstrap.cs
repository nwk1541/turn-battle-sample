using Cysharp.Threading.Tasks;
using Noname.Asset;
using Noname.UI;
using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    #region MonoBehaviour

    private void Start()
    {
        LoadTitleUIAsync().Forget();
    }

    #endregion

    private async UniTask LoadTitleUIAsync()
    {
        UIManager.Instance.ClearHistory();
        await UIManager.Instance.PushAsync<TitleUI>(AssetAddresses.TITLE_UI, Application.exitCancellationToken);
    }
}
