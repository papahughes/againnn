using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;
using TMPro;

namespace Stornaway.QA
{
    public class QA : MonoBehaviour
    {
        [SerializeField]
        TextMeshProUGUI variablesText;

        [SerializeField]
        static bool debugEnabled = false;

        public static void SwitchQA()
        {
            if (Application.isEditor || Debug.isDebugBuild)
            {
                QA.debugEnabled = !debugEnabled;          
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (QA.debugEnabled)
            {               
                variablesText.text = SaveSystem.m_variablesNode.ToString();
            }
            else
            {
                variablesText.text = "";
            }
        }


    }
}