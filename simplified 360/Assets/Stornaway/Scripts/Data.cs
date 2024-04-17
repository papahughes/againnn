// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
using System.Collections.Generic;


namespace Stornaway
{
    public enum ExportMethod
    {
        STREAM,
        DOWNLOAD
    }

    [System.Serializable]
    public struct ViewedVariant
    {
        public string variantId { get; set; }
        public int age { get; set; }

        public ViewedVariant(string _variantId, int _age = 0)
        {
            variantId = _variantId;
            age = _age;
        }
    }

    [System.Serializable]
    public class Timer
    {
        public bool show;
        public bool invert_colors;
        public bool transparent_background;
    }

    [System.Serializable]
    public class Prompt
    {
        public string text;
        public string style;
        public Timer timer;
        public bool hidden;
        public bool hotspot;
        public string substyle;
        public string display_at;
        public string text_color;
        public string background_color;
    }

    [System.Serializable]
    public class LinkedVariant
    {
        public LinkedVariant(string _id, string _label)
        {
            id = _id;
            label = _label;
            @default = false;
            external_url = "";
            external_url_target = "";
            if_viewed_most_recently = "";
        }

        public string id;
        public string label;
        public bool @default;
        public string external_url;
        public string external_url_target;
        public string if_viewed_most_recently;
    }

    [System.Serializable]
    public class Choice
    {
        public string id;
        public string label;
        public bool hidden;
        public bool @default;
        public string location;
        public int position_x;
        public int position_y;
        public int position_z;
        public int width;
        public int height; 
        public int rotation_x;
        public int rotation_y;
        public int rotation_z;
        public string image_url;
        public bool cut_on_click;
        public List<LinkedVariant> linkedVariants;
        public string image_active_url;
    }

    [System.Serializable]
    public class MediaAlternativeSource
    {
        public string url;
        public string mime;
    }

    [System.Serializable]
    public class Image
    {
        public string url;
    }

    [System.Serializable]
    public class Variant
    {
        public string id;
        public string name;
        public Image image;
        public string label;
        public Prompt prompt;
        public List<Choice> choices;
        public string summary;
        public string island_id;
        public string media_url;
        public string island_name;
        public string end_behavior;
        public List<string> text_overlay;
        public string media_projection;
        public List<MediaAlternativeSource> media_alternative_sources;
        public bool? is_placeholder;
        public string mediaName;
    }

    [System.Serializable]
    public class Root
    {
        public List<Variant> variants;
        public string start_variant;
        public bool use_device_orientation;


        public Variant GetVariant(string _id)
        {
            for(int i = 0; i < variants.Count; i++)
            {
                if (variants[i].id == _id)
                    return variants[i];
            } // i

            return null;
        }

        public int GetVariantIndex(string _id)
        {
            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i].id == _id)
                    return i;
            } // i

            return -1;
        }
    }
}