using System;
using System.Collections.Generic;
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
        private Dictionary<uint, PlayerData> lastSentData;
        private uint nextPlayerId = 1; // player ids start at 1, the client treats itself as 0
        
        public static readonly int KeyframeRateHz = 20;
        private uint nextSnapshotId = 1;
        private Dictionary<IPEndPoint, uint> latestSequences;
        private Thread keyframeThread;

        public void InitializeServer()
        {
            clientIds = new Dictionary<IPEndPoint, uint>();
            players = new Dictionary<uint, PlayerData>();
            lastSentData = new Dictionary<uint, PlayerData>();
            logFilePath = Path.Combine(Application.persistentDataPath, "server_logs.txt");
            latestSequences = new Dictionary<IPEndPoint, uint>();
            
            LogToFile("=== UDP Server Started ===");

            IsRunning = true;
            serverThread = new Thread(StartServer);
            serverThread.Start();
            
            keyframeThread = new Thread(KeyframeLoop);
            keyframeThread.Start();
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

        private void KeyframeLoop()
        {
            int delayMs = (int)(1000f / KeyframeRateHz);

            while (IsRunning)
            {
                try
                {
                    // Send to all clients
                    if (nextSnapshotId % KeyframeRateHz != 0)
                    {
                        SendSnapshot();
                    }
                    else // One second has passed
                    {
                        SendKeyframe(true);
                    }
                }
                catch (Exception e)
                {
                    LogToFile("Keyframe loop error: " + e.Message);
                }

                Thread.Sleep(delayMs);
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
                            uint playerId = nextPlayerId++;
                            clientIds[sender] = playerId;
                            PlayerData newPlayer = new PlayerData();
                            players.Add(playerId, newPlayer);
                            
                            latestSequences[sender] = packet.seqNum;
                            
                            LogToFile($"Registered new client {sender} with PlayerID {playerId}");
                            SendPlayerId(sender, playerId);
                            
                            var others = clientIds.Keys
                                .Where(ep => !ep.Equals(sender))
                                .ToList();
                            SendJoinMessage(others,playerId, true);
                        }
                        else // Terminated the connection
                        {
                            //todo implement
                        }
                        break;
                    case (MessageType.SNAPSHOT):
                        int latestKeyframe = 0;
                        int packetKeyframe = (int) packet.seqNum / KeyframeRateHz;
                        
                        if (latestSequences.TryGetValue(sender, out uint seqNum))
                        {
                            latestKeyframe = (int) seqNum / KeyframeRateHz;
                        }
                        else
                        {
                            latestKeyframe = packetKeyframe;
                        }
                        
                        if (packetKeyframe < latestKeyframe) return; // Part of an older keyframe
                        // Older sequences in the same keyframe are fine
                        string payload = Encoding.ASCII.GetString(packet.payload);
                        PlayerData deltaData = PlayerData.ParseSingularData(payload);
                        
                        latestSequences[sender] = packet.seqNum;
                        PlayerData playerData = players[clientIds[sender]];
                        playerData.Add(deltaData);
                        break;
                    case (MessageType.KEYFRAME):
                        int latestKf = 0;
                        int packetKf = (int) packet.seqNum / KeyframeRateHz;
                        LogToFile("Received keyframe:\n" + packet.ToString());
                        
                        if (latestSequences.TryGetValue(sender, out uint seqNumber))
                        {
                            latestKf = (int) seqNumber / KeyframeRateHz;
                        }
                        else
                        {
                            latestKf = packetKf;
                        }
                        
                        if (packetKf < latestKf) return; // Part of an older keyframe
                        // Older sequences in the same keyframe are fine
                        
                        string strPayload = Encoding.ASCII.GetString(packet.payload);
                        PlayerData realData = PlayerData.ParseSingularData(strPayload);
                        
                        latestSequences[sender] = packet.seqNum;
                        PlayerData currentPlayerData = players[clientIds[sender]];
                        currentPlayerData.CopyData(realData);
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

        private void SendJoinMessage(List<IPEndPoint> receivers, uint playerId, bool flag)
        {
            int joined = flag ? 1 : 0;
            string payload = "ID:" + playerId.ToString() + "/" + joined.ToString();
            byte[] payloadBytes = Encoding.ASCII.GetBytes(payload);
            
            NetPacket packet = new NetPacket
            {
                msgType = MessageType.CONNECT,
                snapshotId = nextSnapshotId,
                seqNum = 0,
                serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = payloadBytes,
                payloadLength = (ushort)payloadBytes.Length
            };
            byte[] data = packet.ToBytes();

            foreach (IPEndPoint clientId in receivers)
            {
                udpServer.Send(data, data.Length, clientId);  
            }
        }

        private void SendSnapshot()
        {
            IPEndPoint[] receivers = clientIds.Keys.ToArray();
            var result = clientIds.
                Where(kvps => receivers.Contains(kvps.Key))
                .ToList();
            var sb = new StringBuilder();

            foreach (KeyValuePair<IPEndPoint, uint> clientId in clientIds)
            {
                PlayerData playerData = players[clientId.Value];
                PlayerData deltaData;
                
                if (lastSentData.TryGetValue(clientId.Value, out PlayerData oldData))
                {
                    deltaData = PlayerData.SubtractData(playerData, oldData);
                    oldData.CopyData(playerData);
                }
                else
                {
                    SendKeyframe(false);
                    return;
                }

                string str = deltaData.ToDeltaString();
                if (str.Equals("")) continue;

                sb.Append($"{clientId.Value}|");
                sb.Append(str);
                sb.Append(';');
            }

            // Remove the trailing semi-column if present
            if (sb.Length > 0)
                sb.Length--;
            
            string payload = sb.ToString();
            
            byte[] payloadBytes = Encoding.ASCII.GetBytes(payload);
            NetPacket packet = new NetPacket
            {
                msgType = MessageType.SNAPSHOT,
                snapshotId = nextSnapshotId++,
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
        
        /*
         * Different clients are seperated by a ';'
         */
        private void SendKeyframe(bool isPeriodicalKeyframe)
        {
            IPEndPoint[] receivers = clientIds.Keys.ToArray();
            var result = clientIds.
                Where(kvps => receivers.Contains(kvps.Key))
                .ToList();
            var sb = new StringBuilder();

            foreach (KeyValuePair<IPEndPoint, uint> clientId in clientIds)
            {
                PlayerData playerData = players[clientId.Value];

                string str = playerData.ToRealString();
                if (str.Equals("")) continue;

                sb.Append($"{clientId.Value}|");
                sb.Append(str);
                sb.Append(';');
                if (lastSentData.TryGetValue(clientId.Value, out PlayerData latestData))
                {
                    latestData.CopyData(playerData);
                }
                else
                {
                    PlayerData pd = new PlayerData();
                    pd.CopyData(playerData);
                    lastSentData.Add(clientId.Value, pd);
                }
            }

            // Remove the trailing semi-column if present
            if (sb.Length > 0)
                sb.Length--;
            
            string payload = sb.ToString();
            
            byte[] payloadBytes = Encoding.ASCII.GetBytes(payload);
            NetPacket packet = new NetPacket
            {
                msgType = MessageType.KEYFRAME,
                snapshotId = nextSnapshotId,
                seqNum = 0,
                serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = payloadBytes,
                payloadLength = (ushort)payloadBytes.Length
            };
            byte[] data = packet.ToBytes();

            if (isPeriodicalKeyframe) nextSnapshotId++;
            
            LogToFile("Sent keyframe:\n" + packet.ToString());
            
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
                snapshotId = nextSnapshotId,
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
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to write log: " + e.Message);
            }
        }
    }
}