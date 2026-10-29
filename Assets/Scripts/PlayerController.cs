using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movimentação")]
    public float moveSpeed = 2.5f;

    [Header("Limites da Quadra (Sua Metade)")]
    public float minX = -0.617f;
    public float maxX = 0.617f;
    public float minY = -0.75f;
    public float maxY = -0.15f;

    [Header("Rebatida")]
    public KeyCode hitKey = KeyCode.Space;
    public Transform hitArea;
    public float hitRadius = 0.40f;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Vector2 movement;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (hitArea == null)
        {
            Transform foundArea = transform.Find("HitArea");
            if (foundArea != null) hitArea = foundArea;
        }
    }

    public void ApplyLevel(int level)
    {
        // Ajusta atributos do Player proporcionalmente a cada nível
        switch (Mathf.Clamp(level, 1, 5))
        {
            case 1: moveSpeed = 2.5f; hitRadius = 0.40f; break;
            case 2: moveSpeed = 2.8f; hitRadius = 0.42f; break;
            case 3: moveSpeed = 3.1f; hitRadius = 0.45f; break;
            case 4: moveSpeed = 3.4f; hitRadius = 0.48f; break;
            case 5: moveSpeed = 3.7f; hitRadius = 0.50f; break;
        }
    }

    void Update()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        if (animator != null)
        {
            bool isMoving = movement.sqrMagnitude > 0;
            animator.SetBool("IsRunning", isMoving);
            animator.SetFloat("MoveX", Mathf.Abs(movement.x));
            animator.SetFloat("MoveY", movement.y);
        }

        if (spriteRenderer != null && Mathf.Abs(movement.x) > 0.01f)
        {
            spriteRenderer.flipX = movement.x < 0f;
        }

        if (Input.GetKeyDown(hitKey))
        {
            HitBall();
        }
    }

    void FixedUpdate()
    {
        Vector2 targetPosition = rb.position + movement.normalized * moveSpeed * Time.fixedDeltaTime;

        targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);

        rb.MovePosition(targetPosition);
    }

    public void HitBall()
    {
        if (animator != null)
        {
            animator.SetTrigger("Hit");
        }

        if (hitArea != null)
        {
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(hitArea.position, hitRadius);
            foreach (var col in hitColliders)
            {
                if (col.CompareTag("Ball"))
                {
                    // Passa a posição e a direção de movimento para gerar efeito (Spin) na bola
                    col.SendMessage("OnHitByPlayer", new object[] { transform.position, movement }, SendMessageOptions.DontRequireReceiver);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (hitArea != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(hitArea.position, hitRadius);
        }
    }
}