using Fusion;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

public class PlayerCamera : NetworkBehaviour
{
    [Header("Cinemachine 3 Cameras")]
    public CinemachineCamera fpvCam;
    public CinemachineCamera tpvCam;

    private bool _isFirstPerson = true;

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            UpdateCameraPriorities();

            // Opcional: Bloquear cursor para el juego
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            // Si es otro jugador, bajo la prioridad de sus cámaras a 0 
            // para que mi CinemachineBrain las ignore
            fpvCam.Priority = 0;
            tpvCam.Priority = 0;
        }
    }

    // Lógica de cambio de vista (WASD ya funciona, ahora cambiamos prioridad)
    private void UpdateCameraPriorities()
    {
        if (!HasInputAuthority) return;

        if (_isFirstPerson)
        {
            fpvCam.Priority = 100;
            tpvCam.Priority = 10;
        }
        else
        {
            fpvCam.Priority = 10;
            tpvCam.Priority = 100;
        }
    }

    void Update()
    {
        // Solo procesamos el input si somos el dueño del objeto
        if (!HasInputAuthority) return;

        // Verificamos si la tecla V fue presionada en este frame
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            _isFirstPerson = !_isFirstPerson;
            UpdateCameraPriorities();
            Debug.Log($"Perspectiva cambiada: {(_isFirstPerson ? "Primera Persona" : "Tercera Persona")}");
        }
    }
}