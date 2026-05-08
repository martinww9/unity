using UnityEngine;
using UnityEngine.InputSystem; 

public class PlayerCamera : MonoBehaviour
{
    [Header("Configuración")]
    public Transform target;        
    public float distance = 5.0f;    
    public float sensitivity = 0.1f; 

    [Header("Límites")]
    public float minY = -20f;        
    public float maxY = 80f;         

    private float _rotationX = 0f;
    private float _rotationY = 0f;

void LateUpdate()
{
    if (target == null) return;

    bool isGameReady = GameManager.Instance != null && 
                       GameManager.Instance.Object != null && 
                       GameManager.Instance.Object.IsValid;

    Cursor.lockState = CursorLockMode.None;
    Cursor.visible = true;

    if (isGameReady && Mouse.current != null)
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        _rotationX += mouseDelta.x * sensitivity;
        _rotationY -= mouseDelta.y * sensitivity;
        _rotationY = Mathf.Clamp(_rotationY, minY, maxY);
    }

    ActualizarPosicionCamara();
}

    private void ActualizarPosicionCamara()
    {
        Quaternion rotation = Quaternion.Euler(_rotationY, _rotationX, 0);
        Vector3 position = target.position - (rotation * Vector3.forward * distance);

        transform.rotation = rotation;
        transform.position = position;
    }
}