using System;
using System.IO;
using System.Text;
using Unity.VisualScripting;
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
        
        // CRC32 lookup table for polynomial 0xEDB88320 (reversed form of 0x04C11DB7)
        private static readonly uint[] Crc32Table = InitializeCrc32Table();
        public static string CsvHeader =>
            "protocol_id,version,msg_type,snapshot_id,seq_num,server_timestamp,payload_length,payload,checksum";

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

                // Read the fixed fields first
                MessageType msgType = (MessageType)reader.ReadByte();
                uint snapshotId = reader.ReadUInt32();
                uint seqNum = reader.ReadUInt32();
                long serverTimestamp = reader.ReadInt64();

                // Read payload length
                ushort payloadLength = reader.ReadUInt16();

                // Now read exactly payloadLength bytes
                byte[] payload = reader.ReadBytes(payloadLength);

                // Optional checksum
                uint checksum = 0;
                if (hasChecksum)
                {
                    checksum = reader.ReadUInt32();
                    // Validate checksum - calculate on everything except the checksum field itself
                    byte[] dataWithoutChecksum = new byte[data.Length - 4];
                    Array.Copy(data, 0, dataWithoutChecksum, 0, data.Length - 4);
                    uint calculated = Crc32(dataWithoutChecksum);
                    if (checksum != calculated)
                    {
                        throw new InvalidDataException($"Checksum mismatch. Received: {checksum}, Calculated: {calculated}");
                    }
                }

                // Build the packet instance
                NetPacket packet = new NetPacket
                {
                    msgType = msgType,
                    snapshotId = snapshotId,
                    seqNum = seqNum,
                    serverTimestamp = serverTimestamp,
                    payloadLength = payloadLength,
                    payload = payload,
                    checksum = checksum
                };

                return packet;
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
                    if ((crc & 1) != 0)
                        crc = (crc >> 1) ^ polynomial;
                    else
                        crc >>= 1;
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
            string payloadStr = Encoding.ASCII.GetString(payload);
            payloadStr = payloadStr.Replace(",", " "); //Otherwise it'll count as a column
            
            return string.Join(",",
                PROTOCOL_ID,
                VERSION,
                msgType,
                snapshotId,
                seqNum,
                serverTimestamp,
                payloadLength,
                payloadStr,
                checksum
            );
        }

    }
}