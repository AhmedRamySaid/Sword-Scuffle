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
        CONNECT = 3
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
                    checksum = Crc32(payload);
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
                    throw new Exception("Invalid protocol ID");

                byte version = reader.ReadByte();
                if (version != VERSION)
                    throw new Exception("Version mismatch");

                NetPacket packet = new NetPacket
                {
                    msgType = (MessageType)reader.ReadByte(),
                    snapshotId = reader.ReadUInt32(),
                    seqNum = reader.ReadUInt32(),
                    serverTimestamp = reader.ReadInt64(),
                    payloadLength = reader.ReadUInt16(),
                    payload = reader.ReadBytes(reader.ReadUInt16())
                };

                if (hasChecksum)
                    packet.checksum = reader.ReadUInt32();

                return packet;
            }
        }

        private static uint Crc32(byte[] data)
        {
            // Placeholder CRC32 – in a real version use a fast lookup table
            unchecked
            {
                uint crc = 0xFFFFFFFF;
                foreach (byte b in data)
                {
                    crc ^= b;
                    for (int i = 0; i < 8; i++)
                        crc = (crc >> 1) ^ (0xEDB88320u & ((crc & 1) != 0 ? 0xFFFFFFFFu : 0));
                }
                return ~crc;
            }
        }
    }
}
