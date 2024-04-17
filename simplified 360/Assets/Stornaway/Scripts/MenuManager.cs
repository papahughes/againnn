using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;


namespace Stornaway
{
    public class MenuManager : MonoBehaviour
    {
        [SerializeField, Tooltip("The scene to load when clicking play")]
        private string m_mainScene = "Example_Scene";

        public UnityEvent OnStartNoSaveDetected;
        public UnityEvent OnStartSaveFound;

        private void Awake()
        {
            SaveData saveData = SaveSystem.Load();
            if (saveData != null)
            {
                OnStartSaveFound.Invoke();
            }
            else
            {
                OnStartNoSaveDetected.Invoke();
            }
        }

        public void Play()
        {
            SceneManager.LoadScene(m_mainScene);
        }

        public void Restart()
        {
            SaveSystem.ClearSave();
            SceneManager.LoadScene(m_mainScene);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }
    }
}