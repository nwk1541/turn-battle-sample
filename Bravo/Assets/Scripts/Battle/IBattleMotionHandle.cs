using System;

namespace Noname.Battle
{
    public interface IBattleMotionHandle
    {
        /// <summary>타격 시 onHit, 복귀까지 끝나면 onCompleted를 호출하고 실패는 onFailed로 알린다.</summary>
        void PlayAction(DamageDesc damage, Action onHit, Action onCompleted, Action<Exception> onFailed);

        /// <summary>완료 또는 실패 콜백을 호출하지 않고 진행 중인 연출을 중단한다.</summary>
        void Stop();
    }
}
