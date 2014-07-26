using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Windows.Forms;
using System.Threading;
//using Microsoft.DirectX.DirectSound;
//using WaveFormat = Microsoft.DirectX.DirectSound.WaveFormat;
//using Microsoft.DirectX;
using System.Runtime.InteropServices;
using IrrKlang;

namespace ZiggySound
{
    public class SoundManager
    {
        ISoundEngine engine;
        public AudioFormat audioFormat;
        ISoundSource source;
        float[] sampleData = new float[882];
        // byte[] bData;// = new byte[BUFFER_COUNT][];
        byte[] bData = new byte[882 * 4 * 2];
        int currentBuffer = 0;
        int samplePos = 0;
        private bool isPlaying = false;

        public SoundManager(IntPtr handle, short BitsPerSample, short Channels, int SamplesPerSecond)
        {
            // start up the engine
            engine = new ISoundEngine();
            audioFormat = new AudioFormat();
            audioFormat.ChannelCount = Channels;
            audioFormat.SampleRate = SamplesPerSecond;
            audioFormat.Format = SampleFormat.Unsigned8Bit;
            audioFormat.FrameCount = 1;
            
        }

        public void Play() { }
        public void Stop() 
        {
            if (engine.IsCurrentlyPlaying("beeper"))
                engine.StopAllSounds();
        }

        public void Shutdown()
        {
            engine.RemoveAllSoundSources();
        }

        public void AddSample(float soundOut)
        {
            if (samplePos < 882)
            {
                sampleData[samplePos++] = soundOut;
                return;
            }

            for (int i = 0, j = 0; j < samplePos; i += 8, j++)
            {
                float data = (float)sampleData[j];
                byte[] tmp = System.BitConverter.GetBytes(data);

                bData[i] = tmp[0];
                bData[i + 1] = tmp[1];
                bData[i + 2] = tmp[2];
                bData[i + 3] = tmp[3];
                bData[i + 4] = tmp[0];
                bData[i + 5] = tmp[1];
                bData[i + 6] = tmp[2];
                bData[i + 7] = tmp[3];
            }
            source = engine.AddSoundSourceFromPCMData(bData, "beeper", audioFormat);
            engine.Play2D("beeper");
        }

        public void PlayBuffer()
        {
            /*   for (int i = 0, j = 0; j < sampleData.Length; i += 8, j++)
               {
                   float data = (float)sampleData[j];
                   byte[] tmp = System.BitConverter.GetBytes(data);

                   bData[i] = tmp[0];
                   bData[i + 1] = tmp[1];
                   bData[i + 2] = tmp[2];
                   bData[i + 3] = tmp[3];
                   bData[i + 4] = tmp[0];
                   bData[i + 5] = tmp[1];
                   bData[i + 6] = tmp[2];
                   bData[i + 7] = tmp[3];
               }

               buffer.AudioData.SetLength(0);
               buffer.AudioData.Write(bData, 0, bData.Length);
               buffer.AudioData.Position = 0;
               buffer.AudioBytes = bData.Length;
               buffer.Flags = BufferFlags.None;
               sourceVoice.SubmitSourceBuffer(buffer);
               samplePos = 0;
             */
        }

        public void Reset()
        {
            engine.RemoveSoundSource("beeper");
        }

        public bool FinishedPlaying()
        {
            return true;
        }

    }

}
