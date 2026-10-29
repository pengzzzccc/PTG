using System;
using System.Globalization;
using System.IO;
using TerrainDemo.Core;
using UnityEngine;

namespace TerrainDemo
{
    [RequireComponent(typeof(TerrainGenerator))]
    public class PerfLog : MonoBehaviour
    {
        [SerializeField] private bool logEnabled = true;

        private TerrainGenerator _generator;
        private StreamWriter _writer;

        public string FilePath { get; private set; }

        private void OnEnable()
        {
            if (!logEnabled) return;

            _generator = GetComponent<TerrainGenerator>();
            string root = Directory.GetParent(Application.dataPath).FullName;
            string fileName = $"perf_{DateTime.Now:yyyy-MM-dd_HHmmss}.csv";
            string folder = Application.isEditor
                ? Path.Combine(root, "Evaluation", "logs")
                : Path.Combine(root, "log");

            try
            {
                Directory.CreateDirectory(folder);
                FilePath = Path.Combine(folder, fileName);
                _writer = new StreamWriter(FilePath, false) { AutoFlush = true };
            }
            catch (Exception e) when (e is UnauthorizedAccessException || e is IOException)
            {
                folder = Path.Combine(Application.persistentDataPath, "log");
                Directory.CreateDirectory(folder);
                FilePath = Path.Combine(folder, fileName);
                _writer = new StreamWriter(FilePath, false) { AutoFlush = true };
                Debug.LogWarning($"[PerfLog] Game folder is not writable, falling back to {folder}");
            }

            WriteHeader();
            _generator.Generated += WriteRow;
            Debug.Log($"[PerfLog] Logging to {FilePath}");
        }

        private void OnDisable()
        {
            if (_generator != null) _generator.Generated -= WriteRow;
            _writer?.Dispose();
            _writer = null;
        }

        private void WriteHeader()
        {
            _writer.WriteLine("# PTG performance log");
            _writer.WriteLine($"# unity={Application.unityVersion} platform={Application.platform} editor={Application.isEditor}");
            _writer.WriteLine($"# cpu={SystemInfo.processorType} cores={SystemInfo.processorCount} sysMemMB={SystemInfo.systemMemorySize}");
            _writer.WriteLine($"# gpu={SystemInfo.graphicsDeviceName} os={SystemInfo.operatingSystem}"
                + $" quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            _writer.WriteLine("time_s,frame,seed,resolution,vertices,mode,octaves,color_count,path,"
                + "height_ms,mesh_ms,color_ms,upload_ms,normals_ms,bounds_ms,total_ms");
        }

        private void WriteRow()
        {
            if (_writer == null) return;

            TerrainParams p = _generator.Parameters;
            _writer.WriteLine(string.Join(",",
                F(Time.realtimeSinceStartupAsDouble),
                Time.frameCount.ToString(CultureInfo.InvariantCulture),
                p.Seed.ToString(CultureInfo.InvariantCulture),
                p.Resolution.ToString(CultureInfo.InvariantCulture),
                (p.Resolution * p.Resolution).ToString(CultureInfo.InvariantCulture),
                p.Mode.ToString(),
                p.Octaves.ToString(CultureInfo.InvariantCulture),
                p.ColorCount.ToString(CultureInfo.InvariantCulture),
                _generator.LastWasRecolor ? "color" : "full",
                F(_generator.HeightMs),
                F(_generator.MeshMs),
                F(_generator.ColorMs),
                F(_generator.UploadMs),
                F(_generator.NormalsMs),
                F(_generator.BoundsMs),
                F(_generator.TotalMs)));
        }

        private static string F(double value)
        {
            return value.ToString("F3", CultureInfo.InvariantCulture);
        }
    }
}
