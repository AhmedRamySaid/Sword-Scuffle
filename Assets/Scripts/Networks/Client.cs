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
                SendConnection(true);
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
                    
                    ParsePayload(packet);
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

        private void ParsePayload(NetPacket packet)
        {
            string payloadStr = Encoding.ASCII.GetString(packet.payload);
            
            switch (packet.msgType)
            {
                case MessageType.CONNECT:
                    string[] connectParts = payloadStr.Split(new char[] { ':', '.' }, StringSplitOptions.RemoveEmptyEntries);
                    uint connectPlayerId = uint.Parse(connectParts[1]);
                    
                    if (((char)(connectParts[2][0])).Equals('1'))
                    {
                        // a player established a connection
                        // Forward to GameManager on the main thread
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            GameManager.Instance.AddPlayer(connectPlayerId);
                            LogToFile("Player connected with ID:" + connectPlayerId);
                        });
                    }
                    else
                    { 
                        // A player terminated their connection
                        // Forward to GameManager on the main thread
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            GameManager.Instance.RemovePlayer(connectPlayerId);
                            LogToFile("Player disconnected with ID:" + connectPlayerId);
                        });
                    }
                    break;
                case MessageType.SNAPSHOT:
                    string[] snapshotParts = payloadStr.Split(new char[] { ':', '.' }, StringSplitOptions.RemoveEmptyEntries);
                    uint snapshotPlayerId = uint.Parse(snapshotParts[1]);
                    
                    // Parse payload into Vector3
                    string[] vectorParts = snapshotParts[2].Split(',');

                    if (snapshotParts.Length == 3 && 
                        float.TryParse(vectorParts[0], out float x) &&
                        float.TryParse(vectorParts[1], out float y) &&
                        float.TryParse(vectorParts[2], out float z))
                    {
                        Vector3 delta = new Vector3(x, y, z);

                        // Forward to GameManager on the main thread
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            GameManager.Instance.ApplyDeltaMovement(snapshotPlayerId, delta);
                        });
                    }
                    else
                    {
                        Debug.LogWarning("Invalid payload received: " + payloadStr);
                    }
                    break;
                case MessageType.KEYFRAME:
                    string[] lines = payloadStr.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string line in lines)
                    {
                        string[] keyframeParts = line.Split(':');
                        if (keyframeParts.Length != 4)
                        {
                            LogToFile("Invalid line in keyframe payload: " + line);
                            continue;
                        }

                        // Parse ID and coordinates
                        if (!uint.TryParse(keyframeParts[0], out uint keyframeId)) continue;
                        if (!float.TryParse(keyframeParts[1], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float xCor)) continue;
                        if (!float.TryParse(keyframeParts[2], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float yCor)) continue;
                        if (!float.TryParse(keyframeParts[3], System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float zCor)) continue;

                        Vector3 position = new Vector3(xCor, yCor, zCor);

                        // Apply position to your player object
                        // Ensure this runs on the main thread if using Unity
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            GameManager.Instance.ApplyMovement(keyframeId, position);
                        });
                    }
                    break;
                case MessageType.ID_SET:
                    if (uint.TryParse(payloadStr, out uint playerId))
                    {
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            GameManager.Instance.ApplyClientId(playerId);
                        });
                    }
                    break;
            }
        }

        public void SendMessageToServer(string message)
        {
            if (!Connected || udpClient == null) return;

            byte[] data = Encoding.UTF8.GetBytes(message);
            udpClient.Send(data, data.Length);
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
        }

        public void SendConnection(bool establishedConnection)
        {
            if (!Connected || udpClient == null) return;

            byte[] payload = { (byte) (establishedConnection ? '1' : '0') };
            NetPacket packet = new NetPacket
            {
                msgType = MessageType.CONNECT,
                snapshotId = 0,
                seqNum = nextSeqNum++,
                serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = payload,
                payloadLength = (ushort)payload.Length
            };

            byte[] data = packet.ToBytes();
            udpClient.Send(data, data.Length);

            Debug.Log($"[UDP] Sent connection packet ({data.Length} bytes)");
            LogToFile($"[Sent Connection] seqNum={packet.seqNum}, len={data.Length}");
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
