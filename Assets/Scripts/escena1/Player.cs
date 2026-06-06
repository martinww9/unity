using Fusion;
using UnityEngine;
using Unity.Cinemachine;

public enum EPlayerState { Responding, Stunned, Advancing, Finished }

public class Player : NetworkBehaviour
{
    [Networked] public EPlayerState State { get; set; }
    [Networked] public int CurrentLevel { get; set; } = 1;
    [Networked] public int LevelQuestionIndex { get; set; } = -1;
    [Networked] private TickTimer LevelCycleTimer { get; set; }
    [Networked] private TickTimer _stunTimer { get; set; }
    [Networked] public int LastAnsweredIndex { get; set; } = -1;
    [Networked] public int PlayerRank { get; set; }
    [Networked] public int RespuestasCorrectas { get; set; }
    [Networked] public int PuntajeObtenido { get; set; }
    [Networked] public NetworkString<_32> DisplayName { get; set; }
    [Networked] private float _yaw { get; set; }
    [Networked] private float _pitch { get; set; }
    [Networked] private bool NetAnimWalking { get; set; }
    [Networked] private bool NetAnimRunning { get; set; }

    private const float CycleDuration = 10f;
    private const float ResponseWindow = 5f;
    private const float StunDuration = 3f;

    private int _lastDisplayedQuestionIndex = -1;
    [SerializeField] private Animator _animator;
    [SerializeField] private float _forwardSpeed = 12f;
    [SerializeField] private float _sprintSpeed = 20f;

    [Header("Configuración de Cámara")]
    [SerializeField] private Transform _cameraPivot;
    [SerializeField] private CinemachineCamera _fpCamera;
    public float mouseSensitivity = 6f;

    public static Player Local;
    private NetworkCharacterController _cc;
    private GameManager _gameManager;

    private void Awake()
    {
        _cc = GetComponent<NetworkCharacterController>();
        if (_animator == null)
            _animator = GetComponent<Animator>();

        if (_fpCamera == null)
            _fpCamera = GetComponentInChildren<CinemachineCamera>(true);
    }

    public override void Spawned()
    {
        ResolveGameManager();
        HideLegacyBlinkRig();

        if (Object.HasStateAuthority)
        {
            _yaw = transform.eulerAngles.y;
            State = EPlayerState.Advancing;
        }

        if (Object.HasInputAuthority)
        {
            Local = this;
            if (_fpCamera != null)
            {
                _fpCamera.enabled = true;
                if (_cameraPivot != null)
                    _fpCamera.Follow = _cameraPivot;
            }
        }
        else
        {
            if (_fpCamera != null)
                _fpCamera.enabled = false;
            if (_cameraPivot != null)
                _cameraPivot.gameObject.SetActive(false);
        }

        if (_cc != null)
            _cc.rotationSpeed = 0f;

        EnsureNameTag();

        if (Object.HasInputAuthority)
            RPC_SetDisplayName(PlayerNameStorage.Get());
    }

    private void EnsureNameTag()
    {
        if (GetComponentInChildren<PlayerNameTag>(true) != null)
            return;

        var nameTagGo = new GameObject("NameTag");
        nameTagGo.transform.SetParent(transform, false);
        nameTagGo.AddComponent<PlayerNameTag>();
    }

    public string GetDisplayName()
    {
        string name = DisplayName.ToString();
        if (string.IsNullOrWhiteSpace(name))
            return $"Jugador {Object.InputAuthority.PlayerId}";
        return name;
    }

