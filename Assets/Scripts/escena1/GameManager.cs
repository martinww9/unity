using Fusion;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [Networked] public TickTimer GlobalCycleTimer { get; set; }
    [Networked] public int CurrentQuestionIndex { get; set; } = -1; // Empezar en -1
    
    private const float CycleDuration = 15f;
    private const float ResponseWindow = 10f;

    public override void FixedUpdateNetwork()
    {
        // El Servidor gestiona el tiempo
        if (Object.HasStateAuthority)
        {
            if (GlobalCycleTimer.ExpiredOrNotRunning(Runner))
            {
                // Reiniciar ciclo de 15 segundos
                GlobalCycleTimer = TickTimer.CreateFromSeconds(Runner, CycleDuration);
                CurrentQuestionIndex++;
                Debug.Log($"Iniciando Pregunta: {CurrentQuestionIndex}");
            }
        }
    }

    // Método útil para que los jugadores sepan cuánto tiempo queda de respuesta
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