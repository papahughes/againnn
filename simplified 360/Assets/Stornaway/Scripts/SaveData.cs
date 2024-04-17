using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Stornaway
{
    [System.Serializable]
    public class SaveData
    {
        public string currentVariant;
        public string[] variantHistory;
        public string variables;

        public SaveData(string _currentVariant, string[] _variantHistory, JsonNode _dataNode) 
        { 
            currentVariant = _currentVariant;
            variantHistory = _variantHistory;
            variables = _dataNode?.ToString();
        }
    }
}