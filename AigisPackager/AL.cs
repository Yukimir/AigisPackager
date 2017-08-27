using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;

namespace AigisPackager
{
    public class AL
    {
        public byte[] RawBuffer { get; set; }
        public AL(byte[] buffer)
        {
            RawBuffer = buffer;
        }
        public virtual void SaveFile(string path)
        {
            FileStream fs = new FileStream(path, FileMode.OpenOrCreate);
            fs.Write(RawBuffer,0,RawBuffer.Length);
            fs.Close();
        }
        public virtual byte[] Package(string path)
        {
            return RawBuffer;
        }
    }
    class ALLZ
    {
        private void ensure(int count)
        {
            while (bitsCount < count)
            {
                bits = bits | (ms.ReadByte() << bitsCount);
                bitsCount += 8;
            }
        }

        private int readBit()
        {
            ensure(1);
            int result = bits & 1;
            bits = bits >> 1;
            bitsCount -= 1;
            return result;
        }

        private int readBits(int count)
        {
            ensure(count);
            int result = bits & ((1 << count) - 1);
            bits = bits >> count;
            bitsCount -= count;
            return result;
        }

        private int readUnary()
        {
            int n = 0;
            while (readBit() == 1) n++;
            return n;
        }

        private int readControl(int minBits)
        {
            int u = readUnary();
            int n = readBits(u + minBits);
            if (u > 0)
            {
                return n + (((1 << u) - 1) << minBits);
            }
            else
            {
                return n;
            }
        }
        private int readControlLength()
        {
            return 3 + readControl(minbitsLength);
        }
        private int readControlOffset()
        {
            int offset = -1 - readControl(minbitsOffset);
            return offset;
        }
        private int readControlLiteral()
        {
            return 1 + readControl(minbitsLiteral);
        }
        private void copyWord(int offset, int length)
        {
            int trueOffset = offset - 1;
            for (int i = 0; i < length; i++)
            {
                if (offset < 0) trueOffset = (int)dstMs.Position + offset;
                dstMs.WriteByte(dst[trueOffset]);
            }
        }
        private void copyLiteral(int control)
        {
            byte[] temp = new byte[control];
            ms.Read(temp, 0, control);
            dstMs.Write(temp, 0, control);
        }

        private MemoryStream ms = null;
        int vers = 0;
        int minbitsLength = 0;
        int minbitsOffset = 0;
        int minbitsLiteral = 0;
        int dstSize = 0;
        byte[] dst = null;
        MemoryStream dstMs = null;

        int bits = 0;
        int bitsCount = 0;

        private ALLZ(byte[] buffer)
        {
            buffer = buffer.Skip(4).Take(buffer.Length - 4).ToArray();
            ms = new MemoryStream(buffer);
            vers = ms.ReadByte();
            minbitsLength = ms.ReadByte();
            minbitsOffset = ms.ReadByte();
            minbitsLiteral = ms.ReadByte();
            dstSize = ms.ReadInt32();
            dst = new byte[dstSize];
            dstMs = new MemoryStream(dst);
        }

        public byte[] Decompress()
        {
            copyLiteral(readControlLiteral());
            int wordOffset = readControlOffset();
            int wordLength = readControlLength();
            int literalLength = 0;

            string finish = "overflow";
            while (ms.Position < ms.Length)
            {
                if (dstMs.Position + wordLength >= dst.Length)
                {
                    finish = "word";
                    break;
                }
                if (readBit() == 0)
                {
                    literalLength = readControlLiteral();
                    if (dstMs.Position + wordLength + literalLength >= dst.Length)
                    {
                        finish = "literal";
                        break;
                    }
                    copyWord(wordOffset, wordLength);
                    copyLiteral(literalLength);
                    wordOffset = readControlOffset();
                    wordLength = readControlLength();
                }
                else
                {
                    copyWord(wordOffset, wordLength);
                    wordOffset = readControlOffset();
                    wordLength = readControlLength();
                }
            }
            if (finish == "word") copyWord(wordOffset, wordLength);
            if (finish == "literal")
            {
                copyWord(wordOffset, wordLength);
                copyLiteral(literalLength);
            }

            return dst.ToArray();
        }

