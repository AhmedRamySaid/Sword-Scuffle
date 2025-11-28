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
        public Dictionary<uint, Player> Players;
        public Player player;
        
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
            if (localClient.Connected) StartGame();
        }
        
        private async void StartGame()
        {
            Players = new Dictionary<uint, Player>();
            GameObject obj = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            player = obj.GetComponent<Player>();
            player.Initialize();
            player.isPlayer = true;
            Players.Add(1, player);
            await SendMovement();
        }

        private async Task SendMovement()
        {
            while (localClient.Connected)
            {
                PlayerData deltaData = player.GetDeltaData();
                await Task.Run(() => localClient.SendDeltaData(deltaData));
                await Task.Delay((int)(SendInterval * 1000));
            }
        }

        public void ApplyMovement(uint id, Vector3 position)
        {
            if (!Players.TryGetValue(id, out Player p))
            {
                p = AddPlayer(id);
            }
            p.transform.position = position;
        }

        public void ApplyDeltaMovement(uint id, Vector3 deltaPos)
        {
            //todo: change
            Player player = Players[id];
            player.transform.position += deltaPos;
        }

        public Player AddPlayer(uint id)
        {
            GameObject newPlayer = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            Player p = newPlayer.GetComponent<Player>();
            Players.Add(id, p);
            return p;
        }
        
        public void RemovePlayer(uint id)
        {
            if (Players.TryGetValue(id, out Player value))
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