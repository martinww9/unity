using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public Vector3 Direction;
    public NetworkButtons Buttons;
    
    public const byte MouseButton0 = 1;

}
