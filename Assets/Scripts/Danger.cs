using System.Collections;
using UnityEngine;

public class Danger : MonoBehaviour
{
    [SerializeField]
    protected int damage = 1;
    [SerializeField]
    protected float pushback = 10;
    [SerializeField]
    protected float cooldown = 3;

    private Coroutine cooldownRoutine;
    private bool canDamage = true;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        DamageAndPushPlayer(collision);
    }

    protected void DamageAndPushPlayer(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;

        if (!canDamage)
        {
            DaveController.Instance.PushBack(pushback, transform.position);
            return;
        }

        DaveController.Instance.TakeDamage(damage, pushback, transform.position);

        if (cooldownRoutine != null)
            StopCoroutine(cooldownRoutine);
        cooldownRoutine = StartCoroutine(CountCooldown());
    }

    private IEnumerator CountCooldown()
    {
        canDamage = false;

        yield return new WaitForSeconds(cooldown);

        canDamage = true;
        cooldownRoutine = null;
    }
}
