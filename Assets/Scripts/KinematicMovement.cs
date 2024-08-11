using AdventureCore;
using KinematicCharacterController;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class KinematicMovement : MovementBasePersisted, ICharacterController
{
    [Header("General")]
    public KinematicCharacterMotor Motor;
    [Tooltip("transform used to transform input so when the player pushes left the character moves left relative to the camera")]
    public Transform Camera;
    public bool MoveByRootMotion;
    [Header("Settings")]
    public float MaxStableMoveSpeed = 5;
    public float StableMovementSharpness = 15;
    public float OrientationSharpness = 10;
    public float JumpUpSpeed = 10f;
    public Vector3 Gravity = new Vector3(0, -30f, 0);
    [Header("Events")]
    [Tooltip("fired when starting a jump")]
    public UnityEvent Jumping;
    [Tooltip("movement is connected to the ground again, parameter is the distance traveled downwards")]
    public UnityEvent<float> Fallen;
    [Tooltip("called whenever the character is moved instantly to another spot by teleporting, can be used to reset things like the camera")]
    public UnityEvent Teleported;

    private bool _wasSprintingSuspended;
    private bool _isSprintingSuspended;
    public bool IsSprintingSuspended
    {
        get
        {
            return _isSprintingSuspended;
        }
        set
        {
            _isSprintingSuspended = value;

            if (value)
            {
                _wasSprintingSuspended = true;
                IsSprinting = false;
            }
        }
    }

    private Vector3 _lastGrounded;

    private Vector2 _input;
    private Vector2 _inputAdopted;
    private Vector3 _inputDirection;
    private Vector3 _moveDirection;
    private Vector3 _characterDirection;

    private Vector3 _rootMotionPositionDelta;
    private Quaternion _rootMotionRotationDelta;

    private Vector3 _internalVelocityAdd = Vector3.zero;

    private bool _isAligning;

    protected override void Awake()
    {
        Motor.CharacterController = this;

        if (Persister && Persister.Check(PERSISTENCE_SUB_KEY))
        {
            var data = Persister.Get<MovementData>(PERSISTENCE_SUB_KEY);

            if (Motor.Transform)
                Motor.SetPositionAndRotation(data.Position, data.Rotation);
            else
                Motor.transform.SetPositionAndRotation(data.Position, data.Rotation);

            if (CameraPivot)
                CameraPivot.rotation = data.CameraRotation;
        }
    }

    private void Start()
    {
        if (!Camera)
            Camera = UnityEngine.Camera.main.transform;
    }

    public void AddVelocity(Vector3 velocity)
    {
        _internalVelocityAdd += velocity;
    }

    #region Input
    public void OnMove(InputAction.CallbackContext callbackContext) => OnMove(callbackContext.ReadValue<Vector2>());
    public void OnMove(InputValue value) => OnMove(value.Get<Vector2>());
    public void OnMove(Vector2 value)
    {
        _input = value;

        if (_input == Vector2.zero)
        {
            SpeedFactorForward = 0f;
            SpeedFactorSideways = 0f;
        }
        else
        {
            if (MoveByRootMotion)
            {
                if (HasTarget)
                {
                    SpeedFactorForward = _input.normalized.y;
                    SpeedFactorSideways = _input.normalized.x;
                }
                else
                {
                    SpeedFactorForward = _input.normalized.magnitude;
                    SpeedFactorSideways = 0f;
                }
            }

            _inputAdopted = _input;

        }
    }

    public void OnSprint(InputAction.CallbackContext callbackContext) => OnSprint(callbackContext.ReadValueAsButton());
    public void OnSprint(InputValue value) => OnSprint(value.isPressed);
    public void OnSprint(bool value)
    {
        if (IsSprintingSuspended)
            return;

        if (_wasSprintingSuspended)
        {
            if (value)
                return;
            else
                _wasSprintingSuspended = false;
        }

        IsSprinting = value;
    }

    public void OnJump(InputAction.CallbackContext callbackContext) => OnJump(callbackContext.ReadValueAsButton() ? 1f : 0f);
    public void OnJump(InputValue value) => OnJump(value.isPressed ? 1f : 0f);
    public void OnJump(float strength)
    {
        if (strength > 0f && Motor.GroundingStatus.IsStableOnGround)
        {
            Jumping?.Invoke();
            AddVelocity(Motor.CharacterUp * JumpUpSpeed);
        }
    }    
    #endregion

    #region MovementBase
    public override Vector3 Position
    {
        get => Motor.TransientPosition; set => Motor.SetPosition(value);
    }
    public override Quaternion Rotation
    {
        get => Motor.TransientRotation; set => Motor.SetRotation(value);
    }

    public override void Align(Vector3 direction) => StartCoroutine(alignCharacter(direction));
    public override void AlignToInput() => StartCoroutine(alignCharacter(_inputDirection));
    public override void AlignToTarget()
    {
        if (Target)
            StartCoroutine(alignCharacter(Target.position - transform.position));
        else
            StartCoroutine(alignCharacter(_inputDirection));
    }
    private IEnumerator alignCharacter(Vector3 direction)
    {
        _isAligning = true;

        var start = Rotation.eulerAngles.y;
        var target = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        return tween(t =>
        {
            Rotation = Quaternion.Euler(0.0f, Mathf.LerpAngle(start, target, t), 0.0f);
        }, () => _isAligning = false, 0.1f, true);
    }

    public override void TeleportCharacter(Vector3 position, Quaternion rotation)
    {
        Motor.SetPositionAndRotation(position, rotation);
        Teleported?.Invoke();
    }

    protected override IEnumerator moveCharacter(Vector3 position, Quaternion rotation)
    {
        var startPosition = Position;
        var startRotation = Rotation;

        return tween(t =>
        {
            Motor.SetPositionAndRotation(Vector3.Slerp(startPosition, position, t), Quaternion.Slerp(startRotation, rotation, t));
        }, null, 0.1f, true);
    }

    public override void PropelCharacter(Vector3 value)
    {
        base.PropelCharacter(value);

        AddVelocity(value);
    }

    public override void ApplyRootMotion(Animator animator)
    {
        base.ApplyRootMotion(animator);

        _rootMotionPositionDelta += animator.deltaPosition;
        _rootMotionRotationDelta = animator.deltaRotation * _rootMotionRotationDelta;
    }
    #endregion

    #region ICharacterController
    public void BeforeCharacterUpdate(float deltaTime)
    {

    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        if (!IsControlSuspended)
        {
            if (HasTarget)
                _characterDirection = (Target.position - Position).NormalizeXZ();
            else
                _characterDirection = _moveDirection;
        }

        if (_isAligning)
            return;

        if (IsRotationSuspended)
        {
            currentRotation = _rootMotionRotationDelta * currentRotation;
        }
        else
        {
            currentRotation = Quaternion.LookRotation(Vector3.Slerp(Motor.CharacterForward, _characterDirection, 1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized, Motor.CharacterUp);
        }
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (_input != Vector2.zero)
        {
            _inputDirection = Camera.TransformDirection(new Vector3(_input.x, 0, _input.y)).NormalizeXZ();
        }

        if (!IsControlSuspended)
        {
            _moveDirection = _inputDirection;
        }

        if (IsTranslationSuspended)
        {
            if (deltaTime > 0)
                currentVelocity = _rootMotionPositionDelta / deltaTime;
            else
                currentVelocity = Vector3.zero;
            return;
        }

        if (Motor.GroundingStatus.IsStableOnGround)
        {
            if (MoveByRootMotion)
            {
                currentVelocity = _rootMotionPositionDelta / deltaTime;
            }
            else
            {
                //SPEED
                var speedFactor = _input.magnitude * (IsSprinting ? 2.0f : 1.0f) * SpeedMultiplier;

                if (HasTarget)
                {
                    SpeedFactorForward = speedFactor * _inputAdopted.normalized.y;
                    SpeedFactorSideways = speedFactor * _inputAdopted.normalized.x;
                }
                else
                {
                    SpeedFactorForward = speedFactor;
                    SpeedFactorSideways = 0f;
                }

                //POSITION
                currentVelocity = Vector3.Lerp(currentVelocity, _moveDirection * speedFactor * MaxStableMoveSpeed, 1 - Mathf.Exp(-StableMovementSharpness * deltaTime));
            }
        }

        if (_internalVelocityAdd.sqrMagnitude > 0f)
        {
            if (_internalVelocityAdd.y >= 0f)
                Motor.ForceUnground();//unsticks the character when jumping

            currentVelocity += _internalVelocityAdd;
            _internalVelocityAdd = Vector3.zero;
        }

        if (!Motor.GroundingStatus.IsStableOnGround)
            currentVelocity += Gravity * deltaTime;
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        // Reset root motion deltas
        _rootMotionPositionDelta = Vector3.zero;
        _rootMotionRotationDelta = Quaternion.identity;
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        IsGrounded = Time.frameCount < 5 || Motor.GroundingStatus.IsStableOnGround;

        if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround)
        {
            //landing

            var delta = transform.position - _lastGrounded;
            if (delta.y < 0f)
                Fallen?.Invoke(-delta.y);
        }
        else if (!Motor.GroundingStatus.IsStableOnGround && Motor.LastGroundingStatus.IsStableOnGround)
        {
            //lifting off


        }

        if (Motor.GroundingStatus.IsStableOnGround)
            _lastGrounded = transform.position;
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        return !IsCollisionSuspended;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
    }
    #endregion
}
