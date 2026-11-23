using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class Player : MonoBehaviour
{
    private static readonly int Run = Animator.StringToHash("run");
    private static readonly int VelocityY = Animator.StringToHash("velocityY");
    private static readonly int IsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int IdleState = Animator.StringToHash("Base Layer.Ide");
    private static readonly int RunState = Animator.StringToHash("Base Layer.run");
    private static readonly int JumpStartState = Animator.StringToHash("Base Layer.jump-start");
    private static readonly int JumpFallState = Animator.StringToHash("Base Layer.jump-fall");
    private static readonly int[] AttackStates =
    {
        Animator.StringToHash("Base Layer.attack1"),
        Animator.StringToHash("Base Layer.attack2"),
        Animator.StringToHash("Base Layer.attack3")
    };

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private LayerMask groundLayers = Physics2D.DefaultRaycastLayers;
    [SerializeField] private float respawnHeight = -6f;

    private readonly List<ContactPoint2D> groundContacts = new List<ContactPoint2D>();
    private ContactFilter2D groundFilter;
    private Rigidbody2D body;
    private BoxCollider2D bodyCollider;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Vector2 spawnPosition;
    private float moveDirection;
    private bool isGrounded;
    private bool jumpRequested;
    private bool airJumpAvailable = true;
    private int comboStep;
    private bool attackBuffered;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        spawnPosition = body.position;
        body.freezeRotation = true;
        groundFilter.SetLayerMask(groundLayers);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        moveDirection = 0f;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                moveDirection -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                moveDirection += 1f;

            jumpRequested |= keyboard.spaceKey.wasPressedThisFrame
                || keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame;
        }

        if (moveDirection != 0f)
            spriteRenderer.flipX = moveDirection < 0f;

        UpdateAnimationParameters();
        FinishAttackIfReady();

        if (keyboard != null && keyboard.zKey.wasPressedThisFrame)
        {
            if (comboStep == 0)
                PlayAttack(1);
            else if (comboStep < AttackStates.Length)
                attackBuffered = true;
        }
    }

    private void FixedUpdate()
    {
        if (body.position.y < respawnHeight)
        {
            Respawn();
            return;
        }

        Vector2 velocity = body.linearVelocity;
        isGrounded = velocity.y <= 0.01f && HasGroundContact();
        if (isGrounded)
            airJumpAvailable = true;

        if (jumpRequested && (isGrounded || airJumpAvailable))
        {
            if (!isGrounded)
                airJumpAvailable = false;

            velocity.y = jumpForce;
            isGrounded = false;
            if (comboStep == 0)
                animator.Play(JumpStartState, 0, 0f);
        }

        jumpRequested = false;
        velocity.x = moveDirection * moveSpeed;
        body.linearVelocity = velocity;
    }

    private bool HasGroundContact()
    {
        bodyCollider.GetContacts(groundFilter, groundContacts);
        foreach (ContactPoint2D contact in groundContacts)
        {
            if (contact.normal.y > 0.5f)
                return true;
        }

        return false;
    }

    private void UpdateAnimationParameters()
    {
        animator.SetFloat(Run, Mathf.Abs(moveDirection));
        animator.SetFloat(VelocityY, body.linearVelocity.y);
        animator.SetBool(IsGrounded, isGrounded);
    }

    private void PlayAttack(int step)
    {
        comboStep = step;
        attackBuffered = false;
        animator.Play(AttackStates[step - 1], 0, 0f);
    }

    private void FinishAttackIfReady()
    {
        if (comboStep == 0)
            return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash != AttackStates[comboStep - 1] || state.normalizedTime < 1f)
            return;

        if (attackBuffered)
        {
            PlayAttack(comboStep + 1);
            return;
        }

        comboStep = 0;
        int locomotionState = body.linearVelocity.y > 0f ? JumpStartState : JumpFallState;
        if (isGrounded)
            locomotionState = moveDirection == 0f ? IdleState : RunState;

        animator.Play(locomotionState, 0, 0f);
    }

    private void Respawn()
    {
        body.position = spawnPosition;
        body.linearVelocity = Vector2.zero;
        moveDirection = 0f;
        isGrounded = false;
        jumpRequested = false;
        airJumpAvailable = true;
        comboStep = 0;
        attackBuffered = false;
        spriteRenderer.flipX = false;
        UpdateAnimationParameters();
        animator.Play(IdleState, 0, 0f);
    }
}
