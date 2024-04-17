using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Stornaway.Utility
{
    [System.Serializable]
    public class Button
    {
        [SerializeField] private UnityEngine.UI.Button m_button = null;
        [SerializeField] public UnityEngine.UI.Image m_buttonBackground = null;
        [SerializeField] private TextMeshProUGUI m_text = null;

        private Color m_buttonColour = Color.black;
        private Color m_textColour = Color.white;

        private bool m_link = false;
        private bool m_selected = false;

        private int m_choiceId = 0;


        public void ActivateButton(string _label, int _choiceId, Color _buttonColour, Color _textColour, bool _link = false)
        {
            m_choiceId = _choiceId;
            m_buttonColour = _buttonColour;
            m_textColour = _textColour;
            m_link = _link;

            Highlight(false);

            m_button.gameObject.SetActive(true);
            m_text.text = _label;
        }

        public void Highlight(bool _highlight = true)
        {
            if (_highlight)
            {
                m_buttonBackground.color = m_textColour;
                m_text.color = m_buttonColour;
            }
            else
            {
                m_buttonBackground.color = m_buttonColour;
                m_text.color = m_textColour;
            }
        }

        public void Reboot()
        {
            Highlight(false);
            Underline(false);
            m_button.interactable = true;
            m_button.gameObject.SetActive(false);
            Select(false);
        }

        public void SetInteractable(bool _interactable)
        {
            m_button.interactable = _interactable;
        }

        public void Select(bool _selected)
        {
            m_selected = _selected;
        }

        public void Hover(bool _hover)
        {
            if (m_link && !m_selected)
            {
                Underline(_hover);
            }
        }

        public int GetChoiceID()
        {
            return m_choiceId;
        }

        private void Underline(bool _underline)
        {
            if (_underline)
                m_text.fontStyle = FontStyles.Underline;
            else
                m_text.fontStyle = FontStyles.Normal;
        }

    }
}
