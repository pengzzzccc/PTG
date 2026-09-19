// ─────────────────────────────────────────────────────────────────────────────
// File: Core/FractalNoise2D.cs
// Module: Procedural terrain generation · Core layer (pure C#, no Unity runtime)
// Status: Fully implemented (fixed-weight fBm + altitude-feedback modes from
//         Musgrave et al. 1989, §3.3)
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;

namespace TerrainDemo.Core
{
    /// <summary>
    /// Fractal layering mode. BasicFbm is the fixed-weight stack (every layer's
    /// amplitude depends only on its index); Multifractal and RidgedMultifractal add
    /// the altitude feedback of Musgrave et al. (1989, §3.3) — each layer's increment
    /// is scaled by the altitude accumulated so far, so low regions stay smooth while
    /// peaks grow rugged.
    /// </summary>
    public enum FractalMode
    {
        /// <summary>Fixed-weight fBm: statistically homogeneous everywhere, range-normalized to [0,1].</summary>
        BasicFbm = 0,

        /// <summary>Altitude feedback on plain noise (HeteroTerrain-style): roughness grows with accumulated altitude.</summary>
        Multifractal = 1,

        /// <summary>Altitude feedback on squared ridged noise: sharp crests concentrated on high ground.</summary>
        RidgedMultifractal = 2,
    }

    /// <summary>
    /// fBm (fractional Brownian motion) layering: a weighted sum of Perlin-noise layers
    /// at increasing frequencies and decreasing amplitudes, normalized to [0,1]. The
    /// low-frequency layers shape the large-scale silhouette, the high-frequency layers
    /// add surface detail. The altitude-feedback modes optionally replace the fixed
    /// weights with the paper's recursion, breaking the statistical uniformity of plain
    /// fBm.
    /// </summary>
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

        /// <summary>
        /// Builds the fBm stack on top of a PerlinNoise2D shuffled from the seed. The
        /// feedback parameters only matter for Multifractal/RidgedMultifractal modes;
        /// the defaults select plain BasicFbm so existing call sites are unaffected.
        /// </summary>
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

        /// <summary>
        /// Samples the fBm field (BasicFbm semantics). Contract: range [0,1] (float
        /// tolerance ~1e-4 allowed); with octaves=1 the result equals
        /// 0.5 + 0.5·Noise(x/scale, y/scale).
        /// </summary>
        public float Sample(float x, float y)
        {
            if (_octaves <= 0) return 0.5f;

            float frequency = 1f / _scale;
            float amplitude = 1f, sum = 0f, amplitudeSum = 0f;

            // Octave loop: coordinates multiply by lacunarity each layer (denser grid),
            // amplitudes multiply by persistence each layer (shrinking contribution).
            for (int i = 0; i < _octaves; i++)
            {
                sum += amplitude * _baseNoise.Noise(x * frequency, y * frequency);
                amplitudeSum += amplitude;
                amplitude *= _persistence;
                frequency *= _lacunarity;
            }

            // Normalize by the amplitude sum back into [-1,1], then map to [0,1];
            // the division makes results with different octave counts directly comparable.
            return 0.5f + 0.5f * (sum / amplitudeSum);
        }

        /// <summary>
        /// Mode-aware sampling used by the heightmap pipeline. BasicFbm returns the same
        /// normalized value as Sample; the feedback modes return the raw accumulated
        /// altitude, which is NOT bounded to [0,1] — callers (HeightmapGenerator)
        /// normalize per map via min/max before shaping.
        /// </summary>
        public float SampleRaw(float x, float y)
        {
            if (_octaves <= 0 || _mode == FractalMode.BasicFbm)
                return Sample(x, y);

            float frequency = 1f / _scale;

            // First octave seeds the accumulated altitude; the ridged variant also seeds
            // its feedback weight and the spectral decay (H = 1.0 → half per octave).
            float n0 = _baseNoise.Noise(x * frequency, y * frequency);
            float signal;
            float value;
            float weight = 1f;
            float spectral = 1f;

            if (_mode == FractalMode.RidgedMultifractal)
            {
                // (offset − |n|)² inverts the noise so creases stick up as ridges, and
                // squaring sharpens them. Offset must be ≈1.0: a smaller offset lets the
                // squared difference turn positive again wherever |n| exceeds it,
                // spraying spurious spikes across flat areas.
                signal = _offset - Mathf.Abs(n0);
                signal *= signal;
                value = signal;
            }
            else
            {
                // The offset lifts the noise above zero so the feedback stays mostly positive.
                signal = n0 + _offset;
                value = signal;
            }

            for (int i = 1; i < _octaves; i++)
            {
                frequency *= _lacunarity;

                if (_mode == FractalMode.RidgedMultifractal)
                {
                    // Textbook order: the weight comes from the PREVIOUS octave's ridged
                    // signal, and each contribution decays by pow(frequency, −H) — with
                    // H = 1.0 and lacunarity 2 that is one half per octave.
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
                    // Paper §3.3: the increment is proportional to the altitude
                    // accumulated so far (value), damped by amplitude and gain. The
                    // damping factor stays small enough that the growth factor
                    // 1 + (n + offset)·amplitude·gain is always positive, so low regions
                    // shrink toward smoothness instead of going negative.
                    float amplitude = Mathf.Pow(_persistence, i);
                    value += value * ((_baseNoise.Noise(x * frequency, y * frequency) + _offset) * amplitude * _gain);
                }
            }
            return value;
        }
    }
}

// ── Implementation notes ─────────────────────────────────────────────────────
//
// Purpose. The fixed-weight stack is statistically self-similar everywhere — Musgrave's
// "inversion test": flip the terrain upside down and nothing changes statistically,
// while real landscapes are asymmetric (valleys silted smooth, ridges left rugged).
// The feedback modes close that gap inside the synthesis stage itself, which is the
// paper's core contribution and the strongest lever on both fidelity criteria.
//
// Principle. BasicFbm keeps the spectral-synthesis form of Lagae et al. (2010):
// lacunarity 2, persistence 0.5, amplitude-sum normalization — each octave then
// contributes equal visual energy. The feedback modes implement the paper's recursion
// aᵢ = aᵢ₋₁ + aᵢ₋₁·(N(Fᵢ)+ca)·cs·ωⁱ in its matured textbook form: each increment is
// multiplied by a weight derived from the altitude accumulated so far, and the weight
// is clamped to [0,1] so the positive feedback cannot diverge. The ridged variant
// squares (offset − |n|) to turn noise zero-crossings into narrow crests; its weight
// feedback replaces amplitude decay entirely, following the reference implementation.
//
// Approach. Sample keeps the basic-fBm contract untouched (24 existing tests pin it);
// SampleRaw is the mode-aware entry the heightmap pipeline consumes. The offset exists
// to keep the feedback mostly positive (noise lifted above zero, as in the paper's
// "+ca" term); the gain concentrates detail onto peaks — larger values make weight
// saturate at 1 sooner. Raw feedback values are unbounded by design, so the caller
// normalizes per map; that keeps SampleRaw stateless and the whole class a pure
// deterministic function object.
// ─────────────────────────────────────────────────────────────────────────────
