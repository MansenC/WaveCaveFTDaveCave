using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BruceTalksAtYou : MonoBehaviour
{
    public static bool isTalking;

    private static BruceTalksAtYou instance;

    [SerializeField]
    private ScrollRect scrollView;

    [SerializeField]
    private TMP_Text content;
    [SerializeField]
    private float pauseBetweenLetters = 0.06f;
    [SerializeField]
    private float delayBeforeHide = 5;
    [SerializeField]
    private string interruptionMessage = "Ah, nevermind that.";

    [SerializeField, Space(10)]
    private List<string> randomSentences = new List<string>();
    [SerializeField]
    private float timeIdleBeforeRandomMin = 7;
    [SerializeField]
    private float timeIdleBeforeRandomMax = 20;

    private Coroutine buildTextRoutine;
    private Coroutine interruptAndNewRoutine;
    private Coroutine idleRoutine;

    private void Awake()
    {
        instance = this;
        gameObject.SetActive(false);        
    }

    private void Start()
    {
        RestartIdleRoutine();
    }

    public static void TriggerMessage(string message)
    {
        if (isTalking)
        {
            instance.ShowNewMessage(message);

            return;
        }

        instance.ShowMessage(message);
    }

    private void ShowMessage(string text)
    {
        if (!DaveController.Instance.canMove)
            return;

        instance.gameObject.SetActive(true);

        if (buildTextRoutine != null)
            StopCoroutine(buildTextRoutine);
        buildTextRoutine = StartCoroutine(BuildMessage(text));
    }

    private void ShowNewMessage(string message)
    {
        if (!DaveController.Instance.canMove)
            return;

        instance.gameObject.SetActive(true);

        if (interruptAndNewRoutine != null)
            StopCoroutine(interruptAndNewRoutine);

        interruptAndNewRoutine = StartCoroutine(InterruptAndStartOver(interruptionMessage, message));
    }

    private IEnumerator BuildMessage(string text)
    {
        content.text = "";
        isTalking = true;
        var chars = text.ToCharArray();

        while (content.text.Length < text.Length)
        {
            content.text += chars[content.text.Length];
            yield return new WaitForSeconds(pauseBetweenLetters);

            scrollView.verticalNormalizedPosition = 0;
        }

        yield return new WaitForSeconds(delayBeforeHide);
        isTalking = false;
        gameObject.SetActive(false);

        RestartIdleRoutine();

        buildTextRoutine = null;
    }

    private IEnumerator InterruptAndStartOver(string interruption, string newMessage)
    {
        if (buildTextRoutine != null)
            StopCoroutine(buildTextRoutine);

        buildTextRoutine = null;

        yield return AddToCurrentMessage(interruption);

        yield return new WaitForSeconds(0.5f);

        ShowMessage(newMessage);

        interruptAndNewRoutine = null;
    }

    private IEnumerator AddToCurrentMessage(string addition)
    {
        isTalking = true;

        if (buildTextRoutine != null)
            StopCoroutine(buildTextRoutine);

        content.text += "-";
        yield return new WaitForSeconds(0.5f);
        content.text += "\n";

        var currentAdditional = "";
        var chars = addition.ToCharArray();

        while (currentAdditional.Length < addition.Length)
        {
            content.text += chars[currentAdditional.Length];
            currentAdditional += chars[currentAdditional.Length];
            yield return new WaitForSeconds(pauseBetweenLetters);
            scrollView.verticalNormalizedPosition = 0;
        }
    }

    private void RestartIdleRoutine()
    {
        if (idleRoutine != null)
            StopCoroutine(idleRoutine);

        idleRoutine = DaveController.Instance.StartCoroutine(RandomAfterIdle());
    }

    private IEnumerator RandomAfterIdle()
    {
        var wait = Random.Range(timeIdleBeforeRandomMin, timeIdleBeforeRandomMax);
        yield return new WaitForSeconds(wait);

        idleRoutine = null;
        DoRandomMessage();
    }

    private void DoRandomMessage()
    {
        int randomIndex = Random.Range(0, randomSentences.Count);

        ShowMessage(randomSentences[randomIndex]);
    }
}
