using Fusion;
using UnityEngine;

public class GoalTrigger : NetworkBehaviour
{
    [SerializeField] private int _levelId = 1;

    private void OnTriggerEnter(Collider other)
    {
        if (!Object.HasStateAuthority) return;

        if (other.TryGetComponent<Player>(out var player))
        {
            if (player.State == EPlayerState.Finished) return;
            if (player.CurrentLevel != _levelId) return;
            player.CompleteLevel(_levelId);
        }
    }
}
