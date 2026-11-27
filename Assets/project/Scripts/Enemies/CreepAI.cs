using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class CreepAI : MonoBehaviour, IDamageable
{
    [SerializeField] private Player player;
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float damagePerHit = 25f;
    [SerializeField] private float moveSpeed = 1.8f;
    [SerializeField] private float aggroRange = 8f;
    [SerializeField] private float attackRange = 1.4f;
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackHitRange = 1.8f;
    [SerializeField] private float hitVerticalTolerance = 1f;
    [SerializeField] private Vector2 healthBarOffset = new Vector2(0f, 1.45f);

    private static readonly int Running = Animator.StringToHash("isRunning");
    private static readonly int Attack = Animator.StringToHash("attack");
    private static readonly int Dead = Animator.StringToHash("isDead");
    private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
    private static readonly int RunState = Animator.StringToHash("Base Layer.Run");
    private static readonly int AttackState = Animator.StringToHash("Base Layer.Attack");
    private static readonly int HurtState = Animator.StringToHash("Base Layer.Hurt");
    private static readonly int DeathState = Animator.StringToHash("Base Layer.Death");

    private Rigidbody2D body;
    private Animator animator;
    private SpriteRenderer sprite;
    private SpriteRenderer healthBack;
    private SpriteRenderer healthFill;
    private float health;
    private float cooldown;
    private float attackFacing;
    private bool attacking;
    private bool attackAnimationStarted;
    private bool attackHit;
    private bool dead;
    private bool hurt;
    private float hurtTimer;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
        if (player == null) player = FindFirstObjectByType<Player>();
        health = maxHealth;
        CreateHealthBar();
        UpdateHealthBar();
    }

    private void Update()
    {
        if (dead) return;
        if (hurt)
        {
            hurtTimer -= Time.deltaTime;
            if (hurtTimer <= 0f)
            {
                hurt = false;
                animator.Play(BaseState, 0, 0f);
            }
            return;
        }

        if (cooldown > 0f) cooldown -= Time.deltaTime;
        if (attacking)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash == AttackState)
                attackAnimationStarted = true;
            else if (attackAnimationStarted && !animator.IsInTransition(0))
            {
                attacking = false;
                attackAnimationStarted = false;
            }
            return;
        }

        if (player == null) { StopMoving(); return; }
        Vector2 delta = (Vector2)player.transform.position - body.position;
        if (delta.sqrMagnitude > aggroRange * aggroRange) { StopMoving(); return; }
        float direction = Mathf.Sign(delta.x);
        sprite.flipX = direction < 0f;
        if (Mathf.Abs(delta.x) <= attackRange && Mathf.Abs(delta.y) <= hitVerticalTolerance)
        {
            StopMoving();
            if (cooldown <= 0f) BeginAttack(direction);
            return;
        }

        body.linearVelocity = new Vector2(direction * moveSpeed, body.linearVelocity.y);
        animator.SetBool(Running, true);
    }

    private int BaseState => Mathf.Abs(body.linearVelocity.x) > 0.05f ? RunState : IdleState;

    private void StopMoving()
    {
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        animator.SetBool(Running, false);
    }

    private void BeginAttack(float direction)
    {
        attacking = true;
        attackAnimationStarted = false;
        attackHit = false;
        attackFacing = direction;
        cooldown = attackCooldown;
        animator.SetTrigger(Attack);
    }

    public void OnAttackContact()
    {
        if (!attacking || attackHit) return;
        attackHit = true;
        HitPlayer();
    }

    private void HitPlayer()
    {
        if (player == null) return;
        Vector2 offset = (Vector2)player.transform.position - body.position;
        if (offset.x * attackFacing >= 0f && Mathf.Abs(offset.x) <= attackHitRange && Mathf.Abs(offset.y) <= hitVerticalTolerance)
            player.TakeDamage(attackDamage);
    }

    public void TakeHit(float damage, Vector2 hitDirection)
    {
        if (dead || damage <= 0f) return;
        healthBack.enabled = true;
        healthFill.enabled = true;
        health = Mathf.Max(0f, health - Mathf.Min(damage, damagePerHit));
        UpdateHealthBar();
        if (health <= 0f)
        {
            dead = true;
            attacking = false;
            attackAnimationStarted = false;
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
            animator.SetBool(Dead, true);
            animator.Play(DeathState, 0, 0f);
            Destroy(gameObject, 1.5f);
            return;
        }

        hurt = true;
        hurtTimer = 0.35f;
        attacking = false;
        attackAnimationStarted = false;
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        animator.ResetTrigger(Attack);
        sprite.flipX = hitDirection.x < 0f;
        animator.Play(HurtState, 0, 0f);
    }

    private void CreateHealthBar()
    {
        Sprite pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        healthBack = MakeBar("Health Bar Background", pixel, new Color(0.12f, 0.06f, 0.06f), 0.8f);
        healthFill = MakeBar("Health Bar Fill", pixel, new Color(0.8f, 0.12f, 0.12f), 0.68f);
        healthBack.enabled = false;
        healthFill.enabled = false;
    }

    private SpriteRenderer MakeBar(string name, Sprite pixel, Color color, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = healthBarOffset + new Vector2(0f, name.EndsWith("Fill") ? 0f : -0.035f);
        if (name.EndsWith("Fill")) go.transform.localPosition += new Vector3(-0.4f, 0f, 0f);
        go.transform.localScale = new Vector3(width, 0.09f, 1f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = pixel;
        renderer.color = color;
        renderer.sortingOrder = sprite.sortingOrder + 2;
        return renderer;
    }

    private void UpdateHealthBar()
    {
        float healthRatio = Mathf.Clamp01(health / maxHealth);
        if (healthFill != null)
        {
            healthFill.transform.localScale = new Vector3(0.8f * healthRatio, 0.09f, 1f);
            healthFill.transform.localPosition = healthBarOffset + new Vector2(-0.4f + 0.4f * (1f - healthRatio), 0f);
        }
    }
}
