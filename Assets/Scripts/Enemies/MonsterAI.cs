using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(CharacterStats))]
public class MonsterAI : MonoBehaviour
{
    [SerializeField] private TriggerZone watchZone;
    [SerializeField] private Animator animator;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float turnSpeed = 8f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float hitDelay = 0.4f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float corpseDelay = 5f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

    private CharacterController controller;
    private CharacterStats stats;
    private CharacterStats target;
    private Vector3 home;
    private float verticalVelocity;
    private float nextAttackTime;
    private bool attacking;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        stats = GetComponent<CharacterStats>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator) animator.applyRootMotion = false;
        home = transform.position;
    }

    private void OnEnable()
    {
        if (watchZone)
        {
            watchZone.Entered += OnTargetEntered;
            watchZone.Exited += OnTargetExited;
        }
        stats.Damaged += OnDamaged;
        stats.Died += OnDied;
    }

    private void OnDisable()
    {
        if (watchZone)
        {
            watchZone.Entered -= OnTargetEntered;
            watchZone.Exited -= OnTargetExited;
        }
        stats.Damaged -= OnDamaged;
        stats.Died -= OnDied;
    }

    private void OnTargetEntered(Transform t) => target = t.GetComponentInParent<CharacterStats>();
    private void OnTargetExited(Transform t) => target = null;

    private void Update()
    {
        if (stats.IsDead) return;

        Vector3 horizontal = Vector3.zero;
        bool hasTarget = target && !target.IsDead;

        if (hasTarget && !attacking)
        {
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            Face(toTarget);

            if (toTarget.magnitude > attackRange) horizontal = toTarget.normalized * moveSpeed;
            else if (Time.time >= nextAttackTime) StartCoroutine(Attack());
        }
        else if (!hasTarget)
        {
            Vector3 toHome = home - transform.position;
            toHome.y = 0f;
            if (toHome.magnitude > 0.5f)
            {
                Face(toHome);
                horizontal = toHome.normalized * moveSpeed * 0.6f;
            }
        }

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;
        controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);

        animator.TrySetFloat(SpeedHash, horizontal.magnitude);
    }

    private void Face(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.001f) return;
        Quaternion look = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
    }

    private IEnumerator Attack()
    {
        attacking = true;
        nextAttackTime = Time.time + attackCooldown;
        animator.TrySetTrigger(AttackHash);
        yield return new WaitForSeconds(hitDelay);

        if (!stats.IsDead && target && !target.IsDead)
        {
            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist <= attackRange + 0.5f) target.TakeDamage(stats.AttackPower);
        }

        yield return new WaitForSeconds(0.3f);
        attacking = false;
    }

    private void OnDamaged() => animator.TrySetTrigger(HitHash);

    private void OnDied()
    {
        StopAllCoroutines();
        animator.TrySetBool(DeadHash, true);
        controller.enabled = false;
        if (watchZone) watchZone.gameObject.SetActive(false);
        Destroy(gameObject, corpseDelay);
    }
}
