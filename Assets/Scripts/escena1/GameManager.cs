using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Networked] public bool IsMatchStarted { get; set; }
    [Networked] public int FinishedPlayersCount { get; set; }
    [Networked] public bool IsRaceOver { get; set; }
    [Networked] public int FeedbacksCompleted { get; set; }
    [Networked] public int Level1Completions { get; set; }
    [Networked] public int Level2Completions { get; set; }
    [Networked] public int Level3Completions { get; set; }

    private ChangeDetector _changeDetector;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public static bool IsMatchStartedSafe
    {
        get
        {
            var gm = Instance;
            if (gm == null || gm.Object == null || !gm.Object.IsValid)
                return false;
            return gm.IsMatchStarted;
        }
    }

    public static bool HasFinishers
    {
        get
        {
            var gm = Instance;
            if (gm == null || gm.Object == null || !gm.Object.IsValid)
                return false;
            return gm.FinishedPlayersCount > 0;
        }
    }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(IsMatchStarted):
                    if (IsMatchStarted && TriviaUI.Instance != null)
                        TriviaUI.Instance.StartGameUI();
                    break;

                case nameof(FinishedPlayersCount):
                    if (FinishedPlayersCount > 0 && TriviaUI.Instance != null)
                        TriviaUI.Instance.ShowPodiumForAll();
                    break;
            }
        }
    }

    public void UI_BotonIniciarPartida()
    {
        if (Object.HasStateAuthority && QuestionManager.Instance.IsReady)
        {
            ResetAllPlayersForMatch();
            IsMatchStarted = true;
        }
    }

    private void ResetAllPlayersForMatch()
    {
        if (Object.HasStateAuthority)
        {
            Level1Completions = 0;
            Level2Completions = 0;
            Level3Completions = 0;
            FinishedPlayersCount = 0;
            IsRaceOver = false;
        }

        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (var player in players)
            player.ResetForMatch();
    }

    public int RegisterLevelCompletion(int level)
    {
        if (!Object.HasStateAuthority)
            return 0;

        switch (level)
        {
            case 1: Level1Completions++; return Level1Completions;
            case 2: Level2Completions++; return Level2Completions;
            case 3: Level3Completions++; return Level3Completions;
            default: return 0;
        }
    }

    public void RegisterPlayerFinish(Player player)
    {
        if (!Object.HasStateAuthority || player == null)
            return;

        FinishedPlayersCount++;
        player.FinishOrder = FinishedPlayersCount;

        int totalPlayers = 0;
        foreach (var _ in Runner.ActivePlayers) totalPlayers++;

        if (FinishedPlayersCount >= totalPlayers)
            IsRaceOver = true;

        RecalculatePodiumRanks();
    }

    public void RecalculatePodiumRanks()
    {
        if (!Object.HasStateAuthority)
            return;

        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        var finished = new System.Collections.Generic.List<Player>();

        foreach (var player in players)
        {
            if (player.State == EPlayerState.Finished)
                finished.Add(player);
        }

        finished.Sort((a, b) =>
        {
            int scoreCompare = b.PuntajeObtenido.CompareTo(a.PuntajeObtenido);
            if (scoreCompare != 0)
                return scoreCompare;
            return a.FinishOrder.CompareTo(b.FinishOrder);
        });

        for (int i = 0; i < finished.Count; i++)
            finished[i].PlayerRank = i + 1;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_FeedbackCompletado()
    {
        FeedbacksCompleted++;
    }

    public void UI_BotonRegenerarPreguntas()
    {
        if (Object.HasStateAuthority)
        {
            Debug.Log("GameManager: Reiniciando estado y regenerando preguntas...");

            IsMatchStarted = false;
            FinishedPlayersCount = 0;
            IsRaceOver = false;
            FeedbacksCompleted = 0;
            Level1Completions = 0;
            Level2Completions = 0;
            Level3Completions = 0;

            if (QuestionManager.Instance != null)
                QuestionManager.Instance.RequestNewGeneration();
        }
    }

    public void UI_BotonReiniciarServidor()
    {
        Debug.Log("GameManager: Cerrando el servidor y volviendo al menú...");

        if (Runner != null)
            Runner.Shutdown();

        SceneManager.LoadScene(SceneNames.MenuPrincipal);
    }
}
