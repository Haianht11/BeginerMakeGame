using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
public sealed class DoomKnightBoss : MonoBehaviour
{
    private enum BossState
    {
        Idle,
        Chase,
        Attack,
        Hurt,
        Dead
    }

    private static readonly int FacingParam = Animator.StringToHash("facing");
    private static readonly int IsWalkingParam = Animator.StringToHash("isWalking");
    private static readonly int IsDeadParam = Animator.StringToHash("isDead");

    private static readonly int IdleRightState = Animator.StringToHash("Base Layer.IdleRight");
    private static readonly int IdleLeftState = Animator.StringToHash("Base Layer.IdleLeft");
    private static readonly int WalkRightState = Animator.StringToHash("Base Layer.WalkRight");
    private static readonly int WalkLeftState = Animator.StringToHash("Base Layer.WalkLeft");
    private static readonly int AttackRightState = Animator.StringToHash("Base Layer.AttackRight");
    private static readonly int AttackLeftState = Animator.StringToHash("Base Layer.AttackLeft");
    private static readonly int HurtRightState = Animator.StringToHash("Base Layer.HurtRight");
    private static readonly int HurtLeftState = Animator.StringToHash("Base Layer.HurtLeft");
    private static readonly int DeathRightState = Animator.StringToHash("Base Layer.DeathRight");
    private static readonly int DeathLeftState = Animator.StringToHash("Base Layer.DeathLeft");

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private HealthBar healthBar;
    [SerializeField] private SpriteRenderer hitSpark;
    [SerializeField] private Sprite[] hitFrames;
    [SerializeField] private float hitFramesPerSecond = 30f;

    [Header("Combat Stats")]
    [SerializeField] private float maxHealth = 200f;
    [SerializeField] private float damageTakenPerHit = 25f;
    [SerializeField] private float attackDamage = 20f;
    [SerializeField] private int hitsPerStagger = 2;

    [Header("AI Movement & Range")]
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float aggroRange = 13f;
    [SerializeField] private float attackRange = 1.85f;
    [SerializeField] private float attackCooldown = 1.35f;
    [SerializeField] private float bossAttackReach = 2.35f;
    [SerializeField] private float bossAttackVerticalTolerance = 1.45f;
    [SerializeField] private float playerHitReach = 1.85f;
    [SerializeField] private float playerHitVerticalTolerance = 1.35f;
    [SerializeField] private Vector2 hitCenterOffset = new Vector2(0f, 0.8f);

    [Header("Arena Bounds & Respawn")]
    [SerializeField] private float minArenaX = -6.8f;
    [SerializeField] private float maxArenaX = 6.8f;
    [SerializeField] private float respawnDelay = 3.5f;

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Color baseColor;
    private Vector3 spawnPosition;
    private BossState state = BossState.Idle;
    private float currentHealth;
    private float facingDirection = -1f;
    private float cooldownTimer;
    private float hitSparkAge;
    private float flashTimer;
    private int currentStateHash;
    private int hitsSinceStagger;
    private Coroutine respawnCoroutine;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        baseColor = spriteRenderer.color;
        spawnPosition = transform.position;
        if (player == null)
            player = FindAnyObjectByType<Player>();

