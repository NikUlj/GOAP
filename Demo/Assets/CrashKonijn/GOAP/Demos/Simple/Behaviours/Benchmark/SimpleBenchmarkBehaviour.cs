using System;
using System.Collections.Generic;
using CrashKonijn.Goap.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using Random = UnityEngine.Random;
using Process = System.Diagnostics.Process;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace CrashKonijn.Goap.Demos.Simple.Behaviours.Benchmark
{
    public static class SimpleBenchmarkRuntimeStats
    {
        public static int ApplesEaten { get; private set; }

        public static void Reset()
        {
            ApplesEaten = 0;
        }

        public static void RecordAppleEaten()
        {
            ApplesEaten++;
        }
    }

    [DefaultExecutionOrder(-1000)]
    public class SimpleBenchmarkBehaviour : MonoBehaviour
    {
        [Header("Initialization")]
        public int seed = 12345;
        public int initialAgentCount = 50;
        public int initialAppleCount = 50;
        public Vector2 bounds = new Vector2(15f, 8f);
        public float hungerMin = 0f;
        public float hungerMax = 100f;
        public float appleNutritionMin = 80f;
        public float appleNutritionMax = 150f;

        [Header("Measurement")]
        public float warmupSeconds = 5f;
        public float measureSeconds = 15f;
        public bool quitOnComplete;

        private bool initialized;
        private bool logged;
        private float timer;
        private SettingsBehaviour settingsBehaviour;
        private AgentTypeBehaviour sceneAgentType;

        private readonly List<float> samples = new(2048);
        private readonly List<float> sortedSamples = new(2048);
        private readonly Stopwatch wallClock = new();

        private Process currentProcess;
        private bool canMeasureCpu;
        private bool measurementStarted;
        private TimeSpan measurementStartCpuTime;
        private TimeSpan measurementStartWallTime;

        private void Awake()
        {
            SimpleBenchmarkRuntimeStats.Reset();
            Random.InitState(seed);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;

            try
            {
                currentProcess = Process.GetCurrentProcess();
                canMeasureCpu = true;
            }
            catch
            {
                canMeasureCpu = false;
            }

            wallClock.Start();
        }

        private void Start()
        {
            if (!ResolveSceneSetup())
                return;

            StripPrefabVisuals();
            RespawnScenario();
            EnsureAgentTypesAssigned();
            DisablePresentationAndDebug();
            initialized = true;
        }

        private bool ResolveSceneSetup()
        {
            settingsBehaviour = FindObjectOfType<SettingsBehaviour>(true);
            sceneAgentType = FindObjectOfType<AgentTypeBehaviour>(true);

            if (settingsBehaviour?.agentPrefab != null)
                settingsBehaviour.agentPrefab.SetActive(false);

            if (settingsBehaviour == null)
            {
                Debug.LogError("[Benchmark] No SettingsBehaviour found in SimpleBenchmarkScene.");
                return false;
            }

            if (settingsBehaviour.applePrefab == null || settingsBehaviour.agentPrefab == null)
            {
                Debug.LogError("[Benchmark] Benchmark scene is missing apple/agent prefab references on SettingsBehaviour.");
                return false;
            }

            if (sceneAgentType == null)
            {
                Debug.LogError("[Benchmark] No AgentTypeBehaviour found in SimpleBenchmarkScene.");
                return false;
            }

            return true;
        }

        private void EnsureAgentTypesAssigned()
        {
            if (sceneAgentType == null || sceneAgentType.AgentType == null)
                return;

            foreach (var provider in FindObjectsOfType<GoapActionProvider>(true))
            {
                if (provider == null || provider.AgentType != null)
                    continue;

                if (provider.AgentTypeBehaviour == null)
                    provider.AgentTypeBehaviour = sceneAgentType;

                if (provider.AgentTypeBehaviour != null && provider.AgentTypeBehaviour.AgentType != null)
                    provider.AgentType = provider.AgentTypeBehaviour.AgentType;
            }
        }

        private void RespawnScenario()
        {
            ClearSceneObjects();

            var agentValueRandom = CreateSequence(seed, 1);
            var appleValueRandom = CreateSequence(seed, 2);
            var agentPositionRandom = CreateSequence(seed, 3);
            var applePositionRandom = CreateSequence(seed, 4);

            for (int i = 0; i < Mathf.Max(0, initialAppleCount); i++)
            {
                var appleObject = Instantiate(settingsBehaviour.applePrefab, GetRandomPosition(applePositionRandom), Quaternion.identity);
                var apple = appleObject.GetComponent<AppleBehaviour>();
                if (apple != null)
                    apple.nutritionValue = NextRange(appleValueRandom, appleNutritionMin, appleNutritionMax);
            }

            for (int i = 0; i < Mathf.Max(0, initialAgentCount); i++)
            {
                var agentObject = Instantiate(settingsBehaviour.agentPrefab, GetRandomPosition(agentPositionRandom), Quaternion.identity);
                var provider = agentObject.GetComponent<GoapActionProvider>();
                if (provider != null)
                {
                    provider.AgentTypeBehaviour = sceneAgentType;
                    if (sceneAgentType != null && sceneAgentType.AgentType != null)
                        provider.AgentType = sceneAgentType.AgentType;
                }

                agentObject.SetActive(true);

                var hunger = agentObject.GetComponent<SimpleHungerBehaviour>();
                if (hunger != null)
                    hunger.hunger = NextRange(agentValueRandom, hungerMin, hungerMax);
            }

            SimpleBenchmarkRuntimeStats.Reset();
        }

        private void StripPrefabVisuals()
        {
            DisableVisualComponents(settingsBehaviour.agentPrefab);
            DisableVisualComponents(settingsBehaviour.applePrefab);

            foreach (var tree in FindObjectsOfType<TreeBehaviour>(true))
            {
                DisableVisualComponents(tree.gameObject);
            }
        }

        private void ClearSceneObjects()
        {
            foreach (var provider in FindObjectsOfType<GoapActionProvider>(true))
            {
                if (provider == null)
                    continue;

                provider.gameObject.SetActive(false);
                Destroy(provider.gameObject);
            }

            foreach (var apple in FindObjectsOfType<AppleBehaviour>(true))
            {
                if (apple == null)
                    continue;

                apple.gameObject.SetActive(false);
                Destroy(apple.gameObject);
            }
        }

        private void Update()
        {
            if (!initialized || logged)
                return;

            float dt = Time.unscaledDeltaTime;
            timer += dt;

            if (timer <= warmupSeconds)
                return;

            if (timer <= warmupSeconds + measureSeconds)
            {
                EnsureMeasurementStarted();
                samples.Add(dt * 1000f);
                return;
            }

            LogSummary();
        }

        private void DisablePresentationAndDebug()
        {
            var eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem != null)
                eventSystem.gameObject.SetActive(false);

            var mainCamera = Camera.main;
            if (mainCamera != null)
                mainCamera.gameObject.SetActive(false);

            foreach (var behaviour in FindObjectsOfType<MonoBehaviour>(true))
            {
                if (behaviour == null)
                    continue;

                string fullName = behaviour.GetType().FullName;
                if (fullName == "Demos.Shared.Behaviours.AnimationBehaviour" ||
                    fullName == "CrashKonijn.Goap.Demos.Simple.Behaviours.AnimationBehaviour")
                {
                    behaviour.enabled = false;
                }
            }

            foreach (var animator in FindObjectsOfType<Animator>(true))
            {
                if (animator != null)
                    animator.enabled = false;
            }

            foreach (var spriteRenderer in FindObjectsOfType<SpriteRenderer>(true))
            {
                if (spriteRenderer != null)
                    spriteRenderer.enabled = false;
            }

            foreach (var textBehaviour in FindObjectsOfType<SimpleTextBehaviour>())
            {
                textBehaviour.enabled = false;

                var canvas = textBehaviour.GetComponentInChildren<Canvas>(true);
                if (canvas != null)
                    canvas.gameObject.SetActive(false);
            }
        }

        private Vector3 GetRandomPosition(System.Random random)
        {
            float x = NextRange(random, -bounds.x, bounds.x);
            float z = NextRange(random, -bounds.y, bounds.y);
            return new Vector3(x, 0f, z);
        }

        private static void DisableVisualComponents(GameObject root)
        {
            if (root == null)
                return;

            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (animator != null)
                    animator.enabled = false;
            }

            foreach (var spriteRenderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (spriteRenderer != null)
                    spriteRenderer.enabled = false;
            }
        }

        private void LogSummary()
        {
            logged = true;

            if (samples.Count == 0)
            {
                Debug.LogWarning("[Benchmark] project=CrashKonijn status=no_samples");
            }
            else
            {
                float sum = 0f;
                sortedSamples.Clear();

                for (int i = 0; i < samples.Count; i++)
                {
                    float sample = samples[i];
                    sum += sample;
                    sortedSamples.Add(sample);
                }

                sortedSamples.Sort();
                float average = sum / samples.Count;
                float averageFps = average > 0f ? 1000f / average : 0f;
                int p95Index = Mathf.Clamp(Mathf.CeilToInt(sortedSamples.Count * 0.95f) - 1, 0, sortedSamples.Count - 1);
                float p95 = sortedSamples[p95Index];
                string avgUsedCoresText = "n/a";
                string avgCpuPercentText = "n/a";

                if (TryGetCpuMetrics(out float avgUsedCores, out float avgCpuPercent))
                {
                    avgUsedCoresText = avgUsedCores.ToString("0.###");
                    avgCpuPercentText = avgCpuPercent.ToString("0.###");
                }

                Debug.Log(
                    $"[Benchmark] project=CrashKonijn seed={seed} initialAgents={initialAgentCount} initialApples={initialAppleCount} warmupSeconds={warmupSeconds:0.###} measureSeconds={measureSeconds:0.###} samples={samples.Count} avgFrameMs={average:0.###} avgFps={averageFps:0.###} p95FrameMs={p95:0.###} avgUsedCores={avgUsedCoresText} avgCpuPercent={avgCpuPercentText} applesEaten={SimpleBenchmarkRuntimeStats.ApplesEaten}");
            }

            if (quitOnComplete)
                Application.Quit();
        }

        private void OnDestroy()
        {
            currentProcess?.Dispose();
        }

        private void EnsureMeasurementStarted()
        {
            if (measurementStarted)
                return;

            measurementStarted = true;
            measurementStartWallTime = wallClock.Elapsed;

            if (!canMeasureCpu || currentProcess == null)
                return;

            currentProcess.Refresh();
            measurementStartCpuTime = currentProcess.TotalProcessorTime;
        }

        private bool TryGetCpuMetrics(out float avgUsedCores, out float avgCpuPercent)
        {
            avgUsedCores = 0f;
            avgCpuPercent = 0f;

            if (!canMeasureCpu || !measurementStarted || currentProcess == null)
                return false;

            double wallSeconds = (wallClock.Elapsed - measurementStartWallTime).TotalSeconds;
            if (wallSeconds <= 0d)
                return false;

            currentProcess.Refresh();
            double cpuSeconds = (currentProcess.TotalProcessorTime - measurementStartCpuTime).TotalSeconds;
            avgUsedCores = (float)(cpuSeconds / wallSeconds);

            int logicalCoreCount = System.Environment.ProcessorCount;
            avgCpuPercent = logicalCoreCount > 0
                ? avgUsedCores / logicalCoreCount * 100f
                : 0f;

            return true;
        }

        private static System.Random CreateSequence(int baseSeed, int salt)
        {
            int seedValue = unchecked(baseSeed * 486187739 + salt * 16777619);
            if (seedValue == int.MinValue)
                seedValue = int.MaxValue;
            if (seedValue < 0)
                seedValue = -seedValue;
            return new System.Random(seedValue);
        }

        private static float NextRange(System.Random random, float min, float max)
        {
            if (Mathf.Approximately(min, max))
                return min;

            return min + (float)random.NextDouble() * (max - min);
        }
    }
}
