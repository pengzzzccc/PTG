using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerrainDemo.Core
{
    [Serializable]
    public class TerrainParams
    {
        public int Seed = 1;

        public int Resolution = 257;

        public float WorldSize = 200f;

        public float HeightScale = 30f;

        public float Scale = 120f;

        public int Octaves = 5;

        public float Lacunarity = 2f;

        public float Persistence = 0.5f;

        public float HeightExponent = 1f;

        [Range(0, 64)] public int TerraceLevels = 0;

        public FractalMode Mode = FractalMode.BasicFbm;

        public float Offset = 0.7f;

        public float Gain = 2f;

        public const int MaxColorLayers = 8;

        [Range(1, MaxColorLayers)] public int ColorCount = MaxColorLayers;

        public List<ColorLayer> ColorLayers = DefaultColorLayers();

        public static List<ColorLayer> DefaultColorLayers()
        {
            return new List<ColorLayer>
            {
                new ColorLayer(new Color(0.00f, 0.24f, 0.55f), 0.00f, 0.08f),
                new ColorLayer(new Color(0.39f, 0.63f, 0.86f), 0.08f, 0.12f),
                new ColorLayer(new Color(0.78f, 0.78f, 1.00f), 0.12f, 0.15f),
                new ColorLayer(new Color(0.76f, 0.70f, 0.50f), 0.15f, 0.20f),
                new ColorLayer(new Color(0.30f, 0.50f, 0.20f), 0.20f, 0.55f),
                new ColorLayer(new Color(0.45f, 0.38f, 0.30f), 0.55f, 0.75f),
                new ColorLayer(new Color(1.00f, 1.00f, 1.00f), 0.75f, 1.00f),
                new ColorLayer(new Color(0.40f, 0.40f, 0.40f), 0.00f, 1.00f, 0.35f, 1.00f),
            };
        }

        public void EnsureColorLayers()
        {
            List<ColorLayer> defaults = DefaultColorLayers();
            if (ColorLayers == null) ColorLayers = new List<ColorLayer>();
            for (int i = ColorLayers.Count; i < MaxColorLayers; i++) ColorLayers.Add(defaults[i]);
            ColorCount = Mathf.Clamp(ColorCount, 1, MaxColorLayers);
        }

        public static TerrainParams RollingHills()
        {
            return new TerrainParams
            {
                Seed = 1,
                Scale = 150f,
                Octaves = 4,
                Persistence = 0.45f,
                HeightScale = 18f,
                HeightExponent = 1.2f,
            };
        }

        public static TerrainParams RuggedPeaks()
        {
            return new TerrainParams
            {
                Seed = 1,
                Scale = 175f,
                Octaves = 7,
                Lacunarity = 1.96f,
                Persistence = 0.43f,
                HeightScale = 45f,
                HeightExponent = 2.33f,
                Mode = FractalMode.Multifractal,
                Offset = 1.165f,
                Gain = 2.64f,
            };
        }

        public static TerrainParams TerracedMesas()
        {
            return new TerrainParams
            {
                Seed = 1,
                Scale = 130f,
                Octaves = 5,
                Persistence = 0.5f,
                HeightScale = 30f,
                HeightExponent = 1f,
                TerraceLevels = 8,
            };
        }
    }
}
