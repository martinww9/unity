using Fusion;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Networked] public TickTimer GlobalCycleTimer { get; set; }
    [Networked] public int CurrentQuestionIndex { get; set; } = -1;
    [Networked] public bool IsMatchStarted { get; set; }
    [Networked] public int FinishedPlayersCount { get; set; }

    private ChangeDetector _changeDetector;
    private const float CycleDuration = 15f;
    private const float ResponseWindow = 10f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public override void Render()
    {
        // Actualizar el timer visual en cada frame para todos
        if (IsMatchStarted && TriviaUI.Instance != null)
        {
            TriviaUI.Instance.UpdateTimer(GetRemainingResponseTime());
        }

        // Revisar qué variables han cambiado en la red
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(IsMatchStarted):
                    if (IsMatchStarted) TriviaUI.Instance.StartGameUI();
                    break;
                
                case nameof(CurrentQuestionIndex):
                    if (CurrentQuestionIndex >= 0)
                    {
                        // Pedimos la pregunta al QuestionManager y la mostramos
                        var q = QuestionManager.Instance.GetQuestion(CurrentQuestionIndex);
                        TriviaUI.Instance.ShowQuestion(q);
                    }
                    break;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority && IsMatchStarted)
        {
            if (GlobalCycleTimer.ExpiredOrNotRunning(Runner))
            {
                GlobalCycleTimer = TickTimer.CreateFromSeconds(Runner, CycleDuration);
                CurrentQuestionIndex++;
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

    public int RegisterPlayerFinish()
    {
        if (Object.HasStateAuthority)
        {
            FinishedPlayersCount++;
            return FinishedPlayersCount;
        }
        return 0;
    }
}