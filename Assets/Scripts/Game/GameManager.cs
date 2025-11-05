using System.Collections;
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
        private GameObject player;
        private Vector3 lastSentPosition;

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
            player = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            lastSentPosition = player.transform.position;
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

        public void ApplyMovement(int ID, Vector3 deltaPos)
        {
            
        }
    }
}