using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class DungeonTrainingDummy : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private SpriteRenderer hitSpark;
    [SerializeField] private Sprite[] hitFrames;
    [SerializeField] private float hitFramesPerSecond = 30f;
    [SerializeField] private float reach = 1.3f;
    [SerializeField] private float verticalTolerance = 0.65f;

    private SpriteRenderer dummy;
    private Color idleColor;
    private Quaternion idleRotation;
    private float hitAge;
    private float hitDirection;

    private void Awake()
    {
        dummy = GetComponent<SpriteRenderer>();
        idleColor = dummy.color;
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
        dummy.color = new Color(1f, 0.72f, 0.52f, idleColor.a);
    }

    private void LateUpdate()
    {
        if (!hitSpark.enabled)
            return;

        int frame = Mathf.FloorToInt(hitAge * hitFramesPerSecond);
        if (frame >= hitFrames.Length)
        {
            StopReaction();
            return;
        }

        float remaining = Mathf.Clamp01(1f - hitAge * 5f);
        float tilt = -hitDirection * Mathf.Sin(hitAge * 27f) * 13f * remaining;
        transform.localRotation = idleRotation * Quaternion.Euler(0f, 0f, tilt);
        dummy.color = Color.Lerp(new Color(1f, 0.72f, 0.52f, idleColor.a), idleColor, Mathf.Clamp01(hitAge * 12f));
        hitSpark.sprite = hitFrames[frame];
        hitAge += Time.deltaTime;
    }

    private void StopReaction()
    {
        transform.localRotation = idleRotation;
        dummy.color = idleColor;
        hitSpark.enabled = false;
    }
}
