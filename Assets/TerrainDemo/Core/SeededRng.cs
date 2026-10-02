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
        private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

        private ulong _state;

        public SeededRng(int seed)
        {
            _state = (ulong)(uint)seed;
        }

        public uint NextUInt()
        {
            _state += GoldenGamma;

            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z = z ^ (z >> 31);
            return (uint)(z ^ (z >> 32));
        }

        public float Next01()
        {
            _state += GoldenGamma;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z = z ^ (z >> 31);

            return (float)(z >> 40) * (1f / 16777216f);
        }
    }
}


