using System;
using UnityEngine;

namespace TerrainDemo.Core
{
    [Serializable]
    public class ColorLayer
    {
        public Color Color = Color.white;

        public float HeightMin = 0f;

        public float HeightMax = 1f;

        public float SlopeMin = 0f;

        public float SlopeMax = 1f;

        public ColorLayer()
        {
        }

        public ColorLayer(Color color, float heightMin, float heightMax, float slopeMin = 0f, float slopeMax = 1f)
        {
            Color = color;
            HeightMin = heightMin;
            HeightMax = heightMax;
            SlopeMin = slopeMin;
            SlopeMax = slopeMax;
        }

        public ColorLayer Clone()
        {
            return new ColorLayer(Color, HeightMin, HeightMax, SlopeMin, SlopeMax);
        }
    }
}
