using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Yaml_Creator.SongData;

namespace Yaml_Creator
{

    public static class SongDataConverter
    {
        public class CompressedSongData
        {
            public string Title { get; set; }
            public Dictionary<string, int> Difficulties { get; set; }
        }
        public static string ConvertSongDataToBase64(IEnumerable<SongExportExtendedData> songArray)
        {
            var songDict = songArray.ToDictionary(x => x.core.SongChecksum, x => x.Compress());

            string Json = JsonConvert.SerializeObject(songDict, Formatting.None);

            byte[] jsonBytes = Encoding.UTF8.GetBytes(Json);

            using (var outputStream = new MemoryStream())
            {
                using (var deflateStream = new DeflateStream(outputStream, CompressionLevel.Optimal))
                {
                    deflateStream.Write(jsonBytes, 0, jsonBytes.Length);
                }

                byte[] compressed = outputStream.ToArray();

                string base64 = Convert.ToBase64String(compressed);
                string base64url = base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');

                return base64url;
            }
        }
    }
}
