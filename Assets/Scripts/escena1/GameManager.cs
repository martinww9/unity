using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Networked] public TickTimer GlobalCycleTimer { get; set; }
    [Networked] public int CurrentQuestionIndex { get; set; } = -1;
    [Networked] public bool IsMatchStarted { get; set; }
    [Networked] public int FinishedPlayersCount { get; set; }
    [Networked] public bool IsRaceOver { get; set; }
    [Networked] public int FeedbacksCompleted { get; set; }

    private ChangeDetector _changeDetector;
    private const float CycleDuration = 13f;
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
                    
                case nameof(FinishedPlayersCount):
                    if (FinishedPlayersCount > 0 && TriviaUI.Instance != null) 
                    {
                        // Actualiza y muestra el podio lateral para TODOS los jugadores en la sala
                        TriviaUI.Instance.UpdatePodiumLive();
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
            return ResponseWindow - elapsed;
        }
        return 0;
    }

    public int RegisterPlayerFinish()
    {
        if (Object.HasStateAuthority)
        {
            FinishedPlayersCount++;

            // Contamos cuántos jugadores hay en la sala actualmente
            int totalPlayers = 0;
            foreach(var p in Runner.ActivePlayers) totalPlayers++;
            
            // Si ya llegaron todos, la carrera termina
            if (FinishedPlayersCount >= totalPlayers)
            {
                IsRaceOver = true;
            }

            return FinishedPlayersCount;
        }
        return 0;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_FeedbackCompletado()
    {
        FeedbacksCompleted++;
    }

    public void UI_BotonRegenerarPreguntas()
    {
        // Solo el Host puede pedir regenerar las preguntas
        if (Object.HasStateAuthority)
        {
            Debug.Log("GameManager: Reiniciando estado y regenerando preguntas...");

            // 1. Reseteamos las variables de la partida por si ya había empezado
            IsMatchStarted = false;
            CurrentQuestionIndex = -1;
            GlobalCycleTimer = TickTimer.None;
            FinishedPlayersCount = 0;
            IsRaceOver = false;
            FeedbacksCompleted = 0;

            // 2. Le pedimos al QuestionManager que inicie el proceso con la IA
            if (QuestionManager.Instance != null)
            {
                QuestionManager.Instance.RequestNewGeneration();
            }
        }
    }

    public void UI_BotonReiniciarServidor()
    {
        Debug.Log("GameManager: Cerrando el servidor y volviendo al menú...");

        // 1. Apagamos el Runner (Esto desconecta a todos de Photon)
        if (Runner != null)
        {
            Runner.Shutdown();
        }
        
        // 2. Cargamos tu escena inicial. Asegúrate de que tu escena inicial se llame exactamente "UI".
        SceneManager.LoadScene("UI"); 
    }
}