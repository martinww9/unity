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
    [SerializeField] private float _forwardSpeed = 8f;
    
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
        if (State == EPlayerState.Finished || _gameManager == null) return;

        // 1. Lógica de Transición Sincronizada
        CheckGlobalCycle();

        // 2. Input y Acciones
        if (GetInput(out NetworkInputData data))
        {
            // -- ROTACIÓN DE CÁMARA --
            // Solo rotamos si NO estamos respondiendo una pregunta.
            if (State != EPlayerState.Responding)
            {
                // Eje X (Izquierda/Derecha): Rota todo el cuerpo del personaje
                transform.Rotate(0, data.lookRotationDeltaX * mouseSensitivity, 0);

                // Eje Y (Arriba/Abajo): Rota solo la cámara
                _pitch -= data.lookRotationDeltaY * mouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, -85f, 85f); // Bloqueamos para no rompernos el cuello
                
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
            State = EPlayerState.Advancing;
            LastAnsweredIndex = currentQIndex; 
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
        if (data.Direction.sqrMagnitude > 0)
        {
            // Como el ratón ya rota el cuerpo (transform.forward), el movimiento es muy simple:
            Vector3 moveDir = (transform.forward * data.Direction.z) + (transform.right * data.Direction.x);
            moveDir.Normalize();

            _cc.Move(moveDir * _forwardSpeed * Runner.DeltaTime);
            
            // Eliminamos el Slerp() que tenías antes porque ahora miramos siempre hacia donde apunta la cámara
        }
    }

    public override void Render()
    {
        if (!Object.HasInputAuthority) return;

        if (_gameManager != null && _gameManager.Object.IsValid)
        {
            bool isResponding = (State == EPlayerState.Responding);
            
            // Verificamos si estamos presionando la tecla ALT usando el Nuevo Input System
            bool isAltPressed = UnityEngine.InputSystem.Keyboard.current != null && 
                                (UnityEngine.InputSystem.Keyboard.current.leftAltKey.isPressed || 
                                 UnityEngine.InputSystem.Keyboard.current.rightAltKey.isPressed);

            // CONTROL DEL CURSOR: Se libera si estamos respondiendo, si terminamos, o si presionamos ALT
            bool shouldUnlockCursor = isResponding || isAltPressed || (State == EPlayerState.Finished);
            
            Cursor.lockState = shouldUnlockCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = shouldUnlockCursor;

            // CONTROL DE LA UI DE TRIVIA
            if (isResponding)
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
                float speed = (State == EPlayerState.Advancing) ? 1f : 0f;
                _animator.SetFloat("Speed", speed);
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