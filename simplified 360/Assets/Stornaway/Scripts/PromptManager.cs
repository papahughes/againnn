using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;
using System.Collections;
using Stornaway.Utility;
using System.Text.Json.Nodes;
using Json.Logic.Rules;
using Json.Logic;
using System.Text.Json;
using UnityEngine.U2D;
using System.Reflection;
using UnityEngine.EventSystems;

namespace Stornaway
{
    public class PromptManager : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image m_timer = null;
        [SerializeField] private UnityEngine.UI.Image m_timerBackground = null;
        [SerializeField] private CanvasGroup m_buttonGroup = null;
        [SerializeField] private List<Button> m_buttons = new List<Button>();
        [SerializeField] private CanvasGroup m_3dButtonGroup = null;
        [SerializeField] private TextMeshProUGUI m_questionText = null;
        [SerializeField] private Texture2D m_handIcon = null;
        [SerializeField] private TextAsset m_stylesJson = null;
        [SerializeField] private Sprite m_roundedSprite = null;
        [SerializeField] private Sprite m_rectangularSprite = null;
        [SerializeField] private GameObject m_questionHolder = null;
        [SerializeField] private GameObject m_timerGO = null;
        [Header("Default Video")]
        [SerializeField] private TextMeshProUGUI m_islandNameText = null;                                                                                       
        [SerializeField] private TextMeshProUGUI m_nameText = null;
        [SerializeField] private TextMeshProUGUI m_summaryText = null;
        private int m_styleNum = 0;
        private string m_360CustomStyleName = "custom_360_button_positions";

        private Variant c_currentVariant = null;
        private GenericStyle c_readJson = null;
        private Component c_newLayout = null;
        private SubStyle c_neededStyle = null;
        private List<string> c_locations = new List<string>();

        #region UNITY CALLBACKS
        private void Awake()
        {
            DeactivateButtons();
        }
        #endregion


        #region PUBLIC METHODS
        public void SelectButton(int _index)
        {
            int choiceId = m_buttons[_index].GetChoiceID();
            m_buttons[_index].Select(true);


            string buttonImageName = c_currentVariant.choices[choiceId].image_active_url;
            Sprite sprite = null;

            if (buttonImageName.Length > 0)
            {
                int startIndex = buttonImageName.LastIndexOf("/") + 1;
                buttonImageName = buttonImageName.Substring(startIndex, buttonImageName.LastIndexOf(".") - startIndex);
                sprite = Resources.Load<Sprite>("Images/Buttons/" + buttonImageName);
            }

            // Set active image
            if (sprite)
            {
                if (c_currentVariant.prompt.style == m_360CustomStyleName)
                    StartCoroutine(WaitForButtonImage3D(choiceId, sprite));
                else
                    StartCoroutine(WaitForButtonImage(choiceId, sprite));
            }

            // Go to next variant if there is an id
            LinkedVariant linkedVariant = SequenceManager.s_instance.GetCorrectLinkedVariant(SequenceManager.s_instance.m_currentVariant.choices[choiceId]);
            string id = linkedVariant.id;
            if (!string.IsNullOrEmpty(id))
            {
                SequenceManager.s_instance.m_nextVariant = SequenceManager.s_instance.m_root.GetVariant(id);
                m_buttons[_index].Highlight(true);

                if (SequenceManager.s_instance.m_currentVariant.choices[choiceId].cut_on_click)
                    SequenceManager.s_instance.SetActiveMedia(SequenceManager.s_instance.m_nextVariant, true);
                else
                {
                    for (int i = 0; i < m_buttons.Count; i++)
                        m_buttons[i].SetInteractable(false);
                }
            }

            // Open link if the choice has a url
            string url = linkedVariant.external_url;
            if (!string.IsNullOrEmpty(url))
                Application.OpenURL(url);

            EventSystem.current.SetSelectedGameObject(null);
        }

        public void InitialisePrompt(Variant _variant)
        {
            c_currentVariant = _variant;

            DeactivateButtons();
            HideQuestion();

            Color backgroundColour = new Color(0, 0, 0, 0.8f);
            Color textColour = Color.white;
            SetColourScheme(_variant, out backgroundColour, out textColour);

            if (_variant.prompt.hotspot)
                ShowButtons(false);
            else
                ShowButtons(true);

            string wantedStyle = _variant.prompt.style + "_" + _variant.prompt.substyle;
            SetUpNewLayout(transform.gameObject, wantedStyle, true);
            SetUpNewLayout(m_buttonGroup.gameObject, wantedStyle, false);

            if (_variant.prompt.style == "split_quadrant")
                MakeButtonsVisibleAndHitTestable(false);
            else
                MakeButtonsVisibleAndHitTestable(true);

            InitialiseChoices(_variant, backgroundColour, textColour);
            
            SetQuestionColours(backgroundColour, textColour);
            SetTimerColours(backgroundColour, textColour, _variant.prompt.timer.invert_colors);
        }

