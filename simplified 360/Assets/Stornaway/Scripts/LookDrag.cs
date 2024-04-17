using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Stornaway
{
    public class LookDrag : MonoBehaviour
    {
        [SerializeField] private float m_speed = 2;
        private bool dragging = false;


        private void Start()
        {
            SequenceManager.s_instance.clickedAction.started += ctx => StartDrag();
            SequenceManager.s_instance.clickedAction.canceled += ctx => EndDrag();
        }

        private void Update()
        {
            if (dragging)
            {
                Vector2 delta = SequenceManager.s_instance.cursorDeltaAction.ReadValue<Vector2>();
                transform.eulerAngles += new Vector3(delta.y, -delta.x, 0) * m_speed;
            }
        }

        private void StartDrag()
        {
            dragging = true;
        }

        private void EndDrag()
        {
            dragging = false;
        }
    }
}