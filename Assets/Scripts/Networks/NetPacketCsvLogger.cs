using System.IO;
using Game;

namespace Networks
{
    public static class NetPacketCsvLogger
    {
        /*
         * clientIp must have , in the end to work correctly
         */
        public static void Log(string filePath, NetPacket packet, string clientIp = "")
        {
            bool fileExists = File.Exists(filePath);

            using (StreamWriter writer = new StreamWriter(filePath, append: true))
            {
                if (!fileExists)
                {
                    string header = NetPacket.CsvHeader;
                    if (clientIp != "") header = "client_ip_address," + header;

                    writer.WriteLine(header);
                }

                writer.WriteLine(clientIp + packet.ToCsvRow());
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