using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Douyu
{
    public static class TimeStamp
    {
        public static int Now()
        {
            return (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }

        public static int FromTime(DateTime dateTime)
        {
            return (int)(dateTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }

        public static DateTime GetDateTime(int Stamp)
        {
            return (new DateTime(1970, 1, 1).AddSeconds(Stamp).ToLocalTime());
        }
    }
    class DataConverter
    {
        //Convert UInt32 Array into bytes[] Array, isLittleEndian defines input bytes endianness whether in LE/BE order,BE as default
        public static byte[] Uint32ToBytes(uint[] Source, bool isLittleEndian = false)
        {
            int bytesCount = Source.Length * 4;
            byte[] res = new byte[bytesCount];
            byte[] b;
            int blockCount = Source.Length;
            int blen;

            for (int i = 0; i < blockCount; i++)
            {
                blen = bytesCount - (i * 4) < 4 ? 4 : bytesCount % 4;
                b = BitConverter.GetBytes(Source[i]);
                if (BitConverter.IsLittleEndian != isLittleEndian) Array.Reverse(b);
                Array.Copy(b, 0, res, i * 4, blen);
            }
            return res;
        }

        //Convert HEX string into UInt32 Array, isLittleEndian defines input string endianness format whether in LE/BE order,BE as default
        public static uint[] HexToUInt32(string hex, bool isLittleEndian = false)
        {
            StringBuilder sb = new(hex);
            int strLen = hex.Length;
            int byteCount = strLen % 2 == 0 ? strLen / 2 : strLen / 2 + 1;
            int len = byteCount % 4 == 0 ? byteCount / 4 : byteCount / 4 + 1;
            byte[] b = new byte[4];
            uint[] res = new uint[len];
            int j;

            for (int i = 0; i < strLen; i += 2)
            {
                j = strLen - i == 1 ? 1 : 2;
                byte.TryParse(sb.ToString(i, j), System.Globalization.NumberStyles.HexNumber, null, out b[(i % 8) / 2]);

                if (i > 0 && (i % 8 == 6 || strLen - i <= 2))
                {
                    if (BitConverter.IsLittleEndian != isLittleEndian) Array.Reverse(b);
                    res[i / 8] = BitConverter.ToUInt32(b);
                    Array.Clear(b, 0, 4);
                }
            }            
            return res;
        }


        //Convert UInt32 array into Hex string, isLittleEndian switchs output format between LE/BE,BE as default
        public static string UInt32ToHex(uint[] SourceWordArray, bool isLittleEndian = false)
        {
            uint[] sourceArray = SourceWordArray;
            int byteCount = sourceArray.Length * 4;
            StringBuilder sb = new();
            byte[] b = new byte[4];
            for (int i = 0; i < byteCount; i++)
            {
                if (i % 4 == 0)
                {
                    b = BitConverter.GetBytes(sourceArray[i / 4]);
                    if (BitConverter.IsLittleEndian != isLittleEndian) Array.Reverse(b);
                }
                sb.Append(b[i % 4].ToString("x2"));

            }
            return sb.ToString();
        }



        public static uint[] LatinToUInt32(string sourceString, bool isLittleEndian = false)
        {
            int l = sourceString.Length;
            int len = l / 4;
            byte b;
            if (l % 4 != 0) len++;
            uint[] res = new uint[len];
            for (int i = 0; i < l; i++)
            {
                b = Encoding.Unicode.GetBytes(sourceString.Substring(i, 1))[0];
                res[i / 4] |= (uint)(b << (isLittleEndian ? (i % 4) * 8 : 24 - (i % 4) * 8));
            }
            return res;
        }


        public static string UInt32ToLatin(uint[] sourceArray,bool isLittleEndian)
        {
            StringBuilder sb = new();
            byte[] b = new byte[4];
            for (int i = 0; i < sourceArray.Length; i++)
            {
                if (i % 4 == 0)
                {
                    b = BitConverter.GetBytes(sourceArray[i / 4]);
                    if (BitConverter.IsLittleEndian != isLittleEndian) Array.Reverse(b);
                }
                sb.Append(Convert.ToChar(b[i % 4]));
            }
            return sb.ToString();

        }

        // a alternative of Javascript: escape() function
        public static string Escape(string SourceString)
        {

            string unEscape = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@*_+-./";
            StringBuilder sb = new();
            char[] c = SourceString.ToCharArray();
            int count = 0;
            for (int i = 0; i < c.Length; i++)
            {
                if (unEscape.Contains(c[i]))
                {
                    sb.Append(c[i]);
                    count++;
                }
                else
                {
                    int ci = Convert.ToInt32(c[i]);

                    if (ci < 256)
                    {

                        sb.Append($"%{ ci:X2}");
                        count += 3;
                    }
                    else
                    {
                        byte b;
                        for (int j = 0; j < 2; j++)
                        {
                            b = (byte)(ci & 0xff);
                            ci >>= 8;
                            sb.Insert(count, b.ToString("X2"));
                        }
                        sb.Insert(count, "%u");
                        count += 6;
                    }
                }
            }
            return sb.ToString();
        }


        // a alternative of Javascript: unescape() function
        public static string Unescape(string SourceString)
        {

            int len = SourceString.Length;
            StringBuilder sb = new();
            if (SourceString.LastIndexOf("%") > len - 3 || SourceString.LastIndexOf("%u") > len - 6)
            {
                return SourceString;
            }
            else
            {
                int count = 0;
                int ci;
                string subString;
                while (count < len)
                {
                    if (SourceString.Substring(count, 1) != "%")
                    {
                        subString = SourceString.Substring(count, 1);
                        sb.Append(subString);
                        count++;
                    }
                    else if (SourceString.Substring(count, 2) == "%u")
                    {
                        subString = SourceString.Substring(count + 2, 4);
                        if (int.TryParse(subString, System.Globalization.NumberStyles.HexNumber, null, out ci))
                        {
                            sb.Append(Convert.ToChar(ci));
                            count += 6;
                        }
                        else
                        {
                            return SourceString;
                        }
                    }
                    else
                    {
                        subString = SourceString.Substring(count + 1, 2);
                        if (int.TryParse(subString, System.Globalization.NumberStyles.HexNumber, null, out ci))
                        {
                            sb.Append(Convert.ToChar(ci));
                            count += 3;
                        }
                        else
                        {
                            return SourceString;
                        }
                    }

                }
            }
            return sb.ToString();
        }

        // a alternative of Javascript: encodeURIComponent() function
        public static string EncodeURIComponent(string SourceString)
        {

            byte[] b = Encoding.UTF8.GetBytes(SourceString);
            StringBuilder sb = new();
            for (int i = 0; i < b.Length; i++)
            {
                if (b[i] == 0x21 || (b[i] > 0x26 && b[i] < 0x2b) || (b[i] > 0x2c && b[i] < 0x2f) || b[i] == 0x5f || b[i] == 0x7e) sb.Append(Convert.ToChar(b[i])); //special marks ! '()* -. _`
                else if ((b[i] > 0x2f && b[i] < 0x3a)) sb.Append(Convert.ToChar(b[i]));  //numbers
                else if ((b[i] > 0x40 && b[i] < 0x5b)) sb.Append(Convert.ToChar(b[i]));  //uppercase letters
                else if ((b[i] > 0x60 && b[i] < 0x7b)) sb.Append(Convert.ToChar(b[i]));  //lowercase letters
                else sb.Append($"%{b[i]:X2}");

            }
            return sb.ToString();
        }



        // a alternative of Javascript: eecodeURIComponent() function
        public static string DecodeURIComponent(string SourceString)
        {
            int count = 0;
            int len = SourceString.Length;
            List<byte> lb = new();
            string substr;
            if (SourceString.LastIndexOf("%") > len - 3) //if % after third last return source as a error of encoded URI；
            {
                return SourceString;
            }
            while (count < len)
            {
                if (SourceString.Substring(count, 1) == "%")  //寻找UTF8字符，转换为byte保存在list
                {
                    substr = SourceString.Substring(count + 1, 2);
                    if (byte.TryParse(substr, System.Globalization.NumberStyles.HexNumber, null, out byte b)) lb.Add(b);
                    else return SourceString;
                    count += 3;
                }
                else
                {
                    substr = SourceString.Substring(count, 1); //unescape字符，直接转换为byte保存到list
                    lb.Add(Convert.ToByte(substr.ToCharArray()[0]));
                    count++;
                }
            }
            return Encoding.UTF8.GetString(lb.ToArray());
        }

    }
}
