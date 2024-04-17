using System.Collections.Generic;
using System.IO;
using System.Security.Policy;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;


namespace Stornaway.Editor
{
    public class JsonImporter : EditorWindow
    {
        private const string TOOL_NAME = "JSON Importer";
        private const string VIDEOS_PATH = "/Resources/Videos";
        private const string IMAGES_PATH = "/Resources/Images";
        private const string BUTTON_IMAGES_PATH = "/Resources /Images/Buttons";
        private const string SEQUENCE_MANAGER = "Sequence Manager";
        private const string SEQUENCE_MANAGER_VR = "Sequence Manager VR";

        private TextAsset m_jsonFile = null;
        private ExportMethod m_playbackMethod = ExportMethod.DOWNLOAD;

        private SequenceManager m_sequenceManager = null;
        private string m_jsonData = "";
        private GameObject c_Go = null;


        #region CUSTOM EDITOR WINDOW
        [MenuItem("Window/" + TOOL_NAME)]
        public static void ShowWindow()
        {
            GetWindow<JsonImporter>(TOOL_NAME);
        }

        private void OnGUI()
        {
            CreateJsonDragAndDrop();
            CreateDownloadStreamDropdownItem();
            CreateImportButton();
            CreateValidateButton();
        }

        private void CreateImportButton()
        {
            // Create a button for the user to be able to initiate the process & check if it was pressed
            if (GUILayout.Button("Import", GUILayout.Height(32)))
            {
                bool success = false;

                Debug.Log("Starting...");

                // 1. Parse
                if (m_jsonFile != null)
                {
                    success = Parse(m_jsonFile.text);

                    if (!success)
                        Debug.LogError(TOOL_NAME + ": Parsing failed");
                }
                else
                    Debug.LogWarning(TOOL_NAME + ": No file was selected!");

                // 2. Generate Data
                DeserialiseJSON();
                

                // 3a. Download the images and videos
                DownloadImages();
                if (m_playbackMethod == ExportMethod.DOWNLOAD)
                {
                    SequenceManager.s_instance.m_playbackMethod = ExportMethod.DOWNLOAD;
                    DownloadVideo();
                }

                // 3b. Stream video
                else
                {
                    SequenceManager.s_instance.m_playbackMethod = ExportMethod.STREAM;

                    if (success)
                        Debug.Log("Success!");
                }

                // Mark scene as dirty
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        private void CreateValidateButton()
        {
            if (GUILayout.Button("Validate Project", GUILayout.Height(32)))
            {
                RunValidationProcess();
            }
        }

        private void CreateJsonDragAndDrop()
        {
            // Drag-and-drop for users JSON file
            m_jsonFile = (TextAsset)EditorGUILayout.ObjectField(m_jsonFile, typeof(TextAsset), true);
        }

        private void CreateDownloadStreamDropdownItem()
        {
            EditorGUILayout.BeginHorizontal();
            m_playbackMethod = (ExportMethod)EditorGUILayout.EnumPopup(m_playbackMethod);
            EditorGUILayout.LabelField(m_playbackMethod == ExportMethod.DOWNLOAD ? " (Offline supported)" : " (Online only)");
            EditorGUILayout.EndHorizontal();
        }
        #endregion


        #region PARSING
        private bool Parse(string _data)
        {
            bool success = true;

            // Islands Translation
            if (!ParseIslands(ref _data))
            {
                Debug.LogError(TOOL_NAME + ": Translating Islands failed");
                success = false;
            }
            // Variants Translation
            if (!ParseVariants(ref _data))
            {
                Debug.LogError(TOOL_NAME + ": Translating Variants failed");
                success = false;
            }
            // Linked Variants Translation
            if (!ParseLinkedVariants(ref _data))
            {
                Debug.LogError(TOOL_NAME + ": Translating Linked_Variants failed");
                success = false;
            }


            // End of operation
            if (success)
            {
                m_jsonData = _data;
                File.WriteAllText(Application.dataPath + "/Resources/" + SaveSystem.GetProjectName() + ".JSON", m_jsonData);
                AssetDatabase.Refresh();
            }

            return success;
        }

        private bool ParseIslands(ref string _data)
        {
            string toReplace = "\"variants\":";

            // 1.1 Add opening square bracket
            int startIndex = _data.IndexOf(toReplace) + toReplace.Length;
            _data = _data.Insert(startIndex, "[");

            // 1.2. Add closing square bracket
            int match = GetIndexOfMatchingCurlyBracket(_data, startIndex);
            _data = _data.Insert(match + 1, "]");

            // 1.3. Remove curly brackets
            _data = _data.Remove(match, 1);
            _data = _data.Remove(startIndex + 1, 1);

            return true;
        }
        
        private bool ParseVariants(ref string _data)
        {
            string phrase = "\"variants\":";
            Stack<int> indices = new Stack<int>();
            int i = _data.IndexOf(phrase) + phrase.Length;
            bool done = true;

            while (i < _data.Length)
            {
                if (!done)
                {
                    int variantEnd = _data.LastIndexOf("\"", i - 1);
                    int variantStart = _data.LastIndexOf("\"", variantEnd - 2) + 1;

                    string word = _data.Substring(variantStart, variantEnd - variantStart);

                    // 2.1. Remove variants
                    /// 01/08/2023
                    /// Checking for word 'JsonLogic_project_variables' to avoid removing it
                    /// and any further variables that might be important in the future. This should not affect variants
                    /// if the word is found, pop the indices to say we know there is an ending curly bracket (we assume)
                    /// then break
                    if (word != "jsonlogic_project_variables")
                        _data = _data.Remove(variantStart - 1, variantEnd - variantStart + 3);
                    else
                    {
                        indices.Pop();
                        break;
                    }

                    done = true;
                }
                else if (_data[i] == '{')
                {
                    indices.Push(i);

                    if (indices.Count == 1)
                        done = false;
                }
                else if (_data[i] == '}')
                {
                    if (indices.Count > 0)
                        indices.Pop();
                    else
                        return true;
                }

                i++;
            } // while

            if (indices.Count != 0)
            {
                Debug.LogError(TOOL_NAME + ": Get matching curly bracket failed. Could be too many opening brackets?");
                return false;
            }

            return true;
        }

        private bool ParseLinkedVariants(ref string _data)
        {
            string toReplace = "linked_variants";
            string replaceWith = "linkedVariants";

            // 3.1. Rename "linked_variants" to "linkedVariant"
            _data = _data.Replace(toReplace, replaceWith);
            int i = 0;

            while (i < _data.Length)
            {
                i = _data.IndexOf(replaceWith, i);

                // If no more linkedVariants found, end
                if (i < 0)
                    break;

                // Find the first curly bracket after "linked_variants"
                i += replaceWith.Length + 1;
                i = _data.IndexOf("{", i);

                if (_data[i + 1] != '\"')
                {
                    continue;
                }

                // Find matching bracket, then replace both with square brackets
                int match = GetIndexOfMatchingCurlyBracket(_data, i);
                _data = _data.Remove(match, 1);
                _data = _data.Insert(match, "]");
                _data = _data.Remove(i, 1);
                _data = _data.Insert(i, "[");
                i++;

                
                // 3.2. Remove id's from all linked variants
                while(i < _data.Length && _data[i] != ']')
                {
                    // Located a linked variant
                    if(_data[i] == '{')
                    {
                        for(int j = i-1; j > 0; j--)
                        {
                            if (_data[j] == '[' || _data[j] == ',')
                                break;
                            else
                                _data = _data.Remove(j, 1);
                        } // i
                    }
                    i++;
                }

            } // while
            return true;
        }
        #endregion


        #region DESERIALISATION
        private void DeserialiseJSON()
        {
            InitialiseSequenceManager();
            m_sequenceManager.m_root = JsonUtility.FromJson<Root>(m_jsonData);
            RenameVideosAndImages();
            StoreImageAndVideoNames();
        }

        private void StoreImageAndVideoNames()
        {
            for(int i = 0; i < SequenceManager.s_instance.m_root.variants.Count; i++)
            {
                if (SequenceManager.s_instance.m_root.variants[i].media_alternative_sources.Count > 0)
                {
                    string videoName = GetMediaNameFromURL(SequenceManager.s_instance.m_root.variants[i].media_alternative_sources[0].url);
                    SequenceManager.s_instance.m_root.variants[i].mediaName = videoName;
                }
                else if(!string.IsNullOrEmpty(SequenceManager.s_instance.m_root.variants[i].image.url))
                {
                    string url = SequenceManager.s_instance.m_root.variants[i].image.url;
                    string imageName = url.Substring(url.LastIndexOf("/") + 1);
                    SequenceManager.s_instance.m_root.variants[i].mediaName = imageName;
                }
            } // i
        }

        private void InitialiseSequenceManager()
        {
            // Find a game object with the 'SequenceManager' script attached
            m_sequenceManager = FindObjectOfType<SequenceManager>();

            // If none found, create one
            if (!m_sequenceManager)
                InstantiateSequenceManager();

            if (!SequenceManager.s_instance)
                SequenceManager.s_instance = m_sequenceManager;
        }

        private void InstantiateSequenceManager()
        {
            if (Camera.main)
                DestroyImmediate(Camera.main.gameObject);

            c_Go = Resources.Load("Stornaway/" + SEQUENCE_MANAGER_VR) as GameObject;
            if (c_Go)
            {
                m_sequenceManager = Instantiate(Resources.Load("Stornaway/" + SEQUENCE_MANAGER_VR) as GameObject).GetComponent<SequenceManager>();
                m_sequenceManager.gameObject.name = "Sequence Manager VR";
            }
            else
            {
                m_sequenceManager = Instantiate(Resources.Load("Stornaway/" + SEQUENCE_MANAGER) as GameObject).GetComponent<SequenceManager>();
                m_sequenceManager.gameObject.name = "Sequence Manager";
            }
        }
        #endregion


        #region DOWNLOADER
        private async void DownloadVideo()
        {
            string filePath = Application.dataPath + VIDEOS_PATH + "/";

            for (int i = 0; i < m_sequenceManager.m_root.variants.Count; i++)
            {
                // If the url contains /PLACEHOLDER/ its likely there is no video, so e can skip
                if(m_sequenceManager.m_root.variants[i].media_url.Contains("/PLACEHOLDER/"))
                {
                    Debug.Log("No video found for: " + m_sequenceManager.m_root.variants[i].mediaName);
                    continue;
                }

                // Skip downloading this particular video if it already exists in the videos folder
                if (File.Exists(filePath + m_sequenceManager.m_root.variants[i].mediaName))
                {
                    Debug.Log("Video already exists for: " + m_sequenceManager.m_root.variants[i].mediaName);
                    continue;
                }
                

                // Get the download link using 'media alternative sources'
                string url = m_sequenceManager.m_root.variants[i].media_alternative_sources[0].url;


                // Begin the download
                UnityWebRequest www = UnityWebRequest.Get(url);
                var operation = www.SendWebRequest();

                // Await download completion, also display loading progression here
                while (!operation.isDone)
                {
                    float percent = operation.progress;
                    if (EditorUtility.DisplayCancelableProgressBar("Downloading Project Video Files", (i + 1).ToString() + "/" + m_sequenceManager.m_root.variants.Count, percent))
                    {
                        EditorUtility.ClearProgressBar();
                        AssetDatabase.Refresh();
                        return;
                    }
                    await Task.Yield();
                }
                EditorUtility.ClearProgressBar();

                // Create the appropriate folders for given directory if they don't already exist
                System.IO.FileInfo file = new System.IO.FileInfo(filePath);
                file.Directory.Create();

                // Save the download to disk
                File.WriteAllBytes(filePath + m_sequenceManager.m_root.variants[i].mediaName, www.downloadHandler.data);

                // Print out success and failures to the console
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log($"Failed: { www.error }");
                    Debug.Log(i + "/" + m_sequenceManager.m_root.variants.Count);
                    AssetDatabase.Refresh();
                    return;
                }
            } // i
            AssetDatabase.Refresh();
            Debug.Log("Success! Video files imported to \"" + VIDEOS_PATH + "\"");
        }

        private async void DownloadImages()
        {
            string filePath = Application.dataPath + IMAGES_PATH + "/";
            int numImages = 0;

            for (int i = 0; i < m_sequenceManager.m_root.variants.Count; i++)
            {
                DownloadButtonImages(m_sequenceManager.m_root.variants[i], true);
                DownloadButtonImages(m_sequenceManager.m_root.variants[i], false);
                // Skip downloading this particular video if it already exists in the videos folder
                if (File.Exists(filePath + m_sequenceManager.m_root.variants[i].mediaName))
                {
                    Debug.Log("Image already exists for: " + m_sequenceManager.m_root.variants[i].mediaName);
                    continue;
                }

                // Get the download link
                string url = m_sequenceManager.m_root.variants[i].image.url;
                string imageName = "";

                if (!string.IsNullOrEmpty(m_sequenceManager.m_root.variants[i].image.url))
                    imageName = url.Substring(url.LastIndexOf("/") + 1);
                else
                    continue;

                // Begin the download
                UnityWebRequest www = UnityWebRequest.Get(url);
                var operation = www.SendWebRequest();

                // Await download completion, also display loading progression here
                while (!operation.isDone)
                {
                    float percent = operation.progress;
                    if (EditorUtility.DisplayCancelableProgressBar("Downloading Project Image Files", (i + 1).ToString() + "/" + m_sequenceManager.m_root.variants.Count, percent))
                    {
                        EditorUtility.ClearProgressBar();
                        AssetDatabase.Refresh();
                        return;
                    }
                    await Task.Yield();
                }
                EditorUtility.ClearProgressBar();

                // Create the appropriate folders for given directory if they don't already exist
                System.IO.FileInfo file = new System.IO.FileInfo(filePath);
                file.Directory.Create();

                // Save the download to disk
                File.WriteAllBytes(filePath + m_sequenceManager.m_root.variants[i].mediaName, www.downloadHandler.data);

                // Print out success and failures to the console
                if (www.result != UnityWebRequest.Result.Success)
                {
                    numImages++;
                    Debug.Log($"Failed: { www.error }");
                    Debug.Log(i + "/" + m_sequenceManager.m_root.variants.Count);
                    AssetDatabase.Refresh();
                    return;
                }
            } // i
            AssetDatabase.Refresh();

            if(numImages > 0)
                Debug.Log("Success! Image files imported to \"" + IMAGES_PATH + "\"");
        }


        private async void DownloadButtonImages(Variant _variant, bool _activeImage)
        {
            string filePath = Application.dataPath + BUTTON_IMAGES_PATH + "/";

            for (int i = 0; i < _variant.choices.Count; i++)
            {

                
                string buttonImageName = "";

                if (!_activeImage)
                {
                    buttonImageName = GetMediaNameFromURL(_variant.choices[i].image_url);
                    // Skip if no button image
                    if (string.IsNullOrEmpty(_variant.choices[i].image_url))
                        continue;
                }
                else
                {
                    buttonImageName = GetMediaNameFromURL(_variant.choices[i].image_active_url);
                    // Skip if no button image
                    if (string.IsNullOrEmpty(_variant.choices[i].image_active_url))
                        continue;
                }

                // Skip downloading this particular video if it already exists in the videos folder
                if (File.Exists(filePath + buttonImageName))
                {
                    Debug.Log("Image already exists for: " + buttonImageName);
                    continue;
                }

                // Begin the download
                UnityWebRequest www = UnityWebRequest.Get(_variant.choices[i].image_url);
                var operation = www.SendWebRequest();

                // Await download completion, also display loading progression here
                while (!operation.isDone)
                {
                    float percent = operation.progress;
                    if (EditorUtility.DisplayCancelableProgressBar("Downloading Project Image Files", "Downloading Button Image", percent))
                    {
                        EditorUtility.ClearProgressBar();
                        AssetDatabase.Refresh();
                        return;
                    }
                    await Task.Yield();
                }
                EditorUtility.ClearProgressBar();

                // Create the appropriate folders for given directory if they don't already exist
                System.IO.FileInfo file = new System.IO.FileInfo(filePath);
                file.Directory.Create();

                // Save the download to disk
                File.WriteAllBytes(filePath + buttonImageName, www.downloadHandler.data);

                // Print out success and failures to the console
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log($"Failed: {www.error}");
                    //Debug.Log(i + "/" + m_sequenceManager.m_root.variants.Count);
                    AssetDatabase.Refresh();
                    return;
                }
                else
                {
                    AssetDatabase.Refresh();
                }
            } // i
        }
        #endregion


