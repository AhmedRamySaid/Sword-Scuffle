using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Networks
{
    public class Server
    {
        public bool IsRunning = false;
        
        private TcpListener server;
        private Thread serverThread;

        private const int Port = 5555;
        private string logFilePath;

        public void InitializeServer()
        {
            // Prepare log file path
            logFilePath = Path.Combine(Application.persistentDataPath, "server_logs.txt");
            LogToFile("=== Server Started ===");

            serverThread = new Thread(StartServer);
            serverThread.Start();
        }

        void StartServer()
        {
            try
            {
                server = new TcpListener(IPAddress.Any, Port);
                server.Start();
                IsRunning = true;
                Debug.Log("Server started on port " + Port);
                LogToFile("Server listening on port " + Port);

                while (IsRunning)
                {
                    TcpClient client = server.AcceptTcpClient();
                    Debug.Log("Client connected!");
                    LogToFile("Client connected.");

                    Thread clientThread = new Thread(() => HandleClient(client));
                    clientThread.Start();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Server error: " + e.Message);
                LogToFile("Server error: " + e.Message);
            }
        }

        void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];
            int bytesRead;

            try
            {
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    Debug.Log("Received from client: " + message);
                    LogToFile("[From Client] " + message);

                    // Echo back
                    byte[] response = Encoding.ASCII.GetBytes("Server received: " + message);
                    stream.Write(response, 0, response.Length);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Client error: " + e.Message);
                LogToFile("Client error: " + e.Message);
            }
            finally
            {
                client.Close();
                Debug.Log("Client disconnected.");
                LogToFile("Client disconnected.");
            }
        }

        void OnApplicationQuit()
        {
            IsRunning = false;
            server?.Stop();
            serverThread?.Abort();
            LogToFile("=== Server Stopped ===");
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