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
    private const int PhaseArriveBlend = 9;

    [SerializeField] private Animator _animator;
    [SerializeField] private Transform _standPoint;
    [SerializeField] private Transform _danceCenter;
    [SerializeField] private Transform _chair;
    [SerializeField] private Transform _chairBackPoint;
    [SerializeField] private Transform _movementPivot;
    [SerializeField] private float _standAnimDelay = 0.7f;
    [SerializeField] private float _chairMoveBackDistance = 0.5f;
    [SerializeField] private float _chairMoveSpeed = 0.8f;
    [SerializeField] private float _walkSpeed = 1.2f;
    [SerializeField] private float _arriveThreshold = 0.15f;
    [SerializeField] private float _walkArriveThreshold = 0.05f;
    [SerializeField] private float _arriveBlendDuration = 0.25f;
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
    private int _pendingArrivePhase;
    private string _walkTargetLabel = "destino";

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();

        if (_movementPivot == null)
        {
            var feet = transform.Find("FeetAnchor");
            if (feet != null)
                _movementPivot = feet;
        }

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
        RefreshFloorY();

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
            RefreshFloorY();
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
            case PhaseArriveBlend:
                if (TickArriveBlend())
                    MovePhase = _pendingArrivePhase;
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
            case PhaseArriveBlend:
                if (TickArriveBlend())
                    MovePhase = _pendingArrivePhase;
                break;
        }
    }

    private void TickWalkPhase(int arrivePhase, string arriveLabel)
    {
        _debugTickCounter++;
        if (_debug && _debugTickCounter % 30 == 0)
            DebugLog($"Caminando a {arriveLabel}. Distancia pivot={GetDistanceToTarget():F3}m");

        if (StepWalkTowards(_targetWorldPos, _walkSpeed))
            BeginArriveBlend(arrivePhase, arriveLabel);
    }

    private void BeginArriveBlend(int arrivePhase, string arriveLabel)
    {
        _pendingArrivePhase = arrivePhase;
        MovePhase = PhaseArriveBlend;
        PhaseTimer = TickTimer.CreateFromSeconds(Runner, _arriveBlendDuration);
        DebugLog($"Iniciando blend de llegada a {arriveLabel}. Distancia pivot={GetDistanceToTarget():F3}m");
    }

    private bool TickArriveBlend()
    {
        float duration = Mathf.Max(_arriveBlendDuration, 0.001f);
        float t = 1f - (PhaseTimer.RemainingTime(Runner) ?? 0f) / duration;
        Vector3 target = FlatAtY(_targetWorldPos, _floorY);

        // Snap position first; rotation is blended with pivot pinned to avoid orbiting.
        SetPivotWorldPos(target);
        ApplyRotationKeepingPivot(target, Quaternion.Slerp(_lockedWalkRotation, _targetWorldRot, Mathf.Clamp01(t)));

        if (!PhaseTimer.Expired(Runner))
            return false;

        ApplyRotationKeepingPivot(target, _targetWorldRot);
        WalkAnimEnabled = false;
        DebugLog($"Blend completado. Distancia pivot final={GetDistanceToTarget():F3}m");
        return true;
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
        RefreshFloorY();
        CacheStandTarget();
        _walkTargetLabel = "StandPoint";
        _debugTickCounter = 0;
        MovePhase = PhaseTurnToStand;
        WalkAnimEnabled = false;
        DebugLog($"Iniciando giro hacia StandPoint. Distancia pivot={GetDistanceToTarget():F2}m");
    }

    private void BeginWalkToDancePhase()
    {
        RefreshFloorY();
        CacheDanceTarget();
        DebugLogMarkerAlignment(_danceCenter, "DancePoint");
        _walkTargetLabel = "DancePoint";
        _debugTickCounter = 0;
        MovePhase = PhaseTurnToDance;
        WalkAnimEnabled = false;
        DebugLog($"Iniciando giro hacia DancePoint. Distancia pivot={GetDistanceToTarget():F2}m");
    }

    private void CompleteTurnAndStartWalk(int walkPhase)
    {
        Vector3 delta = GetFlatDeltaTo(_targetWorldPos);
        _lockedWalkRotation = delta.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(delta.normalized, Vector3.up)
            : transform.rotation;
        ApplyRotationKeepingPivot(GetPivotWorldPos(), _lockedWalkRotation);
        WalkAnimEnabled = true;
        MovePhase = walkPhase;
        _debugTickCounter = 0;

        float dot = delta.sqrMagnitude > 0.001f
            ? Vector3.Dot(transform.forward, delta.normalized)
            : 1f;
        DebugLog($"Giro completo. Caminando a {_walkTargetLabel}. Distancia pivot={delta.magnitude:F2}m, dot={dot:F2}");
    }

    private void RefreshFloorY()
    {
        _floorY = GetPivotWorldPos().y;
    }

    private Vector3 GetPivotWorldPos()
    {
        if (_movementPivot != null)
            return _movementPivot.position;
        return transform.position;
    }

    private void SetPivotWorldPos(Vector3 feetWorldPos)
    {
        if (_movementPivot == null || _movementPivot == transform)
        {
            transform.position = new Vector3(feetWorldPos.x, feetWorldPos.y, feetWorldPos.z);
            return;
        }

        Vector3 worldOffset = transform.rotation * _movementPivot.localPosition;
        Vector3 rootPos = feetWorldPos - worldOffset;
        transform.position = rootPos;
    }

    private void ApplyRotationKeepingPivot(Vector3 pivotWorldPos, Quaternion rotation)
    {
        transform.rotation = rotation;
        SetPivotWorldPos(FlatAtY(pivotWorldPos, _floorY));
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
        if (delta.sqrMagnitude <= _walkArriveThreshold * _walkArriveThreshold)
            return true;

        float dt = Runner.DeltaTime;
        Quaternion faceTarget = Quaternion.LookRotation(delta.normalized, Vector3.up);
        Vector3 pinnedPivot = FlatAtY(GetPivotWorldPos(), _floorY);
        Quaternion nextRotation = Quaternion.RotateTowards(transform.rotation, faceTarget, _rotationSpeed * dt);
        ApplyRotationKeepingPivot(pinnedPivot, nextRotation);

        return Vector3.Angle(transform.forward, delta.normalized) <= _walkAlignAngle;
    }

    private bool StepWalkTowards(Vector3 worldPos, float speed)
    {
        float dt = Runner.DeltaTime;
        Vector3 current = FlatAtY(GetPivotWorldPos(), _floorY);
        Vector3 target = FlatAtY(worldPos, _floorY);
        Vector3 delta = target - current;

        if (delta.sqrMagnitude <= _walkArriveThreshold * _walkArriveThreshold)
            return true;

        WalkAnimEnabled = true;
        Vector3 next = Vector3.MoveTowards(current, target, speed * dt);
        ApplyRotationKeepingPivot(new Vector3(next.x, _floorY, next.z), _lockedWalkRotation);

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
        Vector3 current = FlatAtY(GetPivotWorldPos(), _floorY);
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

        Vector3 toMarker = marker.position - GetPivotWorldPos();
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
        Vector3 pivotPos = _movementPivot != null ? _movementPivot.position : transform.position;

        if (_movementPivot != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(pivotPos, 0.1f);
            Gizmos.DrawLine(transform.position, pivotPos);
        }

        if (_standPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pivotPos, _standPoint.position);
            Gizmos.DrawWireSphere(_standPoint.position, _walkArriveThreshold);
        }

        if (_danceCenter != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(pivotPos, _danceCenter.position);
            Gizmos.DrawWireSphere(_danceCenter.position, _walkArriveThreshold);
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
            Gizmos.DrawWireSphere(_targetWorldPos, _walkArriveThreshold);
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
            if (MovePhase == PhaseWalkToCenter || MovePhase == PhaseArriveBlend)
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
