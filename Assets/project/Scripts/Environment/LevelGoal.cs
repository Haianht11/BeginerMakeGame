using System;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class LevelGoal : MonoBehaviour
{
    [SerializeField] private Color completedColor = new Color(0.37f, 1f, 0.65f);

    public event Action GoalReached;

    private SpriteRenderer spriteRenderer;
    private Color initialColor;
    private bool triggered;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialColor = spriteRenderer.color;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered)
            return;

        bool isPlayer = other.CompareTag("Player")
            || other.GetComponent<Player>() != null
            || (other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent<Player>(out _));

        if (!isPlayer)
            return;

        triggered = true;
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.color = completedColor;
        GoalReached?.Invoke();
    }

    public void ResetGoal()
    {
        triggered = false;
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.color = initialColor;
    }
}
