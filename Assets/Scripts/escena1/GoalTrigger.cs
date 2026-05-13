using Fusion;
using UnityEngine;

public class GoalTrigger : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!Object.HasStateAuthority) return;

        if (other.TryGetComponent<Player>(out var player))
        {
            if (player.State != EPlayerState.Finished)
            {
                int rank = GameManager.Instance.RegisterPlayerFinish();
                player.FinishRace(rank);
            }
        }
    }
}