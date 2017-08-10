using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace AigisPackager
{
    public static class MethodExtensions
    {
        public static int ReadInt32(this MemoryStream ms)
        {
            byte[] i = new byte[4];
            ms.Read(i, 0, 4);
            return BitConverter.ToInt32(i, 0);
        }
        public static void WriteWord(this MemoryStream ms,ushort s)
        {
            byte[] b = BitConverter.GetBytes(s);
            ms.Write(b, 0, 2);
        }
        public static void WriteInt32(this MemoryStream ms,int i)
        {
            byte[] b = BitConverter.GetBytes(i);
            ms.Write(b, 0, 4);
        }
        public static void WriteString(this MemoryStream ms,string s,int length)
        {
            byte[] b = Encoding.UTF8.GetBytes(s);
            ms.Write(b, 0, b.Length);
            ms.WriteByte(0);
            int i = length - b.Length - 1;
            if (i > 0)
            {
                for(int j = 0; j < i; j++)
                {
                    ms.WriteByte(0);
                }
            }
        }

        public static string ReadString(this MemoryStream ms,int length)
        {
            string s;
            if (length > 0)
            {
                byte[] stringBytes = new byte[length];
                ms.Read(stringBytes, 0, length);
                s = Encoding.UTF8.GetString(stringBytes);
            }
            else
            {
                List<byte> stringBytes = new List<byte>();
                for(int i = 0; i < 0xFFFF; i++)
                {
                    byte t = (byte)ms.ReadByte();
                    if (t == 0) break;
                    stringBytes.Add(t);
                }
                s = Encoding.UTF8.GetString(stringBytes.ToArray());
            }
            return s;
        }
        public static ushort ReadWord(this MemoryStream ms)
        {
            byte[] w = new byte[2];
            ms.Read(w, 0, 2);
            return BitConverter.ToUInt16(w, 0);
        }
        public static void Align(this MemoryStream ms,int length)
        {
            long position = ms.Position;
            if (position % 4 == 0) return;
            ms.Seek((4 - position % 4), SeekOrigin.Current);
        }
        public static string ReadString(this FileStream fs, int length)
        {
            byte[] stringBytes = new byte[length];
            fs.Read(stringBytes, 0, length);
            return Encoding.ASCII.GetString(stringBytes);
        }
    }
}
