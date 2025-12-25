using System.IO;
using Game;

namespace Networks
{
    public static class NetPacketCsvLogger
    {
        public static void Log(string filePath, NetPacket packet, string clientIp = "", long latency = 0)
        {
            bool fileExists = File.Exists(filePath);

            using (StreamWriter writer = new StreamWriter(filePath, append: true))
            {
                if (!fileExists)
                {
                    string header = NetPacket.CsvHeader;
                    if (clientIp != "") header = "client_ip_address," + header + "," + "latency";

                    writer.WriteLine(header);
                }
                
                if (clientIp == "") writer.WriteLine(packet.ToCsvRow());
                else
                {
                    string line = clientIp + "," + NetPacket.CsvHeader + "," + latency;
                    writer.WriteLine(line);
                }
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