using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Stornaway
{ 
    public class AutoUINavigationSelect : MonoBehaviour
    {
        [SerializeField]
        private Transform buttons = null;

        private EventSystem eventSystem = null;
        private InputSystemUIInputModule inputModule = null;


        private void Awake()
        {
            eventSystem = GetComponent<EventSystem>();
            inputModule = GetComponent<InputSystemUIInputModule>();
        }


        void Update()
        {
            Vector2 navigation = inputModule.move.action.ReadValue<Vector2>();
            
            if (!eventSystem.currentSelectedGameObject)
            {
                if (Mathf.Abs(navigation.x) > 0.1f ||
                        Mathf.Abs(navigation.y) > 0.1f)
                {
                    for(int i = 0; i < buttons.childCount; i++)
                    {
                        if (buttons.GetChild(i).gameObject.activeSelf)
                        {
                            eventSystem.SetSelectedGameObject(buttons.GetChild(i).gameObject);
                            return;
                        }
                    } // i
                    
                }
            }
        }
    }
}