using UnityEngine;

public class ParticleSource : MonoBehaviour
{
    private bool active;

    private ParticleSystem pSystem;
    private bool playerNearby;

    private CircleCollider2D col;


    private void Awake()
    {
        pSystem = GetComponent<ParticleSystem>();
        col = GetComponent<CircleCollider2D>();
        ParticleSourceManager.AddToSources(this);
    }

    public void Activate(float speed)
    {
        active = true;

        if (Vector3.Distance(DaveController.Instance.gameObject.transform.position, gameObject.transform.position) < col.radius)
            playerNearby = true;
        else
            playerNearby = false;
        
        var main = pSystem.main;
        main.startSpeed = speed;

        if (playerNearby)
        {
            pSystem.Play();
        }
    }

    public void Deactivate()
    {
        active = false;
        pSystem.Stop();
    }

    public void FlowAwayFromEntrance()
    {
        var shape = pSystem.shape;
        shape.scale = new Vector3(shape.scale.x, -Mathf.Abs(shape.scale.y), shape.scale.z);
    }

    public void FlowTowardsEntrance()
    {
        var shape = pSystem.shape;
        shape.scale = new Vector3(shape.scale.x, Mathf.Abs(shape.scale.y), shape.scale.z);
    }

    public void RotateToEntrace(Vector3 pos)
    {
        //transform.right = pos.normalized;

        var dir = pos - transform.position;
        var angle = Mathf.Atan2(dir.y, dir.x) + Mathf.Rad2Deg;

        var shape = pSystem.shape;
        shape.rotation = new Vector3(0, 0, angle);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("Player") || !active)
        {
            return;
        }
        Debug.Log("Player nearby");
        pSystem.Play();
        playerNearby = true;
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
        {
            return;
        }

        pSystem.Stop();
        playerNearby = false;
    }
}
