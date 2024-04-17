using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.Json.Nodes;
using UnityEngine;
using UnityEngine.InputSystem.Interactions;

namespace Stornaway
{
    public static class SaveSystem
    {
        private static string m_path = Application.persistentDataPath + "/" + GetProjectName() + ".sav";

        public static string m_currentVariant;
        public static string[] m_variantHistory;
        public static JsonNode m_variablesNode;
        public static Dictionary<string, string[]> idToTimes = new Dictionary<string, string[]>();
        // Variant Id -> Specific Times : logic nodes


        public static void Save(string _currentVariant, string[] _variantHistory)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            FileStream stream = new FileStream(m_path, FileMode.Create);

            SaveData saveData = new SaveData(_currentVariant, _variantHistory, m_variablesNode);

            formatter.Serialize(stream, saveData);
            stream.Close();
        }

        public static void ClearSave()
        {
            m_currentVariant = null;
            m_variantHistory = null;
            m_variablesNode = null;
            InitVariables();
        }

        public static SaveData Load()
        {
            InitVariables();

            if(File.Exists(m_path)) 
            {
                BinaryFormatter formatter = new BinaryFormatter();
                FileStream stream = new FileStream(m_path, FileMode.Open);
                SaveData saveData = null;

                if (stream.Length > 0)
                {
                    saveData = formatter.Deserialize(stream) as SaveData;
                    m_currentVariant = saveData.currentVariant;
                    m_variantHistory = saveData.variantHistory;
                    m_variablesNode = saveData.variables != null? JsonNode.Parse(saveData.variables) : null;
                }

                stream.Close();

                if (saveData == null)
                    Debug.LogWarning("Save is empty");

                return saveData;
            }
            else
            {
                Debug.Log("Save data not found");
                return null; 
            }
        }

        public static string GetProjectName()
        {
            return Application.productName;
            //string[] s = Application.dataPath.Split('/');
            //return s[s.Length - 2];
        }


        #region PRIVATE


        private static void InitVariables()
        {
            idToTimes.Clear();
            string jsonString = Resources.Load<TextAsset>(GetProjectName()).text;
            JsonNode tempNode = JsonNode.Parse(jsonString)!;
            m_variablesNode = tempNode["jsonlogic_project_variables"]!;

            tempNode = JsonNode.Parse(jsonString);
            
            for(int i = 0 ; i < tempNode["variants"].AsArray().Count; i++)
            {
                //Debug.Log(tempNode!["variants"]![i]!["id"]!.ToString());
                JsonNode timesNode = tempNode["variants"]![i]!["behaviours_jsonlogic"]!;
                if (timesNode != null)
                    timesNode = tempNode["variants"]![i]!["behaviours_jsonlogic"]!["specific_times"]!;

                if(timesNode != null)
                    idToTimes.Add(tempNode!["variants"]![i]!["id"]!.ToString(), GetVariantTimes(timesNode));
            } 
        }

        private static string[] GetVariantTimes(JsonNode _specificTimesNode)
        {
            //Debug.Log(_specificTimesNode.ToString());
            List<string> keyList = new List<string>();
            int scope = 0;
            string specificTimesString = _specificTimesNode.ToString();

            for (int i = 0; i < specificTimesString.Length; i++)
            {
                // if scope = 0 check for "
                // if { scope ++
                // if } scope --
                
                if (scope == 1 && specificTimesString[i] == '"')
                {
                    i++;
                    int j = i;
                    while (true)
                    {
                        j++;

                        if (specificTimesString[j] == '"')
                        {
                            break;
                        }// if


                        if(j > 256)
                        {
                            Debug.LogError("No closing quotation found on specific time");
                            break;
                        }// if

                    }// while

                    keyList.Add(specificTimesString.Substring(i, j-i));
                    i = j;
                }// if
                else if(specificTimesString[i] == '{')
                {
                    scope++;
                }// else if
                else if(specificTimesString[i] == '}')
                {
                    scope--;
                }// else if

            }// for
            return keyList.ToArray();
        }

        #endregion
    }
}