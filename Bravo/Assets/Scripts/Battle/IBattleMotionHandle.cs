using System;
using System.Collections;

namespace Noname.Battle
{
    /// <summary>타격 시 표현 콜백을 호출하고 원위치 복귀까지 기다린다.</summary>
    public interface IBattleMotionHandle
    {
        IEnumerator PlayAction(DamageDesc damage, Action onHit);
    }
}