        public static byte[] Decompress(byte[] buffer)
        {
            ALLZ obj = new ALLZ(buffer);
            return obj.Decompress();
        }
        public static byte[] Compress(byte[] buffer)
        {

            return new byte[5];
        }
    }
    public class ALTB : AL
    {
        private byte[] rowBuffer;
        private int vers;
        private int form;
        private int count;
        private int unk1;
        private int tableEntry;
        private int size;
        private int stringsStart = -1;
        private int stringsSize = -1;
        private int stringsSizePosition = -1;
        private int namesStart = -1;
        private int namesOffset = -1;
        private string label;
        private string name = "";
        private List<ALRD.Header> headers;
        private List<Dictionary<string, object>> contents;
        public Dictionary<long, string> stringDictionary;
        public ALTB(byte[] buffer):base(buffer)
        {
            buffer = buffer.Skip(4).Take(buffer.Length - 4).ToArray();
            rowBuffer = buffer;
            int start_offset = -4;

            MemoryStream ms = new MemoryStream(buffer);
            vers = ms.ReadByte();
            form = ms.ReadByte();
            count = ms.ReadWord();
            unk1 = ms.ReadWord();
            //需要加验证form和unk1的值
            //0x10 -> 0x14
            //0x14 -> 0x1c
            //0x1e -> 0x20
            tableEntry = start_offset + ms.ReadWord();
            size = ms.ReadInt32();
            if (form == 0x14 || form == 0x1e)
            {
                stringsSizePosition = (int)ms.Position;
                stringsSize = ms.ReadInt32();
                stringsStart = ms.ReadInt32() + start_offset;
                //读取为stringDictionary
                stringDictionary = new Dictionary<long, string>();
                long position = ms.Position;
                ms.Position = stringsStart;
                while (ms.Position < stringsStart + stringsSize)
                {
                    long offset = ms.Position - stringsStart;
                    string s = ms.ReadString(-1);
                    stringDictionary.Add(offset, s);
                }
                ms.Position = position;
            }
            if (form == 0x1e)
            {
                namesOffset = (int)ms.Position;
                namesStart = ms.ReadInt32() + start_offset;
            }

            label = ms.ReadString(4);
            byte[] alrdByte = new byte[tableEntry - ms.Position];
            ms.Read(alrdByte, 0, alrdByte.Length);
            ALRD alrd = new ALRD(alrdByte);
            //ms -> tableEntry
            headers = alrd.Headers;
            contents = new List<Dictionary<string, object>>();
            for (int i = 0; i < count; i++)
            {
                long rowEntry = ms.Position;
                Dictionary<string, object> row = new Dictionary<string, object>();
                foreach (ALRD.Header header in headers)
                {
                    ms.Position = rowEntry + header.Offset;
                    string key = header.NameEN;
                    object v = null;
                    if (header.Type == 1)
                    {
                        v = ms.ReadInt32();
                    }
                    if (header.Type == 4)
                    {
                        v = (float)ms.ReadInt32();
                    }
                    if (header.Type == 5)
                    {
                        v = ms.ReadByte();
                    }
                    if (header.Type == 0x20)
                    {
                        int stringOffset = ms.ReadInt32();
                        ms.Position = stringsStart + stringOffset;
                        v = ms.ReadString(-1);
                    }
                    row.Add(key, v);
                }
                contents.Add(row);
                ms.Position = rowEntry + size;
            }
            if (namesStart != -1)
            {
                ms.Position = namesStart;
                int unknownNames = ms.ReadInt32();
                int nameLength = ms.ReadByte();
                string name = ms.ReadString(nameLength);
                this.name = name;
            }
            ms.Close();
        }
        public byte[] CreateNewALTBFileWithStringField(string[] strings)
        {
            byte[] newStringFieldBytes = GenerateStringFieldBytes(strings.ToArray());
            Dictionary<long, string> newStringDictionary = GetStringDictionary(newStringFieldBytes);
            if (stringDictionary.Count != newStringDictionary.Count) throw new Exception("长度错误");

            //创建StringField的Offset变化表
            List<long> oldOffsets = new List<long>();
            List<long> newOffsets = new List<long>();
            foreach (long offset in stringDictionary.Keys)
            {
                oldOffsets.Add(offset);
            }
            foreach (long offset in newStringDictionary.Keys)
            {
                newOffsets.Add(offset);
            }
            Dictionary<long, long> offsetChangeDictionary = new Dictionary<long, long>();
            for (int i = 0; i < oldOffsets.Count; i++)
            {
                offsetChangeDictionary.Add(oldOffsets[i], newOffsets[i]);
            }

            //按照字典修改Filed部分所有type为"0x20"的值
            MemoryStream ms = new MemoryStream(rowBuffer);
            ms.Position = tableEntry;
            for (int i = 0; i < count; i++)
            {
                long rowEntry = ms.Position;
                foreach (ALRD.Header header in headers)
                {
                    ms.Position = rowEntry + header.Offset;
                    if (header.Type == 0x20)
                    {
                        long offset = ms.ReadInt32();
                        ms.Seek(-4, SeekOrigin.Current);
                        if (offsetChangeDictionary.ContainsKey(offset))
                        {
                            ms.Write(BitConverter.GetBytes((int)offsetChangeDictionary[offset]), 0, 4);
                        }
                        else
                        {
                            throw new Exception("没有该offset");
                        }
                    }
                }
                ms.Position = rowEntry + size;
            }
            //把strings块的大小写入相应位置
            ms.Position = stringsSizePosition;
            ms.Write(BitConverter.GetBytes(newStringFieldBytes.Length), 0, 4);
            //制作新strings块
            //首先对齐
            int alignLength = newStringFieldBytes.Length + (4 - newStringFieldBytes.Length % 4);
            if (namesStart != -1)
            {
                //计算新name块的位置
                //nameStart - stringStart = oldStringLengthWithAlign
                //alignLength - oldStringLengthWithAlign = delta
                //nameStart + delta = newNameStart
                //写入buffer
                int newNameStart = namesStart + (alignLength - (namesStart - stringsStart)) + 4;
                ms.Position = namesOffset;
                ms.Write(BitConverter.GetBytes(newNameStart), 0, 4);
            }
            //准备搬运
            //先把最头的部分拿出来
            byte[] literal = rowBuffer.Take(stringsStart).ToArray();
            //然后制作新的带Align的Strings块
            byte[] alignedStrings = new byte[alignLength];
            newStringFieldBytes.CopyTo(alignedStrings, 0);
            //最后拿出尾巴
            ms.Position = stringsStart + stringsSize;
            ms.Align(4);
            byte[] tail = new byte[ms.Length - ms.Position];
            ms.Read(tail, 0, tail.Length);

            //声明最终Byte的List
            List<byte> result = new List<byte>();
            //开始搬运
            result.AddRange(Encoding.ASCII.GetBytes("ALTB"));
            result.AddRange(literal);
            result.AddRange(alignedStrings);
            result.AddRange(tail);
            ms.Close();
            return result.ToArray();

        }
        public static byte[] GenerateStringFieldBytes(string[] s)
        {
            List<byte> b = new List<byte>();
            for (int i = 0; i < s.Length; i++)
            {
                byte[] UTFBytes = Encoding.UTF8.GetBytes(s[i]);
                b.AddRange(UTFBytes);
                b.Add(0);
            }
            return b.ToArray();
        }
        public static Dictionary<long, string> GetStringDictionary(byte[] buffer)
        {
            Dictionary<long, string> stringDictionary = new Dictionary<long, string>();
            MemoryStream ms = new MemoryStream(buffer);
            while (ms.Position < ms.Length)
            {
                long offset = ms.Position;
                string s = ms.ReadString(-1);
                stringDictionary.Add(offset, s);
            }
            return stringDictionary;
        }
        public string[] GetStringFields()
        {
            List<string> strings = new List<string>();
            foreach (string s in stringDictionary.Values)
            {
                string news = s.Replace("\n", @"\n");
                strings.Add(news);
            }
            return strings.ToArray();
        }