        public void ActivateButtons(bool _b)
        {
            if(SequenceManager.s_instance != null) 
                c_currentVariant = SequenceManager.s_instance.m_currentVariant;

            if (c_currentVariant != null && ShouldUseCustomPosition(c_currentVariant))
            {
                m_3dButtonGroup.gameObject.SetActive(_b);
                m_buttonGroup.gameObject.SetActive(false);

                // Reposition 3D buttons
                for (int i = 0; i < m_3dButtonGroup.transform.childCount; i++)
                {
                    if (i >= c_currentVariant.choices.Count)
                    {
                        m_3dButtonGroup.transform.GetChild(i).gameObject.SetActive(false);
                    }
                    else
                    {
                        m_3dButtonGroup.transform.GetChild(i).gameObject.SetActive(true);
                        
                        // Set position
                        m_3dButtonGroup.transform.GetChild(i).localPosition = new Vector3(
                            c_currentVariant.choices[i].position_x,
                            c_currentVariant.choices[i].position_y,
                            c_currentVariant.choices[i].position_z);
                    }
                }
                
            }
            else
            {
                m_buttonGroup.gameObject.SetActive(_b);
                m_3dButtonGroup.gameObject.SetActive(false);
            }
        }

        public void DeactivateButtons()
        {
            for (int i = 0; i < m_buttons.Count; i++)
            {
                m_buttons[i].Reboot();
                m_3dButtonGroup.transform.GetChild(i).gameObject.SetActive(false);
            } // i
            
            ActivateButtons(false);
            SetCursorToPointer(0);
        }

        public void UpdatePrompt(Variant _variant, double _currentTime, double _length, double _displayAt)
        {
            if (_currentTime >= _length - _displayAt)
            {
                if (!m_timerBackground.gameObject.activeSelf)
                {
                    if(!m_buttonGroup.gameObject.activeSelf && !m_3dButtonGroup.gameObject.activeSelf)
                        HandleButtonConditions();

                    ActivateButtons(true);
                    ShowTimer(_variant.prompt.timer.show, _variant.prompt.timer.transparent_background);
                    UpdateQuestion(_variant);
                }

                float fill = 0.99f - (float)((_currentTime - (_length - _displayAt)) / _displayAt);
                SetTimerFill(fill);
            }
            else
            {
                if (m_timerBackground.gameObject.activeSelf)
                {
                    ActivateButtons(false);
                    ShowTimer(false);
                    HideQuestion();
                }
            }
        }

        public void SetCursorToPointer(int _index)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

            if (_index >= 0 && _index < m_buttons.Count)
                m_buttons[_index].Hover(false);
        }

        public void SetCursorToHand(int _index)
        {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
            Cursor.SetCursor(m_handIcon, new Vector2(6, 0), CursorMode.Auto);
#endif
            if (_index >= 0 && _index < m_buttons.Count)
                m_buttons[_index].Hover(true);
        }

        public void ShowButtons(bool _show)
        {
            if (_show)
                m_buttonGroup.alpha = 1f;
            else
                m_buttonGroup.alpha = 0f;
        }

        public void ShowTimer(bool _show, bool _transparentBackground = false)
        {
            if (_show)
                m_timerBackground.gameObject.SetActive(true);
            else
                m_timerBackground.gameObject.SetActive(false);

            if (_transparentBackground)
                m_timerBackground.color = new Color(0, 0, 0, 0);
        }

        public void UpdateQuestion(Variant _variant)
        {
            string text = _variant.prompt.text;

            if (string.IsNullOrEmpty(_variant.prompt.text) || _variant.prompt.hidden)
            {
                m_questionText.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                m_questionText.transform.parent.gameObject.SetActive(true);
                m_questionText.text = text;
            }
        }

        public void HideQuestion()
        {
            m_questionText.transform.parent.gameObject.SetActive(false);
        }
        
        public void UpdateDefaultVideoPrompt(bool _show, string _islandName = "", string _name = "", string _summary = "")
        {
            m_islandNameText.transform.parent.gameObject.SetActive(_show);
            m_islandNameText.text = _islandName;
            m_nameText.text = _name;
            m_summaryText.text = _summary;
        }
#endregion


