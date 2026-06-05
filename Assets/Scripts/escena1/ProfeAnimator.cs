using Fusion;
using UnityEngine;

public class ProfeAnimator : NetworkBehaviour
{
    public const int StageSitting = 0;
    public const int StageStanding = 1;
    public const int StageDancing = 2;

    private const int PhaseNone = 0;
    private const int PhaseStandDelay = 1;
    private const int PhaseChairBack = 2;
    private const int PhaseTurnToStand = 3;
    private const int PhaseWalkToStand = 4;
    private const int PhaseStandIdle = 5;
    private const int PhaseWalkToCenter = 6;
    private const int PhaseDancing = 7;
    private const int PhaseTurnToDance = 8;

    [SerializeField] private Animator _animator;
    [SerializeField] private Transform _standPoint;
    [SerializeField] private Transform _danceCenter;
    [SerializeField] private Transform _chair;
    [SerializeField] private Transform _chairBackPoint;
    [SerializeField] private float _standAnimDelay = 0.7f;
    [SerializeField] private float _chairMoveBackDistance = 0.5f;
    [SerializeField] private float _chairMoveSpeed = 0.8f;
    [SerializeField] private float _walkSpeed = 1.2f;
    [SerializeField] private float _arriveThreshold = 0.15f;
    [SerializeField] private float _rotationSpeed = 360f;
    [SerializeField] private float _walkAlignAngle = 15f;
    [SerializeField] private bool _debug;

    [Networked] private int AnimStage { get; set; }
    [Networked] private int MovePhase { get; set; }
    [Networked] private TickTimer PhaseTimer { get; set; }
    [Networked] private Vector3 NetPosition { get; set; }
    [Networked] private Quaternion NetRotation { get; set; }
    [Networked] private Vector3 NetChairPosition { get; set; }
    [Networked] private Quaternion NetChairRotation { get; set; }
    [Networked] private NetworkBool WalkAnimEnabled { get; set; }

    private Vector3 _targetWorldPos;
    private Quaternion _targetWorldRot;
    private Vector3 _chairTargetPos;
    private Quaternion _lockedWalkRotation;
    private float _floorY;
    private float _chairFloorY;
    private int _debugTickCounter;
    private string _walkTargetLabel = "destino";

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();

        if (_standPoint == null)
        {
            var stand = transform.Find("StandPoint");
            if (stand != null)
                _standPoint = stand;
        }

        if (_danceCenter == null)
        {
            var dance = transform.Find("DancePoint");
            if (dance != null)
                _danceCenter = dance;
        }

        if (_animator != null)
            _animator.applyRootMotion = false;

