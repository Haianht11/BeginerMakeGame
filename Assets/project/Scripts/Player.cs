using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 10f;

    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer spriteRenderer;
    public Animator animator;
    private bool isGrounded;
    private int jumpsRemaining;

    // --- Combo Attack ---
    private int comboStep = 0;        // đang ở đòn nào (0 = không đánh)
    private bool attackBuffered = false; // người chơi đã bấm trong lúc đang đánh chưa

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        // Giữ nhân vật không bị xoay tròn khi va chạm
        rb.freezeRotation = true;
        jumpsRemaining = 2; // tối đa 2 lần nhảy
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        // 1. Di chuyển trái / phải: A/Left = -1, D/Right = 1
        float moveDirection = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            moveDirection -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            moveDirection += 1f;

        // Cập nhật vận tốc di chuyển ngang
        rb.linearVelocity = new Vector2(moveDirection * moveSpeed, rb.linearVelocity.y);

        // 3. Tấn công
        if (keyboard.zKey.wasPressedThisFrame)
            HandleAttackInput();

        // Cập nhật animation theo tốc độ di chuyển
        animator.SetFloat("run", Mathf.Abs(moveDirection));
        animator.SetFloat("velocityY", rb.linearVelocity.y);
        animator.SetBool("isGrounded", isGrounded);

        // Lật mặt Sprite theo hướng di chuyển
        if (moveDirection < 0 && spriteRenderer != null)
            spriteRenderer.flipX = true;
        else if (moveDirection > 0 && spriteRenderer != null)
            spriteRenderer.flipX = false;

        // 2. Nhảy bằng phím SPACE (hoặc phím W / phím Mũi tên Lên)
        bool jumpPressed = keyboard.spaceKey.wasPressedThisFrame;
        if (jumpPressed && jumpsRemaining > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpsRemaining--;
            isGrounded = false;
        }
    }

    // Kiểm tra chạm đất kết hợp 2 cách để đảm bảo 100% nhận diện được sàn
    private bool CanJump()
    {
        if (isGrounded) return true;

        if (col != null)
        {
            // Quét nhẹ Collider xuống dưới 0.1 đơn vị (bỏ qua chính Collider của Player)
            RaycastHit2D[] hits = new RaycastHit2D[5];
            int hitCount = col.Cast(Vector2.down, hits, 0.1f);
            for (int i = 0; i < hitCount; i++)
            {
                if (hits[i].collider != null && hits[i].collider.gameObject != gameObject && !hits[i].collider.isTrigger)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // Nhận diện va chạm vật lý trực tiếp với mặt đất/bục sàn
    private void OnCollisionStay2D(Collision2D collision)
    {
        foreach (ContactPoint2D contact in collision.contacts)
        {
            // Vector pháp tuyến hướng lên trên (mặt đất ở dưới chân)
            if (contact.normal.y > 0.5f)
            {
                isGrounded = true;
                jumpsRemaining = 2; // hoàn trả lượt nhảy khi chạm đất
                return;
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        isGrounded = false;
    }

    // --- Combo Attack ---

    private void HandleAttackInput()
    {
        if (comboStep == 0)
        {
            // Chưa đánh → bắt đầu đòn 1
            comboStep = 1;
            animator.SetInteger("attackIndex", 1);
        }
        else
        {
            // Đang đánh → lưu buffer để đánh đòn tiếp theo
            attackBuffered = true;
        }
    }

    // Hàm này gọi bằng Animation Event ở cuối mỗi clip attack
    public void OnAttackEnd()
    {
        if (attackBuffered && comboStep < 3)
        {
            // Còn buffer và chưa đến đòn cuối → đánh tiếp
            comboStep++;
            attackBuffered = false;
            animator.SetInteger("attackIndex", comboStep);
        }
        else
        {
            // Hết combo hoặc không bấm tiếp → reset
            comboStep = 0;
            attackBuffered = false;
            animator.SetInteger("attackIndex", 0);
        }
    }
}
