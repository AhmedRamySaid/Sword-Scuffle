using System.Collections;
using System.Collections.Generic;
using Networks;
using UnityEngine;

namespace Game
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;
        public GameObject playerPrefab;

        private Server server;
        private Client localClient;

        void Awake() => Instance = this;

        public void HostGame()
        {
            StartCoroutine(HostAndJoin());
        }

        private IEnumerator HostAndJoin()
        {
            server = new Server();
            server.InitializeServer();

            yield return new WaitUntil(() => server.IsRunning);
            JoinGame("127.0.0.1");
        }

        public void JoinGame(string serverIP)
        {
            localClient = new Client(serverIP);
            StartGame();
        }

        private void StartGame()
        {
            Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        }
    }
}