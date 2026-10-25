using System.Collections.Generic;
using Noname.Battle;
using TMPro;
using UnityEngine;

namespace Noname.UI
{
    public sealed class BattleHUD : UIBase, IBattleUIHandle
    {
        private sealed class HealthView
        {
            public readonly Transform Target;
            public readonly TMP_Text Health;
            public readonly int MaxHealth;

            public string DisplayText;

            public HealthView(Transform target, TMP_Text health, int maxHealth)
            {
                Target = target;
                Health = health;
                MaxHealth = maxHealth;
                DisplayText = health.text;
            }
        }

        [SerializeField] private RectTransform _healthRoot = null;
        [SerializeField] private UnityEngine.UI.Button _returnButton = null;

        private readonly Dictionary<int, HealthView> _units = new Dictionary<int, HealthView>();

        private Camera _camera = null;

        #region MonoBehaviour

        /// <summary>위치는 현재 Transform을 따르되 체력은 마지막 타격 결과를 유지한다.</summary>
        private void LateUpdate()
        {
            foreach (HealthView unit in _units.Values)
            {
                Vector3 screenPoint = _camera.WorldToScreenPoint(unit.Target.position + Vector3.up);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_healthRoot, screenPoint, null, out Vector2 position);
                unit.Health.rectTransform.anchoredPosition = position;
                // 비활성 유닛이나 카메라 뒤의 표시는 텍스트만 숨기고 재표시할 체력은 DisplayText에 보관한다.
                unit.Health.text = unit.Target.gameObject.activeInHierarchy && screenPoint.z > 0f ? unit.DisplayText : string.Empty;
            }
        }

        #endregion

        #region Override

        public override void Init()
        {
            _camera = Camera.main;
            BindButton(_returnButton, OnClickReturn);
        }

        public override void Release()
        {
            _units.Clear();
            _camera = null;
            base.Release();
        }

        #endregion

        #region IBattleUIHandle Impl

        public void ShowAction(DamageDesc damage)
        {
            HealthView target = _units[damage.TargetId];
            target.DisplayText = $"{damage.TargetFinalHealth}/{target.MaxHealth}";
            target.Health.text = target.DisplayText;
            target.Health.color = damage.IsCritical ? Color.yellow : Color.white;
        }

        #endregion

        public void RegisterUnit(BattleUnitState state, Transform target)
        {
            GameObject healthObject = new GameObject($"Health {state.Id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            healthObject.transform.SetParent(_healthRoot, false);
            TextMeshProUGUI health = healthObject.GetComponent<TextMeshProUGUI>();
            health.font = TMP_Settings.defaultFontAsset;
            health.fontSize = 24f;
            health.alignment = TextAlignmentOptions.Center;
            health.raycastTarget = false;
            health.rectTransform.sizeDelta = new Vector2(120f, 40f);
            health.text = $"{state.CurrentHealth}/{state.Stats.MaxHealth}";
            _units.Add(state.Id, new HealthView(target, health, state.Stats.MaxHealth));
        }

        private void OnClickReturn()
        {
            BattleManager.Instance.ReturnToTitle();
        }
    }
}
