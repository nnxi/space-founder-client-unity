using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> executionQueue = new Queue<Action>();
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
        Action[] actionsToRun = null;

        // 메인 스레드 대기 시간을 최소화하기 위해 큐의 데이터만 복사 후 즉시 락 해제
        lock (executionQueue)
        {
            if (executionQueue.Count > 0)
            {
                actionsToRun = executionQueue.ToArray();
                executionQueue.Clear();
            }
        }

        // 락이 풀린 상태에서 안전하게 작업 실행
        if (actionsToRun != null)
        {
            for (int i = 0; i < actionsToRun.Length; i++)
            {
                actionsToRun[i]?.Invoke();
            }
        }
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