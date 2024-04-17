using System.Collections;
using System.Collections.Generic;
using TMPro;
//using Stornaway;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Stornaway
{
    public class VideoManager : MonoBehaviour
    {
        public static VideoManager v_instance = null;

        public float m_displayForTimeInSeconds = 3;
        public GameObject m_closeButton = null;
        [Space(5)]
        public GameObject m_progressBarBGObject = null;
        public GameObject m_progressBarObject = null;
        public GameObject m_progressBarBackgroundObject = null; // This is different to m_progressBarBGObject
        public GameObject m_volumeSliderContainer = null;
        public GameObject m_videoControls = null;
        public GameObject m_videoAnimParent = null;
        [Space(5)]
        public UnityEngine.UI.Image m_currentTimeIndicator = null;
        public UnityEngine.UI.Image m_seekingTimeIndicator = null;
        public UnityEngine.UI.Image m_progressHandle = null;
        public UnityEngine.UI.Image m_volumeButtonIcon = null;
        public UnityEngine.UI.Image m_playPauseIcon = null;
        [Space(5)]
        public Slider m_progressBar = null;
        public Slider m_volumeBar = null;
        [Space(5)]
        public TextMeshProUGUI m_seekTimeText = null;
        public TextMeshProUGUI m_currentTimeText = null;
        [Space(5)]
        public Sprite m_playSprite = null;
        public Sprite m_pauseSprite = null;
        public Sprite m_audioMutedSoundSprite = null;
        public Sprite m_audioLowSoundSprite = null;
        public Sprite m_audioMidSoundSprite = null;
        public Sprite m_audioFullSoundSprite = null;

        private Vector2 mousePosition = Vector2.zero;
        private float m_currentVolume = 1;              // The current volume. We could use the videoPlayer's volume, however, when you mute and then unmute, it would not go back to how the user last set it.
        private bool m_audioMuted = false;              // If the audio is muted or not.
        private bool m_UIhidden = false;                // If the UI is hidden or not.
        private bool m_scrubbing = false;               // If we're scrubbing (Dragging).
        private bool m_hoverOverUI = false;             // If we're hovering over the UI.
        private bool m_videoTimeChanged = false;        // If the video time has changed.

        private float m_leftX = -float.MaxValue;
        private float m_rightX = -float.MaxValue;
        private float m_timeClicked = 0;


        #region UNITY CALLBACKS

        void Start()
        {
            SequenceManager.s_instance.clickedAction.started += ctx => OnHUDPressed();
            SequenceManager.s_instance.playPauseAction.performed += ctx => OnHUDClicked(true);
            SequenceManager.s_instance.debugAction.started += ctx => QA.QA.SwitchQA();

            // Add our events to the videoPlayer's event handler.
            SequenceManager.s_instance.m_videoPlayer.started += OnVideoStarted;
			SequenceManager.s_instance.m_videoPlayer.loopPointReached += OnVideoEnded;
			SequenceManager.s_instance.m_videoPlayer.seekCompleted += OnSeekCompleted;

            m_timeClicked = Time.time;

            // Calculate left and right from this.

            Vector3[] corners = new Vector3[4];

            float halfWidth = m_progressBar.GetComponent<RectTransform>().sizeDelta.x / 2;

            m_leftX = halfWidth * -1;
            m_rightX = halfWidth;
        }
        
        void Update()
        {
            mousePosition = SequenceManager.s_instance.mousePositionAction.ReadValue<Vector2>();

            if (m_videoTimeChanged)
            {
                // No longer setting m_videoTimeChanged as false here. Going to set it as false when the video has been seeked.
                return;
            }

            if (SequenceManager.s_instance.m_imageDisplay.IsActive())
            {
                // I'm opting for disabling all the children as, if I turn off the game object here, the script stops working.
                foreach (Transform child in transform)
                    child.gameObject.SetActive(false);
            }
            else if (!SequenceManager.s_instance.m_videoPlayer.isPaused)
            {
                foreach (Transform child in transform)
                    child.gameObject.SetActive(true);

                m_progressBar.value = (float)SequenceManager.s_instance.m_videoPlayer.time;

                int minutes = Mathf.FloorToInt((float)(SequenceManager.s_instance.m_videoPlayer.time / 60F));
                int seconds = Mathf.FloorToInt((float)(SequenceManager.s_instance.m_videoPlayer.time - minutes * 60));

                m_currentTimeText.SetText(string.Format("{0:00}:{1:00}", minutes, seconds));

                if (!SequenceManager.s_instance.m_videoPlayer.GetDirectAudioMute(0))
                {
                    if (m_currentVolume == 0)
                    {
                        m_volumeButtonIcon.sprite = m_audioMutedSoundSprite;
                    }
                    else if (m_currentVolume > 0 & m_currentVolume <= .33)
                    {
                        m_volumeButtonIcon.sprite = m_audioLowSoundSprite;
                    }
                    else if (m_currentVolume > .33 & m_currentVolume <= .66)
                    {
                        m_volumeButtonIcon.sprite = m_audioMidSoundSprite;
                    }
                    else
                    {
                        m_volumeButtonIcon.sprite = m_audioFullSoundSprite;
                    }
                }
            }

            float currentTime = Time.time;

            if ((currentTime - (m_timeClicked)) >= m_displayForTimeInSeconds && !m_UIhidden && SequenceManager.s_instance.m_videoPlayer.isPlaying)
            {
                m_videoAnimParent.GetComponent<Animator>().SetTrigger("Exit");
                m_closeButton.GetComponent<Animator>().SetTrigger("Exit");
                m_UIhidden = true;
                

                StartCoroutine("HideAfterTime");
            }
            
            if (!m_hoverOverUI)
            {
                m_seekingTimeIndicator.GetComponent<RectTransform>().position = new Vector3(
                    m_progressHandle.GetComponent<RectTransform>().position.x, 
                    m_seekingTimeIndicator.GetComponent<RectTransform>().position.y, 
                    m_seekingTimeIndicator.GetComponent<RectTransform>().position.z);
            }

            m_currentTimeIndicator.GetComponent<RectTransform>().position = new Vector3(
                m_progressHandle.GetComponent<RectTransform>().position.x, 
                m_currentTimeIndicator.GetComponent<RectTransform>().position.y, 
                m_currentTimeIndicator.GetComponent<RectTransform>().position.z);
        }

        void Awake()
        {
            if (!v_instance)
                v_instance = this;
        }

		#endregion

		#region PUBLIC METHODS
		public void MuteUnmuteVideo()
        {
            if (m_audioMuted)
            {
                SequenceManager.s_instance.m_videoPlayer.SetDirectAudioMute(0, false);
                SequenceManager.s_instance.m_videoPlayer.SetDirectAudioVolume(0, m_currentVolume);
                m_volumeBar.value = m_currentVolume;
                m_audioMuted = false;
            }
            else
            {
                SequenceManager.s_instance.m_videoPlayer.SetDirectAudioMute(0, true);
                m_volumeButtonIcon.sprite = m_audioMutedSoundSprite;
                m_volumeBar.value = 0;
                m_audioMuted = true;
            }
        }

        IEnumerator HideAfterTime()
        {
            yield return new WaitForSeconds(.5f);
            StopCoroutine("HideAfterTime");
        }

        public void PlayPauseVideo()
        {
            m_timeClicked = Time.time;

            m_seekingTimeIndicator.gameObject.SetActive(false);

            if (!SequenceManager.s_instance.m_videoPlayer.isPaused)
            {
                SequenceManager.s_instance.m_videoPlayer.Pause();
                m_playPauseIcon.sprite = m_playSprite;
            }
            else
            {
                SequenceManager.s_instance.m_videoPlayer.Play();
                m_playPauseIcon.sprite = m_pauseSprite;
            }
        }

        public void OnExitButtonClicked()
        {
            if (SceneManager.GetActiveScene().buildIndex == 0)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
            }
            else
                SceneManager.LoadScene(0);
        }
        

        public void OnProgressBarClick()
        {
            SequenceManager.s_instance.m_videoPlayer.Pause();
            
            SequenceManager.s_instance.m_videoPlayer.time = m_progressBar.value;

            int minutes = Mathf.FloorToInt(m_progressBar.value / 60F);
            int seconds = Mathf.FloorToInt(m_progressBar.value - minutes * 60);

            m_currentTimeText.SetText(string.Format("{0:00}:{1:00}", minutes, seconds));

            m_timeClicked = Time.time;
            m_scrubbing = true;
        }

        public void OnProgressBarRelease()
        {
            SequenceManager.s_instance.m_videoPlayer.time = m_progressBar.value;
            OnSeekCompleted(SequenceManager.s_instance.m_videoPlayer);
            m_scrubbing = false;
            m_videoTimeChanged = true;
        }
        
        protected void OnHUDPressed()
        {
            m_timeClicked = Time.time;
        }
        
        public void OnHUDClicked(bool _forced = false)
        {
            if (!_forced)
            {
                if (Time.time - m_timeClicked > 0.2f)
                    return;
                
                m_timeClicked = Time.time;
            }

            if (m_UIhidden && SequenceManager.s_instance.m_videoPlayer.isPlaying)
            {
                m_videoAnimParent.GetComponent<Animator>().SetTrigger("Enter");
                m_closeButton.GetComponent<Animator>().SetTrigger("Enter");
                m_UIhidden = false;

                if (_forced)
                    PlayPauseVideo();
            }
            else
            {
                if (!SequenceManager.s_instance.IsCurrentVideoFinished())
                {
                    PlayPauseVideo();
                    m_timeClicked = Time.time;
                }
            }
        }

        public void OnProgressBarHover()
        {
            if (!m_scrubbing)
                m_currentTimeIndicator.gameObject.SetActive(true);

            m_seekingTimeIndicator.gameObject.SetActive(true);

            m_hoverOverUI = true;

            StartCoroutine("CheckMousePos");
        }

        private IEnumerator CheckMousePos()
        {
            while (true)
            {
                float mousePositionX = mousePosition.x;
                
                float alpha = Mathf.InverseLerp(m_leftX, m_rightX, m_seekingTimeIndicator.GetComponent<RectTransform>().anchoredPosition.x);

                float value = Mathf.Lerp(0, (float)SequenceManager.s_instance.m_videoPlayer.length, alpha);

             
                float time = GetHighlightedValueOnSlider(m_progressBar);
                int minutes = (int)time / 60;
                int seconds = (int)time % 60;

                m_seekTimeText.SetText(string.Format("{0:00}:{1:00}", minutes, seconds));
                
                m_seekingTimeIndicator.GetComponent<RectTransform>().localPosition = new Vector3(
                    GetHighlightedPositionOnSlider(m_progressBar).x, 
                    m_seekingTimeIndicator.GetComponent<RectTransform>().localPosition.y, 
                    0);

                yield return null;
            }
        }

        public void OnProgressBarNoHover()
        {
            m_currentTimeIndicator.gameObject.SetActive(false);
            m_seekingTimeIndicator.gameObject.SetActive(false);

            m_hoverOverUI = false;

            StopCoroutine("CheckMousePos");
        }

        public void OnVolumeHover()
        {
            m_progressBarBGObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1300, 40);
            m_progressBarObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1275, 20);
            m_volumeSliderContainer.SetActive(true);
        }

        public void OnVolumeLeftHover()
        {
            m_progressBarBGObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1400, 40);
            m_progressBarObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1375, 20);
            m_volumeSliderContainer.SetActive(false);

            m_timeClicked = Time.time;
        }

        public void OnVolumeDrag()
        {
            SetVideoVolume(m_volumeBar.value);
        }

        public void OnVolumeClick()
        {
            SetVideoVolume(m_volumeBar.value);
        }

        public void NextVideo()
        {
            SequenceManager.s_instance.SetActiveMedia(SequenceManager.s_instance.GetDefaultChoice(SequenceManager.s_instance.m_currentVariant), true);
        }

        public void LastVideo()
        {
            Variant _variant = SequenceManager.s_instance.GetPreviousVideo();

            if (_variant != null)
            {
                SequenceManager.s_instance.SetActiveMedia(_variant, false);
            }
            else
            {
                //SequenceManager.s_instance.SetActiveMedia(SequenceManager.s_instance.m_currentVariant);
            }
        }

        public void OpenStornawayInBrowser()
        {
            Application.OpenURL("https://www.stornaway.io");
        }

        #endregion

        #region PRIVATE METHODS
        private void SetVideoVolume(float _volume)
        {
            SequenceManager.s_instance.m_videoPlayer.SetDirectAudioVolume(0, _volume);
            m_currentVolume = _volume;
        }

        private void OnVideoStarted(UnityEngine.Video.VideoPlayer _source)
        {
            m_progressBar.maxValue = (float) SequenceManager.s_instance.m_videoPlayer.length;
            m_timeClicked = Time.time;

            if (SequenceManager.s_instance.m_videoPlayer.isPaused)
            {
                m_playPauseIcon.sprite = m_playSprite;
            }
            else
            {
                m_playPauseIcon.sprite = m_pauseSprite;
            }

            if (m_audioMuted)
            {
                SequenceManager.s_instance.m_videoPlayer.SetDirectAudioMute(0, true);
            }
            else
            {
                SequenceManager.s_instance.m_videoPlayer.SetDirectAudioMute(0, false);
                SequenceManager.s_instance.m_videoPlayer.SetDirectAudioVolume(0, m_currentVolume);
            }
        }

        private void OnVideoEnded(UnityEngine.Video.VideoPlayer _source)
        {
            if (!m_UIhidden)
            {
                m_videoAnimParent.GetComponent<Animator>().SetTrigger("Exit");
                m_closeButton.GetComponent<Animator>().SetTrigger("Exit");
                m_UIhidden = true;
            }
        }

        private void OnSeekCompleted(UnityEngine.Video.VideoPlayer _source)
        {
            if (m_videoTimeChanged)
            {
                SequenceManager.s_instance.m_videoPlayer.Play();
                m_videoTimeChanged = false;
            }
        }

        private Vector2 GetHighlightedPositionOnSlider(Slider _slider)
        {
            Vector2 answer = Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(((RectTransform)_slider.transform), mousePosition, Camera.main, out answer);
            return answer;
        }

        private float GetHighlightedValueOnSlider(Slider _slider)
        {
            float answer = 0;

            Vector2 relativeMousePosition = GetHighlightedPositionOnSlider(_slider);
            answer = relativeMousePosition.x / ((RectTransform)_slider.transform).offsetMax.x;
            answer = (answer + 1) / 2;
            answer = Mathf.Clamp(answer * _slider.maxValue, 0, _slider.maxValue);

            return answer;
        }
        #endregion
    }
}