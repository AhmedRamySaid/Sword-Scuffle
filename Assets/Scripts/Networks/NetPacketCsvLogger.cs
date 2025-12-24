using System.IO;
using Game;

namespace Networks
{
    public static class NetPacketCsvLogger
    {
        public static void Log(string filePath, NetPacket packet)
        {
            bool fileExists = File.Exists(filePath);

            using (StreamWriter writer = new StreamWriter(filePath, append: true))
            {
                if (!fileExists)
                {
                    writer.WriteLine(NetPacket.CsvHeader);
                }

                writer.WriteLine(packet.ToCsvRow());
            }
        }
        
        public static void LogPlayer(string filePath, long timestamp, PlayerData clientSideData, PlayerData serverSideData, uint id)
        {
            bool fileExists = File.Exists(filePath);

            using (StreamWriter writer = new StreamWriter(filePath, append: true))
            {
                if (!fileExists)
                {
                    writer.WriteLine(PlayerData.CsvHeader);
                }


                writer.WriteLine(
                    $"{id}," +
                    $"{timestamp}," +
                    $"{clientSideData.ToCsvRow()}," +
                    $"{serverSideData.ToCsvRow()}"
                );
            }
        }
    }
}