        #region RENAMING
        private void RenameVideosAndImages()
        {
            RenameMedia(VIDEOS_PATH);
            RenameMedia(IMAGES_PATH);
        }

        private void RenameMedia(string _path)
        {
            if (!AssetDatabase.IsValidFolder($"Assets/" + _path))
                return;

            DirectoryInfo dir = new DirectoryInfo(Application.dataPath + _path + "/");
            FileInfo[] info = dir.GetFiles("*.*");
            foreach (FileInfo f in info)
            {
                string newName = f.Name.ToUpper();

                if (newName.Contains("DEFAULTVIDEO."))
                    break;

                newName = newName.Replace(' ', '_');
                f.MoveTo(Application.dataPath + _path + "/" + newName);
            }

            AssetDatabase.Refresh();
        }
        #endregion


        #region PRIVATE METHODS
        private string ConvertM3u8ToMp4(string _url)
        {
            _url = _url.Replace("/HLS/", "/SOURCE/");
            _url = _url.Replace(".m3u8", ".mp4");
            return _url;
        }

        private int GetIndexOfMatchingCurlyBracket(string _data, int _openBracketIndex)
        {
            Stack<int> indices = new Stack<int>();

            for (int i = _openBracketIndex; i < _data.Length; i++)
            {
                switch (_data[i])
                {
                    case '{':
                        indices.Push(i);
                        break;

                    case '}':
                        if (indices.Count == 1)
                            return i;
                        else
                            indices.Pop();
                        break;
                }
            } // i

            Debug.LogError(TOOL_NAME + ": Get matching curly bracket failed. Could be too many opening brackets?");
            return -1;
        }
        
