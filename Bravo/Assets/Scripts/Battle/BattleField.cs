using UnityEngine;

public class BattleField : MonoBehaviour
{
    [SerializeField] private Transform[] _mySidePositions;
    [SerializeField] private Transform[] _enemyPositions;

#if UNITY_EDITOR
    private void Reset()
    {
        _mySidePositions = FindPositions("MySide");
        _enemyPositions = FindPositions("Enemies");
    }

    private Transform[] FindPositions(string sideName)
    {
        const int POSITION_COUNT = 12;
        var positions = new Transform[POSITION_COUNT];

        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (root.name != sideName)
            {
                continue;
            }

            for (var i = 0; i < positions.Length; i++)
            {
                positions[i] = root.transform.Find($"Pos - {i + 1}");
            }

            break;
        }

        return positions;
    }
#endif
}
