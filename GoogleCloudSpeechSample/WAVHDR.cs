using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoogleCloudSpeechSample
{
    class WaveHdr
    {
        public UInt16 NumberOfChannel { get; set; } = 1;
        public UInt32 SamplingRate { get; set; } = 16000;
        public UInt16 NumberOfBitPerSample { get; set; } = 16;
        public UInt32 NumberOfBytesOfWaveData { get; set; } = 0;

        public UInt16 NumberOfBytePerSample
        {
            get
            {
                return ((UInt16)(Math.Ceiling((double)NumberOfBitPerSample / 8)));
            }
        }

        public UInt32 FileSize
        {
            get
            {
                return (NumberOfBytesOfWaveData + 44);
            }
        }

        public UInt32 DataRate
        {
            get
            {
                return (SamplingRate * NumberOfChannel * NumberOfBytePerSample);
            }
        }

        public UInt16 BlockSize
        {
            get
            {
                return (UInt16)(NumberOfBytePerSample * NumberOfChannel);
            }
        }

        public byte[] Bytes
        {
            get
            {
                byte[] Datas = new byte[44];

                Array.Copy(Encoding.ASCII.GetBytes("RIFF"), 0, Datas, 0, 4);
                Array.Copy(BitConverter.GetBytes((UInt32)(FileSize - 8)), 0, Datas, 4, 4);
                Array.Copy(Encoding.ASCII.GetBytes("WAVE"), 0, Datas, 8, 4);
                Array.Copy(Encoding.ASCII.GetBytes("fmt "), 0, Datas, 12, 4);
                Array.Copy(BitConverter.GetBytes((UInt32)(16)), 0, Datas, 16, 4);
                Array.Copy(BitConverter.GetBytes((UInt16)(1)), 0, Datas, 20, 2);
                Array.Copy(BitConverter.GetBytes((UInt16)(NumberOfChannel)), 0, Datas, 22, 2);
                Array.Copy(BitConverter.GetBytes((UInt32)(SamplingRate)), 0, Datas, 24, 4);
                Array.Copy(BitConverter.GetBytes((UInt32)(DataRate)), 0, Datas, 28, 4);
                Array.Copy(BitConverter.GetBytes((UInt16)(BlockSize)), 0, Datas, 32, 2);
                Array.Copy(BitConverter.GetBytes((UInt16)(NumberOfBitPerSample)), 0, Datas, 34, 2);
                Array.Copy(Encoding.ASCII.GetBytes("data"), 0, Datas, 36, 4);
                Array.Copy(BitConverter.GetBytes((UInt32)(NumberOfBytesOfWaveData)), 0, Datas, 40, 4);

                return (Datas);
            }
        }
    }
}
