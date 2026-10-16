using System;
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
