using System;
using System.Security.Cryptography.X509Certificates;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;



    [System.Serializable]
    public class GenericStyle
    {
        public Style[] styles;
    }

    [System.Serializable]
    public class Style
    {
        //these variables are case sensitive and must match the strings "firstName" and "lastName" in the JSON.
        public string id;
        public SubStyle parent;
        public SubStyle child;
        public int timerIndex;

        public TimerStyle timerStyle;
        public bool longTimer;
        public bool longQuestion;
        public bool roundedButtons;

        public bool bottomTimer;
    }

/*      Timer          */

    [System.Serializable]
    public class TimerStyle
    {

        public int width;
        public int anchoredPosX;
        public int timerParentHeight;
        public int timerChildHeight;
    }

/*      SubStyle       */

    [System.Serializable]
    public class SubStyle
    {
        public string layout;
        public Controls controls;
        public bool reverseAlign;
        public Padding padding;
        public int spacing;
        public string childAlignment;
        public ReactTransform transform;
        public GridOptions gridOptions;
    }


/*      Transform     */
    [System.Serializable]
    public class ReactTransform
    {
        public V3Transform localPos;
        public V3Transform localScale;
        public V2Transform anchorMin;
        public V2Transform anchorMax;
        public V2Transform anchoredPos;
        public V2Transform sizeDelta;
        public V2Transform piviot;
    }
    [System.Serializable]
    public class V3Transform
    {
        public float x;
        public float y;
        public float z;
    }

    [System.Serializable]
    public class V2Transform
    {
        public float x;
        public float y;
    }

/*      Grid Options        */
    [System.Serializable]
    public class GridOptions
    {
        public string startCorner;
        public string startAxis;
        public CellSize cellSize;
        public Constraint constraint;
        public Padding padding;
        public Spacing spacing;
    }

    [System.Serializable]
    public class Spacing{
        public int x;
        public int y;
    }

    [System.Serializable]
    public class CellSize
    {
        public int x;
        public int y;
    }
    [System.Serializable]
    public class Constraint
    {
        public string type;
        public int constraintCount;
    }

/*      Padding       */

[System.Serializable]
    public class Padding
    {
        public int left;
        public int right;
        public int top;
        public int bottom;
    }

/*      CONTROLS      */

    [System.Serializable]
    public class Controls
    {
        public Size size;
        public Scale scale;
        public Expand expand;
    }

    [System.Serializable]
    public class Size
    {
        public bool width;
        public bool height;
    }
    
    [System.Serializable]
    public class Scale
    {
        public bool horizontal;
        public bool height;
    }
    
    [System.Serializable]
    public class Expand
    {
        public bool width;
        public bool height;
    }