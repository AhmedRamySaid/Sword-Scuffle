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
        private uint latestSnapshot = 0;
            
        private IPEndPoint serverEndPoint;
        private Thread keyframeThread;

        public Client(string serverIP)
        {
            this.serverIP = serverIP;
            StartConnection();
        }

        void StartConnection()
        {
            // Delete CSV logs at startup
            DeleteIfExists(Path.Combine(Application.persistentDataPath, "client_logs.txt"));
            
            logFilePath = Path.Combine(Application.persistentDataPath, "client_logs.txt");
            LogToFile("=== UDP Client Started ===");

            ConnectToServer();
            keyframeThread = new Thread(KeyframeLoop);
            keyframeThread.Start();
        }

        private void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to delete file {path}: {e.Message}");
            }
        }
        
        void ConnectToServer()
        {
            try
            {
                serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), Port);
                udpClient = new UdpClient();
                udpClient.Connect(serverEndPoint);
                
                LogToFile("UDP Client initialized and ready.");
                Connected = true;

                // Start receiving thread
                receiveThread = new Thread(ReceiveData);
                receiveThread.Start();
                SendConnection(true);
            }
            catch (Exception e)
            {
                LogToFile("Client error: " + e.Message);
            }
        }

        private void KeyframeLoop()
        {
            int delayMs = (int)(1000f / Server.KeyframeRateHz);

            while (Connected)
            {
                try
                {
                    // Send delta data
                    if (nextSeqNum % Server.KeyframeRateHz != 0)
                    {
                        RetrieveDeltaData(SendDeltaData);
                    }
                    else // One second has Passed, send real data
                    {
                        RetrieveRealData(SendRealData);
                    }
                }
                catch (Exception e)
                {
                    LogToFile("Keyframe loop error: " + e.Message);
                }

                Thread.Sleep(delayMs);
            }
        }
        
        private void RetrieveDeltaData(Action<PlayerData> onDeltaReady)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                // This runs on the main thread
                PlayerData delta = GameManager.Instance.player.GetDeltaData();

                // Invoke the callback immediately
                onDeltaReady?.Invoke(delta);
            });
        }
        
        private void RetrieveRealData(Action<PlayerData> onDataReady)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                // This runs on the main thread
                PlayerData data = GameManager.Instance.player.GetRealData();

                // Invoke the callback immediately
                onDataReady?.Invoke(data);
            });
        }
        
        void ReceiveData()
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                while (Connected)
                {
                    byte[] receivedBytes = udpClient.Receive(ref remoteEP); // blocking call

                    try
                    {
                        NetPacket packet = NetPacket.FromBytes(receivedBytes);
                        ParsePayload(packet);
                    }
                    catch (InvalidDataException exception)
                    {
                        LogToFile("Invalid packet received: " + exception.Message);
                    }

                }
            }
            catch (SocketException se)
            {
                if (Connected)
                {
                    LogToFile("UDP receive error: " + se.Message);
                }
            }
            catch (Exception e)
            {
                LogToFile("Receive error: " + e.Message);
            }
        }

        private void ParsePayload(NetPacket packet)
        {
            string payloadStr = Encoding.ASCII.GetString(packet.payload);
            
            switch (packet.msgType)
            {
                case MessageType.CONNECT:
                    string[] connectParts = payloadStr.Split(new char[] { ':', '/' }, StringSplitOptions.RemoveEmptyEntries);
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
                    int latestKf = (int) latestSnapshot / Server.KeyframeRateHz; // Get keyframe
                    int packetKf = (int) packet.snapshotId / Server.KeyframeRateHz;
                    
                    if (packetKf < latestKf) return; // Part of an older keyframe
                    
                    latestSnapshot = packet.snapshotId;
                    
                    try
                    {
                        PlayerData[] deltaData = PlayerData.ParseData(payloadStr);
                        foreach (PlayerData data in deltaData)
                        {
                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                GameManager.Instance.ApplyDeltaData(data);
                            });
                        }
                    }
                    catch (FormatException e)
                    {
                        LogToFile("Invalid line in keyframe payload: " + payloadStr);
                        LogToFile("Exception: " + e);
                    }
                    break;
                case MessageType.KEYFRAME:
                    LogToFile("Received keyframe:\n" + packet.ToString());
                    int latestKeyframe = (int) latestSnapshot / Server.KeyframeRateHz; // Get keyframe
                    int packetKeyframe = (int) packet.snapshotId / Server.KeyframeRateHz;
                    
                    if (packetKeyframe < latestKeyframe) return; // Part of an older keyframe
                    
                    latestSnapshot = packet.snapshotId;
                    try
                    {
                        PlayerData[] realData = PlayerData.ParseData(payloadStr);
                        foreach (PlayerData data in realData)
                        {
                            UnityMainThreadDispatcher.Instance().Enqueue(() =>
                            {
                                GameManager.Instance.ApplyPlayerData(data);
                            });
                        }
                    }
                    catch (FormatException e)
                    {
                        LogToFile("Invalid line in keyframe payload: " + payloadStr);
                        LogToFile("Exception: " + e);
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
        
        private void SendDeltaData(PlayerData data)
        {
            if (!Connected || udpClient == null) return;
            
            byte[] payload = Encoding.ASCII.GetBytes(data.ToDeltaString());
            
            NetPacket packet = new NetPacket
            {
                msgType = MessageType.SNAPSHOT,
                snapshotId = latestSnapshot,
                seqNum = nextSeqNum++,
                serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = payload,
                payloadLength = (ushort)payload.Length
            };
            byte[] packetBytes = packet.ToBytes();
            udpClient.Send(packetBytes, packetBytes.Length);
        }

        private void SendRealData(PlayerData data)
        {
            if (!Connected || udpClient == null) return;
            
            byte[] payload = Encoding.ASCII.GetBytes(data.ToRealString());
            
            NetPacket packet = new NetPacket
            {
                msgType = MessageType.KEYFRAME,
                snapshotId = latestSnapshot,
                seqNum = nextSeqNum++,
                serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = payload,
                payloadLength = (ushort)payload.Length
            };
            byte[] packetBytes = packet.ToBytes();
            LogToFile("Sent keyframe:\n" + packet.ToString());
            udpClient.Send(packetBytes, packetBytes.Length);
        }

        private void SendConnection(bool establishedConnection)
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
            
            LogToFile($"[Sent Connection] seqNum={packet.seqNum}, len={data.Length}");
        }

        public void StopClient()
        {
            Connected = false;
            SendConnection(false);
            udpClient?.Close();
            receiveThread?.Join();
            LogToFile("=== UDP Client Stopped ===");
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
