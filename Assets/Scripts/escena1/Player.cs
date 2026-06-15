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
    [Networked] private TickTimer _movementResumeLock { get; set; }
    [Networked] public int LastAnsweredIndex { get; set; } = -1;
    [Networked] public int PlayerRank { get; set; }
    [Networked] public int FinishOrder { get; set; }
    [Networked] public int RespuestasCorrectas { get; set; }
    [Networked] public int PuntajeObtenido { get; set; }
    [Networked] public int CurrentLevelCorrectCount { get; set; }
    [Networked] public int Level1CorrectCount { get; set; }
    [Networked] public int Level2CorrectCount { get; set; }
    [Networked] public int Level3CorrectCount { get; set; }
    [Networked, OnChangedRender(nameof(OnDisplayNameChanged))]
    public NetworkString<_32> DisplayName { get; set; }
    [Networked] private float _yaw { get; set; }
    [Networked] private float _pitch { get; set; }
    [Networked] private bool NetAnimWalking { get; set; }
    [Networked] private bool NetAnimRunning { get; set; }

    private const float CycleDuration = 15f;
    private const float ResponseWindow = 10f;
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
    private bool _pendingStabilize;
    private bool _blockedAtGoal;
    private bool _levelBlockedMessageShown;

    private void Awake()
    {
        _cc = GetComponent<NetworkCharacterController>();
        if (_animator == null)
            _animator = GetComponent<Animator>();

        if (_fpCamera == null)
            _fpCamera = GetComponentInChildren<CinemachineCamera>(true);

        var controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.stepOffset = 0.12f;
            controller.skinWidth = 0.1f;
        }
    }

    public void StabilizeCharacterPhysics()
    {
        if (!Object.HasStateAuthority || _cc == null)
            return;

        ResetCharacterMotion();

        var controller = GetComponent<CharacterController>();
        if (controller == null)
            return;

        float bottomOffset = controller.center.y - controller.height * 0.5f;
        Vector3 rayOrigin = transform.position + Vector3.up * (controller.height * 0.5f + 0.25f);
        float rayLength = controller.height + 1.5f;

        RaycastHit? bestHit = null;
        foreach (var hit in Physics.RaycastAll(rayOrigin, Vector3.down, rayLength, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.normal.y < 0.7f) continue;
            if (bestHit == null || hit.point.y < bestHit.Value.point.y)
                bestHit = hit;
        }

        if (bestHit.HasValue)
        {
            float groundedY = bestHit.Value.point.y - bottomOffset;
            if (Mathf.Abs(transform.position.y - groundedY) > 0.02f)
                _cc.Teleport(new Vector3(transform.position.x, groundedY, transform.position.z), transform.rotation);
        }
    }

    private void ResetCharacterMotion()
    {
        if (Object.HasStateAuthority && _cc != null)
        {
            _cc.Velocity = Vector3.zero;
            _cc.Grounded = true;
        }
    }

    private void LockMovementResume(int ticks = 2)
    {
        if (!Object.HasStateAuthority || Runner == null)
            return;

        _movementResumeLock = TickTimer.CreateFromTicks(Runner, ticks);
    }

    public override void Spawned()
    {
        ResolveGameManager();
        HideLegacyBlinkRig();

        if (Object.HasStateAuthority)
        {
            _yaw = transform.eulerAngles.y;
            State = EPlayerState.Advancing;
            _pendingStabilize = true;
            StabilizeCharacterPhysics();
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
        {
            string name = PlayerNameStorage.Get();
            if (Object.HasStateAuthority)
                DisplayName = PlayerNameStorage.Sanitize(name);
            else
                RPC_SetDisplayName(name);

            OnDisplayNameChanged();
        }
    }

    private void OnDisplayNameChanged()
    {
        if (TriviaUI.Instance == null || Runner == null || !Runner.IsRunning)
            return;

        TriviaUI.Instance.RefreshPlayerList(Runner);
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
        {
            if (Object.HasInputAuthority)
                return PlayerNameStorage.Get();
            return $"Jugador {Object.InputAuthority.PlayerId}";
        }
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

        if (playerRef == runner.LocalPlayer)
            return PlayerNameStorage.Get();

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
        FinishOrder = 0;
        RespuestasCorrectas = 0;
        PuntajeObtenido = 0;
        CurrentLevelCorrectCount = 0;
        Level1CorrectCount = 0;
        Level2CorrectCount = 0;
        Level3CorrectCount = 0;
        _blockedAtGoal = false;
        _levelBlockedMessageShown = false;
        _lastDisplayedQuestionIndex = -1;

        Transform spawn = LevelManager.Instance != null ? LevelManager.Instance.GetSpawnPoint(1) : null;
        if (spawn != null)
        {
            _cc.Teleport(spawn.position, spawn.rotation);
            _yaw = spawn.rotation.eulerAngles.y;
            _pitch = 0f;
            StabilizeCharacterPhysics();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (_pendingStabilize && Object.HasStateAuthority)
        {
            StabilizeCharacterPhysics();
            _pendingStabilize = false;
        }

        ResolveGameManager();

        bool matchStarted = GameManager.IsMatchStartedSafe;

        if (State != EPlayerState.Finished && matchStarted)
            CheckLevelCycle();

        if (!matchStarted)
        {
            if (GetInput(out NetworkInputData lobbyData))
            {
                ApplyLookInput(lobbyData);
                ResetCharacterMotion();
            }
            return;
        }

        if (!_movementResumeLock.ExpiredOrNotRunning(Runner))
        {
            ResetCharacterMotion();
            SetLocomotionAnim(false, false);
            if (GetInput(out NetworkInputData lockedData))
                ApplyLookInput(lockedData);
            return;
        }

        if (GetInput(out NetworkInputData data))
        {
            switch (State)
            {
                case EPlayerState.Responding:
                    ResetCharacterMotion();
                    SetLocomotionAnim(false, false);
                    HandleRespondingState(data);
                    break;

                case EPlayerState.Stunned:
                    ResetCharacterMotion();
                    SetLocomotionAnim(false, false);
                    if (_stunTimer.Expired(Runner))
                    {
                        LockMovementResume(1);
                        State = EPlayerState.Advancing;
                    }
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
            {
                State = EPlayerState.Responding;
                ResetCharacterMotion();
            }
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
        ResetCharacterMotion();
        StabilizeCharacterPhysics();
        LockMovementResume();

        if (data.SelectedAnswerIndex == currentQ.correctAnswerIndex)
        {
            State = EPlayerState.Advancing;
            RespuestasCorrectas++;
            CurrentLevelCorrectCount++;
            IncrementLevelCorrectCount(CurrentLevel);
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

    private void IncrementLevelCorrectCount(int level)
    {
        switch (level)
        {
            case 1: Level1CorrectCount++; break;
            case 2: Level2CorrectCount++; break;
            case 3: Level3CorrectCount++; break;
        }
    }

    public int GetCurrentLevelCorrectCount() => CurrentLevelCorrectCount;

    public int GetLevelCorrectCount(int level)
    {
        switch (level)
        {
            case 1: return Level1CorrectCount;
            case 2: return Level2CorrectCount;
            case 3: return Level3CorrectCount;
            default: return 0;
        }
    }

    public bool HasReachedLastQuestionOfLevel()
    {
        int total = QuestionManager.Instance != null ? QuestionManager.Instance.GetQuestionCount(CurrentLevel) : 0;
        return total > 0 && LevelQuestionIndex >= total - 1;
    }

    public int GetRemainingQuestionOpportunities()
    {
        int total = QuestionManager.Instance != null ? QuestionManager.Instance.GetQuestionCount(CurrentLevel) : 0;
        return LevelProgressRules.GetRemainingQuestionOpportunities(LevelQuestionIndex, total, LastAnsweredIndex);
    }

    public bool CanStillReachPassAtCurrentLevel()
    {
        int total = QuestionManager.Instance != null ? QuestionManager.Instance.GetQuestionCount(CurrentLevel) : 0;
        return LevelProgressRules.CanStillReachPassThreshold(CurrentLevelCorrectCount, total, LevelQuestionIndex, LastAnsweredIndex);
    }

    public void TryCompleteLevel(int level)
    {
        if (!Object.HasStateAuthority) return;
        if (State == EPlayerState.Finished) return;
        if (CurrentLevel != level) return;

        int total = QuestionManager.Instance != null ? QuestionManager.Instance.GetQuestionCount(level) : 0;
        bool lastQuestionReached = HasReachedLastQuestionOfLevel();

        if (!LevelProgressRules.CanAdvance(CurrentLevelCorrectCount, total, lastQuestionReached))
        {
            _blockedAtGoal = true;
            if (!_levelBlockedMessageShown)
            {
                _levelBlockedMessageShown = true;
                RPC_ShowLevelBlocked(CurrentLevelCorrectCount, total, LevelQuestionIndex, LastAnsweredIndex);
            }
            return;
        }

        _blockedAtGoal = false;
        _levelBlockedMessageShown = false;
        RPC_HideLevelBlocked();

        ResolveGameManager();
        int position = _gameManager != null ? _gameManager.RegisterLevelCompletion(level) : 1;
        int bonus = ScoringRules.GetLevelCompletionBonusByPosition(level, position);
        PuntajeObtenido += bonus;

        if (level < 3)
        {
            int nextLevel = level + 1;
            Transform spawn = LevelManager.Instance != null ? LevelManager.Instance.GetSpawnPoint(nextLevel) : null;
            if (spawn != null)
            {
                _cc.Teleport(spawn.position, spawn.rotation);
                _yaw = spawn.rotation.eulerAngles.y;
                StabilizeCharacterPhysics();
            }

            CurrentLevel = nextLevel;
            LevelQuestionIndex = -1;
            LastAnsweredIndex = -1;
            CurrentLevelCorrectCount = 0;
            _lastDisplayedQuestionIndex = -1;
            LevelCycleTimer = TickTimer.None;
            State = EPlayerState.Advancing;
            return;
        }

        FinishRace();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ShowLevelBlocked(int correct, int total, int levelQuestionIndex, int lastAnsweredIndex)
    {
        TriviaUI.Instance?.ShowLevelBlockedMessage(correct, total, levelQuestionIndex, lastAnsweredIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_HideLevelBlocked()
    {
        TriviaUI.Instance?.HideLevelBlockedMessage();
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

        ui.UpdateLevelHud(
            CurrentLevel,
            CurrentLevelCorrectCount,
            QuestionManager.Instance != null ? QuestionManager.Instance.GetQuestionCount(CurrentLevel) : 0);
        ui.UpdateScoreDisplay(
            PuntajeObtenido,
            QuestionManager.Instance != null ? QuestionManager.Instance.GetMaxPossibleScore() : 0);

        if (TriviaUI.Instance != null && TriviaUI.Instance.IsLevelBlockedMessageActive)
            ui.RefreshBlockedMessage(
                CurrentLevelCorrectCount,
                QuestionManager.Instance != null ? QuestionManager.Instance.GetQuestionCount(CurrentLevel) : 0,
                LevelQuestionIndex,
                LastAnsweredIndex);

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

    public void FinishRace()
    {
        if (!Object.HasStateAuthority) return;

        ResolveGameManager();
        if (_gameManager != null)
            _gameManager.RegisterPlayerFinish(this);

        State = EPlayerState.Finished;

        RPC_NotificarLlegada(PuntajeObtenido, Level1CorrectCount, Level2CorrectCount, Level3CorrectCount);
        RPC_ShowPodiumToAll();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_NotificarLlegada(int puntajeObtenido, int n1Correct, int n2Correct, int n3Correct)
    {
        int puntajeMaximo = QuestionManager.Instance != null ? QuestionManager.Instance.GetMaxPossibleScore() : 0;
        TriviaUI.Instance?.RegistrarFinDeCarreraLocal(puntajeObtenido, puntajeMaximo, n1Correct, n2Correct, n3Correct);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ShowPodiumToAll()
    {
        TriviaUI.Instance?.ShowPodiumForAll();
    }
}
