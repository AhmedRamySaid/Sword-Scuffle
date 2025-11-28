using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Game;
using UnityEngine;

namespace Networks
{
    public class Server
    {
        public bool IsRunning = false;

        private UdpClient udpServer;
        private Thread serverThread;

        private const int Port = 5555;
        private string logFilePath;
        private Dictionary<IPEndPoint, uint> clientIds;
        private Dictionary<uint, PlayerData> players;
        private uint nextPlayerId = 1; // player ids start at 1, the client treats itself as 0

        public void InitializeServer()
        {
            clientIds = new Dictionary<IPEndPoint, uint>();
            players = new Dictionary<uint, PlayerData>();
            logFilePath = Path.Combine(Application.persistentDataPath, "server_logs.txt");
            LogToFile("=== UDP Server Started ===");

            IsRunning = true;
            serverThread = new Thread(StartServer);
            serverThread.Start();
        }

        private void StartServer()
        {
            try
            {
                udpServer = new UdpClient(Port);
                LogToFile($"Server listening on UDP port {Port}");

                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

                while (IsRunning)
                {
                    byte[] receivedBytes = udpServer.Receive(ref remoteEP); // blocking call
                    HandlePacket(receivedBytes, remoteEP);
                }
            }
            catch (SocketException se)
            {
                if (IsRunning)
                {
                    LogToFile("Socket error: " + se.Message);
                }
            }
            catch (Exception e)
            {
                LogToFile("Server error: " + e.Message);
            }
            finally
            {
                udpServer?.Close();
                LogToFile("=== UDP Server Stopped ===");
            }
        }

        private void HandlePacket(byte[] data, IPEndPoint sender)
        {
            try
            {
                // Convert bytes into a NetPacket
                NetPacket packet = NetPacket.FromBytes(data);
                
                switch (packet.msgType)
                {
                    case (MessageType.CONNECT):
                        if (((char)(packet.payload[0])).Equals('1')) // Established a connection
                        {
                            if (!clientIds.TryGetValue(sender, out uint playerId))
                            {
                                playerId = nextPlayerId++;
                                clientIds[sender] = playerId;
                                PlayerData newPlayer = new PlayerData();
                                players.Add(playerId, newPlayer);
                                
                                LogToFile($"Registered new client {sender} with PlayerID {playerId}");
                                SendPlayerId(sender, playerId);
                                SendKeyframe(new IPEndPoint[] {sender});
                            }
                        }
                        else // Terminated the connection
                        {
                            //todo implement
                        }
                        break;
                    case (MessageType.SNAPSHOT):
                        string payload = Encoding.ASCII.GetString(packet.payload);
                        PlayerData playerData = PlayerData.DeltaDataDecoder(payload);
                        // todo: handle playerData
                        break;
                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                LogToFile("Packet handling error: " + e.Message);
            }
        }

        private void SendKeyframe(IPEndPoint[] receivers)
        {
            var result = clientIds.
                Where(kvps => receivers.Contains(kvps.Key))
                .ToList();
            var sb = new StringBuilder();

            foreach (KeyValuePair<IPEndPoint, uint> clientId in result)
            {
                // if (!GameManager.Instance.Players.TryGetValue(clientId.Value, out Player player))
                // {
                //     UnityMainThreadDispatcher.Instance().Enqueue(() =>
                //     {
                //         player = new Player();
                //         player.transform.position = new Vector3(3, 3, 1); // default coordinates for player prefab
                //     });
                // } //todo: fix
                
                
                /*
                 * Form: id:x,y;....;id:x,y
                 * 
                 * Example with 2 players
                 * Player 1 position = 3.5,5.6
                 * Player 2 position = 2.1,3.8
                 * 
                 * payload: 1:3.5,5.6;2:2.1,3.8
                */
                
                sb.Append(clientId.Value.ToString());

                // Append positions using InvariantCulture to use a standard form across all devices
                Vector3 pos = Vector3.zero;
                // UnityMainThreadDispatcher.Instance().Enqueue(() =>
                // {
                //     pos = player.transform.position;
                // });
                
                sb.Append(':')
                    .Append(pos.x.ToString(CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(pos.y.ToString(CultureInfo.InvariantCulture))
                    .Append(';');
            }

            string payload = sb.ToString();
            
            byte[] payloadBytes = Encoding.ASCII.GetBytes(payload);
            NetPacket packet = new NetPacket
            {
                msgType = MessageType.KEYFRAME,
                snapshotId = 0,
                seqNum = 0,
                serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = payloadBytes,
                payloadLength = (ushort)payloadBytes.Length
            };
            byte[] data = packet.ToBytes();

            foreach (KeyValuePair<IPEndPoint, uint> clientId in result)
            {
                udpServer.Send(data, data.Length, clientId.Key);  
            }
        }

        private void BroadcastToClients(byte[] data, IPEndPoint sender = null)
        {
            foreach (var pair in clientIds)
            {
                var clientEP = pair.Key;
                // skip sender
                if (sender != null && clientEP.Equals(sender)) continue;

                try
                {
                    udpServer.Send(data, data.Length, clientEP);
                }
                catch (Exception e)
                {
                    LogToFile($"Failed to send to {clientEP}: {e.Message}");
                }
            }
        }

        private void SendPlayerId(IPEndPoint sender, uint playerId)
        {
            byte[] payloadBytes = Encoding.ASCII.GetBytes(playerId.ToString());
            NetPacket packet = new NetPacket
            {
                msgType = MessageType.ID_SET,
                snapshotId = 0,
                seqNum = 0,
                serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = payloadBytes,
                payloadLength = (ushort)payloadBytes.Length
            };
            
            byte[] data = packet.ToBytes();
            udpServer.Send(data, data.Length, sender);
        }

        public void StopServer()
        {
            IsRunning = false;
            udpServer?.Close();
            serverThread?.Join();
            LogToFile("=== UDP Server Stopped ===");
        }

        private void LogToFile(string text)
        {
            try
            {
                string entry = $"[{DateTime.Now:HH:mm:ss}] {text}\n";
                File.AppendAllText(logFilePath, entry);
                Debug.Log(text); // For development
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to write log: " + e.Message);
            }
        }
    }
}