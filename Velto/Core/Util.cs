using System.Text;
using Decoder = SevenZip.Compression.LZMA.Decoder;
using SevenZip.Compression.LZMA;

namespace Velto.Core;

public class Util
{
    /// <summary>
    /// Reads a stream of bytes from an .osr file representing a string.
    /// Follows: https://osu.ppy.sh/wiki/en/Client/File_formats/osr_%28file_format%29
    /// </summary>
    /// <param name="br">BinaryReader instance to read from.</param>
    /// <returns>String value.</returns>
    public static string ReadOsrString(BinaryReader br)
    {
        byte currentByte = br.ReadByte();

        switch (currentByte)
        {
            case 0:
                return br.ReadString();
            case 11:
                int stringLength = ReadULEB128(br);
                byte[] stringBytes = br.ReadBytes(stringLength);
                return Encoding.UTF8.GetString(stringBytes);
            default:
                return "Expected the first byte to be 0x00 or 0x0b, but got " + currentByte.ToString();
        }
    }

    /// <summary>
    /// Reads an unsigned LEB128 value from the current stream, advancing the stream position.
    /// </summary>
    /// <param name="br"></param>
    /// <returns>Decoded ULEB128 value.</returns>
    public static int ReadULEB128(BinaryReader br)
    {
        int val = 0;
        int shift = 0;
        byte currentByte;

        // Finish when most-significant bit is 0
        do
        {
            currentByte = br.ReadByte();
            val |= (currentByte & 0x7f) << shift;
            shift += 7;
        } while ((currentByte & 0x80) != 0);

        return val;
    }

    /// <summary>
    /// Decompresses an array of bytes using the LZMA algorithm.
    /// </summary>
    /// <param name="input">Input array of bytes.</param>
    /// <returns>Decompressed array of bytes.</returns>
    public static byte[] Decompress(byte[] input)
    {
        Decoder decoder = new Decoder();

        using (Stream inStream = new MemoryStream(input, false))
        {
            MemoryStream outStream = new MemoryStream();

            // Get properties used during the original compression
            byte[] properties = new byte[5];
            inStream.Read(properties, 0, 5);
            decoder.SetDecoderProperties(properties);

            // Get length of the output
            byte[] outputLengthBytes = new byte[8];
            inStream.Read(outputLengthBytes, 0, 8);
            long outputLength = BitConverter.ToInt64(outputLengthBytes, 0);

            decoder.Code(inStream, outStream, inStream.Length, outputLength, null);
            outStream.Flush();

            return outStream.ToArray();
        }
    }
}