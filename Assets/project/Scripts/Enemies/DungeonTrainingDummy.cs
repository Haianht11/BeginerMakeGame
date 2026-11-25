using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class DungeonTrainingDummy : MonoBehaviour
{
    private static readonly int HurtState = Animator.StringToHash("Base Layer.Hurt");

    [SerializeField] private Player player;
    [SerializeField] private SpriteRenderer hitSpark;
    [SerializeField] private Sprite[] hitFrames;
    [SerializeField] private float hitFramesPerSecond = 30f;
    [SerializeField] private float reach = 1.3f;
    [SerializeField] private float verticalTolerance = 0.65f;

    private SpriteRenderer targetRenderer;
    private Animator animator;
    private Color idleColor;
    private Quaternion idleRotation;
    private float hitAge;
    private float hitDirection;

    private void Awake()
    {
        targetRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        idleColor = targetRenderer.color;
        idleRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        player.AttackContact += ReceiveAttack;
    }

    private void OnDisable()
    {
        player.AttackContact -= ReceiveAttack;
        StopReaction();
    }

    private void ReceiveAttack(Vector2 origin, float direction)
    {
        Vector2 offset = (Vector2)hitSpark.transform.position - origin;
        float distanceInFront = offset.x * direction;
        if (distanceInFront < 0f || distanceInFront > reach || Mathf.Abs(offset.y) > verticalTolerance)
            return;

        hitAge = 0f;
        hitDirection = direction;
        hitSpark.flipX = direction < 0f;
        hitSpark.sprite = hitFrames[0];
        hitSpark.enabled = true;
        targetRenderer.color = new Color(1f, 0.72f, 0.52f, idleColor.a);
        if (animator != null)
        {
            targetRenderer.flipX = direction > 0f;
            animator.Play(HurtState, 0, 0f);
        }
    }

    private void LateUpdate()
    {
        if (!hitSpark.enabled)
        {
            if (animator != null)
                targetRenderer.flipX = player.transform.position.x < transform.position.x;
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

    private void StopReaction()
    {
        transform.localRotation = idleRotation;
        targetRenderer.color = idleColor;
        hitSpark.enabled = false;
    }
}
