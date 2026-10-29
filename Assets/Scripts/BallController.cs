using UnityEngine;

public class BallController : MonoBehaviour
{
    public float speed = 7f;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        LaunchBall();
    }

    public void LaunchBall()
    {
        Vector2 direction = new Vector2(Random.Range(-0.4f, 0.4f), -1f).normalized;
        rb.linearVelocity = direction * speed;
    }

    public void OnHitByPlayer(Vector2 playerPosition)
    {
        Vector2 hitDirection = ((Vector2)transform.position - playerPosition).normalized;
        rb.linearVelocity = hitDirection * speed;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        rb.linearVelocity = Vector2.Reflect(rb.linearVelocity, collision.contacts[0].normal).normalized * speed;
    }
}