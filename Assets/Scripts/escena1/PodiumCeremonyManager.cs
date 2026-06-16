using UnityEngine;

public class PodiumCeremonyManager : MonoBehaviour
{
    public static PodiumCeremonyManager Instance { get; private set; }

    private Transform _spawnTop1;
    private Transform _spawnTop2;
    private Transform _spawnTop3;
    private Transform _spawnConsolation;
    private ProfeCeremonyAnimator _profeVictory;
    private ProfeCeremonyAnimator _profeConsolation;
    private bool _resolved;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveSceneReferences();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void TryBeginCeremony()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.Object.HasStateAuthority)
            return;

        if (!gm.IsRaceOver || gm.CeremonyStarted)
            return;

        ResolveSceneReferences();
        if (!HasRequiredReferences())
        {
            Debug.LogWarning("[PodiumCeremonyManager] Referencias de ceremonia incompletas; se omite el teletransporte.");
            return;
        }

        gm.CeremonyStarted = true;

        _profeVictory.PlayVictory();
        _profeConsolation.PlayDefeat();

        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.State != EPlayerState.Finished)
                continue;

            int rank = player.PlayerRank;
            Transform spawn = rank > 0 && rank <= 3 ? GetTopSpawn(rank) : _spawnConsolation;
            if (spawn == null)
                continue;

            player.EnterPodiumCeremony(spawn);
            player.RPC_ShowPodiumCeremonyMessage(rank, player.PuntajeObtenido);
        }
    }

    public void ResetCeremony()
    {
        if (_profeVictory != null)
            _profeVictory.ResetIdle();
        if (_profeConsolation != null)
            _profeConsolation.ResetIdle();
    }

    private Transform GetTopSpawn(int rank)
    {
        return rank switch
        {
            1 => _spawnTop1,
            2 => _spawnTop2,
            3 => _spawnTop3,
            _ => _spawnConsolation
        };
    }

    private bool HasRequiredReferences()
    {
        return _spawnTop1 != null && _spawnTop2 != null && _spawnTop3 != null
            && _spawnConsolation != null && _profeVictory != null && _profeConsolation != null;
    }

    private void ResolveSceneReferences()
    {
        if (_resolved && HasRequiredReferences())
            return;

        _spawnTop1 = FindCeremonyTransform("SpawnTop1");
        _spawnTop2 = FindCeremonyTransform("SpawnTop2");
        _spawnTop3 = FindCeremonyTransform("SpawnTop3");
        _spawnConsolation = FindCeremonyTransform("SpawnConsolation");

        _profeVictory = EnsureCeremonyAnimator(FindCeremonyTransform("ProfeVictory"));
        _profeConsolation = EnsureCeremonyAnimator(FindCeremonyTransform("ProfeConsolation"));

        _resolved = HasRequiredReferences();
    }

    private static Transform FindCeremonyTransform(string objectName)
    {
        var mundoCeremonia = GameObject.Find("Mundo_Ceremonia");
        if (mundoCeremonia != null)
        {
            var found = FindDeep(mundoCeremonia.transform, objectName);
            if (found != null)
                return found;
        }

        var podiumCeremony = GameObject.Find("PodiumCeremony");
        if (podiumCeremony != null)
        {
            var found = FindDeep(podiumCeremony.transform, objectName);
            if (found != null)
                return found;
        }

        var fallback = GameObject.Find(objectName);
        return fallback != null ? fallback.transform : null;
    }

    private static Transform FindDeep(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDeep(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static ProfeCeremonyAnimator EnsureCeremonyAnimator(Transform target)
    {
        if (target == null)
            return null;

        var animator = target.GetComponent<ProfeCeremonyAnimator>();
        if (animator == null)
            animator = target.gameObject.AddComponent<ProfeCeremonyAnimator>();

        return animator;
    }
}
