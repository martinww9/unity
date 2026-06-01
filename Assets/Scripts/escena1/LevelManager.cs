using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header("Spawn al inicio de cada sección del mapa (índice 0 = nivel 1)")]
    [SerializeField] private Transform[] _levelSpawnPoints;

    private void Awake()
    {
        Instance = this;
    }

    public Transform GetSpawnPoint(int level)
    {
        if (_levelSpawnPoints == null || level < 1 || level > _levelSpawnPoints.Length)
            return null;
        return _levelSpawnPoints[level - 1];
    }
}
