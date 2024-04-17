using Stornaway.Utility;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.InputSystem;
using System.Text.Json.Nodes;

namespace Stornaway
{
    public class SequenceManager : MonoBehaviour
    {
        public static SequenceManager s_instance = null;
        
        [Header("Mouse Position")]
        public InputAction mousePositionAction = null;
        [Header("Cursor Delta")]
        public InputAction cursorDeltaAction = null;
        [Header("Play/Pause")]
        public InputAction playPauseAction = null;
        [Header("Clicked")]
        public InputAction clickedAction = null;
        [Header("Debug Display")]
        public InputAction debugAction = null;
        [Space(5)]
        public Camera m_standardCamera = null;
        public Camera m_panoramicCamera = null;
        public VideoPlayer m_videoPlayer = null;
        public RawImage m_videoDisplay = null;
        public RawImage m_imageDisplay = null;
        [Space(5)]
        [SerializeField] Material m_panoramic360SphereMaterial = null;
        [SerializeField] Material m_panoramic360CubeMaterial = null;
        [SerializeField] Material m_panoramic180SphereMaterial = null;
        [Space(5)]
        [SerializeField] RenderTexture m_standardRenderTexture = null;
        [SerializeField] RenderTexture m_panoramicRenderTexture = null;
        [Space(5)]
        public Variant m_currentVariant = null;
        [NonSerialized] public Variant m_nextVariant = null;   // This has to be non-serialized for it to be nullable

        public List<Variant> m_lastVariants = new List<Variant>();

        [HideInNormalInspector, SerializeField] public Root m_root = null;
        [HideInNormalInspector, SerializeField] public ExportMethod m_playbackMethod = ExportMethod.DOWNLOAD;

        [SerializeField] private PromptManager m_promptManager = null;
        [SerializeField] private GameObject m_loadingScreen = null;
        private List<ViewedVariant> m_viewedVariants = new List<ViewedVariant>();
        private double m_displayAt = 0;
        private double m_furthestTime = 0; // The furthest progress the user has made through the current video
        private JsonNode m_jsonNode = null;

        private LinkedVariant c_linkedVariant = null;

        private Choice m_currentChoice = null;



        #region UNITY CALLBACKS
        private void Awake()
        {
            if (!s_instance)
                s_instance = this;

            
            m_jsonNode = CreateStornawayJsonNode();


            if (string.IsNullOrEmpty(SaveSystem.m_currentVariant))
                return;

            m_currentVariant = m_root.GetVariant(SaveSystem.m_currentVariant);

            
            m_lastVariants.Clear();
            for(int i = 0; i < SaveSystem.m_variantHistory.Length; i++)
            {
                m_lastVariants.Add(m_root.GetVariant(SaveSystem.m_variantHistory[i]));
            } // i
        }
        
        private void Start()
        {
            Initialise();

            if (!string.IsNullOrEmpty(m_currentVariant.id))
                SetActiveMedia(m_currentVariant, false);
            else
                SetActiveMedia(m_root.GetVariant(m_root.start_variant), false);
        }
        
        private void Update()
        {
            if (m_videoDisplay.gameObject.activeSelf)
            {
                if (IsCurrentVideoFinished())
                {
                    bool pauseAtEnd = PauseAtEnd(m_currentVariant);

                    if (pauseAtEnd && HasChoiceBeenMade() || !pauseAtEnd && HasChoiceBeenMade())
                        SetActiveMedia(m_nextVariant, true);
                    else if(!pauseAtEnd)
                        SetActiveMedia(GetDefaultChoice(m_currentVariant), true);
                }
                else
                {
                    UpdateSpecificTimeVariables();

                    if(m_videoPlayer.isPrepared)
                        m_promptManager.UpdatePrompt(m_currentVariant, m_videoPlayer.time, m_videoPlayer.length, m_displayAt);
                }
            }
            else if(m_imageDisplay.gameObject.activeSelf)
            {
                if (HasChoiceBeenMade())
                {
                    SetActiveMedia(m_nextVariant, true);
                }
            }
        }

        private void OnEnable()
        {
            mousePositionAction.Enable();
            cursorDeltaAction.Enable();
            playPauseAction.Enable();
            debugAction.Enable();
            clickedAction.Enable();
        }

        private void OnDisable()
        {
            mousePositionAction.Disable();
            cursorDeltaAction.Disable();
            playPauseAction.Disable();
            clickedAction.Disable();

            m_videoPlayer.targetTexture.Release();
        }

        private void OnApplicationFocus(bool _focus)
        {
            if(!_focus)
            {
                m_videoPlayer.Pause();
                Time.timeScale = 0;
            }
            else
            {
                m_videoPlayer.Play();
                Time.timeScale = 1;
            }
        }

