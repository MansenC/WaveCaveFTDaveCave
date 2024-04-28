using UnityEngine;
using UnityEngine.Events;

public class ObjDeactivationArea : MonoBehaviour
{
    [SerializeField]
    private UnityEvent OnEnvironmentTrigger;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player") 
            || collision.gameObject.CompareTag("FallingObj")
            || collision.gameObject.CompareTag("Particles"))
            return;

        OnEnvironmentTrigger.Invoke();
    }
}
