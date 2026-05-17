using Fusion;
using UnityEngine;
using Unity.Cinemachine;

public enum EPlayerState { Responding, Stunned, Advancing, Finished }

public class Player : NetworkBehaviour
{
    [Networked] public EPlayerState State { get; set; }
    [Networked] private TickTimer _stunTimer { get; set; }
    [Networked] public int LastAnsweredIndex { get; set; } = -1;
    [Networked] public int PlayerRank { get; set; }
    [Networked] public int RespuestasCorrectas { get; set; }
    
    [Networked] private float _pitch { get; set; }

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
        _gameManager = FindFirstObjectByType<GameManager>();
        
        // 1. Buscamos todas las cámaras de Cinemachine dentro de NUESTRO Prefab
        var vCams = GetComponentsInChildren<Unity.Cinemachine.CinemachineCamera>(true); 

        if (Object.HasInputAuthority) // ¡SI SOY EL JUGADOR LOCAL!
        {
            Local = this;
            // 2. Le decimos a Cinemachine que NUESTRAS cámaras nos sigan y miren a nuestro Pivot
            foreach (var cam in vCams)
            {
                cam.enabled = true; // Nos aseguramos de que estén encendidas
                if (_cameraPivot != null)
                {
                    cam.Follow = _cameraPivot;
                    //cam.LookAt = _cameraPivot;
                }
            }

            // Opcional: Ocultamos nuestro propio cuerpo local para que no tape la cámara en primera persona
            Transform meshTransform = transform.Find("Mesh");
            if (meshTransform != null) meshTransform.gameObject.SetActive(false);
            
            Transform armorsTransform = transform.Find("Armors");
            if (armorsTransform != null) armorsTransform.gameObject.SetActive(false);
        }
        else // ¡SI ES UN RIVAL EN MI PANTALLA!
        {
            // 3. APAGAMOS por completo sus componentes de cámara para que no secuestren nuestra pantalla
            foreach (var cam in vCams)
            {
                cam.enabled = false;
            }

            Transform cameraHolder = transform.Find("CameraHolder");
            if (cameraHolder != null) cameraHolder.gameObject.SetActive(false);
            if (_cameraPivot != null) _cameraPivot.gameObject.SetActive(false);
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
                _pitch = Mathf.Clamp(_pitch, -80f, 80f); 
                
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
                    RespuestasCorrectas++;
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
        // --- ROTACIÓN VISUAL DE LA CABEZA (Aplica para todos los jugadores en la escena) ---
        if (_cameraPivot != null)
        {
            // Usamos la variable [Networked] '_pitch' para rotar el pivot suavemente en cada frame
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0, 0);
        }

        // 🚨 PROTECCIÓN DE UI: Solo el dueño de este teclado puede manipular la interfaz de su pantalla
        if (Object.HasInputAuthority)
        {
            // --- CONTROL DE UI POST-CARRERA ---
            if (State == EPlayerState.Finished)
            {
                TriviaUI.Instance.ShowWaiting();
            }
            
            else if (State == EPlayerState.Responding)
            {
                int currentIdx = _gameManager.CurrentQuestionIndex;
                if (_lastDisplayedQuestionIndex != currentIdx)
                {
                    // Intentamos pedir la pregunta
                    var q = QuestionManager.Instance.GetQuestion(currentIdx);
                    
                    // LA CLAVE: Solo marcamos la pregunta como "mostrada" 
                    // si realmente logramos sacarla del QuestionManager.
                    if (q != null) 
                    {
                        TriviaUI.Instance.ShowQuestion(q);
                        _lastDisplayedQuestionIndex = currentIdx; // <--- SE MUEVE AQUÍ ADENTRO
                    }
                    // Si 'q' es null, el código ignorará esto y volverá a 
                    // intentarlo en la siguiente fracción de segundo.
                }
            }
            else
            {
                if (State != EPlayerState.Finished) 
                {
                    TriviaUI.Instance.Hide();
                }
            }
        }

        // --- ANIMACIONES (Todos deben procesarlas para verse en red) ---
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
        Debug.Log($"¡Llegaste en la posición: {rank}!");
        
        RPC_NotificarLlegada(RespuestasCorrectas);
        RPC_UpdatePodiumGlobal();
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_NotificarLlegada(int misRespuestasCorrectas)
    {
        // Esto solo se ejecuta en la pantalla local del jugador que cruzó
        Debug.Log($"¡Acabas de cruzar la meta! Tienes {misRespuestasCorrectas} correctas.");
        if (TriviaUI.Instance != null)
        {
            TriviaUI.Instance.RegistrarFinDeCarreraLocal(misRespuestasCorrectas, 10);
        }
    }
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_UpdatePodiumGlobal()
    {
        // Esto se ejecuta en la pantalla de TODOS los jugadores al mismo tiempo
        if (TriviaUI.Instance != null)
        {
            TriviaUI.Instance.UpdatePodiumLive();
        }
    }
}