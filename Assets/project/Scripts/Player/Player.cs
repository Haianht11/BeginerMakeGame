using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public interface IDamageable
{
    void TakeHit(float damage, Vector2 hitDirection);
}

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class Player : MonoBehaviour
{
    public event Action<Vector2, float> AttackContact;

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
    private static readonly int HurtState = Animator.StringToHash("Base Layer.Hurt");
    private static readonly int DeadState = Animator.StringToHash("Base Layer.Dead");

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private LayerMask groundLayers = Physics2D.DefaultRaycastLayers;
    [SerializeField] private float respawnHeight = -6f;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private HealthBar healthBar;

    [Header("Attack Hitbox")]
    [SerializeField] private Vector2 attackHitboxSize = new Vector2(1.5f, 1.25f);
    [SerializeField] private Vector2 attackHitboxOffset = new Vector2(0.9f, 0.15f);
    [SerializeField] private float attackDamage = 25f;
    private float currentHealth;
    private bool isRespawning;
    private bool isDead;
    private float hurtTimer;

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
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        spawnPosition = body.position;
        body.freezeRotation = true;
        groundFilter.SetLayerMask(groundLayers);
        currentHealth = maxHealth;
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth, maxHealth);
    }

    private void Update()
    {
        if (isRespawning || isDead)
            return;

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
        if (hurtTimer > 0f)
        {
            hurtTimer -= Time.deltaTime;
            if (hurtTimer <= 0f && animator.GetCurrentAnimatorStateInfo(0).fullPathHash == HurtState)
                PlayLocomotionState();
        }
        FinishAttackIfReady();

        Mouse mouse = Mouse.current;
        bool attackPressed = mouse != null && mouse.leftButton.wasPressedThisFrame;

        if (attackPressed)
        {
            if (comboStep == 0)
                PlayAttack(1);
            else if (comboStep < AttackStates.Length)
                attackBuffered = true;
        }
    }

    private void FixedUpdate()
    {
        if (isRespawning || isDead)
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
        hitTargets.Clear();
        animator.Play(AttackStates[step - 1], 0, 0f);
    }

    private void OnAttackContact()
    {
        float direction = spriteRenderer.flipX ? -1f : 1f;
        Vector2 center = bodyCollider.bounds.center
            + new Vector3(attackHitboxOffset.x * direction, attackHitboxOffset.y);

        foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, attackHitboxSize, 0f))
        {
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target != null && hitTargets.Add(target))
                target.TakeHit(attackDamage, new Vector2(direction, 0f));
        }

        AttackContact?.Invoke(bodyCollider.bounds.center, direction);
    }

    private void OnDrawGizmosSelected()
    {
        float direction = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
        Vector2 center = Application.isPlaying && bodyCollider != null
            ? bodyCollider.bounds.center
            : (Vector2)transform.position;
        center += new Vector2(attackHitboxOffset.x * direction, attackHitboxOffset.y);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(center, attackHitboxSize);
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
        PlayLocomotionState();
    }

    private void PlayLocomotionState()
    {
        int locomotionState = body.linearVelocity.y > 0f ? JumpStartState : JumpFallState;
        if (isGrounded)
            locomotionState = moveDirection == 0f ? IdleState : RunState;
        animator.Play(locomotionState, 0, 0f);
    }

    public void TakeDamage(float damage)
    {
        if (isRespawning)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            StartRespawn(true);
            return;
        }

        // Play Hurt animation (no loop — Hurt.anim loopTime = 0)
        comboStep = 0;
        attackBuffered = false;
        hurtTimer = 0.35f;
        animator.Play(HurtState, 0, 0f);
    }

    private void StartRespawn(bool playDeathAnimation = false)
    {
        if (isRespawning)
            return;

        StartCoroutine(RespawnRoutine(playDeathAnimation));
    }

    private System.Collections.IEnumerator RespawnRoutine(bool playDeathAnimation)
    {
        isRespawning = true;
        isDead = playDeathAnimation;
        currentHealth = 0f;
        if (healthBar != null)
            healthBar.UpdateHealth(0f, maxHealth);

        body.linearVelocity = Vector2.zero;

        if (playDeathAnimation)
            animator.Play(DeadState, 0, 0f);

        // Chờ 0.5 giây để người chơi thấy thanh máu về 0
        yield return new WaitForSeconds(0.5f);

        body.position = spawnPosition;
        body.linearVelocity = Vector2.zero;
        moveDirection = 0f;
        isGrounded = false;
        jumpRequested = false;
        airJumpAvailable = true;
        comboStep = 0;
        attackBuffered = false;
        spriteRenderer.flipX = false;
        currentHealth = maxHealth;
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth, maxHealth);
        UpdateAnimationParameters();
        animator.Play(IdleState, 0, 0f);

        isRespawning = false;
        isDead = false;
    }
}
