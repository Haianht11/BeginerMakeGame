using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class DungeonTrainingDummy : MonoBehaviour
{
    private static readonly int HurtState = Animator.StringToHash("Base Layer.Hurt");

    public event Action<float, float> HealthChanged;
    public event Action DummyDefeated;

    public bool SuppressAutoReset { get; set; }
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    [SerializeField] private Player player;
    [SerializeField] private SpriteRenderer hitSpark;
    [SerializeField] private Sprite[] hitFrames;
    [SerializeField] private float hitFramesPerSecond = 30f;
    [SerializeField] private float reach = 1.3f;
    [SerializeField] private float verticalTolerance = 0.65f;
    [SerializeField] private float maxHealth = 140f;
    [SerializeField] private float damagePerHit = 28f;
    [SerializeField] private float autoRefillDelay = 4f;
    [SerializeField] private HealthBar healthBar;

    private SpriteRenderer targetRenderer;
    private Animator animator;
    private Color idleColor;
    private Quaternion idleRotation;
    private float currentHealth;
    private float hitAge;
    private float hitDirection;
    private Coroutine autoResetCoroutine;

    private void Awake()
    {
        targetRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        idleColor = targetRenderer.color;
        idleRotation = transform.localRotation;
        if (player == null)
            player = UnityEngine.Object.FindAnyObjectByType<Player>();
        currentHealth = maxHealth;
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth, maxHealth);
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void OnEnable()
    {
        if (player != null)
            player.AttackContact += ReceiveAttack;
    }

    private void OnDisable()
    {
        if (player != null)
            player.AttackContact -= ReceiveAttack;
        if (autoResetCoroutine != null)
        {
            StopCoroutine(autoResetCoroutine);
            autoResetCoroutine = null;
        }
        StopReaction();
    }

    private void ReceiveAttack(Vector2 origin, float direction)
    {
        Vector2 sparkPosition = hitSpark != null ? (Vector2)hitSpark.transform.position : (Vector2)transform.position;
        Vector2 offset = sparkPosition - origin;
        float distanceInFront = offset.x * direction;
        if (distanceInFront < 0f || distanceInFront > reach || Mathf.Abs(offset.y) > verticalTolerance)
            return;

        hitAge = 0f;
        hitDirection = direction;
        if (hitSpark != null && hitFrames != null && hitFrames.Length > 0)
        {
            hitSpark.flipX = direction < 0f;
            hitSpark.sprite = hitFrames[0];
            hitSpark.enabled = true;
        }
        targetRenderer.color = new Color(1f, 0.72f, 0.52f, idleColor.a);
        if (animator != null)
        {
            targetRenderer.flipX = direction > 0f;
            animator.Play(HurtState, 0, 0f);
        }

        if (currentHealth > 0f)
        {
            currentHealth = Mathf.Max(0f, currentHealth - damagePerHit);
            if (healthBar != null)
                healthBar.UpdateHealth(currentHealth, maxHealth);
            HealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                DummyDefeated?.Invoke();
                if (!SuppressAutoReset)
                {
                    if (autoResetCoroutine != null)
                        StopCoroutine(autoResetCoroutine);
                    autoResetCoroutine = StartCoroutine(AutoResetRoutine());
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (hitSpark == null || !hitSpark.enabled)
        {
            if (animator != null && player != null)
                targetRenderer.flipX = player.transform.position.x < transform.position.x;
            return;
        }

        if (hitFrames == null || hitFrames.Length == 0)
        {
            StopReaction();
            return;
        }

        int frame = Mathf.FloorToInt(hitAge * hitFramesPerSecond);
        if (frame >= hitFrames.Length)
        {
            StopReaction();
            return;
        }

        float remaining = Mathf.Clamp01(1f - hitAge * 5f);
        float tilt = -hitDirection * Mathf.Sin(hitAge * 27f) * 13f * remaining;
        transform.localRotation = idleRotation * Quaternion.Euler(0f, 0f, tilt);
        targetRenderer.color = Color.Lerp(new Color(1f, 0.72f, 0.52f, idleColor.a), idleColor, Mathf.Clamp01(hitAge * 12f));
        hitSpark.sprite = hitFrames[frame];
        hitAge += Time.deltaTime;
    }

    public void ResetDummy()
    {
        if (autoResetCoroutine != null)
        {
            StopCoroutine(autoResetCoroutine);
            autoResetCoroutine = null;
        }

        currentHealth = maxHealth;
        if (healthBar != null)
            healthBar.UpdateHealth(currentHealth, maxHealth);
        HealthChanged?.Invoke(currentHealth, maxHealth);
        StopReaction();
    }

    private IEnumerator AutoResetRoutine()
    {
        yield return new WaitForSeconds(autoRefillDelay);
        autoResetCoroutine = null;
        ResetDummy();
    }

    private void StopReaction()
    {
        transform.localRotation = idleRotation;
        if (targetRenderer != null)
            targetRenderer.color = idleColor;
        if (hitSpark != null)
            hitSpark.enabled = false;
    }
}
