using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
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

    private Coroutine buildTextRoutine;
    private Coroutine interruptAndNewRoutine;

    private void Awake()
    {
        instance = this;
        gameObject.SetActive(false);
    }

    public static void TriggerMessage(string message)
    {
        instance.gameObject.SetActive(true);

        if (isTalking)
        {
            instance.ShowNewMessage(message);

            return;
        }

        instance.ShowMessage(message);
    }

    private void ShowMessage(string text)
    {
        if (buildTextRoutine != null)
            StopCoroutine(buildTextRoutine);
        buildTextRoutine = StartCoroutine(BuildMessage(text));
    }

    private void ShowNewMessage(string message)
    {
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
}
