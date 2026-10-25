using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Noname.UI
{
    /// <summary>
    /// UIManager가 생성 시 Init, 반환 시 Release를 호출한다. 재활성화에는 Init을 호출하지 않는다.
    /// </summary>
    public abstract class UIBase : MonoBehaviour
    {
        private readonly List<(UnityEngine.UI.Button Button, UnityAction Callback)> _buttonBindings = new List<(UnityEngine.UI.Button, UnityAction)>();

        public abstract void Init();

        protected void BindButton(UnityEngine.UI.Button button, UnityAction callback)
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
        }
    }
}
