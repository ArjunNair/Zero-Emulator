//#define SOUND_SLIMDX

#if SOUND_SLIMDX
using SlimDX.XAudio2;
#else

using Microsoft.DirectX.DirectSound;

#endif

//using SlimDX.DirectSound;
namespace ZeroSound
{
#if SOUND_SLIMDX

    #region SLIMDX_SOUND

    public class SoundManager
    {
        static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, System.ResolveEventArgs args)
        {
            string dllName = args.Name.Contains(",") ? args.Name.Substring(0, args.Name.IndexOf(',')) : args.Name.Replace(".dll", "");

            dllName = dllName.Replace(".", "_");

            if (dllName.EndsWith("_resources")) return null;

            System.Resources.ResourceManager rm = new System.Resources.ResourceManager(typeof(SoundManager).Namespace + ".Properties.Resources", System.Reflection.Assembly.GetExecutingAssembly());

            byte[] bytes = (byte[])rm.GetObject(dllName);

            return System.Reflection.Assembly.Load(bytes);
        }

        public struct PlayBufferProperty
        {
            public int index;
        };

        //public PlayBufferProperty playBufferProp = new PlayBufferProperty();
        public int SAMPLE_SIZE = 882;
        const int BUFFER_COUNT = 8;
        private XAudio2 device;
        private MasteringVoice masteringVoice;
        private SourceVoice sourceVoice;
        private SlimDX.Multimedia.WaveFormat waveFormat;
        private int bytesPerSample;
        private AudioBuffer buffer;
        //float[][] sampleData = new float[BUFFER_COUNT][];   //each buffer of 882*channel bytes
        short[][] sampleData = new short[BUFFER_COUNT][];
        byte[][] bData = new byte[BUFFER_COUNT][];
        int currentBuffer = 0;
        int playBuffer = 0;
        int submittedBuffers = 0;
        public bool isPlaying = false;
        int samplePos = 0;
        public bool readyToPlay;
        private bool isMute = false;
        public object playLock = new object();

        public void SetVolume(float vol)
        {
            masteringVoice.Volume = vol;
        }

        public SoundManager(System.IntPtr handle, short BitsPerSample, short Channels, int SamplesPerSecond)
        {
            System.AppDomain.CurrentDomain.AssemblyResolve += new System.ResolveEventHandler(CurrentDomain_AssemblyResolve);
            SlimDX.Multimedia.WaveFormat format = new SlimDX.Multimedia.WaveFormat();
            format.BitsPerSample = BitsPerSample;
            format.Channels = Channels;
            format.SamplesPerSecond = SamplesPerSecond;
            format.BlockAlignment = (short)(format.Channels * format.BitsPerSample / 8);
            format.AverageBytesPerSecond = format.SamplesPerSecond * format.BlockAlignment;
            //format.FormatTag = WaveFormatTag.Pcm;
            format.FormatTag = SlimDX.Multimedia.WaveFormatTag.Pcm;

            device = new XAudio2(XAudio2Flags.None, ProcessorSpecifier.AnyProcessor);

            device.StartEngine();

            masteringVoice = new MasteringVoice(device, Channels, SamplesPerSecond);

            sourceVoice = new SourceVoice(device, format, VoiceFlags.None);

            //FilterParameters fp = new FilterParameters();
            //fp.Frequency = 0.5f;//sourceVoice.FilterParameters.Frequency;
            //fp.OneOverQ = 0.5f;//sourceVoice.FilterParameters.OneOverQ;
            //fp.Type = FilterType.LowPassFilter;
            //sourceVoice.FilterParameters = fp;

            //sourceVoice.BufferEnd += new System.EventHandler<ContextEventArgs>(sourceVoice_BufferEnd);
           // sourceVoice.StreamEnd += new System.EventHandler(sourceVoice_StreamEnd);
           // sourceVoice.BufferStart += new System.EventHandler<ContextEventArgs>(sourceVoice_BufferStart);
           // sourceVoice.VoiceError += new EventHandler<ErrorEventArgs>(sourceVoice_VoiceError);

            sourceVoice.Volume = 0.5f;
            buffer = new AudioBuffer();
            buffer.AudioData = new System.IO.MemoryStream();

            waveFormat = format;
            bytesPerSample = (waveFormat.BitsPerSample / 8) * Channels;
            for (int i = 0; i < BUFFER_COUNT; i++)
            {
                //sampleData[i] = new float[SAMPLE_SIZE * Channels];
                sampleData[i] = new short[SAMPLE_SIZE * Channels];
                bData[i] = new byte[SAMPLE_SIZE  * bytesPerSample];
            }
            sourceVoice.SubmitSourceBuffer(buffer);
        }

