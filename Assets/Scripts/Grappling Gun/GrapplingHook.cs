using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrapplingHook : MonoBehaviour
{
    [SerializeField] private float minGrappleLength;
    [SerializeField] private LayerMask grappleLayer;
    [SerializeField] private LineRenderer rope;
    [SerializeField] private float ropeShorteningPerFrame;
    [SerializeField]
    private InputActionAsset actionAsset;

    private Vector3 grapplePoint;
    [SerializeField]
    private DistanceJoint2D joint;

    private float currentGrappleLength;

    void Start()
    {
        joint.enabled = false;
        rope.enabled = false;
    }

    private void OnEnable()
    {
        var grappleAction = actionAsset.FindAction("Player/Grapple");
        grappleAction.started += OnDownActivateGrapple;
        grappleAction.canceled += OnUpDeactivateGrapple;
    }


    private void OnDownActivateGrapple(InputAction.CallbackContext context)
    {
        Vector3 screenPos = Mouse.current.position.ReadValue();
        screenPos.z = Mathf.Abs(Camera.main.transform.position.z);
        var mousePos = Camera.main.ScreenToWorldPoint(screenPos);

        RaycastHit2D hit = Physics2D.Raycast(
        origin: mousePos,
        direction: Vector2.zero,
        distance: Mathf.Infinity,
        layerMask: grappleLayer);

        if (hit.collider != null)
        {
            grapplePoint = hit.point;
            grapplePoint.z = 0;

            currentGrappleLength = Vector3.Distance(grapplePoint, transform.position);


            joint.connectedAnchor = grapplePoint;
            joint.enabled = true;
            rope.SetPosition(0, grapplePoint);
            rope.SetPosition(1, transform.position);
            rope.enabled = true;
        }
    }

    private void OnUpDeactivateGrapple(InputAction.CallbackContext context)
    {
        joint.enabled = false;
        rope.enabled = false;
        currentGrappleLength = minGrappleLength;
    }

    private void DecreaseGrappleLength()
    {
        if (!joint.enabled)
            return;

        currentGrappleLength -= ropeShorteningPerFrame * Time.deltaTime;
        joint.distance = Mathf.Max(minGrappleLength, currentGrappleLength);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        DecreaseGrappleLength();

        if (rope.enabled == true)
        {
            rope.SetPosition(1, transform.position);
        }
    }
}