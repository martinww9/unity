using UnityEngine;

/// <summary>
/// Animación estática de ceremonia usando ProfeCapoeira (bailando = victoria, sentao = derrota).
/// </summary>
public class ProfeCeremonyAnimator : MonoBehaviour
{
    [SerializeField] private Animator _animator;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
    }

    public void PlayVictory()
    {
        SetBools(sentao: false, caminando: false, bailando: true);
    }

    public void PlayDefeat()
    {
        SetBools(sentao: true, caminando: false, bailando: false);
    }

    public void ResetIdle()
    {
        SetBools(sentao: false, caminando: false, bailando: false);
    }

    private void SetBools(bool sentao, bool caminando, bool bailando)
    {
        if (_animator == null) return;

        _animator.SetBool("sentao", sentao);
        _animator.SetBool("caminando", caminando);
        _animator.SetBool("bailando", bailando);
    }
}
