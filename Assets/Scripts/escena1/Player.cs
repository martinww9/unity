using Fusion;
using UnityEngine;

public enum EPlayerState { Responding, Stunned, Advancing, Finished }

public class Player : NetworkBehaviour
{
    [Networked] public EPlayerState State { get; set; }
    [Networked] private TickTimer _stunTimer { get; set; }
    
    // CLAVE: Sincroniza cuál fue la última pregunta que este jugador respondió
    [Networked] public int LastAnsweredIndex { get; set; } = -1;
    private int _lastDisplayedQuestionIndex = -1;

    [SerializeField] private Animator _animator;
    [SerializeField] private float _forwardSpeed = 8f;

    private NetworkCharacterController _cc;
    private GameManager _gameManager;

    private void Awake()
    {
        _cc = GetComponent<NetworkCharacterController>();
    }

    public override void Spawned()
    {
        _gameManager = FindFirstObjectByType<GameManager>();
        
        // Auto-asignación de cámara al aparecer
        if (Object.HasInputAuthority)
        {
            var cam = Camera.main.GetComponent<PlayerCamera>();
            if (cam != null) cam.target = this.transform;
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

        // REGLA: Solo entramos en modo Responding si hay tiempo Y NO hemos respondido a esta pregunta aún
        if (remainingTime > 0 && LastAnsweredIndex != currentQIndex)
        {
            if (State != EPlayerState.Responding && State != EPlayerState.Stunned)
            {
                State = EPlayerState.Responding;
            }
        }
        
        // Si se acaba el tiempo de los 10s y no respondimos:
        if (remainingTime <= 0 && State == EPlayerState.Responding)
        {
            State = EPlayerState.Advancing;
            LastAnsweredIndex = currentQIndex; // Lo marcamos como "hecho" para que no buclee
        }
    }

    private void HandleRespondingState(NetworkInputData data)
    {
        // Solo si el input de red trae un índice válido (0,1,2,3)
        if (data.SelectedAnswerIndex != -1)
        {
            Question currentQ = QuestionManager.Instance.GetQuestion(_gameManager.CurrentQuestionIndex);

            if (currentQ != null)
            {
                // BLOQUEO DE SEGURIDAD: Guardamos que este jugador ya respondió este ciclo
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
            // Movimiento relativo a la Main Camera
            Transform camTransform = Camera.main.transform;
            Vector3 forward = camTransform.forward;
            Vector3 right = camTransform.right;

            forward.y = 0; right.y = 0;
            forward.Normalize(); right.Normalize();

            Vector3 moveDir = (forward * data.Direction.z) + (right * data.Direction.x);
            _cc.Move(moveDir * _forwardSpeed * Runner.DeltaTime);
            
            if (moveDir.sqrMagnitude > 0.01f)
                transform.forward = Vector3.Slerp(transform.forward, moveDir, Runner.DeltaTime * 15f);
        }
    }

public override void Render()
{
    if (!Object.HasInputAuthority) return;

    if (_gameManager != null && _gameManager.Object.IsValid)
    {
        bool isResponding = (State == EPlayerState.Responding);
        Cursor.lockState = isResponding ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isResponding;

        if (State == EPlayerState.Responding)
        {
            int currentIdx = _gameManager.CurrentQuestionIndex;

            // SOLO intentar mostrar si el índice es distinto al último mostrado con éxito
            if (_lastDisplayedQuestionIndex != currentIdx)
            {
                Question q = QuestionManager.Instance.GetQuestion(currentIdx);
                
                // CLAVE: Solo marcamos como "mostrado" si la pregunta NO es nula
                if (q != null)
                {
                    _lastDisplayedQuestionIndex = currentIdx;
                    TriviaUI.Instance.ShowQuestion(q);
                }
            }
            
            // El timer se actualiza siempre en este estado
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
}