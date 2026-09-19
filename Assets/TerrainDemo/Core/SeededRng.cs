// ─────────────────────────────────────────────────────────────────────────────
// File: Core/SeededRng.cs
// Module: Procedural terrain generation · Core layer (pure C#, no Unity runtime)
// Status: Fully implemented
// ─────────────────────────────────────────────────────────────────────────────

namespace TerrainDemo.Core
{
    /// <summary>
    /// Deterministic pseudo-random number generator driven by an integer seed.
    /// It is the single source of randomness for the entire terrain system.
    /// </summary>
    public class SeededRng
    {
        /// <summary>splitmix64 golden-gamma increment (2^64/φ): consecutive states jump maximally around the 64-bit circle.</summary>
        private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

        /// <summary>The 64-bit internal state, determined solely by the seed and the number of calls.</summary>
        private ulong _state;

        /// <summary>Initializes the internal state from an integer seed; the same seed always yields the identical sequence.</summary>
        public SeededRng(int seed)
        {
            // Cast through uint first to avoid sign extension of negative seeds;
            // seed 0 is legal as well — what actually gets mixed is "state + golden gamma", never zero.
            _state = (ulong)(uint)seed;
        }

        /// <summary>Returns the next uniformly distributed unsigned 32-bit integer; each call advances the state once.</summary>
        public uint NextUInt()
        {
            _state += GoldenGamma;

            // splitmix64's three rounds of "xor-shift + multiply by a large odd constant",
            // avalanching all 64 state bits into an apparently unrelated output.
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z = z ^ (z >> 31);
            return (uint)(z ^ (z >> 32));
        }

        /// <summary>Returns a uniform float in [0,1); contract: the value 1 is never produced and the mean is close to 0.5.</summary>
        public float Next01()
        {
            _state += GoldenGamma;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z = z ^ (z >> 31);

            // Take the top 24 bits times 1/2^24: a float has exactly 24 significand bits,
            // so the result is exactly representable and strictly less than 1.
            return (float)(z >> 40) * (1f / 16777216f);
        }
    }
}

// ── Implementation notes ─────────────────────────────────────────────────────
//
// Purpose. Reproducing "the same seed always yields the same mountain" requires every
// seemingly random datum — permutation tables, random offsets — to derive from one
// controllable source. This class is that single source: it turns the "controllability"
// criterion from the talk into a code-level rule. Nothing else may use randomness
// (UnityEngine.Random, System.Random), otherwise the determinism chain breaks.
//
// Principle. splitmix64: on each output, add the golden-ratio constant 0x9E3779B97F4A7C15
// to the 64-bit state, then mix the state with three rounds of xor-shift/multiply-by-odd
// to produce a uniform 64-bit output. Both parts come from Steele, Lea & Flood (2014,
// OOPSLA): the golden-gamma state advance (their GOLDEN_GAMMA) and the Stafford Mix13
// finalizer (their mix64variant13) — the configuration commonly circulated as splitmix64.
// The seed fixes the initial state and everything after is a purely deterministic
// recurrence, with an 8-byte state and one-line outputs, which is plenty for this
// project's sample counts.
// Next01 takes the top 24 bits times 1/2^24: float has exactly 24 significand bits, so
// the result lands on exactly representable grid points in [0,1).
//
// Approach. The constructor only converts the int seed (negative or zero included) into
// the 64-bit state; NextUInt advances the state and emits; Next01 reuses the same
// recurrence for the interval mapping. None of them read the clock or the environment,
// and after construction only the caller drives them — which is exactly what lets the
// "same seed reproduces the same sequence" test pass.
// ─────────────────────────────────────────────────────────────────────────────
