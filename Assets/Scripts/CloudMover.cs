using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloudMover : MonoBehaviour
{
    [SerializeField]
    private RectTransform[] clouds;
    [SerializeField]
    private float rightScreenEdgeCoordinate;
    [SerializeField]
    private float speed;

    private void Update()
    {
        MoveClouds();
    }

    private void MoveClouds()
    {
        foreach (var cloud in clouds)
        {
            if (cloud.anchoredPosition.x > rightScreenEdgeCoordinate)
            {                
                //for this to work, the clouds must be anchored to the left screen edge
                cloud.anchoredPosition = new Vector2(-cloud.rect.width, cloud.anchoredPosition.y);
                continue;
            }

            cloud.anchoredPosition = new Vector2(cloud.anchoredPosition.x + speed * Time.deltaTime, cloud.anchoredPosition.y);
        }
    }
}
