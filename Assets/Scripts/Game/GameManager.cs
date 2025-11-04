using UnityEngine;

namespace Game
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;
        public GameObject player;

        void Awake()
        {
            Instance = this;
        }

        public void StartGame()
        {
            Instantiate(player, new Vector3(0, 0, 0), Quaternion.identity);
        }
    }
}