        public void Reset()
        {
            isPlaying = false;
            buffer.Dispose();
            sourceVoice.FlushSourceBuffers();
            buffer = null;
            sourceVoice.Dispose();
            sourceVoice = null;
            masteringVoice.Dispose();
            masteringVoice = null;

            masteringVoice = new MasteringVoice(device, waveFormat.Channels, waveFormat.SamplesPerSecond);

            sourceVoice = new SourceVoice(device, waveFormat, VoiceFlags.None);
            // sourceVoice.BufferStart += new System.EventHandler<ContextEventArgs>(sourceVoice_BufferStart);

            sourceVoice.Volume = 0.5f;
            buffer = new AudioBuffer();
            buffer.AudioData = new System.IO.MemoryStream();

            bytesPerSample = (waveFormat.BitsPerSample / 8) * waveFormat.Channels;

            for (int i = 0; i < BUFFER_COUNT; i++)
            {
                sampleData[i] = new short[SAMPLE_SIZE * waveFormat.Channels];
                bData[i] = new byte[SAMPLE_SIZE * bytesPerSample];
            }
            sourceVoice.SubmitSourceBuffer(buffer);
            currentBuffer = 0;
            playBuffer = 0;
            samplePos = 0;
        }

        void sourceVoice_BufferStart(object sender, ContextEventArgs e)
        {
        }

        void sourceVoice_BufferEnd(object sender, ContextEventArgs e)
        {
            isPlaying = false;
        }

       public void ChangeSampleSize(int newSampleSize)
       {
           SAMPLE_SIZE = newSampleSize;
         //  for (int i = 0; i < BUFFER_COUNT; i++)
         //  {
         //      sampleData[i] = new float[SAMPLE_SIZE * waveFormat.Channels];
         //      bData[i] = new byte[SAMPLE_SIZE * bytesPerSample];
         //  }
       }

       void sourceVoice_VoiceError(object sender, ErrorEventArgs e)
       {
           //throw new NotImplementedException();
           System.Windows.Forms.MessageBox.Show("Sound error", "Sound error", System.Windows.Forms.MessageBoxButtons.OK);
       }

       public void Play()
       {
           isMute = false;

           sourceVoice.Start();
       }

       public void Stop()
       {
           sourceVoice.FlushSourceBuffers();

           sourceVoice.Stop();
           isMute = true;
       }

       public void Shutdown()
       {
           sourceVoice.Stop();
           buffer.Dispose();

           sourceVoice.FlushSourceBuffers();
           buffer = null;
           sourceVoice.Dispose();
           sourceVoice = null;
           masteringVoice.Dispose();
           masteringVoice = null;
           device.StopEngine();
           device.Dispose();
       }

        //Takes in an array of samples where each sample represents a channel
        //So, a mono channel will have only 1 sample, stereo will have 2 samples etc...
        //   Here’s my code to calculate that: sample_t_states=(frame_length * (50 * (emulation_speed/100))) / snd_freq;
        //    snd_freq  = 44100 usually (or 22050 or 11025!!)
        ////    frame_length = 69888 (or... you know!!)
         //   emulation_speed = speed in % (10 –> 1000, where 100 is 50fps)
      /* public void AddSample(float[] soundOut)
       {
           if (samplePos >= SAMPLE_SIZE << 1 ) //generic: SAMPLE_SIZE * waveFormat.Channels
           {
               playBuffer = currentBuffer;
               currentBuffer = (++currentBuffer) % BUFFER_COUNT;
               samplePos = 0;
               if (!isMute)
               {
                  isPlaying = true;
                  PlayBuffer();
                  //while (!FinishedPlaying())
                  //    System.Threading.Thread.Sleep(1);
                  //FinishedPlaying();
               }
           }
           //else
           //    SubmitBlankBuffer();

           //Since we will always have 2 channels...
           sampleData[currentBuffer][samplePos++] = soundOut[0];
           sampleData[currentBuffer][samplePos++] = soundOut[1];
       }*/

       public void SubmitBlankBuffer()
       {
           if (sourceVoice.State.BuffersQueued > 0)
           {
               sourceVoice.Discontinuity();
           }
       }

