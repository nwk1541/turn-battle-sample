using Cysharp.Threading.Tasks;
using Noname.Asset;
using Noname.UI;
using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    // 진입점
    private void Start()
    {
        LoadTitleUIAsync().Forget();
    }

    private async UniTask LoadTitleUIAsync()
    {
        UIManager.Instance.ClearHistory();
        await UIManager.Instance.PushAsync<TitleUI>(AssetAddresses.TITLE_UI, Application.exitCancellationToken);
    }
}