        private void OnApplicationQuit()
        {
            Save();
        }
        #endregion


        #region PUBLIC METHODS
        public void SetActiveMedia(Variant _variant, bool _addToLastVariants)
        {
            if (m_currentVariant != null && _addToLastVariants)
            {
                HandleButtonPressedLogic(_variant);
                SetEndOfVideoVariables();
                m_lastVariants.Add(m_currentVariant);
            }


            m_currentVariant = _variant;
            m_nextVariant = null;
            SetStartOfVideoVariables();
            UpdateViewedVariants(m_currentVariant.id);
            m_promptManager.InitialisePrompt(m_currentVariant);

            // Should we play video
            if (m_currentVariant.media_alternative_sources.Count > 0 || string.IsNullOrEmpty(m_currentVariant.image.url))
            {
                SetVideoClip(m_currentVariant);
                m_videoPlayer.prepareCompleted += PlayActiveVideo;
                m_videoPlayer.Prepare();

                m_videoDisplay.gameObject.SetActive(true);
                m_imageDisplay.gameObject.SetActive(false);
            }
            else // Show image
            {
                m_promptManager.ActivateButtons(true);
                SetImage(m_currentVariant);
                m_videoPlayer.Stop();

                m_videoDisplay.gameObject.SetActive(false);
                m_imageDisplay.gameObject.SetActive(true);
            }
        }


        public bool HasReachedDisplayAtTime()
        {
            if (m_videoPlayer.isPlaying && GetTimeLeftOnVideo() <= m_displayAt)
                return true;
            else
                return false;
        }

        public bool IsCurrentVideoFinished()
        {
            if (GetTimeLeftOnVideo() <= 0.05 & GetTimeLeftOnVideo() != -1)
                return true;
            else
                return false;
        }

        public Variant GetDefaultChoice(Variant _variant)
        {
            for (int i = 0; i < _variant.choices.Count; i++)
            {
                if (_variant.choices[i].@default)
                    return m_root.GetVariant(GetCorrectLinkedVariant(_variant.choices[i]).id);
            } // i
            return m_root.GetVariant(GetCorrectLinkedVariant(_variant.choices[0]).id);
        }

        public Variant GetPreviousVideo()
        {

            if (m_lastVariants.Count == 0 || m_currentVariant.id == m_root.start_variant)
            {
                return null;
            }
            else
            {
                Variant tempVariant = m_lastVariants[m_lastVariants.Count - 1];

                m_lastVariants.Remove(tempVariant);

                return tempVariant;
            }
        }

        public JsonNode GetMainJsonNode()
        {
            return m_jsonNode;
        }

        public JsonNode GetCurrentVariantChoices()
        {
            return m_jsonNode!["variants"]![m_root.GetVariantIndex(m_currentVariant.id)]!["choices"]!;
        }


        public LinkedVariant GetCorrectLinkedVariant(Choice _choice)
        {
            c_linkedVariant = null;
            int age = int.MaxValue;

            m_currentChoice = _choice;
            
            for (int i = 0; i < _choice.linkedVariants.Count; i++)
            {
                ViewedVariant? viewedVariant = GetViewedVariant(_choice.linkedVariants[i].if_viewed_most_recently);

                if(viewedVariant.HasValue && viewedVariant.GetValueOrDefault().age < age)
                {
                    c_linkedVariant = _choice.linkedVariants[i];
                    age = viewedVariant.GetValueOrDefault().age;
                }
            } // i

            if (c_linkedVariant == null)
                c_linkedVariant = _choice.linkedVariants[0];

            return c_linkedVariant;
        }

        public ViewedVariant? GetViewedVariant(string _variantId)
        {
            for (int i = 0; i < m_viewedVariants.Count; i++)
            {
                if (m_viewedVariants[i].variantId == _variantId)
                    return m_viewedVariants[i];
            } // i

            // If none found
            return null;
        }

        public void Save()
        {
            string[] variantHistory = new string[m_lastVariants.Count];
            for(int i = 0; i <  variantHistory.Length; i++)
            {
                variantHistory[i] = m_lastVariants[i].id; 
            } // i

            SaveSystem.Save(m_currentVariant.id, variantHistory);
        }
        #endregion


        #region PRIVATE METHODS
        private void Initialise()
        {
            // Loading screen is meant to always be on. Video/images will appear over the top, so if there is nothing displayed
            // then the user will see the loading screen
            m_loadingScreen.SetActive(true);
            SetVideoPlayerPlaybackMethod(m_playbackMethod);
        }

        private void SetVideoPlayerPlaybackMethod(ExportMethod _method)
        {
            if (m_playbackMethod == ExportMethod.STREAM)
                m_videoPlayer.source = VideoSource.Url;
            else if (m_playbackMethod == ExportMethod.DOWNLOAD)
                m_videoPlayer.source = VideoSource.VideoClip;
        }

