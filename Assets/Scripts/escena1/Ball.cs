using UnityEngine;
using Fusion;

public class Ball : NetworkBehaviour
{
    [Networked] private TickTimer _life { get; set; }
    private const float Speed = 5f;
    private const float LifetimeSeconds = 5f;

    public void Init(){
        _life = TickTimer.CreateFromSeconds(Runner, LifetimeSeconds);
    }
    
    public override void FixedUpdateNetwork(){
        if (_life.Expired(Runner))
            Runner.Despawn(Object);
        else
            transform.position += Speed * transform.forward * Runner.DeltaTime;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    /*void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }*/
}