        private string GetMediaNameFromURL(string _url)
        {
            string answer = _url;

            // Remove all the slashes to leave only the last part of the url
            answer = answer.Substring(answer.LastIndexOf("/") + 1);

            // Remove the first 2 sections of whats left to leave only the video name
            // [PROJECTNAME]_[UID]_videoName
            for (int i = 0; i < 2; i++)
            {
                answer = answer.Substring(answer.IndexOf("_") + 1);
            } // i

            return answer;
        }


        private static string RemoveWhiteSpace(string _data)
        {
            _data = _data.Replace(" ", "");
            _data = _data.Replace("\r", "");
            _data = _data.Replace("\n", "");
            return _data;
        }

        private void RunValidationProcess()
        {
            string filePath = $"Assets/" + VIDEOS_PATH;
            SequenceManager sequenceManager = GetSequenceManager();

            // STREAMING ----------------------------------------------------------------------------
            // --------------------------------------------------------------------------------------
            // For streaming, check media alternate sources is valid
            if (sequenceManager.m_playbackMethod == ExportMethod.STREAM)
            {
                for (int i = 0; i < sequenceManager.m_root.variants.Count; i++)
                {
                    if (sequenceManager.m_root.variants[i].media_alternative_sources == null ||
                        sequenceManager.m_root.variants[i].media_alternative_sources.Count <= 0)
                    {
                        Debug.LogWarning("Island " + sequenceManager.m_root.variants[i].id + " has no URL, a default video will be used instead.");
                    }
                } // i
            }

            // DOWNLOAD ----------------------------------------------------------------------------
            // --------------------------------------------------------------------------------------
            // For download, check video exists in videos folder
            else if (sequenceManager.m_playbackMethod == ExportMethod.DOWNLOAD)
            {
                if (!AssetDatabase.IsValidFolder(filePath))
                {
                    Debug.Log(filePath);
                    Debug.LogError("No videos folder exists. Have you ran the import process?");
                    return;
                }

                for (int j = 0; j < sequenceManager.m_root.variants.Count; j++)
                {
                    if (!File.Exists(Application.dataPath + VIDEOS_PATH + "/" + m_sequenceManager.m_root.variants[j].mediaName) &&
                        !File.Exists(Application.dataPath + IMAGES_PATH + "/" + m_sequenceManager.m_root.variants[j].mediaName))
                    {
                        Debug.LogWarning("Island " + sequenceManager.m_root.variants[j].id + " is missing media. Could be either a missing video or image.");
                    }
                } // j
            }

            // UNUSED VIDEO -------------------------------------------------------------------------
            // --------------------------------------------------------------------------------------
            // Check if there are any unused videos in the videos folder

            if (AssetDatabase.IsValidFolder(filePath))
            {
                DirectoryInfo dir = new DirectoryInfo(filePath);
                FileInfo[] info = dir.GetFiles("*.*");
                foreach (FileInfo f in info)
                {
                    string fileName = f.Name;
                    if (fileName.Contains("DefaultVideo.") || fileName.ToUpper().Contains(".META"))
                        continue;

                    bool found = false;
                    for(int k = 0; k < sequenceManager.m_root.variants.Count; k++)
                    {
                        if (sequenceManager.m_root.variants[k].mediaName != null && 
                            fileName.ToUpper() == sequenceManager.m_root.variants[k].mediaName.ToUpper())
                        {
                            found = true;
                            break;
                        }
                    } // k

                    if (!found)
                        Debug.LogWarning(fileName + " is not in use.");
                }
            }

            RenameVideosAndImages();
            Debug.Log("Validation Complete.");
        }

        private SequenceManager GetSequenceManager()
        {
            return FindObjectOfType<SequenceManager>();
        }
        #endregion
    }
}