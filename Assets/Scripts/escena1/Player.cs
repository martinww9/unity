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
    [Networked] private float _pitch { get; set; }

    private const float CycleDuration = 13f;
    private const float ResponseWindow = 10f;

    private int _lastDisplayedQuestionIndex = -1;
    [SerializeField] private Animator _animator;
    [SerializeField] private float _forwardSpeed = 12f;
    [SerializeField] private float _sprintSpeed = 20f;

    [Header("Configuración de Cámara")]
    [SerializeField] private Transform _cameraPivot;
    public float mouseSensitivity = 6f;

    public static Player Local;
    private NetworkCharacterController _cc;
    private GameManager _gameManager;

    private void Awake()
    {
        _cc = GetComponent<NetworkCharacterController>();
    }

    public override void Spawned()
    {
        ResolveGameManager();

        var vCams = GetComponentsInChildren<CinemachineCamera>(true);

        if (Object.HasInputAuthority)
        {
            Local = this;
            foreach (var cam in vCams)
            {
                cam.enabled = true;
                if (_cameraPivot != null)
                    cam.Follow = _cameraPivot;
            }

            Transform meshTransform = transform.Find("Mesh");
            if (meshTransform != null) meshTransform.gameObject.SetActive(false);

            Transform armorsTransform = transform.Find("Armors");
            if (armorsTransform != null) armorsTransform.gameObject.SetActive(false);
        }
        else
        {
            foreach (var cam in vCams)
                cam.enabled = false;

            Transform cameraHolder = transform.Find("CameraHolder");
            if (cameraHolder != null) cameraHolder.gameObject.SetActive(false);
            if (_cameraPivot != null) _cameraPivot.gameObject.SetActive(false);
        }

        if (Object.HasStateAuthority)
            State = EPlayerState.Advancing;

        if (_cc != null)
            _cc.rotationSpeed = 0f;
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
            _cc.Teleport(spawn.position, spawn.rotation);
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
                transform.Rotate(0, lobbyData.lookRotationDeltaX * mouseSensitivity, 0);
                _pitch -= lobbyData.lookRotationDeltaY * mouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, -80f, 80f);
            }
            return;
        }

        if (GetInput(out NetworkInputData data))
        {
            switch (State)
            {
                case EPlayerState.Responding:
                    HandleRespondingState(data);
                    break;

                case EPlayerState.Stunned:
                    _cc.Velocity = Vector3.zero;
                    if (_stunTimer.Expired(Runner)) State = EPlayerState.Advancing;
                    break;

                case EPlayerState.Advancing:
                case EPlayerState.Finished:
                    HandleMovement(data);
                    break;
            }

            if (State != EPlayerState.Responding)
            {
                transform.Rotate(0, data.lookRotationDeltaX * mouseSensitivity, 0);
                _pitch -= data.lookRotationDeltaY * mouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, -80f, 80f);
            }
        }
    }

    private void CheckLevelCycle()
    {
        if (!Object.HasStateAuthority) return;

        if (LevelCycleTimer.ExpiredOrNotRunning(Runner))
        {
            LevelCycleTimer = TickTimer.CreateFromSeconds(Runner, CycleDuration);
            LevelQuestionIndex++;
        }

        float remainingTime = GetRemainingResponseTime();
        int currentQIndex = LevelQuestionIndex;

        if (currentQIndex < 0) return;

        int maxQuestions = QuestionManager.Instance != null ? QuestionManager.Instance.GetQuestionCount(CurrentLevel) : 0;
        if (currentQIndex >= maxQuestions) return;

        if (remainingTime > 0 && LastAnsweredIndex != currentQIndex)
        {
            if (State != EPlayerState.Responding && State != EPlayerState.Stunned)
                State = EPlayerState.Responding;
        }

        if (remainingTime <= 0 && State == EPlayerState.Responding)
        {
            State = EPlayerState.Stunned;
            _stunTimer = TickTimer.CreateFromSeconds(Runner, 2f);
            LastAnsweredIndex = currentQIndex;
        }
    }

    public float GetRemainingResponseTime()
    {
        if (LevelCycleTimer.IsRunning)
        {
            float elapsed = CycleDuration - (LevelCycleTimer.RemainingTime(Runner) ?? 0);
            return ResponseWindow - elapsed;
        }
        return 0;
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
            _stunTimer = TickTimer.CreateFromSeconds(Runner, 2f);
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
                _cc.Teleport(spawn.position, spawn.rotation);

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

    public override void Render()
    {
        if (_cameraPivot != null)
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0, 0);

        if (Object.HasInputAuthority)
        {
            ResolveGameManager();
            bool matchStarted = GameManager.IsMatchStartedSafe;

            if (matchStarted && State != EPlayerState.Finished)
            {
                if (TriviaUI.Instance != null)
                {
                    TriviaUI.Instance.UpdateTimer(GetRemainingResponseTime());
                    TriviaUI.Instance.UpdateLevelIndicator(CurrentLevel);
                }
            }

            if (State == EPlayerState.Finished)
            {
                TriviaUI.Instance?.ShowWaiting();
            }
            else if (State == EPlayerState.Responding)
            {
                int currentIdx = LevelQuestionIndex;
                if (_lastDisplayedQuestionIndex != currentIdx)
                {
                    var q = QuestionManager.Instance?.GetQuestion(CurrentLevel, currentIdx);
                    if (q != null)
                    {
                        TriviaUI.Instance?.ShowQuestion(q);
                        _lastDisplayedQuestionIndex = currentIdx;
                    }
                }
            }
            else if (State != EPlayerState.Finished)
            {
                TriviaUI.Instance?.Hide();
            }
        }

        if (_animator != null)
        {
            _animator.SetBool("isStunned", State == EPlayerState.Stunned);
            float speed = (State == EPlayerState.Advancing || State == EPlayerState.Finished) ? 1f : 0f;
            _animator.SetFloat("Speed", speed);
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