        private void SetVideoClip(Variant _variant)
        {
            string mediaName = _variant.mediaName;
            if(!string.IsNullOrEmpty(mediaName))
                mediaName = GetMediaNameWithoutExtension(mediaName);

            m_displayAt = double.Parse(_variant.prompt.display_at);

            if (string.IsNullOrEmpty(mediaName))
            {
                m_videoPlayer.clip = Resources.Load<VideoClip>("Videos/DefaultVideo");
                
                if(_variant.text_overlay.Count > 0)
                    m_promptManager.UpdateDefaultVideoPrompt(true, _variant.island_name, _variant.name, _variant.summary);
                else
                    m_promptManager.UpdateDefaultVideoPrompt(false);
            }
            else
            {
                m_promptManager.UpdateDefaultVideoPrompt(false);

                if (m_playbackMethod == ExportMethod.STREAM)
                    m_videoPlayer.url = _variant.media_alternative_sources[0].url;
                else if (s_instance.m_playbackMethod == ExportMethod.DOWNLOAD)
                    m_videoPlayer.clip = Resources.Load<VideoClip>("Videos/" + GetMediaNameWithoutExtension(_variant.mediaName));
            }
            m_promptManager.ShowTimer(false, _variant.prompt.timer.transparent_background);
        }

        private void SetImage(Variant _variant)
        {
            m_displayAt = 0;
            m_promptManager.ShowTimer(false);
            m_imageDisplay.texture = Resources.Load<Texture>("Images/" + GetMediaNameWithoutExtension(_variant.mediaName));
            m_promptManager.UpdateQuestion(_variant);
            m_promptManager.UpdateDefaultVideoPrompt(false);

            m_promptManager.UpdateDefaultVideoPrompt(false);

            m_standardCamera.gameObject.SetActive(true);
            m_panoramicCamera.gameObject.SetActive(false);
        }

        private void PlayActiveVideo(VideoPlayer _player)
        {
            _player.Play();
            _player.prepareCompleted -= PlayActiveVideo;

            string projection = m_currentVariant.media_projection;

            if (string.IsNullOrEmpty(projection))
            {
                m_standardCamera.gameObject.SetActive(true);
                m_panoramicCamera.gameObject.SetActive(false);
                m_videoPlayer.targetTexture = m_standardRenderTexture;
            }
            else
            {
                m_standardCamera.gameObject.SetActive(false);
                m_panoramicCamera.gameObject.SetActive(true);
                m_videoPlayer.targetTexture = m_panoramicRenderTexture;

                switch (projection)
                {
                    case "360":
                        RenderSettings.skybox = m_panoramic360SphereMaterial;
                        break;

                    case "360_CUBE":
                        RenderSettings.skybox = m_panoramic360CubeMaterial;
                        break;

                    case "180":
                        RenderSettings.skybox = m_panoramic180SphereMaterial;
                        break;
                }
            }

        }

        private double GetTimeLeftOnVideo()
        {
            if (m_videoPlayer.length == 0)
                return -1;

            double elapsedTime = m_videoPlayer.time;
            double length = m_videoPlayer.length;

            return length - elapsedTime;
        }

        private bool HasChoiceBeenMade()
        {
            return m_nextVariant != null;
        }

        public static string GetMediaNameWithoutExtension(string _name)
        {
            return _name.Substring(0, _name.LastIndexOf("."));
        }

        private bool PauseAtEnd(Variant _variant)
        {
            if (_variant.end_behavior == "pause")
                return true;
            else
                return false;
        }

        private void UpdateViewedVariants(string _variantId)
        {
            bool found = false;
            for (int i = 0; i < m_viewedVariants.Count; i++)
            {
                if (m_viewedVariants[i].variantId == _variantId)
                {
                    m_viewedVariants[i] = new ViewedVariant(_variantId);
                    found = true;
                }
                else
                {
                    m_viewedVariants[i] = new ViewedVariant(m_viewedVariants[i].variantId, m_viewedVariants[i].age + 1);
                }
            } // i

            if (!found)
            {
                ViewedVariant viewedVariant = new ViewedVariant(_variantId);
                m_viewedVariants.Add(viewedVariant);
            }
        }

        private JsonNode CreateStornawayJsonNode()
        {
            string jsonString = Resources.Load<TextAsset>(SaveSystem.GetProjectName()).text;
            return JsonNode.Parse(jsonString)!;
        }



