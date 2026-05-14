using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public const int MouseButton0 = 0;
    
    public Vector3 Direction;
    public NetworkButtons Buttons;
    public int SelectedAnswerIndex; // -1 = ninguna, 0=A, 1=B, 2=C, 3=D.
    
    public float lookRotationDeltaX;
    public float lookRotationDeltaY;
}