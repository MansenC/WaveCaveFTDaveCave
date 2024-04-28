using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DaveController : MonoBehaviour
{
    public static DaveController Instance;

    [field: SerializeField]
    public bool canMove { get ; set; }
    [field: SerializeField]
    public bool canRotate { get ; set; }

    [SerializeField]
    private int maxHP;
    public int currentHP
    {
        get
        {
            return _currentHP;
        }
        set
        {
            _currentHP = value;
            BuildHPBar();
        }
    }
    private int _currentHP;

    [SerializeField]
    private Image hpPrefab;
    [SerializeField]
    private RectTransform hpParent;

    [SerializeField]
    private float pushForce;
    [SerializeField]
    private float pushDuration;
    [SerializeField]
    private float pushCooldown;

    [SerializeField]
    private InputActionAsset actionAsset;

    private Rigidbody2D rb;
    private Animator anim;

    private Vector2 mousePos;

    private Vector2 mouseDirection;
    private bool doPush;
    private bool canPush = true;
    private float currentPushDur;

    private bool currentsActive;
    private float currentsActiveDur;
    private float currentsActiveForce;
    private bool currentsAwayFromSource;
    private Vector3 currentsSourcePos;
    private Vector3 defaultScale;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this.gameObject);
            Debug.LogError("DaveController Singleton Error");
        }

        defaultScale = transform.localScale;

        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        anim.SetBool("pulledIn", false);
        currentHP = maxHP;
        BuildHPBar();
    }

    private void OnEnable()
    {
        actionAsset.Enable();
        var pushAction = actionAsset.FindAction("Player/Push");
        pushAction.started += OnButtonFlex;
        pushAction.canceled += OnButtonPush;

        CurrentsManager.currentsStarted += EnablePushByCurrents;
        CurrentsManager.sourceSpawned += SetCurrentsSource;

    }

    private void OnDisable()
    {
        actionAsset.Disable();

        CurrentsManager.currentsStarted -= EnablePushByCurrents;
        CurrentsManager.sourceSpawned -= SetCurrentsSource;
    }

    public void TakeDamage(int damage)
    {
        currentHP = Mathf.Max(0, currentHP - damage);

        Debug.Log("Took Damage, hp " + currentHP);

        if (currentHP <= 0)
        {
            canMove = false;
            canRotate = false;
            //show some kind of death screen
        }
    }

    /// <summary>
    /// overload of TakeDamage that also pushes the player 
    /// </summary>
    public void TakeDamage(int damage, float pushback, Vector3 pusherPos)
    {
        PushBack(pushback, pusherPos);

        TakeDamage(damage);
    }

    public void PushBack(float force, Vector3 pusherPos)
    {
        var pushDir = transform.position - pusherPos;
        pushDir.z = 0;
        rb.AddForce(pushDir.normalized * force, ForceMode2D.Impulse);
    }

    public void PushDirectionally(float targetX, float yOffset, float pushForce)
    {
        var target = new Vector3(targetX, transform.position.y + yOffset, 0);
        var dir = target - transform.position;
        dir.z = 0;
        rb.AddForce(dir.normalized * pushForce, ForceMode2D.Force);
    }

    public void Heal(int hp)
    {
        currentHP = Mathf.Min(maxHP, currentHP + hp);
    }

    private void BuildHPBar()
    {
        //destroy children. yes.
        for (int i = hpParent.childCount - 1; i >= 0; i--)
        {
            Destroy(hpParent.GetChild(i).gameObject);
        }

        for (int i = 0; i < currentHP; i++)
        {
            Instantiate(hpPrefab, hpParent);
        }
    }

    private void EnablePushByCurrents(bool awayFromSource, float force, float duration)
    {
        currentsActive = true;
        currentsAwayFromSource = awayFromSource;
        currentsActiveDur = duration;
        currentsActiveForce = force;
    }

    private void SetCurrentsSource(Vector3 pos)
    {
        currentsSourcePos = pos;
    }

    private void GetPushedByCurrents()
    {
        if (!currentsActive)
            return;

        currentsActiveDur -= Time.deltaTime;
        var dir = (currentsSourcePos - transform.position).normalized;

        var push = currentsAwayFromSource ? currentsActiveForce * -dir : currentsActiveForce * dir;
        rb.AddForce(push, ForceMode2D.Force);


        if (currentsActiveDur < 0)
            currentsActive = false;
    }

    //just to make the char pull the legs in
    private void OnButtonFlex(InputAction.CallbackContext context)
    {
        //pull legs in
        anim.SetBool("pulledIn", true);
    }

    private void OnButtonPush(InputAction.CallbackContext context)
    {
        //push legs out before this
        anim.SetBool("pulledIn", false);

        if (!canMove || !canPush)
            return;

        StartCoroutine(CountdownPushCooldown());

        doPush = true;
        currentPushDur = 0;
    }

    private void ApplyPush()
    {
        if (!doPush)
            return;

        if (currentPushDur > pushDuration)
        {
            doPush = false;
            return;
        }
            
        currentPushDur += Time.deltaTime;

        var force = mouseDirection * pushForce;
        rb.AddForce(force, ForceMode2D.Impulse);
    }

    private void RotateToMouse()
    {
        if (!canRotate)
            return;

        var noZMouse = new Vector2(mouseDirection.x, mouseDirection.y);

        float angle = Mathf.Atan2(noZMouse.y, noZMouse.x) * Mathf.Rad2Deg;
        //Debug.Log("Angle: " + angle);

        if (mousePos.x < transform.position.x)
        {
            transform.localScale = new Vector3(-defaultScale.x, defaultScale.y, defaultScale.z);
            //transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            //transform.Rotate(new Vector3(0, 0, 180));
            transform.right = -mouseDirection;
        }
        else
        {
            transform.localScale = defaultScale;
            //transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            transform.right = mouseDirection;
        }
    }

    private void FixedUpdate()
    {
        GetPushedByCurrents();

        if (!canMove)
            return;

        Vector3 screenPos = Mouse.current.position.ReadValue();
        screenPos.z = Mathf.Abs(Camera.main.transform.position.z);
        mousePos = Camera.main.ScreenToWorldPoint(screenPos);
        mouseDirection = (mousePos - new Vector2(transform.position.x, transform.position.y)).normalized;

        ApplyPush();
        RotateToMouse();
    }

    private IEnumerator CountdownPushCooldown()
    {
        canPush = false;

        yield return new WaitForSeconds(pushCooldown);
        canPush = true;
    }

}
