using AdventureCore;
using KinematicCharacterController;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class KinematicMovement : MovementBase, ICharacterController
{
    public KinematicCharacterMotor Motor;
    [Tooltip("this transform is rotated in the direction of movement, usually the transform of the visual model")]
    public Transform Pivot;
    [Tooltip("transform used to transform input so when the player pushes left the character moves left relative to the camera")]
    public Transform Camera;

    [Header("Speed")]
    public float MaxStableMoveSpeed = 5;
    public float StableMovementSharpness = 15;
    public float OrientationSharpness = 10;
    public float JumpUpSpeed = 10f;
    [Header("Actions")]
    [Tooltip("optional action that will be performed when the direction suddenly changes")]
    public CharacterActionBase Turn;

    public Vector3 Gravity = new Vector3(0, -30f, 0);

    public float SpeedFactorForward { get; private set; }
    public float SpeedFactorSideways { get; private set; }
    public bool IsGrounded => Motor.GroundingStatus.IsStableOnGround;
    public bool IsSprinting { get; private set; }

    public event Action<float> Fallen;

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

    private void Start()
    {
        Motor.CharacterController = this;

        if (!Camera)
            Camera = UnityEngine.Camera.main.transform;
    }

    public void AddVelocity(Vector3 velocity)
    {
        _internalVelocityAdd += velocity;
    }

    #region Input
    public void OnMove(InputValue value) => OnMove(value.Get<Vector2>());
    public void OnMove(Vector2 value)
    {
        _input = value;

        if (_input == Vector2.zero)
            return;

        _inputAdopted = _input;
        _inputDirection = Camera.TransformDirection(new Vector3(_input.x, 0, _input.y)).NormalizeXZ();

    }

    public void OnSprint(InputValue value) => OnSprint(value.isPressed);
    public void OnSprint(bool value)
    {
        Debug.Log(value);
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

    public void OnJump(InputValue value) => OnJump(value.isPressed);
    public void OnJump(bool value)
    {
        if (!value)
            return;

        if (!Motor.GroundingStatus.IsStableOnGround)
            return;

        AddVelocity(Motor.CharacterUp * JumpUpSpeed);
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
                _characterDirection = (Target.position - Pivot.position).NormalizeXZ();
            else
                _characterDirection = _moveDirection;
        }

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
        }
        else
        {
            if (Motor.GroundingStatus.IsStableOnGround)
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
