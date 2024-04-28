using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallingRockDanger : Danger
{
    [SerializeField]
    private float fallGravity;
    [SerializeField]
    private float succDeactivationDelay;

    [SerializeField]
    private CapsuleCollider2D succCol;
    [SerializeField]
    private Rigidbody2D rb;
    [SerializeField]
    private ParticleSystem pSystem;

    private bool isActive = true;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isActive)
        {
            DamageAndPushPlayer(collision);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        StartFallingOnPlayerContact(collision);
    }

    private void StartFallingOnPlayerContact(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("Player") || !isActive)
            return;

        rb.gravityScale = fallGravity;
        rb.WakeUp();
        pSystem.Play();
    }

    //Subscribe to UnityEvent on Deactivation object
    public void Deactivate()
    {
        isActive = false;
        pSystem.Stop();
        StartCoroutine(DeactivateAfterDelay());

        Debug.Log("Deactivating");
    }

    private IEnumerator DeactivateAfterDelay()
    {
        yield return new WaitForSeconds(succDeactivationDelay);
        succCol.enabled = false;
    }
}