using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Networks;
using UnityEngine;

namespace Game
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;
        public GameObject playerPrefab;
        public Dictionary<uint, GameObject> Players;
        public GameObject player;
        
        private Server server;
        private Client localClient;
        private Vector3 lastSentPosition;
        
        private const int Frequency = 20;
        private const float SendInterval = 1.0f/Frequency;

        void Awake() => Instance = this;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F11))
            {
                Screen.fullScreen = !Screen.fullScreen;
            }
        }

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
            //todo: use serverIP
            localClient = new Client("127.0.0.1");
            StartGame();
        }
        
        private async void StartGame()
        {
            Players = new Dictionary<uint, GameObject>();
            player = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            lastSentPosition = player.transform.position;
            Players.Add(1, player);
            player.GetComponent<Player>().isPlayer = true;
            await SendMovement();
        }

        private async Task SendMovement()
        {
            while (localClient.Connected)   
            {
                Vector3 currentPos = player.transform.position;
                Vector3 deltaPos = lastSentPosition - currentPos;
                lastSentPosition = currentPos;
                await Task.Run(() => localClient.SendMovement(deltaPos));
                await Task.Delay((int)(SendInterval * 1000));
            }
        }

        public void ApplyMovement(uint id, Vector3 position)
        {
            if (!Players.TryGetValue(id, out GameObject p))
            {
                p = AddPlayer(id);
            }
            p.transform.position = position;
        }

        public void ApplyDeltaMovement(uint id, Vector3 deltaPos)
        {
            //todo: change
            GameObject player = Players[id];
            player.transform.position += deltaPos;
        }

        public GameObject AddPlayer(uint id)
        {
            GameObject newPlayer = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            Players.Add(id, newPlayer);
            return newPlayer;
        }
        
        public void RemovePlayer(uint id)
        {
            if (Players.TryGetValue(id, out GameObject value))
            {
                Players.Remove(id);
                Destroy(value);
            }
        }
        
        public void ApplyClientId(uint id)
        {
            Players.Remove(1);
            Players.Add(id, player);
        }

        private void OnApplicationQuit()
        {
            if (server != null) server.StopServer();
        }
    }
}