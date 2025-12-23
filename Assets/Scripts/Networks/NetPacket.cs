using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Networks
{
    public enum MessageType : byte
    {
        SNAPSHOT = 0,
        EVENT = 1,
        ACK = 2,
        CONNECT = 3,
        KEYFRAME = 4,
        ID_SET = 5
    }
    
    public class NetPacket
    {
        public const string PROTOCOL_ID = "LABA"; // 4 ASCII chars
        public const byte VERSION = 1;
       
        public MessageType msgType;
        public uint snapshotId;
        public uint seqNum;
        public long serverTimestamp;
        public ushort payloadLength;
        public byte[] payload;
        public uint checksum;
        
        // Performance Metrics (for logging only, not serialized in ToBytes)
        public uint clientId;
        public long recvTimeMs;
        public float cpuPercent;
        public float posError;
        public float bandwidthKbps;

        // CRC32 lookup table for polynomial 0xEDB88320 (reversed form of 0x04C11DB7)
        private static readonly uint[] Crc32Table = InitializeCrc32Table();
        public static string CsvHeader =>
            "client_id,snapshot_id,seq_num,server_timestamp_ms,recv_time_ms,latency_ms,jitter_ms,perceived_position_error,cpu_percent,bandwidth_per_client_kbps,msg_type,payload_length,checksum,protocol_id,version";

        public byte[] ToBytes(bool includeChecksum = false)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write(Encoding.ASCII.GetBytes(PROTOCOL_ID)); // 4 bytes
                writer.Write(VERSION);                              // 1 byte
                writer.Write((byte)msgType);                        // 1 byte
                writer.Write(snapshotId);                           // 4 bytes
                writer.Write(seqNum);                               // 4 bytes
                writer.Write(serverTimestamp);                      // 8 bytes
                writer.Write(payloadLength);                        // 2 bytes
                writer.Write(payload);                              // N bytes

                if (includeChecksum)
                {
                    byte[] packetContent = ms.ToArray();
                    checksum = Crc32(packetContent);
                    writer.Write(checksum);                         // 4 bytes
                }

                return ms.ToArray();
            }
        }

        public static NetPacket FromBytes(byte[] data, bool hasChecksum = false) 
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                string protocol = Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (protocol != PROTOCOL_ID)
                    throw new InvalidDataException("Invalid protocol ID");

                byte version = reader.ReadByte();
                if (version != VERSION)
                    throw new InvalidDataException("Version mismatch");

                MessageType msgType = (MessageType)reader.ReadByte();
                uint snapshotId = reader.ReadUInt32();
                uint seqNum = reader.ReadUInt32();
                long serverTimestamp = reader.ReadInt64();
                ushort payloadLength = reader.ReadUInt16();
                byte[] payload = reader.ReadBytes(payloadLength);

                uint checksum = 0;
                if (hasChecksum)
                {
                    checksum = reader.ReadUInt32();
                    byte[] dataWithoutChecksum = new byte[data.Length - 4];
                    Array.Copy(data, 0, dataWithoutChecksum, 0, data.Length - 4);
                    uint calculated = Crc32(dataWithoutChecksum);
                    if (checksum != calculated)
                    {
                        throw new InvalidDataException($"Checksum mismatch. Received: {checksum}, Calculated: {calculated}");
                    }
                }

                return new NetPacket
                {
                    msgType = msgType,
                    snapshotId = snapshotId,
                    seqNum = seqNum,
                    serverTimestamp = serverTimestamp,
                    payloadLength = payloadLength,
                    payload = payload,
                    checksum = checksum
                };
            }
        }
        
        private static uint[] InitializeCrc32Table()
        {
            uint[] table = new uint[256];
            const uint polynomial = 0xEDB88320u;
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0) crc = (crc >> 1) ^ polynomial;
                    else crc >>= 1;
                }
                table[i] = crc;
            }
            return table;
        }
        
        private static uint Crc32(byte[] data)
        {
            unchecked
            {
                uint crc = 0xFFFFFFFFu;
                for (int i = 0; i < data.Length; i++)
                {
                    byte index = (byte)((crc ^ data[i]) & 0xFF);
                    crc = (crc >> 8) ^ Crc32Table[index];
                }
                return ~crc;
            }
        }

        public override string ToString()
        {
            return $"MsgType: {msgType}, SnapshotId: {snapshotId}, SeqNum: {seqNum}, " +
                   $"ServerTimestamp: {serverTimestamp}, PayloadLength: {payloadLength}, " +
                   $"Payload: {Encoding.ASCII.GetString(payload)}";
        }
        
        public string ToCsvRow()
        {
            long latency = (recvTimeMs > 0) ? (recvTimeMs - serverTimestamp) : 0;
            
            return string.Join(",",
                clientId,
                snapshotId,
                seqNum,
                serverTimestamp,
                recvTimeMs,
                latency,
                0, // Jitter placeholder
                posError.ToString("F4"),
                cpuPercent.ToString("F2"),
                bandwidthKbps.ToString("F2"),
                msgType,
                payloadLength,
                checksum,
                PROTOCOL_ID,
                VERSION
            );
        }
    }
}