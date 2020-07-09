using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneManager : MonoBehaviour
{
    public void LoadMainScene()
    {
        SceneManager.LoadScene(1);
    }
}
