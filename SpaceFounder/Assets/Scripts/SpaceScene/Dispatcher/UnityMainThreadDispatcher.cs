using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> executionQueue = new Queue<Action>();
    private readonly List<Action> runBuffer = new List<Action>();
    private static UnityMainThreadDispatcher instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (instance == null)
        {
            Instance();
        }
    }
    
    public static UnityMainThreadDispatcher Instance()
    {
        if (instance == null)
        {
            var go = new GameObject("UnityMainThreadDispatcher");
            instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }
        return instance;
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // 메인 스레드 대기 시간을 최소화하기 위해 락 안에서는 큐 내용만 재사용 리스트로 옮김 (할당 없음)
        lock (executionQueue)
        {
            while (executionQueue.Count > 0)
            {
                runBuffer.Add(executionQueue.Dequeue());
            }
        }

        // 락이 풀린 상태에서 실행. 한 작업의 예외가 나머지 작업을 유실시키지 않도록 개별 처리
        for (int i = 0; i < runBuffer.Count; i++)
        {
            try
            {
                runBuffer[i]?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
        runBuffer.Clear();
    }

    public void Enqueue(Action action)
    {
        if (action == null) return;

        lock (executionQueue)
        {
            executionQueue.Enqueue(action);
        }
    }
}