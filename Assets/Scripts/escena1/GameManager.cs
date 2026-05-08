using Fusion;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Networked] public TickTimer GlobalCycleTimer { get; set; }
    [Networked] public int CurrentQuestionIndex { get; set; } = -1;
    
    [Networked] public bool IsMatchStarted { get; set; }

    private const float CycleDuration = 15f;
    private const float ResponseWindow = 10f;

    private void Awake()
    {
        // Inicializar la instancia al despertar
        if (Instance == null) Instance = this;
    }

    public override void FixedUpdateNetwork()
    {

        if (Object.HasStateAuthority && IsMatchStarted)
        {
            if (GlobalCycleTimer.ExpiredOrNotRunning(Runner))
            {
                GlobalCycleTimer = TickTimer.CreateFromSeconds(Runner, CycleDuration);
                CurrentQuestionIndex++;
                Debug.Log($"IA: Iniciando Pregunta: {CurrentQuestionIndex}");
            }
        }
    }
    public void UI_BotonIniciarPartida()
    {
        if (Object.HasStateAuthority && QuestionManager.Instance.IsReady)
        {
            IsMatchStarted = true;
        }
    }

    public float GetRemainingResponseTime()
    {
        if (GlobalCycleTimer.IsRunning)
        {
            float elapsed = CycleDuration - (GlobalCycleTimer.RemainingTime(Runner) ?? 0);
            return Mathf.Max(0, ResponseWindow - elapsed);
        }
        return 0;
    }
}