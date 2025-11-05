using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Game;
using UnityEngine;

namespace Networks
{
    public class Client
    {
        public bool Connected = false;

        private UdpClient udpClient;
        private Thread receiveThread;

        private readonly string serverIP;
        private const int Port = 5555;
        private string logFilePath;
        private uint nextSeqNum = 0;

        private IPEndPoint serverEndPoint;

        public Client(string serverIP)
        {
            this.serverIP = serverIP;
            StartConnection();
        }

        void StartConnection()
        {
            logFilePath = Path.Combine(Application.persistentDataPath, "client_logs.txt");
            LogToFile("=== UDP Client Started ===");

            ConnectToServer();
        }

        void ConnectToServer()
        {
            try
            {
                serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), Port);
                udpClient = new UdpClient();
                udpClient.Connect(serverEndPoint);

                Debug.Log("UDP Client ready.");
                LogToFile("UDP Client initialized and ready.");

                Connected = true;

                // Start receiving thread
                receiveThread = new Thread(ReceiveData);
                receiveThread.Start();
            }
            catch (Exception e)
            {
                Debug.LogError("Client error: " + e.Message);
                LogToFile("Client error: " + e.Message);
            }
        }

        void ReceiveData()
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                while (Connected)
                {
                    byte[] receivedBytes = udpClient.Receive(ref remoteEP); // blocking call

                    NetPacket packet = NetPacket.FromBytes(receivedBytes);

                    Debug.Log($"[UDP] Received packet from {remoteEP.Address}:{remoteEP.Port}");
                    LogToFile($"[From Server] Received packet ({receivedBytes.Length} bytes)");

                    // Parse payload into Vector3
                    string payloadStr = Encoding.ASCII.GetString(packet.payload);
                    string[] parts = payloadStr.Split(',');

                    if (parts.Length == 3 && 
                        float.TryParse(parts[0], out float x) &&
                        float.TryParse(parts[1], out float y) &&
                        float.TryParse(parts[2], out float z))
                    {
                        Vector3 delta = new Vector3(x, y, z);

                        // Forward to GameManager on the main thread
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            GameManager.Instance.ApplyMovement(0, delta);
                        });
                    }
                    else
                    {
                        Debug.LogWarning("Invalid payload received: " + payloadStr);
                    }
                }
            }
            catch (SocketException se)
            {
                if (Connected)
                {
                    Debug.LogError("UDP receive error: " + se.Message);
                    LogToFile("UDP receive error: " + se.Message);
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
            if (!Connected || udpClient == null) return;

            byte[] data = Encoding.UTF8.GetBytes(message);
            udpClient.Send(data, data.Length);

            Debug.Log("[UDP] Sent to server: " + message);
            LogToFile("[Sent] " + message);
        }

        public void SendMovement(Vector3 delta)
        {
            if (!Connected || udpClient == null) return;

            byte[] payload = Encoding.ASCII.GetBytes($"{delta.x},{delta.y},{delta.z}");
            NetPacket packet = new NetPacket
            {
                msgType = MessageType.EVENT,
                snapshotId = 0,
                seqNum = nextSeqNum++,
                serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = payload,
                payloadLength = (ushort)payload.Length
            };

            byte[] data = packet.ToBytes();
            udpClient.Send(data, data.Length);

            Debug.Log($"[UDP] Sent movement packet ({data.Length} bytes)");
            LogToFile($"[Sent Movement] seqNum={packet.seqNum}, len={data.Length}");
        }

        public void StopClient()
        {
            Connected = false;
            udpClient?.Close();
            receiveThread?.Join();
            LogToFile("=== UDP Client Stopped ===");
            Debug.Log("UDP client stopped.");
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
