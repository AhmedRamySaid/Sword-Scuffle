using System.IO;

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
    }
}
