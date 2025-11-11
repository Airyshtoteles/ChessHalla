using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackRange = 1.5f;
    public int attackDamage = 35;
    public float attackRate = 2f;
    public LayerMask enemyLayers;

    private float nextAttackTime = 0f;
    private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (Time.time >= nextAttackTime)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Attack();
                nextAttackTime = Time.time + 1f / attackRate;
            }
        }
    }

    void Attack()
    {
        // Mainkan animasi attack (opsional)
        if (anim)
        {
            anim.ResetTrigger("Jalan");
            anim.SetTrigger("Attack");
        }

        // Cek musuh dalam jarak tertentu (tanpa attack point)
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, attackRange, enemyLayers);

        foreach (Collider2D enemy in hitEnemies)
        {
            EnemyHealth eh = enemy.GetComponent<EnemyHealth>();
            if (eh != null)
            {
                eh.TakeDamage(attackDamage);
            }
        }
    }

    // Menampilkan lingkaran range di Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
