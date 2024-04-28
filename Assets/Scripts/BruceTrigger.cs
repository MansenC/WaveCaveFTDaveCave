using UnityEngine;

public class BruceTrigger : MonoBehaviour
{
    [SerializeField, TextArea]
    private string message;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;

        BruceTalksAtYou.TriggerMessage(message);
    }
}
