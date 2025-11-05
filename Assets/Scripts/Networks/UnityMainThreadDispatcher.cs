namespace Networks
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private static readonly Queue<Action> _actions = new Queue<Action>();

        public static UnityMainThreadDispatcher Instance()
        {
            if (!_instance)
            {
                var obj = new GameObject("MainThreadDispatcher");
                _instance = obj.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }

        /// <summary>
        /// Enqueue an action to run on the main Unity thread
        /// </summary>
        public void Enqueue(Action action)
        {
            lock (_actions)
            {
                _actions.Enqueue(action);
            }
        }

        private void Update()
        {
            lock (_actions)
            {
                while (_actions.Count > 0)
                {
                    _actions.Dequeue().Invoke();
                }
            }
        }
    }
}