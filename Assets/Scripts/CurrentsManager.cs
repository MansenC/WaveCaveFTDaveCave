using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CurrentsManager : MonoBehaviour
{
    public static event System.Action<bool, float, float> currentsStarted;
    public static event System.Action<Vector3> sourceSpawned;

    [SerializeField]
    private Transform source;

    [SerializeField]
    private float minPause, maxPause;
    [SerializeField]
    private float minActiveDur, maxActiveDur;
    [SerializeField]
    private float minForce, maxForce;

    private float currentPause;
    private float currentActiveDur;
    private float currentForce;

    private bool firstCycle;
    private bool awayFromSource = true;

    private Coroutine pauseRoutine;
    private Coroutine currentsRoutine;

    private void Start()
    {
        StartCycles(true);

        sourceSpawned.Invoke(source.transform.position);
    }

    public void StartCycles(bool startWithPause)
    {
        if (pauseRoutine != null || currentsRoutine != null)
        {
            Debug.LogWarning("Something tried to start the Currents Cycles when they were already started");
            return;
        }

        if (startWithPause)
        {
            Debug.Log("Started Cycles with a pause cycle");
            pauseRoutine = StartCoroutine(StartPauseCycle());
            return;
        }

        Debug.Log("Started Cycles with a currents cycle");
        currentsRoutine = StartCoroutine(StartCurrentsCycle());
    }

    private IEnumerator StartPauseCycle()
    {
        currentPause = Random.Range(minPause, maxPause);

        if (firstCycle)
        {
            currentPause /= 2;
            firstCycle = false;
        }
        Debug.Log("Pause Cycle is " + currentPause + " seconds");
        yield return new WaitForSeconds(currentPause);

        currentsRoutine = StartCoroutine(StartCurrentsCycle());
    }

    private IEnumerator StartCurrentsCycle()
    {
        currentForce = Random.Range(minForce, maxForce);
        currentActiveDur = Random.Range(minActiveDur, maxActiveDur);        

        if (firstCycle)
        {
            currentActiveDur /= 2;
            currentForce /= 2;
            firstCycle = false;
        }

        Debug.Log("Currents Cycle is " + currentActiveDur + " seconds with a force of " + currentForce);
        currentsStarted.Invoke(awayFromSource, currentForce, currentActiveDur);
        awayFromSource = !awayFromSource;


        yield return new WaitForSeconds(currentActiveDur);

        pauseRoutine = StartCoroutine(StartPauseCycle());
    }
}