        private void SetStartOfVideoVariables()
        {
            m_furthestTime = 0;
            JsonNode operationsNode = m_jsonNode!["variants"]![m_root.GetVariantIndex(m_currentVariant.id)]!["behaviours_jsonlogic"]!;
            if (operationsNode != null)
            {
                operationsNode = m_jsonNode!["variants"]![m_root.GetVariantIndex(m_currentVariant.id)]!["behaviours_jsonlogic"]!["start"]!;
                PerformOperation(operationsNode);
            }
        }

        private void SetEndOfVideoVariables()
        {
            JsonNode operationsNode = m_jsonNode!["variants"]![m_root.GetVariantIndex(m_currentVariant.id)]!["behaviours_jsonlogic"]!;
            if (operationsNode != null)
            {
                operationsNode = m_jsonNode!["variants"]![m_root.GetVariantIndex(m_currentVariant.id)]!["behaviours_jsonlogic"]!["finish"]!;
                PerformOperation(operationsNode);
            }
        }

        private void UpdateSpecificTimeVariables()
        {
            if (m_videoPlayer.time > m_furthestTime)
            {
                string operationKey = GetKeyToUpdateVariables();

                if (operationKey != null)
                {
                    JsonNode operationsNode = m_jsonNode!["variants"]![m_root.GetVariantIndex(m_currentVariant.id)]!["behaviours_jsonlogic"]!["specific_times"]![operationKey]!;
                    PerformOperation(operationsNode);
                }
                m_furthestTime = m_videoPlayer.time;
            }
        }

        public void PerformOperation(JsonNode _operationsNode)
        {
            JsonNode tempNode = _operationsNode!["set"]!;
            if (tempNode != null)
            {
                SetVariables(tempNode);
            }

            tempNode = _operationsNode!["change"]!;

            if (tempNode != null)
            {
                ChangeVariables(tempNode);
            }

            tempNode = _operationsNode["add_up"]!;

            if (tempNode != null)
            {
                AddUpVariables(tempNode);
            }
        }

        private string GetKeyToUpdateVariables()
        {
            if (!SaveSystem.idToTimes.ContainsKey(m_currentVariant.id))
                return null;

            string[] timeKeys = SaveSystem.idToTimes[m_currentVariant.id];

            for (int i = 0; i < timeKeys.Length; i++)
            {
                double timeKeyDouble = double.Parse(timeKeys[i]);
                if (timeKeyDouble > m_furthestTime &&
                    timeKeyDouble <= m_videoPlayer.time)
                {
                    return timeKeys[i];
                }
            }
            return null;
        }

        private void HandleButtonPressedLogic(Variant _nextVariant)
        {
            JsonNode operationsNode = m_jsonNode!["variants"]![m_root.GetVariantIndex(m_currentVariant.id)]!["choices"]!;
            int choiceIndex = -1;
            for (int i = 0; i < operationsNode.AsArray().Count; i++)
            {
                if(operationsNode[i]!["id"].ToString() == m_currentChoice.id)
                {
                    choiceIndex = i;
                    break;
                }
            }


            JsonNode behaviourClickNode = operationsNode[choiceIndex]!["behaviour_blocks_before_click"]!;
            if (behaviourClickNode == null)
                return;

            for (int i = 0; i < behaviourClickNode.AsArray().Count; i++)
            {
                PerformOperation(behaviourClickNode[i]);
                break;
            }
        }

        #region Stornaway Variables
        private void SetVariables(JsonNode _setNode)
        {
            for (int i = 0; i < _setNode.AsArray().Count; i++)
            {
                string variableKey = _setNode[i]!["variables"]![0].ToString()!;
                SaveSystem.m_variablesNode![variableKey] = _setNode[i]!["value"].ToString()!;
            }
        }

        private void ChangeVariables(JsonNode _changeNode)
        {
            for (int i = 0; i < _changeNode.AsArray().Count; i++)
            {
                string variableToChange = _changeNode[i]!["variables"]![0].ToString();
                int variableValue, changeNodeValue;
                variableValue = int.Parse(SaveSystem.m_variablesNode[variableToChange].ToString());
                changeNodeValue = int.Parse(_changeNode[i]!["value"].ToString());
                SaveSystem.m_variablesNode[variableToChange] = (variableValue + changeNodeValue).ToString();
            }
        }

        private void AddUpVariables(JsonNode _addUpNode)
        {         
            string variableToChange = _addUpNode[0]!["target_var"]!.ToString();
            int runningTotal = 0;
            for (int i = 0; i < _addUpNode[0]!["variables"].AsArray().Count; i++)
            {
                runningTotal += int.Parse(SaveSystem.m_variablesNode[_addUpNode[0]!["variables"]![i]!.ToString()].ToString());
            }
            SaveSystem.m_variablesNode[variableToChange] = runningTotal.ToString();
        }
        #endregion
        #endregion
    }
}