using UnityEngine;

namespace TerrainDemo.Core
{
    public class FractalNoise2D
    {
        private readonly PerlinNoise2D _baseNoise;
        private readonly PerlinNoise3D _volumeNoise;
        private readonly int _octaves;
        private readonly float _lacunarity;
        private readonly float _persistence;
        private readonly float _scale;
        private readonly float _offset;
        private readonly float _gain;

        public FractalNoise2D(int seed, int octaves, float lacunarity, float persistence, float scale,
            float offset = 0.7f, float gain = 2f)
        {
            _baseNoise = new PerlinNoise2D(seed);
            _volumeNoise = new PerlinNoise3D(seed);
            _octaves = octaves;
            _lacunarity = lacunarity;
            _persistence = persistence;
            _scale = scale;
            _offset = offset;
            _gain = gain;
        }

        public float Sample(float x, float y)
        {
            if (_octaves <= 0) return 0.5f;

            float frequency = 1f / _scale;
            float amplitude = 1f, sum = 0f, amplitudeSum = 0f;

            for (int i = 0; i < _octaves; i++)
            {
                sum += amplitude * _baseNoise.Noise(x * frequency, y * frequency);
                amplitudeSum += amplitude;
                amplitude *= _persistence;
                frequency *= _lacunarity;
            }

            return 0.5f + 0.5f * (sum / amplitudeSum);
        }

        public float SampleRaw(float x, float y)
        {
            if (_octaves <= 0) return 0.5f;

            float frequency = 1f / _scale;
            float value = _baseNoise.Noise(x * frequency, y * frequency) + _offset;

            for (int i = 1; i < _octaves; i++)
            {
                frequency *= _lacunarity;
                float amplitude = Mathf.Pow(_persistence, i);
                value += value * ((_baseNoise.Noise(x * frequency, y * frequency) + _offset) * amplitude * _gain);
            }
            return value;
        }

        public float SampleRaw(float x, float y, float z)
        {
            if (_octaves <= 0) return 0.5f;

            float frequency = 1f / _scale;
            float value = _volumeNoise.Noise(x * frequency, y * frequency, z * frequency) + _offset;

            for (int i = 1; i < _octaves; i++)
            {
                frequency *= _lacunarity;
                float amplitude = Mathf.Pow(_persistence, i);
                value += value * ((_volumeNoise.Noise(x * frequency, y * frequency, z * frequency) + _offset) * amplitude * _gain);
            }
            return value;
        }
    }
}
