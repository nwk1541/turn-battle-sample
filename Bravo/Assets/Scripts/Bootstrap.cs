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
        var titleUI = await AssetManager.InstantiateAsync<TitleUI>(AssetAddresses.TITLE_UI);
        titleUI.Init();
    }
}
