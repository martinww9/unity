using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public Vector3 Direction;
    public int SelectedAnswerIndex; // -1 = ninguna, 0=A, 1=B, 2=C, 3=D.
    public NetworkButtons Buttons;
    
    public const byte MouseButton0 = 1;
}