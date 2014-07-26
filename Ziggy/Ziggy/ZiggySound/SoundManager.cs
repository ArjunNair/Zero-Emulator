using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
//using Microsoft.DirectX;
//using Microsoft.DirectX.DirectSound;
//using WaveFormat = Microsoft.DirectX.DirectSound.WaveFormat;


namespace ZiggySound
{
    public class SoundManager
    {
        const int SAMPLECOUNT = 441000;
        const int BUFFER_CHUNK = 69888 / 79; //(Approx 884 bytes)
        const int SOUND_BUFFER_SIZE = BUFFER_CHUNK * 2; //(Approx 1768 bytes)
        public bool soundEnabled = false;

        private Device soundDevice;
        private bool isPlaying = false;
        private SecondaryBuffer soundBuffer;
        public int[] data = new int[BUFFER_CHUNK]; //884 bytes worth of data
        int bufferPos = 0;
        IntPtr parentHandle;
        public bool initialised = false;

        public const int NumberRecordNotifications = 2;
        public BufferPositionNotify[] PositionNotify = new BufferPositionNotify[NumberRecordNotifications ];
        public AutoResetEvent NotificationEvent = null;
        public Notify applicationNotify = null;
        private Thread NotifyThread = null;
        private int bufferWritePos;

        public SoundManager(IntPtr handle)
        {
            parentHandle = handle;
        }

        ~SoundManager()
        {
            soundEnabled = false;
            if (null != NotificationEvent)
            {
                NotificationEvent.Set();
            }
        }

        public void Initialise()
        {


            soundDevice = new Device();
            soundDevice.SetCooperativeLevel(parentHandle, CooperativeLevel.Priority);
            WaveFormat wf = new WaveFormat();

            wf.FormatTag = WaveFormatTag.Pcm;
            wf.SamplesPerSecond = SAMPLECOUNT;
            wf.BitsPerSample = 16;
            wf.Channels = 1;
            wf.BlockAlign = (short)((wf.Channels * wf.BitsPerSample) / 8);
            wf.AverageBytesPerSecond = wf.SamplesPerSecond * wf.BlockAlign;


            if (null != applicationNotify)
            {
                applicationNotify.Dispose();
                applicationNotify = null;
            }

            // Create a buffer with 2 seconds of sample data
            BufferDescription bufferDesc = new BufferDescription(wf);
            bufferDesc.BufferBytes = SOUND_BUFFER_SIZE;
            bufferDesc.ControlPositionNotify = true;
            bufferDesc.GlobalFocus = true;
            bufferDesc.CanGetCurrentPosition = true;
            bufferDesc.ControlVolume = true;
            bufferDesc.LocateInSoftware = true;
            soundBuffer = new SecondaryBuffer(bufferDesc, soundDevice);
            soundBuffer.SetCurrentPosition(0);
            initialised = true;
            soundEnabled = true;

            InitNotifications();
        }

        void InitNotifications()
        {
            //-----------------------------------------------------------------------------
            // Name: InitNotifications()
            // Desc: Inits the notifications on the capture buffer which are handled
            //       in the notify thread.
            //-----------------------------------------------------------------------------

            // Create a thread to monitor the notify events
            if (null == NotifyThread)
            {
                NotifyThread = new Thread(new ThreadStart(WaitThread));

                NotifyThread.Start();

                // Create a notification event, for when the sound stops playing
                NotificationEvent = new AutoResetEvent(false);
            }


            // Setup the notification positions
            for (int i = 0; i < NumberRecordNotifications; i++)
            {
                PositionNotify[i].Offset = (BUFFER_CHUNK * i) + BUFFER_CHUNK - 1;
                PositionNotify[i].EventNotifyHandle = NotificationEvent.SafeWaitHandle.DangerousGetHandle();
            }

            applicationNotify = new Notify(soundBuffer);

            // Tell DirectSound when to notify the app. The notification will come in the from 
            // of signaled events that are handled in the notify thread.
            applicationNotify.SetNotificationPositions(PositionNotify, NumberRecordNotifications);
        }

        public void AddSample(int sample)
        {
            if (bufferPos > BUFFER_CHUNK-1)
            {
                Update();
                bufferPos = 0;
            }
            data[bufferPos++] = sample & 0xff;
            data[bufferPos++] = sample >> 8;
            
        }

        public void WriteDataToBuffer()
        {
            if (bufferPos == 0)
                return;

            int currentPlayPos = 0;
            int[] actualData = new int[bufferPos];
            //Array.Copy(data, actualData, bufferPos);
            soundBuffer.GetCurrentPosition(out currentPlayPos, out bufferWritePos);
            //soundBuffer.Write(bufferWritePos, actualData, LockFlag.None);
            soundBuffer.Write(bufferWritePos, data, LockFlag.None);
        }

        public void Update()
        {
            //soundBuffer.SetCurrentPosition(0);
            //soundBuffer.Write(bufferPos, data, LockFlag.FromWriteCursor);
            //soundBuffer.Play(0, BufferPlayFlags.Default);

            if (!isPlaying)
            {
                soundBuffer.Play(0, BufferPlayFlags.Looping);
                isPlaying = true;
  
            }
        }

        private void WaitThread()
        {
            //Sit here and wait for a message to arrive
            while (soundEnabled)
            {
                NotificationEvent.WaitOne(Timeout.Infinite, true);
                WriteDataToBuffer();
            }
        }


    }
}
