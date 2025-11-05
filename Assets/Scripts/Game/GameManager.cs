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

        private Server server;
        private Client localClient;
        private Vector3 lastSentPosition;
        private readonly Dictionary<uint, GameObject> players = new Dictionary<uint, GameObject>();
        
        private const int Frequency = 20;
        private const float SendInterval = 1.0f/Frequency;

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
            //todo: use serverIP
            localClient = new Client("127.0.0.1");
            StartGame();
        }
        
        private async void StartGame()
        {
            GameObject player = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            lastSentPosition = player.transform.position;
            players.Add(0, player);
            await SendMovement();
        }

        private async Task SendMovement()
        {
            while (localClient.Connected)   
            {
                Vector3 currentPos = players[0].transform.position;
                Vector3 deltaPos = lastSentPosition - currentPos;
                lastSentPosition = currentPos;
                await Task.Run(() => localClient.SendMovement(deltaPos));
                await Task.Delay((int)(SendInterval * 1000));
            }
        }

        public void ApplyMovement(int ID, Vector3 deltaPos)
        {
            
        }

        public void AddPlayer(uint id)
        {
            GameObject newPlayer = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            players.Add(id, newPlayer);
        }
        
        public void RemovePlayer(uint id)
        {
            if (players.TryGetValue(id, out GameObject value))
            {
                players.Remove(1);
                Destroy(value);
            }
        }
    }
}