        foreach (var childAnimator in GetComponentsInChildren<Animator>())
        {
            if (childAnimator != _animator)
                childAnimator.enabled = false;
        }
    }

    public override void Spawned()
    {
        _floorY = transform.position.y;

        if (_chair != null)
        {
            _chairFloorY = _chair.position.y;
            EnsureChairDynamic();
        }

        if (Object.HasStateAuthority)
        {
            AnimStage = StageSitting;
            MovePhase = PhaseNone;
            WalkAnimEnabled = false;
            NetPosition = transform.position;
            NetRotation = transform.rotation;
            SyncChairNetworkState();
        }

        ApplyAnimatorVisuals();
        ApplyChairVisuals();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        switch (AnimStage)
        {
            case StageStanding:
                TickStandingSequence();
                break;
            case StageDancing:
                TickDancingSequence();
                break;
        }

        NetPosition = transform.position;
        NetRotation = transform.rotation;
        SyncChairNetworkState();
    }

    public override void Render()
    {
        if (!Object.HasStateAuthority)
            transform.SetPositionAndRotation(NetPosition, NetRotation);

        ApplyChairVisuals();
        ApplyAnimatorVisuals();
    }

    public void TryAdvanceFromBarrier(int targetStage, Player player)
    {
        if (player == null || !player.Object.HasStateAuthority) return;
        if (targetStage <= StageSitting || targetStage > StageDancing) return;

        RPC_RequestStage(targetStage);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestStage(int targetStage)
    {
        if (targetStage <= AnimStage) return;

        AnimStage = targetStage;
        WalkAnimEnabled = false;

        if (targetStage == StageStanding)
        {
            CacheStandTarget();
            CacheChairTarget();
            DebugLogMarkerAlignment(_standPoint, "StandPoint");
            MovePhase = PhaseStandDelay;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, _standAnimDelay);
            return;
        }

        if (targetStage == StageDancing)
            BeginWalkToDancePhase();
    }

    private void TickStandingSequence()
    {
        switch (MovePhase)
        {
            case PhaseStandDelay:
                if (PhaseTimer.Expired(Runner))
                    BeginChairBackPhase();
                break;
            case PhaseChairBack:
                if (StepChairBack())
                    BeginWalkToStandPhase();
                break;
            case PhaseTurnToStand:
                if (StepTurnTowards(_targetWorldPos))
                    CompleteTurnAndStartWalk(PhaseWalkToStand);
                break;
            case PhaseWalkToStand:
                TickWalkPhase(PhaseStandIdle, "StandPoint");
                break;
        }
    }

    private void TickDancingSequence()
    {
        switch (MovePhase)
        {
            case PhaseTurnToDance:
                if (StepTurnTowards(_targetWorldPos))
                    CompleteTurnAndStartWalk(PhaseWalkToCenter);
                break;
            case PhaseWalkToCenter:
                TickWalkPhase(PhaseDancing, "DancePoint");
                break;
        }
    }

    private void TickWalkPhase(int arrivePhase, string arriveLabel)
    {
        _debugTickCounter++;
        if (_debug && _debugTickCounter % 30 == 0)
            DebugLog($"Caminando a {arriveLabel}. Distancia restante={GetDistanceToTarget():F2}m");

        if (StepWalkTowards(_targetWorldPos, _targetWorldRot, _walkSpeed))
        {
            WalkAnimEnabled = false;
            MovePhase = arrivePhase;
            DebugLog($"Llegó a {arriveLabel}.");
        }
    }

    private void BeginChairBackPhase()
    {
        if (_chair == null || _chairMoveBackDistance <= 0f)
        {
            DebugLog("BeginChairBackPhase: silla no asignada, saltando retroceso.");
            BeginWalkToStandPhase();
            return;
        }

        EnsureChairDynamic();
        CacheChairTarget();
        MovePhase = PhaseChairBack;
    }

    private void BeginWalkToStandPhase()
    {
        CacheStandTarget();
        _walkTargetLabel = "StandPoint";
        _debugTickCounter = 0;
        MovePhase = PhaseTurnToStand;
        WalkAnimEnabled = false;
        DebugLog($"Iniciando giro hacia StandPoint. Distancia={GetDistanceToTarget():F2}m");
    }

    private void BeginWalkToDancePhase()
    {
        CacheDanceTarget();
        DebugLogMarkerAlignment(_danceCenter, "DancePoint");
        _walkTargetLabel = "DancePoint";
        _debugTickCounter = 0;
        MovePhase = PhaseTurnToDance;
        WalkAnimEnabled = false;
        DebugLog($"Iniciando giro hacia DancePoint. Distancia={GetDistanceToTarget():F2}m");
    }

    private void CompleteTurnAndStartWalk(int walkPhase)
    {
        Vector3 delta = GetFlatDeltaTo(_targetWorldPos);
        _lockedWalkRotation = delta.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(delta.normalized, Vector3.up)
            : transform.rotation;
        transform.rotation = _lockedWalkRotation;
        WalkAnimEnabled = true;
        MovePhase = walkPhase;
        _debugTickCounter = 0;

        float dot = delta.sqrMagnitude > 0.001f
            ? Vector3.Dot(transform.forward, delta.normalized)
            : 1f;
        DebugLog($"Giro completo. Caminando a {_walkTargetLabel}. Distancia={delta.magnitude:F2}m, dot={dot:F2}");
    }

    private void CacheChairTarget()
    {
        if (_chair == null) return;

        _chairFloorY = _chair.position.y;

        if (_chairBackPoint != null)
            _chairTargetPos = _chairBackPoint.position;
        else
            _chairTargetPos = _chair.position - _chair.forward * _chairMoveBackDistance;

        _chairTargetPos = FlatAtY(_chairTargetPos, _chairFloorY);
    }

    private bool StepChairBack()
    {
        if (_chair == null) return true;

        float dt = Runner.DeltaTime;
        Vector3 current = FlatAtY(_chair.position, _chairFloorY);
        Vector3 target = FlatAtY(_chairTargetPos, _chairFloorY);
        Vector3 next = Vector3.MoveTowards(current, target, _chairMoveSpeed * dt);
        _chair.position = new Vector3(next.x, _chairFloorY, next.z);

        if ((next - target).sqrMagnitude <= _arriveThreshold * _arriveThreshold)
        {
            _chair.position = new Vector3(target.x, _chairFloorY, target.z);
            return true;
        }

        return false;
    }

    private bool StepTurnTowards(Vector3 worldPos)
    {
        Vector3 delta = GetFlatDeltaTo(worldPos);
        if (delta.sqrMagnitude <= _arriveThreshold * _arriveThreshold)
            return true;

        float dt = Runner.DeltaTime;
        Quaternion faceTarget = Quaternion.LookRotation(delta.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, faceTarget, _rotationSpeed * dt);

        return Vector3.Angle(transform.forward, delta.normalized) <= _walkAlignAngle;
    }

    private bool StepWalkTowards(Vector3 worldPos, Quaternion worldRot, float speed)
    {
        float dt = Runner.DeltaTime;
        Vector3 current = FlatAtY(transform.position, _floorY);
        Vector3 target = FlatAtY(worldPos, _floorY);
        Vector3 delta = target - current;

        if (delta.sqrMagnitude <= _arriveThreshold * _arriveThreshold)
        {
            transform.position = new Vector3(target.x, _floorY, target.z);
            transform.rotation = worldRot;
            return true;
        }

        WalkAnimEnabled = true;
        transform.rotation = _lockedWalkRotation;
        Vector3 next = Vector3.MoveTowards(current, target, speed * dt);
        transform.position = new Vector3(next.x, _floorY, next.z);

        return false;
    }

    private void CacheStandTarget()
    {
        if (_standPoint == null) return;
        _targetWorldPos = FlatAtY(_standPoint.position, _floorY);
        _targetWorldRot = _standPoint.rotation;
    }

    private void CacheDanceTarget()
    {
        if (_danceCenter == null) return;
        _targetWorldPos = FlatAtY(_danceCenter.position, _floorY);
        _targetWorldRot = _danceCenter.rotation;
    }

    private Vector3 GetFlatDeltaTo(Vector3 worldPos)
    {
        Vector3 current = FlatAtY(transform.position, _floorY);
        Vector3 target = FlatAtY(worldPos, _floorY);
        return target - current;
    }

    private float GetDistanceToTarget()
    {
        return GetFlatDeltaTo(_targetWorldPos).magnitude;
    }

    private void EnsureChairDynamic()
    {
        if (_chair == null) return;

        if (_chair.gameObject.isStatic)
        {
            _chair.gameObject.isStatic = false;
            DebugLog($"Silla '{_chair.name}': isStatic desactivado para permitir movimiento en runtime.");
        }
    }

    private void DebugLogMarkerAlignment(Transform marker, string label)
    {
        if (marker == null) return;

        Vector3 toMarker = marker.position - transform.position;
        toMarker.y = 0f;
        if (toMarker.sqrMagnitude < 0.001f) return;

        float dot = Vector3.Dot(transform.forward, toMarker.normalized);
        if (dot < 0f)
            Debug.LogWarning($"[ProfeAnimator] {label} está DETRÁS del Profe (dot={dot:F2}). Reposiciona el marcador delante del personaje.", this);
        else
            DebugLog($"{label} alineación OK (dot={dot:F2}).");
    }

    private void DebugLog(string message)
    {
        if (!_debug) return;
        Debug.Log($"[ProfeAnimator] {message}", this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        DrawMovementGizmos();
    }

    private void OnDrawGizmos()
    {
        if (!_debug) return;
        DrawMovementGizmos();
    }

    private void DrawMovementGizmos()
    {
        if (_standPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, _standPoint.position);
            Gizmos.DrawWireSphere(_standPoint.position, 0.25f);
        }

        if (_danceCenter != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, _danceCenter.position);
            Gizmos.DrawWireSphere(_danceCenter.position, 0.25f);
        }

        if (_chair != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 chairTarget = _chairBackPoint != null
                ? _chairBackPoint.position
                : _chair.position - _chair.forward * _chairMoveBackDistance;
            Gizmos.DrawLine(_chair.position, chairTarget);
            Gizmos.DrawWireSphere(chairTarget, 0.15f);
        }

        if (_debug && _targetWorldPos != Vector3.zero)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_targetWorldPos, _arriveThreshold);
        }
    }
