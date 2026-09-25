using System;
using System.IO;
using System.Text;

namespace InFalsusStudio.Core
{
    // Small local WAV decoder. No networking assemblies and no third-party dependencies.
    // Supports RIFF little-endian PCM 8/16/24/32 and IEEE float32 (including extensible tags).
    public sealed class WaveData
    {
        public int Channels, Frequency;
        public float[] Samples;
        public int Frames {get{return Samples.Length/Channels;}}
    }
    public static class WaveReader
    {
        public static WaveData Decode(byte[] bytes)
        {
            if(bytes==null||bytes.Length<44||bytes.Length>256*1024*1024)throw new InvalidDataException("WAV is empty, truncated, or larger than 256 MiB.");
            using(var reader=new BinaryReader(new MemoryStream(bytes),Encoding.ASCII))
            {
                Func<int,string> tag=n=>Encoding.ASCII.GetString(reader.ReadBytes(n));
                if(tag(4)!="RIFF")throw new InvalidDataException("Only little-endian RIFF WAV is supported.");
                uint riff=reader.ReadUInt32();if(tag(4)!="WAVE")throw new InvalidDataException("Not a WAVE file.");
                if((ulong)riff+8>(ulong)bytes.Length)throw new InvalidDataException("Truncated RIFF payload.");
                long riffEnd=(long)riff+8;
                int format=0,channels=0,rate=0,bits=0,align=0,dataOffset=-1,dataLength=0;
                while(reader.BaseStream.Position+8<=riffEnd)
                {
                    string name=tag(4);uint length=reader.ReadUInt32();long start=reader.BaseStream.Position,end=start+length;
                    if(end>riffEnd||length>int.MaxValue)throw new InvalidDataException("Truncated WAV chunk.");
                    if(name=="fmt ")
                    {
                        if(length<16)throw new InvalidDataException("Invalid fmt chunk.");
                        format=reader.ReadUInt16();channels=reader.ReadUInt16();rate=reader.ReadInt32();reader.ReadUInt32();align=reader.ReadUInt16();bits=reader.ReadUInt16();
                        if(format==65534)
                        {
                            if(length<40)throw new InvalidDataException("Truncated extensible WAV format.");
                            reader.BaseStream.Position=start+24;format=reader.ReadUInt16();
                        }
                    }
                    else if(name=="data"&&dataOffset<0){dataOffset=(int)start;dataLength=(int)length;}
                    reader.BaseStream.Position=Math.Min(riffEnd,end+(length&1));
                }
                if(channels<1||channels>8||rate<8000||rate>384000||dataOffset<0)throw new InvalidDataException("Unsupported WAV channel count/rate or missing data.");
                if(!(format==1&&(bits==8||bits==16||bits==24||bits==32)||format==3&&bits==32))throw new InvalidDataException("Use PCM 8/16/24/32-bit or IEEE float32 WAV.");
                int bytesPer=bits/8;if(align!=channels*bytesPer||dataLength%align!=0)throw new InvalidDataException("Invalid WAV block alignment.");
                int count=dataLength/bytesPer;if(count==0||count>64*1024*1024)throw new InvalidDataException("WAV sample count is empty or exceeds the safety limit.");
                var samples=new float[count];reader.BaseStream.Position=dataOffset;
                for(int i=0;i<count;i++)
                {
                    float v;
                    if(format==3)v=reader.ReadSingle();
                    else if(bits==8)v=(reader.ReadByte()-128)/128f;
                    else if(bits==16)v=reader.ReadInt16()/32768f;
                    else if(bits==24){int n=reader.ReadByte()|(reader.ReadByte()<<8)|(reader.ReadByte()<<16);if((n&0x800000)!=0)n|=unchecked((int)0xff000000);v=n/8388608f;}
                    else v=(float)(reader.ReadInt32()/2147483648.0);
                    if(float.IsNaN(v)||float.IsInfinity(v))v=0;samples[i]=Math.Max(-1,Math.Min(1,v));
                }
                return new WaveData{Channels=channels,Frequency=rate,Samples=samples};
            }
        }
    }
}
