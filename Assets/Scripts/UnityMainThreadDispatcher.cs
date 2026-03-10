// ============================================================
//  UnityMainThreadDispatcher.cs
//  Allows background threads (e.g. UDP receive) to safely
//  enqueue actions that will run on the Unity main thread.
//  Add this script to a persistent GameObject (e.g. GameManager).
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> _queue = new Queue<Action>();
    private static UnityMainThreadDispatcher _instance;

    public static void Enqueue(Action action)
    {
        if (action == null) return;
        lock (_queue) _queue.Enqueue(action);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        lock (_queue)
        {
            while (_queue.Count > 0)
                _queue.Dequeue()?.Invoke();
        }
    }
}