    public static string GetDisplayName(NetworkRunner runner, PlayerRef playerRef)
    {
        if (runner == null)
            return $"Jugador {playerRef.PlayerId}";

        foreach (var player in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (player.Object == null || !player.Object.IsValid)
                continue;
            if (player.Object.InputAuthority == playerRef)
                return player.GetDisplayName();
        }

        return $"Jugador {playerRef.PlayerId}";
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetDisplayName(string name)
    {
        DisplayName = PlayerNameStorage.Sanitize(name);
    }

    private void HideLegacyBlinkRig()
    {
        foreach (var partName in new[] { "Armature", "Mesh", "Armors" })
        {
            Transform part = transform.Find(partName);
            if (part != null)
                part.gameObject.SetActive(false);
        }

        var rootMeshRenderer = GetComponent<MeshRenderer>();
        if (rootMeshRenderer != null)
            rootMeshRenderer.enabled = false;
    }

    private void ResolveGameManager()
    {
        if (_gameManager == null)
            _gameManager = GameManager.Instance ?? FindFirstObjectByType<GameManager>();
    }

    public void ResetForMatch()
    {
        if (!Object.HasStateAuthority) return;

        CurrentLevel = 1;
        LevelQuestionIndex = -1;
        LastAnsweredIndex = -1;
        LevelCycleTimer = TickTimer.None;
        State = EPlayerState.Advancing;
        PlayerRank = 0;
        RespuestasCorrectas = 0;
        PuntajeObtenido = 0;
        _lastDisplayedQuestionIndex = -1;

        Transform spawn = LevelManager.Instance != null ? LevelManager.Instance.GetSpawnPoint(1) : null;
        if (spawn != null)
        {
            _cc.Teleport(spawn.position, spawn.rotation);
            _yaw = spawn.rotation.eulerAngles.y;
            _pitch = 0f;
        }
    }

    public override void FixedUpdateNetwork()
    {
        ResolveGameManager();

        bool matchStarted = GameManager.IsMatchStartedSafe;

        if (State != EPlayerState.Finished && matchStarted)
            CheckLevelCycle();

        if (!matchStarted)
        {
            if (GetInput(out NetworkInputData lobbyData))
            {
                HandleMovement(lobbyData);
                ApplyLookInput(lobbyData);
            }
            return;
        }

        if (GetInput(out NetworkInputData data))
        {
            switch (State)
            {
                case EPlayerState.Responding:
                    SetLocomotionAnim(false, false);
                    HandleRespondingState(data);
                    break;

                case EPlayerState.Stunned:
                    _cc.Velocity = Vector3.zero;
                    SetLocomotionAnim(false, false);
                    if (_stunTimer.Expired(Runner)) State = EPlayerState.Advancing;
                    break;

                case EPlayerState.Advancing:
                case EPlayerState.Finished:
                    HandleMovement(data);
                    break;
            }

            if (State != EPlayerState.Responding)
                ApplyLookInput(data);
        }
    }

    private void ApplyLookInput(NetworkInputData data)
    {
        _yaw += data.lookRotationDeltaX * mouseSensitivity;
        _pitch -= data.lookRotationDeltaY * mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch, -80f, 80f);
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
    }