       public void PlayBuffer(ref short[] _sampleData)
       {
        //   if (sourceVoice.State.BuffersQueued < BUFFER_COUNT - 1)
           if (isMute)
               return;
           isPlaying = true;
           playBuffer = currentBuffer;
           currentBuffer = (++currentBuffer) % BUFFER_COUNT;
           {
               for (int i = 0, j = 0; j < SAMPLE_SIZE << 1 ; i += bytesPerSample >> 1, j++)
               {
                   // float soundValue = ((_sampleData[j]));// - 0.5f) * 2.0f;
                   // if (soundValue > 0.9f)
                   //     soundValue = 0.9f;
                   //else
                   //     if (soundValue < -0.9f)
                   //         soundValue = -0.9f;

                    byte[] tmp = System.BitConverter.GetBytes(_sampleData[j]);

                    bData[playBuffer][i] = tmp[0];
                    bData[playBuffer][i + 1] = tmp[1];
                    //bData[playBuffer][i + 2] = tmp[2];
                    //bData[playBuffer][i + 3] = tmp[3];
               }

               buffer.AudioData.SetLength(0);
               buffer.AudioData = new System.IO.MemoryStream(bData[playBuffer], 0, bData[playBuffer].Length);
               //buffer.AudioData = new System.IO.MemoryStream(_sampleData, 0, _sampleData.Length);
               buffer.AudioData.Position = 0;
               buffer.AudioBytes = bData[playBuffer].Length;
               buffer.Flags = BufferFlags.None;
               //playBufferProp.index = playBuffer;
               buffer.Context = (System.IntPtr)playBuffer;
               isPlaying = true;

               sourceVoice.SubmitSourceBuffer(buffer);

                submittedBuffers++;
                if ((submittedBuffers > 1))
                {
                    if (!readyToPlay)
                    {
                        readyToPlay = true;
                    }

                    submittedBuffers = 2;
                }
           }
       }

       public bool FinishedPlaying()
       {
           //for (; ; )
           {
               if (sourceVoice.State.BuffersQueued < 2)
                   return true;

              // System.Threading.Thread.Sleep(1);
           }
           return false;
       }
    }

    #endregion SLIMDX_SOUND