        spriteRenderer.flipX = false;
        currentHealth = maxHealth;
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth, maxHealth);
        if (hitSpark != null)
            hitSpark.enabled = false;
    }

    private void OnEnable()
    {
        if (player != null)
            player.AttackContact += ReceivePlayerAttack;
    }

    private void OnDisable()
    {
        if (player != null)
            player.AttackContact -= ReceivePlayerAttack;
        if (respawnCoroutine != null)
        {
            StopCoroutine(respawnCoroutine);
            respawnCoroutine = null;
        }
        ResetVisualEffects();
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        UpdateHitFeedback();

        if (state == BossState.Dead)
            return;

        if (state == BossState.Attack || state == BossState.Hurt)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.fullPathHash == currentStateHash && info.normalizedTime < 1f)
                return;

            EnterLocomotionState();
            return;
        }

        if (player == null)
        {
            SetState(BossState.Idle, facingDirection);
            return;
        }

        float dx = player.transform.position.x - transform.position.x;
        float absDx = Mathf.Abs(dx);
        if (absDx > 0.05f)
            facingDirection = dx < 0f ? -1f : 1f;

        if (absDx <= attackRange && cooldownTimer <= 0f)
        {
            StartAttack();
            return;
        }

        if (absDx > attackRange && absDx <= aggroRange)
        {
            SetState(BossState.Chase, facingDirection);
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x + facingDirection * moveSpeed * Time.deltaTime, minArenaX, maxArenaX);
            transform.position = pos;
        }
        else
        {
            SetState(BossState.Idle, facingDirection);
        }
    }

    private void LateUpdate()
    {
        // Strict asymmetric invariant: never mirror Doom Knight horizontally.
        spriteRenderer.flipX = false;
    }

    private void StartAttack()
    {
        cooldownTimer = attackCooldown;
        state = BossState.Attack;
        int hash = facingDirection < 0f ? AttackLeftState : AttackRightState;
        PlayState(hash, false, false);
    }

    // Invoked by AnimationEvent on DoomKnight_AttackRight / DoomKnight_AttackLeft.
    private void OnBossAttackContact()
    {
        if (state != BossState.Attack || player == null)
            return;

        Vector3 playerPos = player.transform.position;
        float distanceInFront = (playerPos.x - transform.position.x) * facingDirection;
        float verticalDelta = Mathf.Abs(playerPos.y - (transform.position.y + 0.75f));
        if (distanceInFront >= -0.2f && distanceInFront <= bossAttackReach && verticalDelta <= bossAttackVerticalTolerance)
            player.TakeDamage(attackDamage);
    }

    private void ReceivePlayerAttack(Vector2 origin, float attackDirection)
    {
        if (state == BossState.Dead)
            return;

        Vector2 hitCenter = (Vector2)transform.position + hitCenterOffset;
        Vector2 offset = hitCenter - origin;
        float distanceInFront = offset.x * attackDirection;
        if (distanceInFront < -0.3f || distanceInFront > playerHitReach || Mathf.Abs(offset.y) > playerHitVerticalTolerance)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damageTakenPerHit);
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth, maxHealth);

        TriggerHitSpark(attackDirection);

        if (currentHealth <= 0f)
        {
            EnterDeathState();
            return;
        }

        hitsSinceStagger++;
        if (state != BossState.Attack || hitsSinceStagger >= hitsPerStagger)
        {
            hitsSinceStagger = 0;
            state = BossState.Hurt;
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x + attackDirection * 0.16f, minArenaX, maxArenaX);
            transform.position = pos;
            int hash = facingDirection < 0f ? HurtLeftState : HurtRightState;
            PlayState(hash, false, false);
        }
    }

    private void EnterLocomotionState()
    {
        if (player != null)
        {
            float dx = player.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.05f)
                facingDirection = dx < 0f ? -1f : 1f;

            if (Mathf.Abs(dx) <= attackRange && cooldownTimer <= 0f)
            {
                StartAttack();
                return;
            }
        }

        SetState(BossState.Idle, facingDirection);
    }

    private void SetState(BossState nextState, float dir)
    {
        int hash = nextState == BossState.Chase
            ? (dir < 0f ? WalkLeftState : WalkRightState)
            : (dir < 0f ? IdleLeftState : IdleRightState);

        if (state == nextState && currentStateHash == hash)
        {
            animator.SetFloat(FacingParam, dir);
            return;
        }

        state = nextState;
        PlayState(hash, nextState == BossState.Chase, false);
    }

    private void EnterDeathState()
    {
        state = BossState.Dead;
        hitsSinceStagger = 0;
        int hash = facingDirection < 0f ? DeathLeftState : DeathRightState;
        PlayState(hash, false, true);
        if (respawnCoroutine != null)
            StopCoroutine(respawnCoroutine);
        respawnCoroutine = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        transform.position = spawnPosition;
        facingDirection = -1f;
        currentHealth = maxHealth;
        cooldownTimer = 0.6f;
        hitsSinceStagger = 0;
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth, maxHealth);
        ResetVisualEffects();
        state = BossState.Idle;
        PlayState(IdleLeftState, false, false);
        respawnCoroutine = null;
    }

    private void PlayState(int stateHash, bool isWalking, bool isDead)
    {
        currentStateHash = stateHash;
        animator.SetFloat(FacingParam, facingDirection);
        animator.SetBool(IsWalkingParam, isWalking);
        animator.SetBool(IsDeadParam, isDead);
        animator.Play(stateHash, 0, 0f);
    }

    private void TriggerHitSpark(float attackDirection)
    {
        flashTimer = 0.12f;
        spriteRenderer.color = new Color(1f, 0.58f, 0.5f, baseColor.a);
        if (hitSpark != null && hitFrames != null && hitFrames.Length > 0)
        {
            hitSparkAge = 0f;
            hitSpark.flipX = attackDirection < 0f;
            hitSpark.sprite = hitFrames[0];
            hitSpark.enabled = true;
        }
    }

    private void UpdateHitFeedback()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(flashTimer / 0.12f);
            spriteRenderer.color = Color.Lerp(new Color(1f, 0.58f, 0.5f, baseColor.a), baseColor, t);
        }
        else if (spriteRenderer.color != baseColor)
        {
            spriteRenderer.color = baseColor;
        }

        if (hitSpark != null && hitSpark.enabled && hitFrames != null && hitFrames.Length > 0)
        {
            int frame = Mathf.FloorToInt(hitSparkAge * hitFramesPerSecond);
            if (frame >= hitFrames.Length)
            {
                hitSpark.enabled = false;
            }
            else
            {
                hitSpark.sprite = hitFrames[frame];
                hitSparkAge += Time.deltaTime;
            }
        }
    }

    private void ResetVisualEffects()
    {
        flashTimer = 0f;
        if (spriteRenderer != null)
            spriteRenderer.color = baseColor;
        if (hitSpark != null)
            hitSpark.enabled = false;
    }
}
