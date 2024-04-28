using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField]
    private Button startButton, quitButton;

    private void OnEnable()
    {
        quitButton.onClick.AddListener(() => Application.Quit());
        startButton.onClick.AddListener(OnButtonStartGame);
    }

    private void OnDisable()
    {
        quitButton.onClick.RemoveAllListeners();
        startButton.onClick.RemoveAllListeners();
    }

    private void OnButtonStartGame()
    {
        SceneManager.LoadScene(1);
    }

}
