using Fusion;
using UnityEngine;

public enum EPlayerState { Responding, Stunned, Advancing, Finished }

public class Player : NetworkBehaviour
{
    [Networked] public EPlayerState State { get; set; }
    [Networked] private TickTimer _stunTimer { get; set; }
    [Networked] public int LastAnsweredIndex { get; set; } = -1;
    [Networked] public int PlayerRank { get; set; }
    
    // Nueva variable sincronizada para la inclinación de la cabeza (Arriba/Abajo)
    [Networked] private float _pitch { get; set; }

    private int _lastDisplayedQuestionIndex = -1;
    [SerializeField] private Animator _animator;
    [SerializeField] private float _forwardSpeed = 12f;
    [SerializeField] private float _sprintSpeed = 20f;
    
    [Header("Configuración de Cámara")]
    [SerializeField] private Transform _cameraPivot; // ¡Asegúrate de asignar este objeto vacío en el Inspector!
    public float mouseSensitivity = 2f;

    private NetworkCharacterController _cc;
    private GameManager _gameManager;

    private void Awake()
    {
        _cc = GetComponent<NetworkCharacterController>();
    }

    public override void Spawned()
    {
        _gameManager = FindFirstObjectByType<GameManager>();
        
        if (Object.HasInputAuthority)
        {
            // Tomamos la cámara principal y la hacemos hija de nuestro CameraPivot
            Camera mainCam = Camera.main;
            if (mainCam != null && _cameraPivot != null)
            {
                mainCam.transform.SetParent(_cameraPivot);
                mainCam.transform.localPosition = Vector3.zero;
                mainCam.transform.localRotation = Quaternion.identity;
                Debug.Log("Cámara emparejada al jugador local.");
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (_gameManager == null) return; // Quitamos el "State == Finished" de aquí

        // 1. Lógica de Transición Sincronizada (SOLO si no hemos terminado)
        if (State != EPlayerState.Finished)
        {
            CheckGlobalCycle();
        }

        // 2. Input y Acciones
        if (GetInput(out NetworkInputData data))
        {
            // -- ROTACIÓN DE CÁMARA --
            if (State != EPlayerState.Responding)
            {
                transform.Rotate(0, data.lookRotationDeltaX * mouseSensitivity, 0);

                _pitch -= data.lookRotationDeltaY * mouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, -85f, 85f); 
                
                if (_cameraPivot != null)
                    _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0, 0);
            }

            // -- MÁQUINA DE ESTADOS --
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
                case EPlayerState.Finished: // <--- AÑADIDO: Permitimos movimiento aunque haya terminado
                    HandleMovement(data);
                    break;
            }
        }
    }

    private void CheckGlobalCycle()
    {
        float remainingTime = _gameManager.GetRemainingResponseTime();
        int currentQIndex = _gameManager.CurrentQuestionIndex;

        if (currentQIndex < 0) return;

        if (remainingTime > 0 && LastAnsweredIndex != currentQIndex)
        {
            if (State != EPlayerState.Responding && State != EPlayerState.Stunned)
            {
                State = EPlayerState.Responding;
            }
        }
        
        if (remainingTime <= 0 && State == EPlayerState.Responding)
        {
            State = EPlayerState.Stunned;
            _stunTimer = TickTimer.CreateFromSeconds(Runner, 2f); // 2 segundos de penalización
            LastAnsweredIndex = currentQIndex; 
            Debug.Log("SERVIDOR: Se acabó el tiempo. Jugador aturdido.");
        }
    }

    private void HandleRespondingState(NetworkInputData data)
    {
        if (data.SelectedAnswerIndex != -1)
        {
            Question currentQ = QuestionManager.Instance.GetQuestion(_gameManager.CurrentQuestionIndex);

            if (currentQ != null)
            {
                LastAnsweredIndex = _gameManager.CurrentQuestionIndex;

                if (data.SelectedAnswerIndex == currentQ.correctAnswerIndex)
                {
                    Debug.Log("SERVIDOR: Respuesta Correcta. Avanzando.");
                    State = EPlayerState.Advancing;
                }
                else
                {
                    Debug.Log("SERVIDOR: Respuesta Incorrecta. Aturdido.");
                    State = EPlayerState.Stunned;
                    _stunTimer = TickTimer.CreateFromSeconds(Runner, 2f);
                }
            }
        }
    }

    private void HandleMovement(NetworkInputData data)
    {
        // 1. Iniciamos la dirección en cero (por si no estamos tocando nada)
        Vector3 moveDir = Vector3.zero;

        if (data.Direction.sqrMagnitude > 0)
        {
            // Mantenemos tu lógica para saber hacia dónde mira la cámara
            moveDir = (transform.forward * data.Direction.z) + (transform.right * data.Direction.x);
            moveDir.Normalize();
        }

        // 2. Elegimos la velocidad basada en si apretamos Shift en la red
        float currentSpeed = data.Buttons.IsSet(NetworkInputData.SprintButton) ? _sprintSpeed : _forwardSpeed;

        // 3. Le asignamos la nueva velocidad máxima al controlador
        _cc.maxSpeed = currentSpeed; 
        // Nota: Si Unity te da error por "maxSpeed", escríbelo con M mayúscula: _cc.MaxSpeed = currentSpeed;

        // 4. Movemos al personaje pasándole SOLO la dirección.
        // Fusion se encarga de aplicar el DeltaTime y la velocidad máxima internamente.
        _cc.Move(moveDir);
    }

    public override void Render()
    {
        if (!Object.HasInputAuthority) return;

        if (_gameManager != null && _gameManager.Object.IsValid)
        {
            // --- CONTROL DEL CURSOR ---
            bool isResponding = (State == EPlayerState.Responding);
            
            bool isAltPressed = UnityEngine.InputSystem.Keyboard.current != null && 
                                (UnityEngine.InputSystem.Keyboard.current.leftAltKey.isPressed || 
                                 UnityEngine.InputSystem.Keyboard.current.rightAltKey.isPressed);

            bool shouldUnlockCursor = isResponding || isAltPressed;
            
            Cursor.lockState = shouldUnlockCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = shouldUnlockCursor;

            if (_gameManager.FinishedPlayersCount > 0)
            {
                TriviaUI.Instance.UpdatePodiumLive();
            }

            // --- CONTROL DE LA UI DE TRIVIA (Solo si no hemos terminado) ---
            if (isResponding && State != EPlayerState.Finished)
            {
                int currentIdx = _gameManager.CurrentQuestionIndex;
                if (_lastDisplayedQuestionIndex != currentIdx)
                {
                    Question q = QuestionManager.Instance.GetQuestion(currentIdx);
                    if (q != null)
                    {
                        _lastDisplayedQuestionIndex = currentIdx;
                        TriviaUI.Instance.ShowQuestion(q);
                    }
                }
                TriviaUI.Instance.UpdateTimer(_gameManager.GetRemainingResponseTime());
            }
            else
            {
                if (_lastDisplayedQuestionIndex != -1)
                {
                    _lastDisplayedQuestionIndex = -1;
                    TriviaUI.Instance.Hide();
                }
            }
            
            // Animaciones
            if (_animator != null)
            {
                _animator.SetBool("isStunned", State == EPlayerState.Stunned);
                float speed = (State == EPlayerState.Advancing || State == EPlayerState.Finished) ? 1f : 0f;
                _animator.SetFloat("Speed", speed);
            }

            // --- NUEVO: CONTROL DE UI POST-CARRERA ---
            if (State == EPlayerState.Finished)
            {
                if (_gameManager.IsRaceOver)
                {
                    TriviaUI.Instance.ShowPodium(); // Todos llegaron
                }
                else
                {
                    TriviaUI.Instance.ShowWaiting(); // Faltan jugadores
                }
                if (State == EPlayerState.Responding)
                {
                    int currentIdx = _gameManager.CurrentQuestionIndex;
                    if (_lastDisplayedQuestionIndex != currentIdx)
                    {
                        _lastDisplayedQuestionIndex = currentIdx;
                        var q = QuestionManager.Instance.GetQuestion(currentIdx);
                        if (q != null) TriviaUI.Instance.ShowQuestion(q);
                    }
                }
                else
                {
                    TriviaUI.Instance.Hide();
                }
            }
        }
    }
    
    public void FinishRace(int rank)
    {
        State = EPlayerState.Finished;
        PlayerRank = rank;
        Debug.Log($"¡Llegaste en la posición: {rank}!");
    }
}