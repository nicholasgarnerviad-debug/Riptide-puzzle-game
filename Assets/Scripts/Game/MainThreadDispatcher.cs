using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace Riptide.Game
{
    /// <summary>
    /// The Star Ladder threading audit fix (GDD 6): ad/IAP SDK callbacks arrive on
    /// arbitrary threads; everything that touches game state goes through here and
    /// runs on the main thread during Update.
    /// </summary>
    public sealed class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();
        private static int mainThreadId = -1;

        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == mainThreadId;

        public static MainThreadDispatcher Ensure()
        {
            var existing = FindFirstObjectByType<MainThreadDispatcher>();
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("MainThreadDispatcher");
            DontDestroyOnLoad(go);
            return go.AddComponent<MainThreadDispatcher>();
        }

        private void Awake()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>Queues the action for the next main-thread Update (safe from any thread).</summary>
        public static void Post(Action action)
        {
            if (action == null)
            {
                return;
            }

            Queue.Enqueue(action);
        }

        private void Update()
        {
            while (Queue.TryDequeue(out Action? action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Dispatched callback failed: {ex}");
                }
            }
        }
    }
}