        public override byte[] Package(string path)
        {
            if (!File.Exists(Path.ChangeExtension(path, "txt")))
            {
                return RawBuffer;
            }
            else
            {
                StreamReader sw = new StreamReader(Path.ChangeExtension(path, "txt"));
                List<string> sList = new List<string>();
                while (!sw.EndOfStream)
                {
                    string s = sw.ReadLine();
                    s = s.Replace(@"\n", "\n");
                    sList.Add(s);
                }
                sw.Close();
                byte[] newALTBBuffer = CreateNewALTBFileWithStringField(sList.ToArray());
                return newALTBBuffer;
            }
        }

        public override void SaveFile(string path)
        {
            byte[] newALTBBuffer = Package(path);
            //输出试试看
            if (!Directory.Exists(Path.GetDirectoryName(path) + @"\output")) Directory.CreateDirectory(Path.GetDirectoryName(path) + @"\output");
            FileStream fs = new FileStream(Path.Combine(Path.GetDirectoryName(path), "output", Path.GetFileNameWithoutExtension(path) + ".atb"), FileMode.OpenOrCreate);
            fs.Write(newALTBBuffer, 0, newALTBBuffer.Length);
            fs.Close();
        }
    }
    public class ALRD : AL
    {
        public class Header
        {
            public int Offset { get; set; }
            public int Type { get; set; }
            public string NameEN { get; set; }
            public string NameJP { get; set; }
        }
        private int count;
        private int size;
        private int vers;
        public List<Header> Headers { get; set; }
        public ALRD(byte[] buffer):base(buffer)
        {
            MemoryStream ms = new MemoryStream(buffer);
            string type = ms.ReadString(4);
            if (type != "ALRD") throw new Exception("这不是ALRD，笨蛋");
            vers = ms.ReadWord();
            count = ms.ReadWord();
            size = ms.ReadWord();
            Headers = new List<Header>();
            for (int i = 0; i < count; i++)
            {
                Header header = new Header();
                header.Offset = ms.ReadWord();
                header.Type = ms.ReadByte();
                int emptyLength = ms.ReadByte();
                int lengthEN = ms.ReadByte();
                int lengthJP = ms.ReadByte();
                header.NameEN = ms.ReadString(-1);
                header.NameJP = ms.ReadString(-1);
                ms.Align(4);
                ms.Seek(emptyLength, SeekOrigin.Current);
                ms.Align(4);
                Headers.Add(header);
            }
            ms.Close();
        }
    }
    public class ALAR : AL
    {
        public class Entry
        {
            public ushort Index { get; set; }
            public ushort Unknown1 { get; set; }
            public int Address { get; set; }
            public int Offset { get; set; }
            public int Size { get; set; }
            public byte[] Unknown2 { get; set; }
            public string Name { get; set; }
            public ushort Unknown3 { get; set; }
            public byte[] Content { get; set; }
            public AL ParsedContent { get; set; }
        }
        public byte Vers { get; set; }
        public byte Unknown { get; set; }
        public byte[] UnknownBytes { get; set; }
        public ushort Unknown1 { get; set; }
        public ushort Unknown2 { get; set; }
        public ushort Count { get; set; }
        public ushort DataOffset { get; set; }
        public int DataOffsetByData { get; set; }
        public List<ushort> TocOffsetList { get; set; }
        public List<Entry> Files { get; set; }
        public ALAR(byte[] buffer):base(buffer)
        {
            buffer = buffer.Skip(4).Take(buffer.Length - 4).ToArray();
            //初始化
            Files = new List<Entry>();
            TocOffsetList = new List<ushort>();
            MemoryStream ms = new MemoryStream(buffer);
            long basePosition = -4;

            //开始
            Vers = (byte)ms.ReadByte();
            Unknown =  (byte)ms.ReadByte();
            if (Vers != 2 && Vers != 3) throw new Exception("ALAR版本错误");
            if(Vers == 2)
            {
                Count = ms.ReadWord();
                byte[] unk = new byte[0x10 - 0x08];
                ms.Read(unk, 0, unk.Length);
                UnknownBytes = unk;
            }
            if(Vers == 3)
            {
                Count = ms.ReadWord(); //+0x06
                Unknown1 = ms.ReadWord(); //+0x08
                Unknown2 = ms.ReadWord(); //+0x0A
                byte[] unk = new byte[0x10 - 0x0C];
                ms.Read(unk, 0, unk.Length);
                UnknownBytes = unk;
                DataOffset = ms.ReadWord();
                for(int i = 0; i < Count; i++)
                {
                    TocOffsetList.Add(ms.ReadWord());
                }
                ms.Align(4);
            }
            for(int i = 0; i < Count; i++)
            {
                Entry entry = parseTocEntry(ms, basePosition);
                long position = ms.Position;
                //拿东西出来
                byte[] data = new byte[entry.Size];
                ms.Position = basePosition + entry.Address;
                ms.Read(data, 0, data.Length);
                entry.Content = data;
                ms.Position = position;
                Files.Add(entry);
            }
            if (Vers == 2) DataOffsetByData = Files[0].Address - 0x22;
            if (Vers == 3) DataOffsetByData = Files[0].Address;
        }

