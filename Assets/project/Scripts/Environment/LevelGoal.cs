using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class LevelGoal : MonoBehaviour
{
    [SerializeField] private Color completedColor = new Color(0.37f, 1f, 0.65f);

    private void OnTriggerEnter2D(Collider2D other)
    {
        Rigidbody2D body = other.attachedRigidbody;
        if (body != null && body.TryGetComponent<Player>(out _))
            GetComponent<SpriteRenderer>().color = completedColor;
    }
}
