using System;
using System.Collections.Generic;
using DG.Tweening;
using Noname.Battle;
using UnityEngine;

/// <summary>한 행동의 연출과 유닛 Transform 연결을 담당한다. 다음 행동의 계산은 Caster가 결정한다.</summary>
public class BattleField : MonoBehaviour, IBattleMotionHandle
{
    private sealed class UnitView
    {
        public readonly GameObject Instance;
        public readonly Vector3 HomePosition;
        public readonly Vector3 HomeScale;

        public UnitView(GameObject instance)
        {
            Instance = instance;
            HomePosition = instance.transform.position;
            HomeScale = instance.transform.localScale;
        }
    }

    [SerializeField] private Transform[] _mySidePositions = null;
    [SerializeField] private Transform[] _enemyPositions = null;

    private readonly Dictionary<int, UnitView> _units = new Dictionary<int, UnitView>();

    private Sequence _activeSequence = null;

    #region MonoBehaviour

    private void OnDisable()
    {
        // DOTween은 Field의 활성 상태와 별개로 갱신되므로 직접 정리한다.
        Stop();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        _mySidePositions = FindPositions("MySide");
        _enemyPositions = FindPositions("Enemies");
    }
#endif

    #endregion

    #region IBattleMotionHandle Impl

    public void PlayAction(DamageDesc damage, Action onHit, Action onCompleted, Action<Exception> onFailed)
    {
        UnitView attacker = _units[damage.AttackerId];
        UnitView target = _units[damage.TargetId];
        Transform attackerTransform = attacker.Instance.transform;
        Transform targetTransform = target.Instance.transform;
        Vector3 direction = (targetTransform.position - attacker.HomePosition).normalized;
        Vector3 approach = targetTransform.position - direction * 1.2f;
        // Sequence 수명은 정상 완료와 명시적 중단 경로에서 직접 정리한다.
        Sequence sequence = DOTween.Sequence().SetAutoKill(false).SetTarget(this);
        _activeSequence = sequence;
        sequence.Append(attackerTransform.DOMove(approach, 0.18f).SetEase(Ease.InOutQuad));
        sequence.AppendCallback(() => onHit());
        sequence.Append(attackerTransform.DOPunchScale(Vector3.one * 0.15f, 0.14f, 1, 0.5f));
        sequence.Join(targetTransform.DOPunchScale(Vector3.one * -0.2f, 0.14f, 1, 0.5f));
        sequence.AppendCallback(() =>
        {
            if (damage.IsTargetDead)
            {
                // 계산상 사망했어도 피격 표현을 마친 뒤 오브젝트를 숨긴다.
                target.Instance.SetActive(false);
            }
        });
        sequence.Append(attackerTransform.DOMove(attacker.HomePosition, 0.18f).SetEase(Ease.InOutQuad));

        sequence.OnComplete(() =>
        {
            // 다음 행동이 새 Sequence를 등록하기 전에 이번 연출을 정리한다.
            Stop();
            onCompleted();
        });
        sequence.OnKill(() =>
        {
            _activeSequence = null;
            RestoreUnits();
            onFailed(new InvalidOperationException("공격 모션이 복귀 전에 종료되었습니다."));
        });
    }

    public void Stop()
    {
        Sequence sequence = _activeSequence;
        _activeSequence = null;
        // 정상 완료와 사용자 중단은 실패가 아니므로 Kill 전에 실패 콜백을 해제한다.
        sequence?.OnKill(null);
        sequence?.Kill();
        RestoreUnits();
    }

    #endregion

    public void RegisterUnit(BattleUnitState state, GameObject instance)
    {
        instance.name = $"{state.Side} Unit {state.Id}";
        instance.transform.position = GetPosition(state.Side, state.Slot).position;
        _units.Add(state.Id, new UnitView(instance));
        instance.SetActive(!state.IsDead);
    }

    public Transform GetPosition(BattleSide side, int slot)
    {
        return (side == BattleSide.MySide ? _mySidePositions : _enemyPositions)[slot - 1];
    }

    public void Release()
    {
        Stop();
        _units.Clear();
    }

    private void RestoreUnits()
    {
        foreach (UnitView unit in _units.Values)
        {
            if (unit.Instance != null)
            {
                unit.Instance.transform.position = unit.HomePosition;
                unit.Instance.transform.localScale = unit.HomeScale;
            }
        }
    }

#if UNITY_EDITOR
    private Transform[] FindPositions(string sideName)
    {
        const int POSITION_COUNT = 12;
        Transform[] positions = new Transform[POSITION_COUNT];

        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (root.name != sideName)
            {
                continue;
            }

            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = root.transform.Find($"Pos - {i + 1}");
            }

            break;
        }

        return positions;
    }
#endif
}
