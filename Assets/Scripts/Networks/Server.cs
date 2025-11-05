using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
        private readonly Dictionary<IPEndPoint, uint> clientIds = new Dictionary<IPEndPoint, uint>();
        private uint nextPlayerId = 1; // player ids start at 1, the client treats itself as 0

        public void InitializeServer()
        {
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
                Debug.Log($"UDP Server started on port {Port}");
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
                    Debug.LogError("Socket error: " + se.Message);
                    LogToFile("Socket error: " + se.Message);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Server error: " + e.Message);
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
                                Debug.Log($"Registered new client {sender} with PlayerID {playerId}");
                                LogToFile($"Registered new client {sender} with PlayerID {playerId}");
                                SendPlayerId(sender, playerId);
                                SendKeyframe(sender);
                            }
                        }
                        else // Terminated the connection
                        {
                            //todo implement
                        }
                        break;
                    default:
                        break;
                }

                clientIds.TryGetValue(sender, out uint id);
                byte[] idBytes = Encoding.ASCII.GetBytes($"ID:{id}."); // convert string to bytes
                byte[] newPayload = new byte[idBytes.Length + packet.payload.Length];

                // copy ID bytes first
                Buffer.BlockCopy(idBytes, 0, newPayload, 0, idBytes.Length);

                // copy the original payload after
                Buffer.BlockCopy(packet.payload, 0, newPayload, idBytes.Length, packet.payload.Length);

                // assign back to packet.payload
                packet.payload = newPayload;
                packet.payloadLength = (ushort)newPayload.Length;

                // Broadcast to all other clients
                BroadcastToClients(packet.ToBytes(), sender);
            }
            catch (Exception e)
            {
                Debug.LogError("Packet handling error: " + e.Message);
                LogToFile("Packet handling error: " + e.Message);
            }
        }

        private void SendKeyframe(IPEndPoint sender)
        {
            if (clientIds.TryGetValue(sender, out uint playerId))
            {
                if (GameManager.Instance.Players.TryGetValue(playerId, out GameObject player))
                {
                    if (GameManager.Instance.player.Equals(player)) return;
                }
            }

            var sb = new StringBuilder();

            foreach (KeyValuePair<IPEndPoint, uint> clientId in clientIds)
            {
                if (!GameManager.Instance.Players.TryGetValue(clientId.Value, out GameObject player))
                {
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        player = new GameObject();
                        player.transform.position = new Vector3(3, 3, 1); // default coordinates for player prefab
                    });
                }
                
                // If it's the sender, mark as 0; otherwise use the player's ID
                sb.Append(clientId.Value.ToString());

                // Append positions using InvariantCulture to use a standard form across all devices
                Vector3 pos = Vector3.zero;
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    pos = player.transform.position;
                });
                sb.Append(':')
                    .Append(pos.x.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(pos.y.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(pos.z.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
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
            udpServer.Send(data, data.Length, sender);
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
            Debug.Log("UDP server stopped.");
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