        private Entry parseTocEntry(MemoryStream ms, long basePosition)
        {
            Entry entry = new Entry();
            if (Vers == 2)
            {
                entry.Index = ms.ReadWord();
                entry.Unknown1 = ms.ReadWord();
                entry.Address = ms.ReadInt32();
                entry.Size = ms.ReadInt32();
                entry.Unknown2 = BitConverter.GetBytes(ms.ReadInt32());
                long position = ms.Position;
                ms.Seek(basePosition + entry.Address - 0x22, SeekOrigin.Begin);
                entry.Name = ms.ReadString(-1);
                ms.Position = basePosition + entry.Address - 0x02;
                entry.Unknown3 = ms.ReadWord();
                ms.Position = position;
            }
            else
            {
                entry.Index = ms.ReadWord();
                entry.Unknown1 = ms.ReadWord();
                entry.Address = ms.ReadInt32();
                entry.Size = ms.ReadInt32();
                byte[] unk = new byte[6];
                ms.Read(unk, 0, unk.Length);
                entry.Unknown2 = unk;
                entry.Name = ms.ReadString(-1);
                ms.Align(4);
            }
            return entry;
        }

        public override byte[] Package(string path)
        {
            if (!Directory.Exists(Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path)))) return RawBuffer;
            //ALAR的package
            //ALAR文件分为三部分，头、索引、主体。
            //这里换个方法吧。把源文件的所有东西都读出来，然后在不依赖源文件的基础上，写新文件出来。美滋滋不是么（直接filestream写，强无敌

