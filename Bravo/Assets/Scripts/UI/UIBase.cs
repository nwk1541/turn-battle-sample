using System.Collections.Generic;
using Noname.Asset;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace Noname.UI
{
    /// <summary>
    /// AssetManager로 생성한 UI의 버튼 콜백과 인스턴스 반환을 관리한다.
    /// 생성 후 Init을 한 번 호출하고, UI 종료 시 씬을 교체하기 전에 Release를 호출한다.
    /// </summary>
    public abstract class UIBase : MonoBehaviour
    {
        private readonly List<(Button Button, UnityAction Callback)> _buttonBindings = new List<(Button, UnityAction)>();

        public abstract void Init();

        protected void BindButton(Button button, UnityAction callback)
        {
            button.onClick.AddListener(callback);
            _buttonBindings.Add((button, callback));
        }

        public virtual void Release()
        {
            foreach (var binding in _buttonBindings)
            {
                if (binding.Button != null)
                {
                    binding.Button.onClick.RemoveListener(binding.Callback);
                }
            }

            _buttonBindings.Clear();
            AssetManager.ReleaseInstance(gameObject);
        }
    }
}
