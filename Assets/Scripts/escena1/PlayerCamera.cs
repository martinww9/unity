using UnityEngine;
using UnityEngine.InputSystem; // Necesario para el nuevo sistema

public class PlayerCamera : MonoBehaviour
{
    [Header("Configuración")]
    public Transform target;        
    public float distance = 5.0f;    
    public float sensitivity = 0.1f; // Sensibilidad (ajusta este valor si va muy rápido)

    [Header("Límites")]
    public float minY = -20f;        
    public float maxY = 80f;         

    private float _rotationX = 0f;
    private float _rotationY = 0f;

    void LateUpdate()
    {
        if (target == null) return;

        // Solo rotamos si el mouse está bloqueado (Modo Carrera)
        if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
        {
            // En el nuevo sistema usamos Mouse.current.delta
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            _rotationX += mouseDelta.x * sensitivity;
            _rotationY -= mouseDelta.y * sensitivity;
            _rotationY = Mathf.Clamp(_rotationY, minY, maxY);
        }

        Quaternion rotation = Quaternion.Euler(_rotationY, _rotationX, 0);
        Vector3 position = target.position - (rotation * Vector3.forward * distance);

        transform.rotation = rotation;
        transform.position = position;
    }
}