#else

    #region DirectSound

    public unsafe class SoundManager : System.IDisposable
    {
        private static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, System.ResolveEventArgs args) {
            string dllName = args.Name.Contains(",") ? args.Name.Substring(0, args.Name.IndexOf(',')) : args.Name.Replace(".dll", "");

            dllName = dllName.Replace(".", "_");

            if (dllName.EndsWith("_resources")) return null;

            System.Resources.ResourceManager rm = new System.Resources.ResourceManager(typeof(SoundManager).Namespace + ".Properties.Resources", System.Reflection.Assembly.GetExecutingAssembly());

            byte[] bytes = (byte[])rm.GetObject(dllName);

            return System.Reflection.Assembly.Load(bytes);
        }

        private const int SAMPLE_SIZE = 882;
        private const int SAMPLE_RATE = 44100;
        private const int BUFFER_CHUNK = SAMPLE_RATE / 50;
        private const int SOUND_BUFFER_SIZE = BUFFER_CHUNK * 3;
        private const int BUFFER_COUNT = 4;
        public bool soundEnabled = true;
        private Device _device = null;
        private SecondaryBuffer _soundBuffer = null;
        private Notify _notify = null;
        public bool isPlaying = false;
        private byte _zeroValue;
        private int _bufferSize;
        private int _bufferCount;

        private System.Collections.Queue _fillQueue = null;
        private System.Collections.Queue _playQueue = null;
        private uint lastSample = 0;

        private System.Threading.Thread _waveFillThread = null;
        private System.Threading.AutoResetEvent _fillEvent = new System.Threading.AutoResetEvent(true);
        private bool _isFinished = false;
        private bool disposed = false;

        public SoundManager(System.IntPtr handle, short bitsPerSample, short channels, int samplesPerSecond) {
            System.AppDomain.CurrentDomain.AssemblyResolve += new System.ResolveEventHandler(CurrentDomain_AssemblyResolve);
            _fillQueue = new System.Collections.Queue(BUFFER_COUNT);
            _playQueue = new System.Collections.Queue(BUFFER_COUNT);
            _bufferSize = SAMPLE_SIZE * 2 * 2;
            for (int i = 0; i < BUFFER_COUNT; i++)
                _fillQueue.Enqueue(new byte[_bufferSize]);

            _bufferCount = BUFFER_COUNT;
            _zeroValue = bitsPerSample == 8 ? (byte)128 : (byte)0;

            _device = new Device();
            _device.SetCooperativeLevel(handle, CooperativeLevel.Priority);

            WaveFormat wf = new WaveFormat();
            wf.FormatTag = WaveFormatTag.Pcm;
            wf.SamplesPerSecond = samplesPerSecond;
            wf.BitsPerSample = bitsPerSample;
            wf.Channels = channels;
            wf.BlockAlign = (short)(wf.Channels * (wf.BitsPerSample / 8));
            wf.AverageBytesPerSecond = (int)wf.SamplesPerSecond * (int)wf.BlockAlign;

            // Create a buffer
            BufferDescription bufferDesc = new BufferDescription(wf);
            bufferDesc.BufferBytes = _bufferSize * _bufferCount;
            bufferDesc.ControlPositionNotify = true;
            bufferDesc.GlobalFocus = true;
            bufferDesc.ControlVolume = true;
            bufferDesc.ControlEffects = false;
            _soundBuffer = new SecondaryBuffer(bufferDesc, _device);

            _notify = new Notify(_soundBuffer);
            BufferPositionNotify[] posNotify = new BufferPositionNotify[_bufferCount];
            for (int i = 0; i < posNotify.Length; i++) {
                posNotify[i] = new BufferPositionNotify();
                posNotify[i].Offset = i * _bufferSize;
                posNotify[i].EventNotifyHandle = _fillEvent.SafeWaitHandle.DangerousGetHandle();
            }
            _notify.SetNotificationPositions(posNotify);

            _waveFillThread = new System.Threading.Thread(new System.Threading.ThreadStart(waveFillThreadProc));
            _waveFillThread.IsBackground = true;
            _waveFillThread.Name = "Wave fill thread";
            _waveFillThread.Priority = System.Threading.ThreadPriority.Highest;
            _waveFillThread.Start();
        }

        ~SoundManager()
        {
            Dispose(false);
        }

        public void Shutdown() {
            this.Dispose();
        }

        public void Reset() {
        }

        public void Play() {
            // _soundBuffer.Play(0, BufferPlayFlags.Looping);
        }

        public void PlayBuffer(ref short[] samples) {
        }

        public void Stop() {
            // _soundBuffer.Stop();
        }

        public bool FinishedPlaying() {
            lock (_fillQueue.SyncRoot)
                if (_fillQueue.Count < 1)
                    return false;
            return true;
        }

        public void SetVolume(float t) {
            if (t <= 0.0f)
                _soundBuffer.Volume = -10000;
            else if (t >= 1.0f)
                _soundBuffer.Volume = 0;
            else
                _soundBuffer.Volume = (int)(-2000.0f * System.Math.Log10(1.0f / t));
        }

        public void Dispose() 
        {
            Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (_waveFillThread != null)
                    {
                        try
                        {
                            _isFinished = true;
                            if (_soundBuffer != null)
                                if (_soundBuffer.Status.Playing)
                                    _soundBuffer.Stop();
                            _fillEvent.Set();

                            _waveFillThread.Join();

                            if (_soundBuffer != null)
                                _soundBuffer.Dispose();
                            if (_notify != null)
                                _notify.Dispose();

                            if (_device != null)
                                _device.Dispose();
                        }
                        catch (System.Threading.ThreadAbortException e)
                        {
                            System.Console.WriteLine("Sound thread exception " + e.Message);
                        }
                        finally
                        {
                            _waveFillThread = null;
                            _soundBuffer = null;
                            _notify = null;
                            _device = null;
                        }
                    }
                }
                // No unmanaged resources to release otherwise they'd go here.
            }
            disposed = true;
        }
        private unsafe void waveFillThreadProc() {
            int lastWrittenBuffer = -1;
            byte[] sampleData = new byte[_bufferSize];
            fixed (byte* lpSampleData = sampleData) {
                try
                {
                    _soundBuffer.Play(0, BufferPlayFlags.Looping);
                    while (!_isFinished) {
                        _fillEvent.WaitOne();

                        for (int i = (lastWrittenBuffer + 1) % _bufferCount; i != (_soundBuffer.PlayPosition / _bufferSize); i = ++i % _bufferCount) {
                            OnBufferFill((System.IntPtr)lpSampleData, sampleData.Length);
                            _soundBuffer.Write(_bufferSize * i, sampleData, LockFlag.None);
                            lastWrittenBuffer = i;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine("Sound thread exception " + ex.Message);
                    //LogAgent.Error(ex);
                }
            }
        }

        protected void OnBufferFill(System.IntPtr buffer, int length) {
            byte[] buf = null;
            lock (_playQueue.SyncRoot)
                if (_playQueue.Count > 0)
                    buf = _playQueue.Dequeue() as byte[];
            if (buf != null) {
                uint* dst = (uint*)buffer;
                fixed (byte* srcb = buf) {
                    uint* src = (uint*)srcb;
                    for (int i = 0; i < length / 4; i++)
                        dst[i] = src[i];
                    lastSample = dst[length / 4 - 1];
                }
                lock (_fillQueue.SyncRoot)
                    _fillQueue.Enqueue(buf);
            } else {
                uint* dst = (uint*)buffer;
                for (int i = 0; i < length / 4; i++)
                    dst[i] = lastSample;
            }
        }

        public byte[] LockBuffer() {
            byte[] sndbuf = null;
            lock (_fillQueue.SyncRoot)
                if (_fillQueue.Count > 0)
                    sndbuf = _fillQueue.Dequeue() as byte[];
            return sndbuf;
        }

        public void UnlockBuffer(byte[] sndbuf) {
            lock (_playQueue.SyncRoot)
                _playQueue.Enqueue(sndbuf);
        }
    }

    #endregion DirectSound

#endif
}