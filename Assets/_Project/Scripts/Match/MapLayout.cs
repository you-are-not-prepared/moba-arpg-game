using System;
using UnityEngine;

namespace Moba
{
    [Serializable]
    public class LanePath
    {
        public string name;
        [Tooltip("World points from the blue core to the red core.")]
        public Vector3[] points;
    }

    // One map layout: its scene plus the lane geometry (for the champion-select preview and the minimap).
    [Serializable]
    public class MapLayoutInfo
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public string sceneName;
        public Vector3 blueCore;
        public Vector3 redCore;
        public LanePath[] lanes;
    }

    // The layout of the loaded map scene.
    public class MapLayout : MonoBehaviour
    {
        public static MapLayout Current { get; private set; }

        public MapLayoutInfo info;

        void OnEnable() => Current = this;

        void OnDisable()
        {
            if (Current == this) Current = null;
        }
    }
}
