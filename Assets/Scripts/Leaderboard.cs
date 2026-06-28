using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace MorBreaker
{
    /// <summary>
    /// Local high-score list for morBreaker. Scores are stored ENTIRELY on the
    /// player's own device — in the browser's <c>localStorage</c> for WebGL builds
    /// (via the MorBreakerLocalStore.jslib bridge), or in memory for the Editor /
    /// other platforms. Nothing is ever sent anywhere, no server, no third party,
    /// no identifying data: just a voluntarily-typed nickname (≤10 sanitised chars)
    /// plus the run's score / level / date / win flag, kept per-browser, per-device.
    /// The list survives between sessions until the user clears the site's data.
    /// </summary>
    public class Leaderboard : MonoBehaviour
    {
        /// <summary>Maximum nickname length.</summary>
        public const int MaxNameLength = 10;

        [Header("Leaderboard")]
        [Tooltip("Master switch. When off, no prompt, no F1 panel, no stored list.")]
        [SerializeField] private bool enableLeaderboard = true;

        [Tooltip("localStorage key the list is stored under (per browser, per site).")]
        [SerializeField] private string storageKey = "morBreaker.highscores";

        [Tooltip("Maximum number of entries retained in storage.")]
        [SerializeField] private int maxStored = 50;

        /// <summary>True when the leaderboard feature is on (name prompt + F1 panel).</summary>
        public bool IsActive => enableLeaderboard;

        [Serializable]
        public struct Entry
        {
            public string name;
            public int score;
            public int level;
            /// <summary>Date the score was made, "yyyy-MM-dd" (no time).</summary>
            public string date;
            /// <summary>True if the player beat the game (cleared the final level).</summary>
            public bool completed;
        }

        [Serializable] private class Store { public List<Entry> entries = new List<Entry>(); }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern IntPtr MorBreakerLSGet(string key);
        [DllImport("__Internal")] private static extern void MorBreakerLSSet(string key, string value);
        [DllImport("__Internal")] private static extern void MorBreakerLSFree(IntPtr ptr);
#else
        // Editor / non-WebGL: in-memory only (lost when play stops).
        private string _memory = "";
#endif

        /// <summary>
        /// Trim and constrain a nickname to a safe set: letters, digits, space,
        /// underscore and hyphen, capped at <see cref="MaxNameLength"/>. Anything
        /// else is dropped. Returns "PLAYER" if nothing usable remains.
        /// </summary>
        public static string SanitizeName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "PLAYER";
            var sb = new StringBuilder(MaxNameLength);
            foreach (char c in raw)
            {
                if (sb.Length >= MaxNameLength) break;
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                          (c >= '0' && c <= '9') || c == ' ' || c == '_' || c == '-';
                if (ok) sb.Append(c);
            }
            string result = sb.ToString().Trim();
            return result.Length == 0 ? "PLAYER" : result;
        }

        /// <summary>Date the score was made, "yyyy-MM-dd" (local date, no time).</summary>
        private static string Today() => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>Add a score to the local list (kept sorted and capped). Callback reports success.</summary>
        public void Submit(string name, int score, int level, bool completed, Action<bool> onComplete = null)
        {
            if (!enableLeaderboard) { onComplete?.Invoke(false); return; }

            var store = Load();
            store.entries.Add(new Entry
            {
                name = SanitizeName(name),
                score = score,
                level = level,
                date = Today(),
                completed = completed
            });
            Sort(store.entries);

            int cap = Mathf.Max(1, maxStored);
            if (store.entries.Count > cap)
                store.entries.RemoveRange(cap, store.entries.Count - cap);

            bool ok = Save(store);
            onComplete?.Invoke(ok);
        }

        /// <summary>Read the top <paramref name="top"/> entries (sorted descending).</summary>
        public void Fetch(int top, Action<Entry[]> onComplete)
        {
            int n = Mathf.Clamp(top, 1, 100);
            if (!enableLeaderboard) { onComplete?.Invoke(Array.Empty<Entry>()); return; }

            var store = Load();
            Sort(store.entries);
            int count = Mathf.Min(n, store.entries.Count);
            var result = new Entry[count];
            store.entries.CopyTo(0, result, 0, count);
            onComplete?.Invoke(result);
        }

        private static void Sort(List<Entry> list) => list.Sort((a, b) => b.score.CompareTo(a.score));

        private Store Load()
        {
            string json = ReadRaw();
            if (string.IsNullOrEmpty(json)) return new Store();
            try
            {
                var s = JsonUtility.FromJson<Store>(json);
                if (s == null) return new Store();
                if (s.entries == null) s.entries = new List<Entry>();
                return s;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Leaderboard load failed (resetting): {e.Message}");
                return new Store();
            }
        }

        private bool Save(Store store)
        {
            try { WriteRaw(JsonUtility.ToJson(store)); return true; }
            catch (Exception e) { Debug.LogWarning($"Leaderboard save failed: {e.Message}"); return false; }
        }

        private string ReadRaw()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            IntPtr ptr = MorBreakerLSGet(storageKey);
            if (ptr == IntPtr.Zero) return "";
            // Stored data is ASCII (sanitised names + numbers + ISO dates), so Ansi is safe.
            string s = Marshal.PtrToStringAnsi(ptr);
            MorBreakerLSFree(ptr);
            return s ?? "";
#else
            return _memory;
#endif
        }

        private void WriteRaw(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            MorBreakerLSSet(storageKey, json);
#else
            _memory = json;
#endif
        }
    }
}
