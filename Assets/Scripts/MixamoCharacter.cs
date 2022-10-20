using AdventureCore;
using UnityEngine;
using UnityEngine.InputSystem;

public class MixamoCharacter : CharacterBaseTyped<CharacterActorBase,KinematicMovement,InventoryBase>
{
    private static int SPEED = Animator.StringToHash("Speed");
    private static int GROUNDED = Animator.StringToHash("Grounded");

    private float _speed;

    protected override void Start()
    {
        base.Start();
    }

    private void Update()
    {
        SetBool(GROUNDED, Movement.IsGrounded);
        Animator.SetFloat(SPEED, _speed, 0.2f, Time.deltaTime);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        var value = context.ReadValue<Vector2>();

        Movement.OnMove(value);

        _speed = value.magnitude;
    }
}
