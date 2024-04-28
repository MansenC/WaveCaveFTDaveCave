using UnityEngine;

public class SuccArea : MonoBehaviour
{
    [SerializeField]
    private float succForce;

    private void OnTriggerStay2D(Collider2D collision)
    {
        SuccPlayer(collision);
    }

    private void SuccPlayer(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;

        DaveController.Instance.PushDirectionally(transform.position.x, -2, succForce);
    }
}