#region PRIVATE METHODS
        private void InitialiseChoices(Variant _variant, Color _backgroundColour, Color _textColour)
        {
            for (int i = 0; i < _variant.choices.Count; i++)
            {
                bool hasLink = false;
                if (_variant.choices[i].linkedVariants.Count > 0)
                    hasLink = !string.IsNullOrEmpty(_variant.choices[i].linkedVariants[0].external_url);

                if (_variant.choices[i].linkedVariants.Count > 0 && !string.IsNullOrEmpty(_variant.choices[i].linkedVariants[0].id) || hasLink)
                {
                    string buttonText = _variant.choices[i].label;

                    if (string.IsNullOrEmpty(buttonText))   // If choice has no text?
                    {
                        if (string.IsNullOrEmpty(SequenceManager.s_instance.m_root.GetVariant(_variant.choices[i].linkedVariants[0].id).name))   // If the linked variant has no name?
                            buttonText = "Story Island " + SequenceManager.s_instance.m_root.GetVariant(_variant.choices[i].linkedVariants[0].id).label;
                        else
                            buttonText = SequenceManager.s_instance.m_root.GetVariant(_variant.choices[i].linkedVariants[0].id).name;
                    }


                    // Load in button image
                    string buttonImageName = _variant.choices[i].image_url;
                    Sprite sprite = null;
                    if (buttonImageName.Length > 0)
                    {
                        int startIndex = buttonImageName.LastIndexOf("/") + 1;
                        buttonImageName = buttonImageName.Substring(startIndex, buttonImageName.LastIndexOf(".") - startIndex);
                        sprite = Resources.Load<Sprite>("Images/Buttons/" + buttonImageName);
                    }

                    if (_variant.prompt.style == "split_quadrant")
                    {
                        int index = GetButtonIndexByLocation(_variant.choices[i].location);
                        if (index >= 0)
                        {
                            m_buttonGroup.transform.GetChild(index).GetComponent<CanvasGroup>().alpha = 1;
                            m_buttonGroup.transform.GetChild(index).GetComponent<CanvasGroup>().blocksRaycasts = true;
                            m_buttonGroup.transform.GetChild(index).GetComponent<CanvasGroup>().interactable = true;
                            m_buttons[index].ActivateButton(buttonText, i, _backgroundColour, _textColour, hasLink);
                            
                            if(sprite)
                                StartCoroutine(WaitForButtonImage(index, sprite));
                        }
                    }
                    else
                    {
                        if (!_variant.choices[i].hidden)
                        {
                            m_buttons[i].ActivateButton(buttonText, i, _backgroundColour, _textColour, hasLink);

                            // 3D buttons
                            if (_variant.prompt.style == m_360CustomStyleName)
                            {
                                m_3dButtonGroup.transform.GetChild(i).GetComponentInChildren<TextMeshProUGUI>().text = buttonText;

                                if (sprite)
                                    StartCoroutine(WaitForButtonImage3D(i, sprite));
                            }
                            else // 2D non-Quadrant
                            {
                                if (sprite)
                                    StartCoroutine(WaitForButtonImage(i, sprite));
                            }
                        }
                    }
                }
            } // i
        }

        private IEnumerator WaitForButtonImage(int _index, Sprite _buttonSprite)
        {
            yield return new WaitForEndOfFrame();
            m_buttons[_index].m_buttonBackground.sprite = _buttonSprite;
            m_buttons[_index].m_buttonBackground.color = Color.white;
            m_buttons[_index].m_buttonBackground.type = UnityEngine.UI.Image.Type.Simple;

            m_buttonGroup.transform.GetChild(_index).GetComponent<CanvasGroup>().alpha = 1;
        }

        private IEnumerator WaitForButtonImage3D(int _index, Sprite _buttonSprite)
        {
            yield return new WaitForEndOfFrame();
            m_3dButtonGroup.transform.GetChild(_index).GetComponentInChildren<UnityEngine.UI.Image>().sprite = _buttonSprite;
            m_3dButtonGroup.transform.GetChild(_index).GetComponentInChildren<UnityEngine.UI.Image>().color = Color.white;
            m_3dButtonGroup.transform.GetChild(_index).GetComponentInChildren<UnityEngine.UI.Image>().type = UnityEngine.UI.Image.Type.Simple;
            
            m_buttonGroup.transform.GetChild(_index).GetComponent<CanvasGroup>().alpha = 1;
        }

        private bool And(JsonNode _operationsNode)
        {
            var rule = JsonSerializer.Deserialize<Rule>(_operationsNode);

            var result = rule.Apply(SaveSystem.m_variablesNode);
            JsonNode resultNode = JsonSerializer.Serialize(result);

            for (int i = 0; i < result.AsArray().Count; i++)
            {
                if (result[i].ToString() == "false")
                {
                    return false;
                }
            }
            return true;
        }


        private bool Or (JsonNode _operationsNode)
        {
            var rule = JsonSerializer.Deserialize<Rule>(_operationsNode);

            var result = rule.Apply(SaveSystem.m_variablesNode);
            JsonNode resultNode = JsonSerializer.Serialize(result);

            for(int i = 0; i < result.AsArray().Count; i++)
            {
                if (result[i].ToString() == "true")
                {
                    return true;
                }
            }
            return false;
        }

        private void HandleChoiceLogic(JsonNode _operationsNode, int _choiceIndex)
        {
            Choice currentChoice = SequenceManager.s_instance.m_currentVariant.choices[_choiceIndex];

            LinkedVariant variantToAdd = new LinkedVariant(_operationsNode["id"].ToString(), _operationsNode["label"].ToString());

            currentChoice.linkedVariants.Clear();
            currentChoice.linkedVariants.Add(variantToAdd);

            SequenceManager.s_instance.PerformOperation(_operationsNode["behaviours_jsonlogic"]);
        }    

        private void HandleButtonConditions()
        {
            JsonNode operationsNode = SequenceManager.s_instance.GetCurrentVariantChoices();

            if (operationsNode == null)
                return;


            for (int i = 0; i < operationsNode.AsArray().Count; i++)
            {
                JsonNode tempNode = operationsNode[i]!["advanced_logic"]!;
                if (tempNode == null)
                    continue;

                if (tempNode.AsArray().Count > 0)
                {
                    for(int j = 0; j < tempNode![0]!["if"]!.AsArray().Count; j++)
                    {
                        JsonNode logicNode = tempNode![0]!["if"]![j];

                        if (j == tempNode![0]!["if"]!.AsArray().Count - 1)
                        {
                            HandleChoiceLogic(tempNode![0]!["if"]![j], i);
                            break;
                        }

                        if (j % 2 == 1)
                        {
                            continue;
                        }
                                              
                        if(logicNode["or"] != null)
                        {
                            if(Or(logicNode["or"]))
                            {
                                HandleChoiceLogic(tempNode![0]!["if"]![j + 1], i);
                                break;
                            }
                        }
                        else if (logicNode["and"] != null)
                        {
                            if(And(logicNode["and"]))
                            {
                                HandleChoiceLogic(tempNode![0]!["if"]![j + 1], i);
                                break;
                            }
                        }
                        else
                        {
                            Debug.LogError("No Or or And operations found in If statement" + j);
                        }
                    }
                }                   
            }
            InitialisePrompt(SequenceManager.s_instance.m_currentVariant);
        }

        private void SetTimerFill(float _fill)
        {
            if (_fill <= 0.01f)
                _fill = 0f;

            Vector3 newScale = m_timer.transform.localScale;
            newScale.x = _fill;
            m_timer.transform.localScale = newScale;
        }

        private void SetTimerColours(Color _backgroundColour, Color _textColour, bool _invert = false)
        {
            if (!_invert)
            {
                m_timer.color = _textColour;
                m_timerBackground.color = _backgroundColour;
            }
            else
            {
                m_timer.color = _backgroundColour;
                m_timerBackground.color = _textColour;
            }
        }

        private void SetQuestionColours(Color _backgroundColour, Color _textColour)
        {
            m_questionHolder.transform.GetChild(0).GetComponent<UnityEngine.UI.Image>().color = _backgroundColour;
            m_questionHolder.transform.GetChild(0).GetChild(0).GetComponent<TextMeshProUGUI>().color = _textColour;
        }

        private void RemoveButtonLayouts(GameObject Go)
        {
            Component[] components = Go.GetComponents(typeof(Component));
            foreach (Component component in components)
            {
                switch (component.GetType().ToString())
                {
                    case "UnityEngine.UI.VerticalLayoutGroup":
                        Destroy(component);
                        break;
                    case "UnityEngine.UI.GridLayoutGroup":
                        Destroy(component);
                        break;
                    case "UnityEngine.UI.HorizontalLayoutGroup":
                        Destroy(component);
                        break;
                }
            }
        }

        private void SetUpNewLayout(GameObject _GO, string _wStyle, bool _isParent)
        {
            GenericStyle stylesInJson = JsonUtility.FromJson<GenericStyle>(m_stylesJson.text);
            c_readJson = stylesInJson;
            int num = 0;
            bool found = false;
            for (int i = 0; i < stylesInJson.styles.Length; i++)
            {
                if (stylesInJson.styles[i].id == _wStyle)
                {
                    num = i;
                    m_styleNum = i;
                    found = true;
                }
            }// i
            if (!found)
            {
                num = 1;
                m_styleNum = 1;
            }
            if (_isParent)
            {
                c_neededStyle = stylesInJson.styles[num].parent;
            }
            else
            {
                c_neededStyle = stylesInJson.styles[num].child;
            }
            AddLayout(_GO, c_neededStyle.layout, _isParent);
        }

        private void SetColourScheme(Variant _variant, out Color _backgroundColour, out Color _textColour)
        {
            // Set background colour
            if (_variant.prompt.background_color[0] == '#')
                ColorUtility.TryParseHtmlString(_variant.prompt.background_color, out _backgroundColour);
            else
                _backgroundColour = ParseColour(_variant.prompt.background_color);

            // Set text colour
            if (_variant.prompt.text_color[0] == '#')
                ColorUtility.TryParseHtmlString(_variant.prompt.text_color, out _textColour);
            else
                _textColour = ParseColour(_variant.prompt.text_color);
        }

        private Color ParseColour(string _colour)
        {
            bool readingNumber = false;
            int index = -1;

            float red = -1;
            float green = -1;
            float blue = -1;
            float alpha = -1;

            for (int i = 0; i < _colour.Length; i++)
            {
                if (!readingNumber)
                {
                    if (char.IsDigit(_colour[i]))
                    {
                        readingNumber = true;
                        index = i;
                    }
                }
                else
                {
                    if (!char.IsDigit(_colour[i]) && _colour[i] != '.')
                    {
                        readingNumber = false;

                        if (red == -1)
                            red = float.Parse(_colour.Substring(index, i - index));
                        else if (green == -1)
                            green = float.Parse(_colour.Substring(index, i - index));
                        else if (blue == -1)
                            blue = float.Parse(_colour.Substring(index, i - index));
                        else if (alpha == -1)
                            alpha = float.Parse(_colour.Substring(index, i - index));
                    }
                }
            } // i

            if (red < 0) red = 0;
            if (green < 0) green = 0;
            if (blue < 0) blue = 0;
            if (alpha < 0) alpha = 1;

            return new Color(red, green, blue, alpha);
        }

        private void AddLayout(GameObject _GO, string _type, bool _isParent)
        {
            //////Debug.Log("AddLayout");
            switch (_type)
            {
                case "VerticalLayoutGroup":
                    StartCoroutine(AddLayoutGroupCoroutine(_GO, typeof(UnityEngine.UI.VerticalLayoutGroup), _isParent, c_neededStyle));
                    break;

                case "GridLayoutGroup":
                    StartCoroutine(AddLayoutGroupCoroutine(_GO, typeof(UnityEngine.UI.GridLayoutGroup), _isParent, c_neededStyle));
                    break;

                case "HorizontalLayoutGroup":
                    StartCoroutine(AddLayoutGroupCoroutine(_GO, typeof(UnityEngine.UI.HorizontalLayoutGroup), _isParent, c_neededStyle));
                    break;

                default:
                    StartCoroutine(AddLayoutGroupCoroutine(_GO, typeof(UnityEngine.UI.VerticalLayoutGroup), _isParent, c_neededStyle));
                    break;
            }
        }

        private void MakeButtonsVisibleAndHitTestable(bool _b)
        {
            foreach (Transform child in m_buttonGroup.transform)
            {
                child.transform.GetComponent<CanvasGroup>().alpha = _b? 1 : 0;
                child.transform.GetComponent<CanvasGroup>().blocksRaycasts = _b;
                child.transform.GetComponent<CanvasGroup>().interactable = _b;
            }
        }

        private int GetButtonIndexByLocation(string _location)
        {
            int answer = -1;

            switch (_location)
            {
                case "top_left":
                    answer = 1;
                    break;

                case "top_right":
                    answer = 2;
                    break;

                case "bottom_left":
                    answer = 5;
                    break;

                case "bottom_right":
                    answer = 6;
                    break;

                default:
                    answer = -1;
                    break;
            }
            return answer;
        }

        public bool ShouldUseCustomPosition(Variant _variant)
        {
            if(_variant.prompt.style == m_360CustomStyleName)
                return true;
            else
                return false;
        }

        public void Set3DButtonColours(Color _colour)
        {
            for(int i = 0; i < m_3dButtonGroup.transform.childCount; i++) 
            {
                m_3dButtonGroup.transform.GetChild(i).GetChild(0).GetComponent<UnityEngine.UI.Image>().color = _colour;
            } // i
        }
