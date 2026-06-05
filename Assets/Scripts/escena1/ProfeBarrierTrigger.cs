using UnityEngine;

public class ProfeBarrierTrigger : MonoBehaviour
{
    public enum ProfeBarrierAction
    {
        StandUp = ProfeAnimator.StageStanding,
        Dance = ProfeAnimator.StageDancing
    }

    [SerializeField] private ProfeAnimator _profe;
    [SerializeField] private ProfeBarrierAction _action = ProfeBarrierAction.StandUp;
    [SerializeField] private bool _triggerOnce = true;

    private bool _consumed;

    private void OnTriggerEnter(Collider other)
    {
        if (_consumed && _triggerOnce) return;
        if (_profe == null) return;
        if (!other.TryGetComponent<Player>(out var player)) return;

        _profe.TryAdvanceFromBarrier((int)_action, player);

        if (_triggerOnce)
            _consumed = true;
    }
}
