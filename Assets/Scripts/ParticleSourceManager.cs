using NUnit.Framework.Constraints;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSourceManager : MonoBehaviour 
{
    private static List<ParticleSource> allSources = new List<ParticleSource>();

    private static ParticleSourceManager instance;

    private void Awake()
    {       
        instance = this;
    }

    private void OnEnable()
    {
        CurrentsManager.currentsStarted += ActivateSources;
        CurrentsManager.sourceSpawned += AlignSourcesToEntrance;
    }

    private void OnDisable()
    {
        CurrentsManager.currentsStarted -= ActivateSources;
    }

    public static void AddToSources(ParticleSource source)
    {
        allSources.Add(source);
    }

    public static void ActivateSources(bool awayFromEntrance, float speed, float duration)
    {
        foreach (ParticleSource source in allSources)
        {
            source.Activate(speed);

            if (awayFromEntrance)
                source.FlowAwayFromEntrance();
            else
                source.FlowTowardsEntrance();
        }

        instance.StartCoroutine(DeactivateSources(duration));
    }

    private static IEnumerator DeactivateSources(float afterSeconds)
    {
        yield return new WaitForSeconds(afterSeconds);

        foreach (ParticleSource source in allSources)
        {
            source.Deactivate();
        }
    }

    public static void AlignSourcesToEntrance(Vector3 entrancePos)
    {
        entrancePos.z = 0;

        foreach(ParticleSource source in allSources)
        {
            source.RotateToEntrace(entrancePos);
        }
    }
}