            //处理新的入口和乱七八糟的东西
            int nowOffset = 0;
            for (int i = 0; i < Files.Count; i++)
            {
                Entry entry = Files[i];
                string filePath = Path.Combine(
                                                Path.GetDirectoryName(path),
                                                Path.GetFileNameWithoutExtension(path),
                                                entry.Name
                                                );
                byte[] newContent = entry.ParsedContent.Package(filePath);
                entry.Content = newContent;
                entry.Size = newContent.Length;
                if (Vers == 3) TocOffsetList[i] = (ushort)nowOffset; nowOffset += entry.Size;
            }

            MemoryStream ms = new MemoryStream();
            ms.Write(Encoding.ASCII.GetBytes("ALAR"), 0, 4);
            ms.WriteByte(Vers);
            ms.WriteByte(Unknown);
            if(Vers == 2)
            {
                ms.WriteWord(Count);
                ms.Write(UnknownBytes,0,UnknownBytes.Length);
            }
            if(Vers == 3)
            {
                ms.WriteWord(Count);
                ms.WriteWord(Unknown1);
                ms.WriteWord(Unknown2);
                ms.Write(UnknownBytes, 0, UnknownBytes.Length);
                ms.WriteWord(DataOffset);
                for(int i = 0; i < Count; i++)
                {
                    ms.WriteWord(TocOffsetList[i]);
                }
                ms.Align(4);
            }
            List<int> offsetAddressList = new List<int>();
            for(int i = 0; i < Count; i++)
            {
                Entry entry = Files[i];
                //一边写Entry一边写内容，要写address的地方，空出来。
                if(Vers == 2)
                {
                    //名字写在文件前面的，固定长度16字节
                    ms.WriteWord(entry.Index);                              //0x00
                    ms.WriteWord(entry.Unknown1);                           //0x02
                    offsetAddressList.Add((int)ms.Position);                
                    ms.WriteInt32(0);                                       //0x04
                    ms.WriteInt32(entry.Size);                              //0x08
                    ms.Write(entry.Unknown2, 0, entry.Unknown2.Length);     //0x0C
                }
                if(Vers == 3)
                {
                    //名字写在Entry里
                    ms.WriteWord(entry.Index);
                    ms.WriteWord(entry.Unknown1);
                    offsetAddressList.Add((int)ms.Position);
                    ms.WriteInt32(0);
                    ms.WriteInt32(entry.Size);
                    ms.Write(entry.Unknown2, 0, entry.Unknown2.Length);
                    ms.WriteString(entry.Name, 0);
                    ms.Align(4);
                }
            }
            ms.WriteWord(0);
            //接下来写内容，写内容的时候把address写到相应位置去
            for(int i = 0; i < Count; i++)
            {
                Entry entry = Files[i];
                if(Vers == 2)
                {
                    //这边还要写名字进去，蛋疼
                    ms.WriteString(entry.Name, 0x20);
                    ms.WriteWord(entry.Unknown3);
                }
                int position = (int)ms.Position;
                ms.Position = offsetAddressList[i];
                ms.WriteInt32(position);
                ms.Position = position;
                ms.Write(entry.Content, 0, entry.Content.Length);
                if (i == Count - 1) continue;
                ms.Align(4);
                ms.WriteWord(0);
            }
            int length = (int)ms.Position;
            byte[] result = new byte[length];
            ms.Position = 0;
            ms.Read(result, 0, length);
            return result;
        }
        public override void SaveFile(string path)
        {
            byte[] newALTBBuffer = Package(path);
            //输出试试看
            if (!Directory.Exists(Path.GetDirectoryName(path) + @"\output")) Directory.CreateDirectory(Path.GetDirectoryName(path) + @"\output");
            FileStream fs = new FileStream(Path.Combine(Path.GetDirectoryName(path), "output", Path.GetFileNameWithoutExtension(path) + ".aar"), FileMode.OpenOrCreate);
            fs.Write(newALTBBuffer, 0, newALTBBuffer.Length);
            fs.Close();
        }
    }
    public class ALFT:AL
    {
        public class Range
        {
            public ushort CharCodeMin { get; set; }
            public ushort CharCodeMax { get; set; }
            public ushort ImageOffset { get; set; }
        }
        public byte Vers { get; set; }
        public byte[] Form { get; set; }
        public byte BlockWidth { get; set; }
        public byte BlockHeight { get; set; }
        public ushort Unknown1 { get; set; }
        public ushort RangeCount { get; set; }
        public List<Range> RangeList { get; set; }
        public ushort WidthFieldCount { get; set; }
        public List<byte> WidthList { get; set; }
        public ALIG FontImage { get; set; }
        public ALFT(byte[] buffer) : base(buffer)
        {
            RangeList = new List<Range>();
            WidthList = new List<byte>();

            buffer = buffer.Skip(4).Take(buffer.Length - 4).ToArray();
            MemoryStream ms = new MemoryStream(buffer);
            Vers = (byte)ms.ReadByte();
            Form = new byte[3];
            ms.Read(Form, 0, 3);
            BlockWidth = (byte)ms.ReadByte();
            BlockHeight = (byte)ms.ReadByte();
            Unknown1 = ms.ReadWord();
            RangeCount = ms.ReadWord();
            for(int i = 0; i < RangeCount; i++)
            {
                Range range = new Range();
                range.CharCodeMin = ms.ReadWord();
                range.CharCodeMax = ms.ReadWord();
                range.ImageOffset = ms.ReadWord();
                RangeList.Add(range);
            }
            WidthFieldCount = ms.ReadWord();
            Console.WriteLine(WidthFieldCount);
            for(int i = 0; i < WidthFieldCount; i++)
            {
                WidthList.Add((byte)ms.ReadByte());
            }
            byte[] ALIGByte = new byte[ms.Length - ms.Position];
            ms.Read(ALIGByte, 0, ALIGByte.Length);
            //处理ALIG
            FontImage = new ALIG(ALIGByte);
        }

        public override byte[] Package(string path)
        {
            CreateFont("UniSun", @"C:\Users\Pro\Documents\AigisTools\charDecode\range.txt");
            MemoryStream ms = new MemoryStream();
            ms.Write(Encoding.ASCII.GetBytes("ALFT"), 0, 4);
            ms.WriteByte(Vers);
            ms.Write(Form, 0, 3);
            ms.WriteByte(BlockWidth);
            ms.WriteByte(BlockHeight);
            ms.WriteWord(Unknown1);
            ms.WriteWord(RangeCount);
            for(int i = 0; i < RangeCount; i++)
            {
                Range range = RangeList[i];
                ms.WriteWord(range.CharCodeMin);
                ms.WriteWord(range.CharCodeMax);
                ms.WriteWord(range.ImageOffset);
            }
            ms.WriteWord(WidthFieldCount);
            for(int i = 0; i < WidthFieldCount; i++)
            {
                ms.WriteByte(WidthList[i]);
            }
            byte[] ALIGByte = FontImage.Package(path);
            ms.Write(ALIGByte, 0, ALIGByte.Length);
            byte[] result = new byte[ms.Position];
            ms.Position = 0;
            ms.Read(result, 0, result.Length);
            return result;
        }

        public override void SaveFile(string path)
        {
            byte[] newALTBBuffer = Package(path);
            //输出试试看
            if (!Directory.Exists(Path.GetDirectoryName(path) + @"\output")) Directory.CreateDirectory(Path.GetDirectoryName(path) + @"\output");
            FileStream fs = new FileStream(Path.Combine(Path.GetDirectoryName(path), "output", Path.GetFileNameWithoutExtension(path) + ".aft"), FileMode.OpenOrCreate);
            fs.Write(newALTBBuffer, 0, newALTBBuffer.Length);
            fs.Close();
        }
        public void CreateFont(string fontName,string rangePath)
        {
            List<Range> rangeList = new List<Range>();
            StreamReader sr = new StreamReader(rangePath);
            string rangeText = sr.ReadToEnd();
            sr.Close();
            string[] rangeArray = rangeText.Split(',');
            int nowOffset = 0;
            for(int i = 0; i < rangeArray.Length; i++)
            {
                Range range = new Range();
                range.ImageOffset = (ushort)nowOffset;
                string[] sp = rangeArray[i].Split('-');
                int max = 0, min = 0;
                if (sp.Length == 1)
                {
                    max = (ushort)Convert.ToInt32(sp[0]);
                    min = (ushort)Convert.ToInt32(sp[0]);
                    range.CharCodeMax = (ushort)max;
                    range.CharCodeMin = (ushort)min;
                }
                else
                {
                    max = (ushort)Convert.ToInt32(sp[1]);
                    min = (ushort)Convert.ToInt32(sp[0]);
                    range.CharCodeMax = (ushort)max;
                    range.CharCodeMin = (ushort)min;
                }
                nowOffset += max - min + 1;
                rangeList.Add(range);
            }
            //创建新的范围

            //更新RangeList和RangeCount
            RangeList = rangeList;
            //RangeCount = (ushort)rangeList.Count;
            //RangeList.Add(new Range() { CharCodeMin = 0xFFE6, CharCodeMax = 0xFFE6, ImageOffset = 0x0ED0 });
            //RangeCount++;
            Font ft = new Font(fontName, 18f,FontStyle.Bold);

            //先求出字符的总数
            int count = RangeList[RangeList.Count - 1].ImageOffset + (RangeList[RangeList.Count - 1].CharCodeMax - RangeList[RangeList.Count - 1].CharCodeMin) + 1;
            //CharWidth也要重新刷一下，就用全全角好了，全部刷24
            /*for(int i = 0; i < WidthFieldCount; i++)
            {
                WidthList[i] = 24;
            }*/

            //一行9个，求出最终图片的宽高
            int width = 256;
            int height = count / 9;
            if (count % 9 > 0) height++;
            height = height * 28;
            //LFY！LFY！LFY！——2017/08/10
            Bitmap result = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics resultPainter = Graphics.FromImage(result);
            int offset = 0;
            for (int i = 0; i < RangeCount; i++)
            {
                Range range = RangeList[i];
                for(int j = 0; j <= range.CharCodeMax - range.CharCodeMin; j++)
                {
                    //先把字取出来
                    ushort charCode = (ushort)(range.CharCodeMin + j);
                    string s = Encoding.Unicode.GetString(BitConverter.GetBytes(charCode));
                    //先取字模好吧，破费
                    Bitmap bmp = new Bitmap(28, 28, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                        g.DrawString(s, ft, Brushes.White, -4, 0);
                    }
                    //然后把字模画进去
                    //先算出来画的位置
                    Point p = new Point((offset % 9) * 28, (offset / 9) * 28);
                    resultPainter.DrawImage(bmp, p);
                    offset++;
                }
            }
            resultPainter.Dispose();
            FontImage.ImportImage(result);
        }
    }
    public class ALIG : AL
    {
        public byte Vers { get; set; }
        public byte Unknown1 { get; set; }
        public byte Unknown2 { get; set; }
        public byte Unknown3 { get; set; }
        public string Form { get; set; }
        public string PaletteForm { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public ushort Unknown4 { get; set; }
        public ushort Unknown5 { get; set; }
        public int Unknown6 { get; set; }
        public List<byte[]> Palette { get; set; }
        public Bitmap Image { get; set; }
        public byte[] RawImage { get; set; }
        public ALIG(byte[] buffer) : base(buffer)
        {
            Palette = new List<byte[]>();
            MemoryStream ms = new MemoryStream(buffer);
            string type = ms.ReadString(4);
            if (type != "ALIG") throw new Exception("这不是ALIG文件");
            //读取文件头
            Vers = (byte)ms.ReadByte();
            Unknown1 = (byte)ms.ReadByte();
            Unknown2 = (byte)ms.ReadByte();
            Unknown3 = (byte)ms.ReadByte();
            Form = ms.ReadString(4);
            PaletteForm = ms.ReadString(4);
            Width = ms.ReadInt32();
            Height = ms.ReadInt32();
            Unknown4 = ms.ReadWord();
            Unknown5 = ms.ReadWord();
            Unknown6 = ms.ReadInt32();
            //读取Plate
            if (Form != "PAL4") throw new Exception("暂时不支持PAL4以外的图片格式");
            if (PaletteForm != "RGBA") throw new Exception("暂时不支持RGBA以外的颜色格式");
            for(int i = 0; i < 16; i++)
            {
                byte[] RGBA = new byte[4];
                ms.Read(RGBA, 0, 4);
                Palette.Add(RGBA);
                Console.WriteLine(String.Format("R:{0} G:{1} B:{2} A:{3}", RGBA[0], RGBA[1], RGBA[2], RGBA[3]));
            }
            Image = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            for (int i = 0; i < Width * Height; i += 2)
            {
                byte raw = (byte)ms.ReadByte();
                byte[] lowRgba = Palette[raw >> 4];
                byte[] highRgba = Palette[raw & 0xF];
                Color colorLow = Color.FromArgb(lowRgba[3], lowRgba[0], lowRgba[1], lowRgba[2]);
                Color colorHigh = Color.FromArgb(highRgba[3], highRgba[0], highRgba[1], highRgba[2]);
                Image.SetPixel(i % Width, i / Width, colorLow);
                Image.SetPixel((i + 1) % Width, (i + 1) / Width, colorHigh);
            }
        }

        private byte getPaletteIndex(byte alpha)
        {
            byte r = 0x00;
            if (alpha == 0) r = 0x00;
            else
            {
                for (int i = 0; i < 15; i++)
                {
                    if (alpha > Palette[i][3] && alpha <= Palette[i + 1][3]) r = (byte)(i + 1);
                }
            }
            return r;
        }

        public void ImportImage(Bitmap bitmap)
        {
            //暂时只支持PAL4

            int pxCount = bitmap.Width * bitmap.Height;
            Width = bitmap.Width;
            Height = bitmap.Height;
            byte[] rawImage = new byte[pxCount / 2];
            MemoryStream ms = new MemoryStream(rawImage);
            for(int i = 0; i < pxCount; i += 2)
            {
                //一次存两个
                Color px1 = bitmap.GetPixel(i % Width, i / Width);
                Color px2 = bitmap.GetPixel((i + 1) % Width, (i + 1) / Width);
                byte combine = getPaletteIndex(px1.A);
                combine = (byte)(combine << 4);
                combine = (byte)(combine + getPaletteIndex(px2.A));
                ms.WriteByte(combine);
            }
            ms.Close();
            RawImage = rawImage;
        }
        public override byte[] Package(string path)
        {
            MemoryStream ms = new MemoryStream();
            ms.Write(Encoding.ASCII.GetBytes("ALIG"), 0, 4);
            ms.WriteByte(Vers);
            ms.WriteByte(Unknown1);
            ms.WriteByte(Unknown2);
            ms.WriteByte(Unknown3);
            ms.Write(Encoding.ASCII.GetBytes(Form), 0, 4);
            ms.Write(Encoding.ASCII.GetBytes(PaletteForm), 0, 4);
            ms.WriteInt32(Width);
            ms.WriteInt32(Height);
            ms.WriteWord(Unknown4);
            ms.WriteWord(Unknown5);
            ms.WriteInt32(Unknown6);
            for(int i = 0; i < 16; i++)
            {
                byte[] RGBA = Palette[i];
                ms.Write(RGBA, 0, 4);
            }
            if (RawImage == null) throw new Exception("你TM又没改图片，重新打包个JB");
            ms.Write(RawImage, 0, RawImage.Length);
            byte[] result = new byte[ms.Position];
            ms.Position = 0;
            ms.Read(result, 0, result.Length);
            ms.Close();
            return result;
        }
    }
}
