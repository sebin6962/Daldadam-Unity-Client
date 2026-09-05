using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneTransitionInfo : MonoBehaviour
{
    public static SceneTransitionInfo Instance;

    public string fromScene;
    public string toScene;

    public string entranceID; 

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }


}
