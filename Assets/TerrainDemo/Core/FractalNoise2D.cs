using UnityEngine;

namespace TerrainDemo.Core
{
    public enum FractalMode
    {
        BasicFbm = 0,

        Multifractal = 1,

        RidgedMultifractal = 2,
    }

    public class FractalNoise2D
    {
        private readonly PerlinNoise2D _baseNoise;
        private readonly int _octaves;
        private readonly float _lacunarity;
        private readonly float _persistence;
        private readonly float _scale;
        private readonly FractalMode _mode;
        private readonly float _offset;
        private readonly float _gain;

        public FractalNoise2D(int seed, int octaves, float lacunarity, float persistence, float scale,
            FractalMode mode = FractalMode.BasicFbm, float offset = 0.7f, float gain = 2f)
        {
            _baseNoise = new PerlinNoise2D(seed);
            _octaves = octaves;
            _lacunarity = lacunarity;
            _persistence = persistence;
            _scale = scale;
            _mode = mode;
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
            if (_octaves <= 0 || _mode == FractalMode.BasicFbm)
                return Sample(x, y);

            float frequency = 1f / _scale;

            float n0 = _baseNoise.Noise(x * frequency, y * frequency);
            float signal;
            float value;
            float weight = 1f;
            float spectral = 1f;

            if (_mode == FractalMode.RidgedMultifractal)
            {
                signal = _offset - Mathf.Abs(n0);
                signal *= signal;
                value = signal;
            }
            else
            {
                signal = n0 + _offset;
                value = signal;
            }

            for (int i = 1; i < _octaves; i++)
            {
                frequency *= _lacunarity;

                if (_mode == FractalMode.RidgedMultifractal)
                {
                    weight = Mathf.Clamp(signal * _gain, 0f, 1f);
                    spectral /= _lacunarity;
                    float n = _baseNoise.Noise(x * frequency, y * frequency);
                    signal = _offset - Mathf.Abs(n);
                    signal *= signal;
                    signal *= weight;
                    value += signal * spectral;
                }
                else
                {
                    float amplitude = Mathf.Pow(_persistence, i);
                    value += value * ((_baseNoise.Noise(x * frequency, y * frequency) + _offset) * amplitude * _gain);
                }
            }
            return value;
        }
    }
}