    private void LateUpdate()
    {
        if (!Object.HasInputAuthority || _cameraPivot == null)
            return;

        _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private void CheckLevelCycle()
    {
        if (!Object.HasStateAuthority) return;

        if (LevelCycleTimer.ExpiredOrNotRunning(Runner))
        {
            LevelCycleTimer = TickTimer.CreateFromSeconds(Runner, CycleDuration);
            LevelQuestionIndex++;
        }

        int currentQIndex = LevelQuestionIndex;

        if (currentQIndex < 0) return;

        int maxQuestions = QuestionManager.Instance != null ? QuestionManager.Instance.GetQuestionCount(CurrentLevel) : 0;
        if (currentQIndex >= maxQuestions) return;

        if (IsInResponseWindow() && LastAnsweredIndex != currentQIndex)
        {
            if (State != EPlayerState.Responding && State != EPlayerState.Stunned)
                State = EPlayerState.Responding;
        }

        if (!IsInResponseWindow() && State == EPlayerState.Responding)
        {
            State = EPlayerState.Stunned;
            _stunTimer = TickTimer.CreateFromSeconds(Runner, StunDuration);
            LastAnsweredIndex = currentQIndex;
        }
    }

    public float GetElapsedQuestionCycleTime()
    {
        if (!LevelCycleTimer.IsRunning)
            return 0f;

        float remaining = LevelCycleTimer.RemainingTime(Runner) ?? 0f;
        return Mathf.Clamp(CycleDuration - remaining, 0f, CycleDuration);
    }

    public float GetRemainingResponseTime()
    {
        if (!LevelCycleTimer.IsRunning)
            return 0f;

        return Mathf.Clamp(ResponseWindow - GetElapsedQuestionCycleTime(), 0f, ResponseWindow);
    }

    public float GetRemainingCycleTime()
    {
        if (!LevelCycleTimer.IsRunning)
            return 0f;

        float remaining = LevelCycleTimer.RemainingTime(Runner) ?? 0f;
        return Mathf.Clamp(remaining, 0f, CycleDuration);
    }

    public float GetRemainingNextQuestionTime()
    {
        return GetRemainingCycleTime();
    }

    public bool HasActiveQuestion()
    {
        if (!LevelCycleTimer.IsRunning || LevelQuestionIndex < 0)
            return false;

        int maxQuestions = QuestionManager.Instance != null ? QuestionManager.Instance.GetQuestionCount(CurrentLevel) : 0;
        return LevelQuestionIndex < maxQuestions;
    }

    public bool IsInResponseWindow()
    {
        return HasActiveQuestion() && GetElapsedQuestionCycleTime() < ResponseWindow;
    }

    private void HandleRespondingState(NetworkInputData data)
    {
        if (data.SelectedAnswerIndex == -1) return;

        Question currentQ = QuestionManager.Instance.GetQuestion(CurrentLevel, LevelQuestionIndex);
        if (currentQ == null) return;

        LastAnsweredIndex = LevelQuestionIndex;

        if (data.SelectedAnswerIndex == currentQ.correctAnswerIndex)
        {
            State = EPlayerState.Advancing;
            RespuestasCorrectas++;
            PuntajeObtenido += currentQ.puntaje;
        }
        else
        {
            State = EPlayerState.Stunned;
            _stunTimer = TickTimer.CreateFromSeconds(Runner, StunDuration);
        }
    }

    private void HandleMovement(NetworkInputData data)
    {
        Vector3 moveDir = Vector3.zero;

        if (data.Direction.sqrMagnitude > 0)
        {
            moveDir = (transform.forward * data.Direction.z) + (transform.right * data.Direction.x);
            moveDir.Normalize();
        }

        float currentSpeed = data.Buttons.IsSet(NetworkInputData.SprintButton) ? _sprintSpeed : _forwardSpeed;
        _cc.maxSpeed = currentSpeed;
        _cc.Move(moveDir);

        bool isMoving = data.Direction.sqrMagnitude > 0;
        bool isSprinting = isMoving && data.Buttons.IsSet(NetworkInputData.SprintButton);
        SetLocomotionAnim(isMoving, isSprinting);
    }

    private void SetLocomotionAnim(bool walking, bool running)
    {
        if (!Object.HasStateAuthority) return;

        NetAnimWalking = walking;
        NetAnimRunning = running;
    }

    public void CompleteLevel(int level)
    {
        if (!Object.HasStateAuthority) return;
        if (State == EPlayerState.Finished) return;
        if (CurrentLevel != level) return;

        if (level < 3)
        {
            int nextLevel = level + 1;
            Transform spawn = LevelManager.Instance != null ? LevelManager.Instance.GetSpawnPoint(nextLevel) : null;
            if (spawn != null)
            {
                _cc.Teleport(spawn.position, spawn.rotation);
                _yaw = spawn.rotation.eulerAngles.y;
            }

            CurrentLevel = nextLevel;
            LevelQuestionIndex = -1;
            LastAnsweredIndex = -1;
            _lastDisplayedQuestionIndex = -1;
            LevelCycleTimer = TickTimer.None;
            State = EPlayerState.Advancing;
            return;
        }

        int rank = GameManager.Instance.RegisterPlayerFinish();
        FinishRace(rank);
    }

    private void UpdateLocalTriviaHud(bool matchStarted)
    {
        var ui = TriviaUI.Instance;
        if (ui == null) return;

        if (!matchStarted)
        {
            ui.ClearTimer();
            return;
        }

        if (State == EPlayerState.Finished)
        {
            ui.ClearTimer();
            ui.ShowWaiting();
            return;
        }

        ui.UpdateLevelIndicator(CurrentLevel);

        if (!HasActiveQuestion())
        {
            ui.ClearTimer();
            ui.Hide();
            return;
        }

        bool waitingForAnswer = State == EPlayerState.Responding &&
            IsInResponseWindow() &&
            LastAnsweredIndex != LevelQuestionIndex;

        if (waitingForAnswer)
        {
            ui.UpdateResponseTimer(GetRemainingResponseTime());

            int currentIdx = LevelQuestionIndex;
            if (_lastDisplayedQuestionIndex != currentIdx)
            {
                var q = QuestionManager.Instance?.GetQuestion(CurrentLevel, currentIdx);
                if (q != null)
                {
                    PlayerQuestionHistory.Record(CurrentLevel, currentIdx);
                    ui.ShowQuestion(q);
                    _lastDisplayedQuestionIndex = currentIdx;
                }
            }
            return;
        }

        ui.Hide();
        ui.UpdateNextQuestionTimer(GetRemainingNextQuestionTime());
    }

    public override void Render()
    {
        if (Object.HasInputAuthority)
        {
            ResolveGameManager();
            bool matchStarted = GameManager.IsMatchStartedSafe;
            UpdateLocalTriviaHud(matchStarted);
        }

        if (_animator != null)
        {
            _animator.SetBool("isWalking", NetAnimWalking);
            _animator.SetBool("isRunning", NetAnimRunning);
        }
    }

    public void FinishRace(int rank)
    {
        State = EPlayerState.Finished;
        PlayerRank = rank;

        RPC_NotificarLlegada(PuntajeObtenido);
        RPC_ShowPodiumToAll();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_NotificarLlegada(int puntajeObtenido)
    {
        int puntajeMaximo = QuestionManager.Instance != null ? QuestionManager.Instance.GetMaxPossibleScore() : 0;
        TriviaUI.Instance?.RegistrarFinDeCarreraLocal(puntajeObtenido, puntajeMaximo);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ShowPodiumToAll()
    {
        TriviaUI.Instance?.ShowPodiumForAll();
    }
}
