using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private Thread keyframeThread;
        private Thread cpuLoggerThread;

        private const int Port = 5555;
        private string logFilePath;
        private string cpuLogPath;
        private Dictionary<IPEndPoint, uint> clientIds;
        private Dictionary<uint, PlayerData> players;
        private Dictionary<uint, PlayerData> lastSentData;
        private uint nextPlayerId = 1; // player ids start at 1, the client treats itself as 0
        public static readonly int KeyframeRateHz = 20;
        private uint nextSnapshotId = 1;
        private Dictionary<IPEndPoint, uint> latestSequences;
        
        private Process currentProcess;
        private TimeSpan prevTotalProcessorTime;
        private DateTime prevTime;

        public void InitializeServer()
        {
            clientIds = new Dictionary<IPEndPoint, uint>();
            players = new Dictionary<uint, PlayerData>();
            lastSentData = new Dictionary<uint, PlayerData>();
            latestSequences = new Dictionary<IPEndPoint, uint>();
            logFilePath = Path.Combine(Application.persistentDataPath, "server_logs.txt");
            currentProcess = Process.GetCurrentProcess();
            // Get CPU log path on main thread
            cpuLogPath = Path.Combine(Application.persistentDataPath, "cpu_logs.csv");

            // Delete CSV logs at startup
            DeleteIfExists(Path.Combine(Application.persistentDataPath, "server_logs.csv"));
            DeleteIfExists(Path.Combine(Application.persistentDataPath, "server_received_logs.csv"));
            DeleteIfExists(Path.Combine(Application.persistentDataPath, "player_logs.csv"));
            DeleteIfExists(Path.Combine(Application.persistentDataPath, "cpu_logs.csv"));
            DeleteIfExists(Path.Combine(Application.persistentDataPath, "server_logs.txt"));

            LogToFile("=== UDP Server Started ===");

            IsRunning = true;

            // Start server thread
            serverThread = new Thread(StartServer);
            serverThread.Start();

            // Start keyframe loop
            keyframeThread = new Thread(KeyframeLoop);
            keyframeThread.Start();

            // Start CPU logger loop
            cpuLoggerThread = new Thread(CpuLoggerLoop);
            cpuLoggerThread.Start();
        }

        private void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to delete file {path}: {e.Message}");
            }
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
                    byte[] receivedBytes = udpServer.Receive(ref remoteEP);
                    HandlePacket(receivedBytes, remoteEP);
                }
            }
            catch (SocketException se)
            {
                if (IsRunning)
                    LogToFile("Socket error: " + se.Message);
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
                    if (nextSnapshotId % KeyframeRateHz != 0)
                        SendSnapshot();
                    else
                        SendKeyframe(true);
                }
                catch (Exception e)
                {
                    LogToFile("Keyframe loop error: " + e.Message);
                }

                Thread.Sleep(delayMs);
            }
        }

        private void CpuLoggerLoop()
        {
            // Initialize CSV header on main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                if (!File.Exists(cpuLogPath))
                    File.WriteAllText(cpuLogPath, "timestamp_ms,cpu_percent\n");
            });
            
            prevTime = DateTime.UtcNow;
            prevTotalProcessorTime = currentProcess.TotalProcessorTime;
            int processorCount = Environment.ProcessorCount;

            while (IsRunning)
            {
                Thread.Sleep(1000);

                DateTime currentTime = DateTime.UtcNow;
                TimeSpan currentTotalProcessorTime = currentProcess.TotalProcessorTime;

                double cpuUsedMs = (currentTotalProcessorTime - prevTotalProcessorTime).TotalMilliseconds;
                double totalMs = (currentTime - prevTime).TotalMilliseconds;
                double cpuPercent = (cpuUsedMs / (totalMs * processorCount)) * 100.0;

                long timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string line = $"{timestampMs},{cpuPercent:F2}\n";

                // Dispatch CSV write to main thread
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    try { File.AppendAllText(cpuLogPath, line); }
                    catch (Exception e) { UnityEngine.Debug.LogError("Failed to write CPU log: " + e.Message); }
                });

                prevTotalProcessorTime = currentTotalProcessorTime;
                prevTime = currentTime;
            }
        }

        private void HandlePacket(byte[] data, IPEndPoint sender)
        {
            try
            {
                NetPacket packet = NetPacket.FromBytes(data);
                long latency = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - packet.serverTimestamp;
                
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    string path = Path.Combine(Application.persistentDataPath, "server_received_logs.csv");
                    NetPacketCsvLogger.Log(path, packet, sender.ToString(), latency);
                });

                switch (packet.msgType)
                {
                    case MessageType.CONNECT:
                        HandleConnect(packet, sender);
                        break;
                    case MessageType.SNAPSHOT:
                        HandleSnapshot(packet, sender);
                        break;
                    case MessageType.KEYFRAME:
                        HandleKeyframe(packet, sender);
                        break;
                }
            }
            catch (Exception e)
            {
                LogToFile("Packet handling error: " + e.Message);
            }
        }
        
        /*
         * Handles a new client connecting to the server 
         */
        private void HandleConnect(NetPacket packet, IPEndPoint sender)
        {
            if (((char)(packet.payload[0])).Equals('1'))
            {
                uint playerId = nextPlayerId++;
                clientIds[sender] = playerId;
                PlayerData newPlayer = new PlayerData();
                players.Add(playerId, newPlayer);

                latestSequences[sender] = packet.seqNum;

                LogToFile($"Registered new client {sender} with PlayerID {playerId}");
                SendPlayerId(sender, playerId);

                var others = clientIds.Keys.Where(ep => !ep.Equals(sender)).ToList();
                SendJoinMessage(others, playerId, true);
            }
            else
            {
                if (clientIds.TryGetValue(sender, out uint playerId))
                {
                    clientIds.Remove(sender);
                    players.Remove(playerId);
                    lastSentData.Remove(playerId);

                    var others = clientIds.Keys.Where(ep => !ep.Equals(sender)).ToList();
                    SendJoinMessage(others, playerId, false);
                }
            }
        }

        private void HandleSnapshot(NetPacket packet, IPEndPoint sender)
        {
            int latestKeyframe = latestSequences.TryGetValue(sender, out uint seqNum) ? (int)seqNum / KeyframeRateHz : (int)packet.seqNum / KeyframeRateHz;
            int packetKeyframe = (int)packet.seqNum / KeyframeRateHz;

            if (packetKeyframe < latestKeyframe) return;

            string payload = Encoding.ASCII.GetString(packet.payload);
            PlayerData deltaData = PlayerData.ParseSingularData(payload);

            latestSequences[sender] = packet.seqNum;
            PlayerData playerData = players[clientIds[sender]];
            playerData.Add(deltaData);
        }
        
        private void HandleKeyframe(NetPacket packet, IPEndPoint sender)
        {
            int latestKf = latestSequences.TryGetValue(sender, out uint seqNumber) ? (int)seqNumber / KeyframeRateHz : (int)packet.seqNum / KeyframeRateHz;
            int packetKf = (int)packet.seqNum / KeyframeRateHz;

            if (packetKf < latestKf) return;

            LogToFile("Received keyframe:\n" + packet.ToString());

            string strPayload = Encoding.ASCII.GetString(packet.payload);
            PlayerData realData = PlayerData.ParseSingularData(strPayload);

            latestSequences[sender] = packet.seqNum;
            
            PlayerData currentPlayerData = players[clientIds[sender]];
            PlayerData clientSnapshot = realData.Clone();  // deep copy
            PlayerData serverSnapshot = currentPlayerData.Clone();  // deep copy before modifying

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                string path = Path.Combine(Application.persistentDataPath, "player_logs.csv");
                NetPacketCsvLogger.LogPlayer(path, packet.serverTimestamp, clientSnapshot, serverSnapshot, clientIds[sender]);
            });

            // Now safely copy data for server update
            currentPlayerData.CopyData(realData);
        }

        private void SendJoinMessage(List<IPEndPoint> receivers, uint playerId, bool flag)
        {
            int joined = flag ? 1 : 0;
            string payload = "ID:" + playerId + "/" + joined;
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

            foreach (IPEndPoint clientId in receivers)
                SendData(packet, clientId);
        }

        private void SendSnapshot()
        {
            IPEndPoint[] receivers = clientIds.Keys.ToArray();
            var sb = new StringBuilder();

            foreach (var clientId in clientIds)
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
                if (str != "")
                {
                    sb.Append($"{clientId.Value}|{str};");
                }
            }

            if (sb.Length > 0) sb.Length--; // remove trailing ;

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

            foreach (var clientId in receivers)
                SendData(packet, clientId);
        }

        private void SendKeyframe(bool isPeriodicalKeyframe)
        {
            IPEndPoint[] receivers = clientIds.Keys.ToArray();
            var sb = new StringBuilder();

            foreach (var clientId in clientIds)
            {
                PlayerData playerData = players[clientId.Value];
                string str = playerData.ToRealString();
                if (str == "") continue;

                sb.Append($"{clientId.Value}|{str};");

                if (lastSentData.TryGetValue(clientId.Value, out PlayerData latestData))
                    latestData.CopyData(playerData);
                else
                {
                    PlayerData pd = new PlayerData();
                    pd.CopyData(playerData);
                    lastSentData.Add(clientId.Value, pd);
                }
            }

            if (sb.Length > 0) sb.Length--;

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

            if (isPeriodicalKeyframe) nextSnapshotId++;

            foreach (var clientId in receivers)
                SendData(packet, clientId);
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

            SendData(packet, sender);
        }

        private void SendData(NetPacket packet, IPEndPoint sender)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                string path = Path.Combine(Application.persistentDataPath, "server_logs.csv");
                NetPacketCsvLogger.Log(path, packet);
            });

            byte[] data = packet.ToBytes();
            udpServer.Send(data, data.Length, sender);
        }

        public void StopServer()
        {
            IsRunning = false;
            udpServer?.Close();
            serverThread?.Join();
            keyframeThread?.Join();
            cpuLoggerThread?.Join();
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
                UnityEngine.Debug.LogError("Failed to write log: " + e.Message);
            }
        }
    }
}