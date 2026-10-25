using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Noname.UI
{
    /// <summary>
    /// UIManager가 최초 생성 시 Init, 스택에서 제거할 때 Release를 호출한다. Pop으로 재표시할 때는 초기화하지 않는다.
    /// </summary>
    public abstract class UIBase : MonoBehaviour
    {
        private readonly List<(UnityEngine.UI.Button Button, UnityAction Callback)> _buttonBindings = new List<(UnityEngine.UI.Button, UnityAction)>();

        public abstract void Init();

        public virtual void Release()
        {
            foreach ((UnityEngine.UI.Button Button, UnityAction Callback) binding in _buttonBindings)
            {
                if (binding.Button != null)
                {
                    binding.Button.onClick.RemoveListener(binding.Callback);
                }
            }

            _buttonBindings.Clear();
        }

        protected void BindButton(UnityEngine.UI.Button button, UnityAction callback)
        {
            button.onClick.AddListener(callback);
            _buttonBindings.Add((button, callback));
        }
    }
}