#endif

    private static Vector3 FlatAtY(Vector3 pos, float y) => new Vector3(pos.x, y, pos.z);

    private void SyncChairNetworkState()
    {
        if (_chair == null) return;
        NetChairPosition = _chair.position;
        NetChairRotation = _chair.rotation;
    }

    private void ApplyChairVisuals()
    {
        if (_chair == null || Object.HasStateAuthority) return;
        _chair.SetPositionAndRotation(NetChairPosition, NetChairRotation);
    }

    private void ApplyAnimatorVisuals()
    {
        if (_animator == null) return;

        if (AnimStage == StageSitting)
        {
            SetAnimBools(true, false, false);
            return;
        }

        if (AnimStage == StageStanding)
        {
            if (MovePhase == PhaseWalkToStand)
                SetAnimBools(false, true, false);
            else
                SetAnimBools(false, false, false);
            return;
        }

        if (AnimStage == StageDancing)
        {
            if (MovePhase == PhaseWalkToCenter)
                SetAnimBools(false, true, false);
            else if (MovePhase >= PhaseDancing)
                SetAnimBools(false, false, true);
            else
                SetAnimBools(false, false, false);
        }
    }

    private void SetAnimBools(bool sentao, bool caminando, bool bailando)
    {
        _animator.SetBool("sentao", sentao);
        _animator.SetBool("caminando", caminando);
        _animator.SetBool("bailando", bailando);
    }
}
