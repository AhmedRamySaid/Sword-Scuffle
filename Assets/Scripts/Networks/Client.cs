using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Networks
{
    public class Client
    {
        private TcpClient client;
        private NetworkStream stream;
        private Thread receiveThread;

        private readonly string serverIP;
        private const int Port = 5555;
        private string logFilePath;

        public Client(string serverIP)
        {
            this.serverIP = serverIP;
            StartConnection();
        }

        void StartConnection()
        {
            logFilePath = Path.Combine(Application.persistentDataPath, "client_logs.txt");
            LogToFile("=== Client Started ===");

            ConnectToServer();
        }

        void ConnectToServer()
        {
            try
            {
                client = new TcpClient(serverIP, Port);
                stream = client.GetStream();

                Debug.Log("Connected to server!");
                LogToFile("Connected to server.");

                // Start a thread to receive messages
                receiveThread = new Thread(ReceiveData);
                receiveThread.Start();

                // Example: Send a test message
                SendMessageToServer("Hello from client!");
            }
            catch (Exception e)
            {
                Debug.LogError("Client error: " + e.Message);
                LogToFile("Client error: " + e.Message);
            }
        }

        void ReceiveData()
        {
            byte[] buffer = new byte[1024];
            int bytesRead;

            try
            {
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    Debug.Log("Received from server: " + message);
                    LogToFile("[From Server] " + message);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Receive error: " + e.Message);
                LogToFile("Receive error: " + e.Message);
            }
        }

        public void SendMessageToServer(string message)
        {
            if (stream == null) return;

            byte[] data = Encoding.ASCII.GetBytes(message);
            stream.Write(data, 0, data.Length);
            Debug.Log("Sent to server: " + message);
            LogToFile("[Sent] " + message);
        }

        void OnApplicationQuit()
        {
            stream?.Close();
            client?.Close();
            receiveThread?.Abort();
            LogToFile("=== Client Stopped ===");
        }

        private void LogToFile(string text)
        {
            try
            {
                string entry = $"[{DateTime.Now:HH:mm:ss}] {text}\n";
                File.AppendAllText(logFilePath, entry);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to write log: " + e.Message);
            }
        }
    }
}