#endregion


#region StyleSetup
        private IEnumerator AddLayoutGroupCoroutine(GameObject _go, Type _type, bool _isParent, SubStyle _neededStyle)
        {
            if(!m_questionHolder.activeSelf)
            {
                m_questionHolder.SetActive(true);
            }
            
            if (_go.GetComponent<UnityEngine.UI.LayoutGroup>())
            {
                Destroy(_go.GetComponent<UnityEngine.UI.LayoutGroup>());
                yield return new WaitForEndOfFrame(); // Wait a frame
            }

            c_newLayout = _go.AddComponent(_type);

            //////Debug.Log(c_newLayout.name);
            GenericStyle stylesInJson = JsonUtility.FromJson<GenericStyle>(m_stylesJson.text);
            if(c_currentVariant.prompt.style + "_" + c_currentVariant.prompt.substyle == "new_split_left_right_central_button" || 
                c_currentVariant.prompt.style + "_" + c_currentVariant.prompt.substyle == "new_split_left_right_central_text" || 
                c_currentVariant.prompt.style + "_" + c_currentVariant.prompt.substyle == "new_split_top_bottom_central_text" || 
                c_currentVariant.prompt.style + "_" + c_currentVariant.prompt.substyle == "new_split_top_bottom_central_button")
            {
                m_questionHolder.SetActive(false);
            }

            

            switch (_type.Name)
            {
                case "HorizontalLayoutGroup":
                    SetUpHorizontalGroup(_neededStyle);
                    break;

                case "VerticalLayoutGroup":
                    SetUpVerticalGroup(_neededStyle);
                    break;
                case "GridLayoutGroup":
                    SetupGrid(_neededStyle);
                    break;
                default:
                    SetUpHorizontalGroup(_neededStyle);
                    break;
            };
            
            SetPosition(_go, _neededStyle);
            //Debug.Log(stylesInJson.styles[m_styleNum].id);
            //Debug.Log(stylesInJson.styles[m_styleNum].bottomTimer);
            if(stylesInJson.styles[m_styleNum].bottomTimer)
            {
                m_timerBackground.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;

                m_timerBackground.GetComponent<RectTransform>().anchoredPosition = new Vector2(960,10);
                switch(stylesInJson.styles[m_styleNum].id)
                {
                    case "split_quadrant_central_text":
                        //Debug.Log("split_quadrant_central_text");
                        gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(stylesInJson.styles[m_styleNum].parent.transform.sizeDelta.x,1180);
                    break;
                    case "split_quadrant_bottom_button":
                        //Debug.Log("split_quadrant_bottom_button");
                        gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(stylesInJson.styles[m_styleNum].parent.transform.sizeDelta.x,1330);
                    break;
                    
                }
                //1180
            }
            else
            {
                m_timerBackground.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = false;
            }
            if(c_currentVariant.prompt.style == "bottom_banner" && (c_currentVariant.prompt.text == "" || c_currentVariant.prompt.hidden))
            {
                //Debug.Log("Running");
                m_questionHolder.SetActive(false);
                if(_go.name == "Buttons")
                {
                    _go.GetComponent<RectTransform>().sizeDelta = new Vector2(_neededStyle.transform.sizeDelta.x, 355);
                }
            }
        }

        private void SetPosition(GameObject _go, SubStyle _style)
        {
            if (_go.name == "Buttons")
            {
                if (c_readJson.styles[m_styleNum].roundedButtons)
                {
                    foreach (Transform child in _go.transform)
                    {
                        //////Debug.Log(child.name);
                        child.transform.GetComponent<UnityEngine.UI.Image>().sprite = m_roundedSprite;
                    }
                    foreach (Transform child in m_questionHolder.transform)
                    {
                        //////Debug.Log(child.name);
                        child.transform.GetComponent<UnityEngine.UI.Image>().sprite = m_roundedSprite;
                    }
                }
                else
                {
                    foreach (Transform child in _go.transform)
                    {
                        child.transform.GetComponent<UnityEngine.UI.Image>().sprite = m_rectangularSprite;
                    }
                    foreach (Transform child in m_questionHolder.transform)
                    {
                        child.transform.GetComponent<UnityEngine.UI.Image>().sprite = m_rectangularSprite;
                    }
                }
            }

            m_timerGO.transform.SetSiblingIndex(c_readJson.styles[m_styleNum].timerIndex);
            _go.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
            _go.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);
            _go.GetComponent<RectTransform>().anchoredPosition = new Vector2(_style.transform.anchoredPos.x, _style.transform.anchoredPos.y);
            _go.GetComponent<RectTransform>().sizeDelta = new Vector2(_style.transform.sizeDelta.x, _style.transform.sizeDelta.y);
            _go.GetComponent<RectTransform>().pivot = new Vector2(_style.transform.piviot.x, _style.transform.piviot.y);
            //_go.GetComponent<RectTransform>().localPosition = new Vector3(_style.transform.localPos.x, _style.transform.localPos.y, _style.transform.localPos.z);
            _go.GetComponent<RectTransform>().localScale = new Vector3(_style.transform.localScale.x, _style.transform.localScale.y, _style.transform.localScale.z);
            SetTimer();
            SetUpQuestion();
        }

        private void SetUpQuestion()
        {
            if(c_readJson.styles[m_styleNum].longQuestion)
            {
                m_questionHolder.GetComponent<RectTransform>().sizeDelta = new Vector2(1920,80);
                foreach (Transform child in m_questionHolder.transform)
                {
                    ////Debug.Log(child.name);
                    if(child.name == "Question")
                    {
                        child.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(1920,80);
                    }
                }
            }
            else
            {
                m_questionHolder.GetComponent<RectTransform>().sizeDelta = new Vector2(1825,80);
                foreach (Transform child in m_questionHolder.transform)
                {
                    if(child.name == "Question")
                    {
                        child.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(1825,80);
                    }
                }
            }
            //1825
            //m_questionHolder
            //1920
        }

        private void SetTimer()
        {
            //c_readJson.styles[m_styleNum].longTimer
            if(c_readJson.styles[m_styleNum].longTimer)
            {
                m_timerGO.GetComponent<RectTransform>().sizeDelta = new Vector2(1920,c_readJson.styles[m_styleNum].timerStyle.timerParentHeight);
                foreach (Transform child in m_timerGO.transform)
                {
                    if(child.name == "Timer")
                    {
                        child.transform.GetComponent<RectTransform>().anchoredPosition = new Vector2(960,c_readJson.styles[m_styleNum].timerStyle.timerParentHeight/2);
                        child.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(1920,c_readJson.styles[m_styleNum].timerStyle.timerChildHeight);
                    }
                }
            }
            else
            {
                m_timerGO.GetComponent<RectTransform>().sizeDelta = new Vector2(960,10);
                foreach (Transform child in m_timerGO.transform)
                {
                    if(child.name == "Timer")
                    {
                        child.transform.GetComponent<RectTransform>().anchoredPosition = new Vector2(460,5);
                        child.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(960,10);
                    }
                }
            }
        }

        private void SetUpHorizontalGroup(SubStyle _style)
        {
            ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).padding.left = _style.padding.left;
            ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).padding.right = _style.padding.right;
            ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).padding.top = _style.padding.top;
            ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).padding.bottom = _style.padding.bottom;
            ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).childForceExpandHeight = _style.controls.expand.height;
            ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).childForceExpandWidth = _style.controls.expand.width;
            ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).childScaleHeight = _style.controls.scale.height;
            ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).childScaleWidth = _style.controls.scale.horizontal;
            ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).childControlHeight = _style.controls.size.height;
            ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).childControlWidth = _style.controls.size.width;
            ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).spacing = _style.spacing;
            ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).reverseArrangement = _style.reverseAlign;
            switch (_style.childAlignment)
            {
                case "UpperCenter":
                    ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).childAlignment = TextAnchor.UpperCenter;
                    break;
                case "UpperLeft":
                    ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).childAlignment = TextAnchor.UpperLeft;
                    break;
                case "UpperRight":
                    ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).childAlignment = TextAnchor.UpperRight;
                    break;
                case "MiddleLeft":
                    ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).childAlignment = TextAnchor.MiddleLeft;
                    break;
                case "MiddleCenter":
                    ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).childAlignment = TextAnchor.MiddleCenter;
                    break;
                case "MiddleRight":
                    ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).childAlignment = TextAnchor.MiddleRight;
                    break;
                case "LowerLeft":
                    ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).childAlignment = TextAnchor.LowerLeft;
                    break;
                case "LowerCenter":
                    ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).childAlignment = TextAnchor.LowerCenter;
                    break;
                case "LowerRight":
                    ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).childAlignment = TextAnchor.LowerRight;
                    break;
                default:
                    ((UnityEngine.UI.HorizontalLayoutGroup)c_newLayout).childAlignment = TextAnchor.MiddleCenter;
                    break;
            }
        }

        private void SetUpVerticalGroup(SubStyle _style)
        {
            ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).padding.left = _style.padding.left;
            ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).padding.right = _style.padding.right;
            ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).padding.top = _style.padding.top;
            ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).padding.bottom = _style.padding.bottom;
            ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).childForceExpandHeight = _style.controls.expand.height;
            ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).childForceExpandWidth = _style.controls.expand.width;
            ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).childScaleHeight = _style.controls.scale.height;
            ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).childScaleWidth = _style.controls.scale.horizontal;
            ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).childControlHeight = _style.controls.size.height;
            ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).childControlWidth = _style.controls.size.width;
            ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).spacing = _style.spacing;
            ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).reverseArrangement = _style.reverseAlign;
            switch (_style.childAlignment)
            {
                case "UpperCenter":
                    ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).childAlignment = TextAnchor.UpperCenter;
                    break;
                case "UpperLeft":
                    ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).childAlignment = TextAnchor.UpperLeft;
                    break;
                case "UpperRight":
                    ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).childAlignment = TextAnchor.UpperRight;
                    break;
                case "MiddleLeft":
                    ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).childAlignment = TextAnchor.MiddleLeft;
                    break;
                case "MiddleCenter":
                    ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).childAlignment = TextAnchor.MiddleCenter;
                    break;
                case "MiddleRight":
                    ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).childAlignment = TextAnchor.MiddleRight;
                    break;
                case "LowerLeft":
                    ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).childAlignment = TextAnchor.LowerLeft;
                    break;
                case "LowerCenter":
                    ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).childAlignment = TextAnchor.LowerCenter;
                    break;
                case "LowerRight":
                    ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).childAlignment = TextAnchor.LowerRight;
                    break;
                default:
                    ((UnityEngine.UI.VerticalLayoutGroup)c_newLayout).childAlignment = TextAnchor.MiddleCenter;
                    break;
            }
        }

        private void SetupGrid(SubStyle _style)
        {
            //////Debug.Log(m_styleNum);
            ((UnityEngine.UI.GridLayoutGroup)c_newLayout).padding.left = _style.gridOptions.padding.left;
            ((UnityEngine.UI.GridLayoutGroup)c_newLayout).padding.right = _style.gridOptions.padding.right;
            ((UnityEngine.UI.GridLayoutGroup)c_newLayout).padding.top = _style.gridOptions.padding.top;
            ((UnityEngine.UI.GridLayoutGroup)c_newLayout).padding.bottom = _style.gridOptions.padding.bottom;
            ((UnityEngine.UI.GridLayoutGroup)c_newLayout).cellSize = new Vector2(_style.gridOptions.cellSize.x, _style.gridOptions.cellSize.y);
            ((UnityEngine.UI.GridLayoutGroup)c_newLayout).spacing = new Vector2(_style.gridOptions.spacing.x,_style.gridOptions.spacing.y);
            foreach (Transform child in m_buttonGroup.transform)
            {
                child.gameObject.SetActive(true);
            }


            switch (_style.gridOptions.startAxis)
            {
                case "Horizontal":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
                    break;
                case "Vertical":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Vertical;
                    break;
                default:
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal; ;
                    break;
            }
            switch (_style.gridOptions.startCorner)
            {
                case "UpperLeft":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).startCorner = UnityEngine.UI.GridLayoutGroup.Corner.UpperLeft;
                    break;
                case "LowerLeft":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).startCorner = UnityEngine.UI.GridLayoutGroup.Corner.LowerLeft;
                    break;
                case "LowerRight":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).startCorner = UnityEngine.UI.GridLayoutGroup.Corner.LowerRight;
                    break;
                case "UpperRight":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).startCorner = UnityEngine.UI.GridLayoutGroup.Corner.UpperRight;
                    break;
                default:
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).startCorner = UnityEngine.UI.GridLayoutGroup.Corner.UpperLeft;
                    break;
            }
            switch (_style.gridOptions.constraint.type)
            {
                case "Flexible":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).constraint = UnityEngine.UI.GridLayoutGroup.Constraint.Flexible;
                    break;
                case "FixedRowCount":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedRowCount;
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).constraintCount = _style.gridOptions.constraint.constraintCount;
                    break;
                case "FixedColumnCount":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).constraintCount = _style.gridOptions.constraint.constraintCount;
                    break;
                default:
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).constraint = UnityEngine.UI.GridLayoutGroup.Constraint.Flexible;
                    break;
            }
            switch (_style.childAlignment)
            {
                case "UpperCenter":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).childAlignment = TextAnchor.UpperCenter;
                    break;
                case "UpperLeft":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).childAlignment = TextAnchor.UpperLeft;
                    break;
                case "UpperRight":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).childAlignment = TextAnchor.UpperRight;
                    break;
                case "MiddleLeft":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).childAlignment = TextAnchor.MiddleLeft;
                    break;
                case "MiddleCenter":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).childAlignment = TextAnchor.MiddleCenter;
                    break;
                case "MiddleRight":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).childAlignment = TextAnchor.MiddleRight;
                    break;
                case "LowerLeft":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).childAlignment = TextAnchor.LowerLeft;
                    break;
                case "LowerCenter":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).childAlignment = TextAnchor.LowerCenter;
                    break;
                case "LowerRight":
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).childAlignment = TextAnchor.LowerRight;
                    break;
                default:
                    ((UnityEngine.UI.GridLayoutGroup)c_newLayout).childAlignment = TextAnchor.MiddleCenter;
                    break;
            }
        }
#endregion
    }
}