using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LTSpice_Lib_Merger
{
    public class EncodingHelper
    {
        /// <summary>
        /// Determines a text file's encoding by analyzing its byte order mark (BOM) and if not found try parsing into diferent encodings       
        /// Defaults to UTF8 when detection of the text file's endianness fails.
        /// </summary>
        /// <param name="filename">The text file to analyze.</param>
        /// <returns>The detected encoding or null.</returns>
        public static Encoding GetEncoding(string filename)
        {
            using (var file = new FileStream(filename, FileMode.Open, FileAccess.Read)) {
                BinaryReader r = new BinaryReader(file, Encoding.Default);
                byte[] ss = r.ReadBytes((int)file.Length);
                r.Close();

                // Try to deduce Encoding by the BOM
                var encodingByBOM = GetEncodingByBOM(ss);
                if (encodingByBOM != null)
                    return encodingByBOM;

                // BOM not found
                if (IsUTF16LEBytes(ss))
                {
                    return Encoding.Unicode;
                }

                // Maybe UTF8 ?
                if (IsUTF8Bytes(ss))
                {
                    return Encoding.UTF8;
                }

                // Let's assume 8 bits per character. Try to parse characters into several encodings
                var encodingByParsingLatin1 = GetEncodingByParsing(filename, Encoding.GetEncoding("iso-8859-1"));
                if (encodingByParsingLatin1 != null)
                    return encodingByParsingLatin1;

                var encodingByParsingUTF8 = GetEncodingByParsing(filename, Encoding.UTF8);
                if (encodingByParsingUTF8 != null)
                    return encodingByParsingUTF8;

                var encodingByParsingUTF7 = GetEncodingByParsing(filename, Encoding.UTF7);
                if (encodingByParsingUTF7 != null)
                    return encodingByParsingUTF7;

                return null;   // no encoding found
            }
        }

        /// <summary>
        /// Determines a text file's encoding by analyzing its byte order mark (BOM)  
        /// </summary>
        /// <param name="filename">The text file to analyze.</param>
        /// <returns>The detected encoding.</returns>
        private static Encoding GetEncodingByBOM(byte[] byteOrderMark)
        {
            if (byteOrderMark.Length < 4)
                return null;

            // Analyze the BOM
            if (byteOrderMark[0] == 0x2b && byteOrderMark[1] == 0x2f && byteOrderMark[2] == 0x76) return Encoding.UTF7;
            if (byteOrderMark[0] == 0xef && byteOrderMark[1] == 0xbb && byteOrderMark[2] == 0xbf) return Encoding.UTF8;
            if (byteOrderMark[0] == 0xff && byteOrderMark[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
            if (byteOrderMark[0] == 0xfe && byteOrderMark[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE
            if (byteOrderMark[0] == 0 && byteOrderMark[1] == 0 && byteOrderMark[2] == 0xfe && byteOrderMark[3] == 0xff) return Encoding.UTF32;

            return null;    // no BOM found
        }

        private static bool IsUTF16LEBytes(byte[] data)
        {
            if ((data.Length & 1) != 0) 
                return false;

            int maxidx = data.Length / 2;
            int even = 0, odd = 0;
            for (int i = 0; i < maxidx; i++)
            {
                even += data[i*2] == 0 ? 1 : 0;
                odd += data[i*2+1] == 0 ? 1 : 0;
            }
            int threshlo = maxidx / 10;
            int threshhi = maxidx - threshlo;
            if (even < threshlo && odd > threshhi)
                return true;
            return false;
        }

        private static bool IsUTF8Bytes(byte[] data)
        {
            int charByteCounter = 1;
            byte curByte;
            for (int i = 0; i < data.Length; i++)
            {
                curByte = data[i];
                if (charByteCounter == 1)
                {
                    if (curByte >= 0x80)
                    {
                        while (((curByte <<= 1) & 0x80) != 0)
                        {
                            charByteCounter++;
                        }

                        if (charByteCounter == 1 || charByteCounter > 6)
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    if ((curByte & 0xC0) != 0x80)
                    {
                        return false;
                    }
                    charByteCounter--;
                }
            }
            if (charByteCounter > 1)
            {
                throw new Exception("Error byte format");
            }
            return true;
        }

        private static Encoding GetEncodingByParsing(string filename, Encoding encoding)
        {
            var encodingVerifier = Encoding.GetEncoding(encoding.BodyName, new EncoderExceptionFallback(), new DecoderExceptionFallback());

            try
            {
                using (var textReader = new StreamReader(filename, encodingVerifier, detectEncodingFromByteOrderMarks: true))
                {
                    while (!textReader.EndOfStream)
                    {
                        textReader.ReadLine();   // in order to increment the stream position
                    }

                    // all text parsed ok
                    return textReader.CurrentEncoding;
                }
            }
            catch (Exception ex) { }

            return null;    // 
        }
    }
}
