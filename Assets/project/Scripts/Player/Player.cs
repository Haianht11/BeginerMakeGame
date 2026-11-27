using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class Player : MonoBehaviour
{
    public event Action<Vector2, float> AttackContact;
    public event Action<float, float> HealthChanged;
    public event Action Jumped;
    public event Action<int> AttackStarted;
    public event Action PlayerDefeated;

    public bool GameplayInputBlocked { get; set; }
    public bool SuppressAutoRespawn { get; set; }
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private static readonly int Run = Animator.StringToHash("run");
    private static readonly int VelocityY = Animator.StringToHash("velocityY");
    private static readonly int IsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int Facing = Animator.StringToHash("facing");
    private static readonly int IdleRightState = Animator.StringToHash("Base Layer.IdleRight");
    private static readonly int IdleLeftState = Animator.StringToHash("Base Layer.IdleLeft");
    private static readonly int WalkRightState = Animator.StringToHash("Base Layer.WalkRight");
    private static readonly int WalkLeftState = Animator.StringToHash("Base Layer.WalkLeft");
    private static readonly int JumpRightState = Animator.StringToHash("Base Layer.JumpRight");
    private static readonly int JumpLeftState = Animator.StringToHash("Base Layer.JumpLeft");
    private static readonly int AttackRightState = Animator.StringToHash("Base Layer.AttackRight");
    private static readonly int AttackLeftState = Animator.StringToHash("Base Layer.AttackLeft");

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private LayerMask groundLayers = Physics2D.DefaultRaycastLayers;
    [SerializeField] private float respawnHeight = -6f;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private HealthBar healthBar;
    private float currentHealth;
    private bool isRespawning;
    private Coroutine respawnCoroutine;

    private readonly List<ContactPoint2D> groundContacts = new List<ContactPoint2D>();
    private ContactFilter2D groundFilter;
    private Rigidbody2D body;
    private BoxCollider2D bodyCollider;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Vector2 spawnPosition;
    private float moveDirection;
    private float facingDirection = 1f;
    private bool isGrounded;
    private bool jumpRequested;
    private bool airJumpAvailable = true;
    private int comboStep;
    private int activeAttackStateHash;
    private bool attackBuffered;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        spawnPosition = body.position;
        body.freezeRotation = true;
        spriteRenderer.flipX = false;
        groundFilter.SetLayerMask(groundLayers);
        currentHealth = maxHealth;
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth, maxHealth);
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Update()
    {
        if (isRespawning)
            return;

        if (GameplayInputBlocked)
        {
            moveDirection = 0f;
            jumpRequested = false;
            attackBuffered = false;
            UpdateAnimationParameters();
            FinishAttackIfReady();
            return;
        }

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

        if (comboStep == 0 && moveDirection != 0f)
            facingDirection = moveDirection < 0f ? -1f : 1f;

        UpdateAnimationParameters();
        FinishAttackIfReady();

        Mouse mouse = Mouse.current;
        bool attackPressed = (mouse != null && mouse.leftButton.wasPressedThisFrame)
            || (keyboard != null && keyboard.zKey.wasPressedThisFrame);

        if (attackPressed)
        {
            if (comboStep == 0)
                PlayAttack(1);
            else
                attackBuffered = true;
        }
    }

    private void FixedUpdate()
    {
        if (isRespawning)
            return;

        if (body.position.y < respawnHeight)
        {
            StartRespawn();
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
                animator.Play(facingDirection < 0f ? JumpLeftState : JumpRightState, 0, 0f);
            Jumped?.Invoke();
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
        animator.SetFloat(Facing, facingDirection);
    }

    private void PlayAttack(int step)
    {
        if (moveDirection != 0f)
        {
            facingDirection = moveDirection < 0f ? -1f : 1f;
            animator.SetFloat(Facing, facingDirection);
        }

        comboStep = step;
        attackBuffered = false;
        activeAttackStateHash = facingDirection < 0f ? AttackLeftState : AttackRightState;
        animator.Play(activeAttackStateHash, 0, 0f);
        AttackStarted?.Invoke(step);
    }

    private void OnAttackContact()
    {
        AttackContact?.Invoke(bodyCollider.bounds.center, facingDirection);
    }

    private void FinishAttackIfReady()
    {
        if (comboStep == 0)
            return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash != activeAttackStateHash || state.normalizedTime < 1f)
            return;

        if (attackBuffered)
        {
            PlayAttack(1);
            return;
        }

        comboStep = 0;
        activeAttackStateHash = 0;
        if (moveDirection != 0f)
        {
            facingDirection = moveDirection < 0f ? -1f : 1f;
            animator.SetFloat(Facing, facingDirection);
        }

        bool facingLeft = facingDirection < 0f;
        int locomotionState = facingLeft ? JumpLeftState : JumpRightState;
        if (isGrounded)
            locomotionState = moveDirection == 0f
                ? (facingLeft ? IdleLeftState : IdleRightState)
                : (facingLeft ? WalkLeftState : WalkRightState);

        animator.Play(locomotionState, 0, 0f);
    }

    public void TakeDamage(float damage)
    {
        if (isRespawning)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        if (currentHealth <= 0f)
        {
            StartRespawn();
            return;
        }

        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth, maxHealth);
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void StartRespawn()
    {
        if (isRespawning)
            return;

        if (SuppressAutoRespawn)
        {
            isRespawning = true;
            currentHealth = 0f;
            if (healthBar != null)
                healthBar.UpdateHealth(0f, maxHealth);
            HealthChanged?.Invoke(0f, maxHealth);
            body.linearVelocity = Vector2.zero;
            moveDirection = 0f;
            jumpRequested = false;
            attackBuffered = false;
            UpdateAnimationParameters();
            PlayerDefeated?.Invoke();
            return;
        }

        if (respawnCoroutine != null)
            StopCoroutine(respawnCoroutine);
        respawnCoroutine = StartCoroutine(RespawnRoutine());
    }

    private System.Collections.IEnumerator RespawnRoutine()
    {
        isRespawning = true;
        currentHealth = 0f;
        if (healthBar != null)
            healthBar.UpdateHealth(0f, maxHealth);
        HealthChanged?.Invoke(0f, maxHealth);

        body.linearVelocity = Vector2.zero;
        PlayerDefeated?.Invoke();

        // Chờ 0.5 giây để người chơi thấy thanh máu về 0
        yield return new WaitForSeconds(0.5f);

        respawnCoroutine = null;
        RespawnNow();
    }

    public void RespawnNow()
    {
        if (respawnCoroutine != null)
        {
            StopCoroutine(respawnCoroutine);
            respawnCoroutine = null;
        }

        body.position = spawnPosition;
        body.linearVelocity = Vector2.zero;
        moveDirection = 0f;
        facingDirection = 1f;
        isGrounded = false;
        jumpRequested = false;
        airJumpAvailable = true;
        comboStep = 0;
        activeAttackStateHash = 0;
        attackBuffered = false;
        spriteRenderer.flipX = false;
        currentHealth = maxHealth;
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth, maxHealth);
        HealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateAnimationParameters();
        animator.Play(IdleRightState, 0, 0f);

        isRespawning = false;
    }
}
