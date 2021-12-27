using SpeccyCommon;

namespace Speccy
{
    internal enum Channel
    {
        A,
        B,
        C
    }

    public class AY_8192: AudioDevice
    {
        private const byte AY_A_FINE = 0;
        private const byte AY_A_COARSE = 1;
        private const byte AY_B_FINE = 2;
        private const byte AY_B_COARSE = 3;
        private const byte AY_C_FINE = 4;
        private const byte AY_C_COARSE = 5;
        private const byte AY_NOISEPER = 6;
        private const byte AY_ENABLE = 7;
        private const byte AY_A_VOL = 8;
        private const byte AY_B_VOL = 9;
        private const byte AY_C_VOL = 10;
        private const byte AY_E_FINE = 11;
        private const byte AY_E_COARSE = 12;
        private const byte AY_E_SHAPE = 13;
        private const byte AY_PORT_A = 14;
        private const byte AY_PORT_B = 15;

        //This is the default ACB configuration.
        private int ChannelLeft = 0;

        private int ChannelRight = 1;   //2 if ABC
        private int ChannelCenter = 2;  //1 if ABC

        private byte[] regs = new byte[16];
        private int noiseOut;
        private int envelopeVolume;
        private int noiseCount;
        public int envelopeCount;
        private int[] channel_out = new int[3];
        private int[] channel_count = new int[3];

        //public float[] averagedChannelSamples = new float[3];
        public int[] averagedChannelSamples = new int[3];

        //private float[] channel_mix = new float[3];
        private short[] channel_mix = new short[3];

        private ulong randomSeed;
        private byte envelopeClock = 0;
        private int selectedRegister;

        public ushort soundSampleCounter;

        private bool stereoSound = true;
        protected int ayTStates = 0;
        protected const int AY_SAMPLE_RATE = 16;
        protected int timeToOutSound = 0;
        private byte lastOut = 0xff;

        public SPECCY_DEVICE DeviceID { get { return SPECCY_DEVICE.AY_3_8912; } }
        public bool Responded { get; set; }
        public bool StereoSound
        {
            get { return stereoSound; }
            set { stereoSound = value; }
        }

        public int SelectedRegister
        {
            get { return selectedRegister; }
            set { if (value < 16) selectedRegister = value; }
        }

        public bool UseLastOutForIN
        {
            get;
            set;
        }

        //private uint soundTStates, sampleTStates;
        private bool sustaining, sustain, alternate;

        // private int hold;
        private int attack, envelopeStep;

        private short[] AY_SpecVolumes =
        {
         //This volume set taken from SpecEmu
         0/2, 108/2, 159/2, 223/2, 335/2, 511/2, 703/2, 1119/2, 1343/2, 2143/2, 2943/2, 3679/2, 4655/2, 5759/2, 6911/2, 8191/2
         //0, 108, 159, 223, 335, 511, 703, 1119, 1343, 2143, 2943, 3679, 4655, 5759, 6911, 8191

         //The older volume set
        };

        // based on the measurements posted to comp.sys.sinclair in Dec 2001 by Matthew Westcott
        private float[] AY_Volumes =
        {
             0.0000f, 0.0137f, 0.0205f, 0.0291f, 0.0423f, 0.0618f, 0.0847f, 0.1369f, 0.1691f, 0.2647f, 0.3527f, 0.4499f, 0.5704f, 0.6873f, 0.8482f, 1.0000f
        };

        public int SoundChannel1
        {
            get { return averagedChannelSamples[0]; }
            set { averagedChannelSamples[0] = value; }
        }

        public int SoundChannel2
        {
            get { return averagedChannelSamples[1]; }
            set { averagedChannelSamples[1] = value; }
        }

        public AY_8192() {
            UseLastOutForIN = false;
            Reset();
        }

        public void RegisterDevice(zx_spectrum speccyModel) {
            UnregisterDevice(speccyModel);
            speccyModel.audio_devices.Add(this);
            speccyModel.io_devices.Add(this);
        }

        public void UnregisterDevice(zx_spectrum speccyModel) {
            speccyModel.io_devices.Remove(this);
            speccyModel.audio_devices.Remove(this);
        }

        public byte In(ushort port) {
            Responded = true;
            if ((port & 0xc002) == 0xc000) { //AY register activate
                if (UseLastOutForIN) {
                    return lastOut;
                }
                if (SelectedRegister == AY_PORT_B) {
                    if ((regs[AY_ENABLE] & 0x80) == 0)
                        return 0xff;
                    else
                        return regs[AY_PORT_B];
                }

                return regs[selectedRegister];
            }
            Responded = false;
            return 0xff;
        }
        public void Out(ushort port, byte val) {
            Responded = true;
            if ((port & 0xC002) == 0xC000) {
                SelectedRegister = val & 0x0F;
            }
            else
            if ((port & 0xC002) == 0x8000) {
                lastOut = val;
                switch (SelectedRegister) {
                    case AY_A_FINE:
                    break;

                    case AY_A_COARSE:
                    val &= 0x0f;
                    break;

                    case AY_B_FINE:
                    break;

                    case AY_B_COARSE:
                    val &= 0x0f;
                    break;

                    case AY_C_FINE:
                    break;

                    case AY_C_COARSE:
                    val &= 0x0f;
                    break;

                    case AY_NOISEPER:
                    val &= 0x1f;
                    break;

                    case AY_ENABLE:
                    /*
                    if ((lastEnable == -1) || ((lastEnable & 0x40) != (regs[AY_ENABLE] & 0x40))) {
                        SelectedRegister = ((regs[AY_ENABLE] & 0x40) > 0 ? regs[AY_PORT_B] : 0xff);
                    }

                    if ((lastEnable == -1) || ((lastEnable & 0x80) != (regs[AY_ENABLE] & 0x80))) {
                         PortWrite((regs[AY_ENABLE] & 0x80) > 0 ? regs[AY_PORT_B] : 0xff);
                    }
                    lastEnable = regs[AY_ENABLE];*/
                    break;

                    case AY_A_VOL:
                    val &= 0x1f;
                    break;

                    case AY_B_VOL:
                    val &= 0x1f;
                    break;

                    case AY_C_VOL:
                    val &= 0x1f;
                    break;

                    case AY_E_FINE:
                    break;

                    case AY_E_COARSE:
                    break;

                    case AY_E_SHAPE:
                    val &= 0x0f;
                    attack = ((val & 0x04) != 0 ? 0x0f : 0x00);
                    // envelopeCount = 0;
                    if ((val & 0x08) == 0) {
                        /* if Continue = 0, map the shape to the equivalent one which has Continue = 1 */
                        sustain = true;
                        alternate = (attack != 0);
                    }
                    else {
                        sustain = (val & 0x01) != 0;
                        alternate = (val & 0x02) != 0;
                    }
                    envelopeStep = 0x0f;
                    sustaining = false;
                    envelopeVolume = (envelopeStep ^ attack);
                    break;

                    case AY_PORT_A:
                    /*
                    if ((regs[AY_ENABLE] & 0x40) > 0) {
                        selectedRegister = regs[AY_PORT_A];
                    }*/
                    break;

                    case AY_PORT_B:
                    /*
                    if ((regs[AY_ENABLE] & 0x80) > 0) {
                        PortWrite(regs[AY_PORT_A]);
                    }*/
                    break;
                }
                regs[SelectedRegister] = (byte)val;
            }
            else {
                Responded = false;
            }
        }

        public void EnableStereoSound(bool is_stereo) {
            StereoSound = is_stereo;
        }

        public void SetChannelsACB(bool is_acb) {
            //ACB
            if (is_acb) {
                ChannelCenter = 2;
                ChannelRight = 1;
            }
            //ABC
            else {
                ChannelCenter = 1;
                ChannelRight = 2;
            }
        }

        public void SetRegisters(byte[] _regs) {
            for (int f = 0; f < 16; f++)
                regs[f] = _regs[f];
        }

        public byte[] GetRegisters() {
            byte[] newArray = new byte[16];
            for (int f = 0; f < 16; f++)
                newArray[f] = (byte)(regs[f] & 0xff);
            return newArray;
        }

        public void Reset() {
            for (int i = 0; i < 16; i++) {
                AY_SpecVolumes[i] = (short)(AY_Volumes[i] * 8191);
            }
            regs = new byte[16];
            channel_out = new int[3];
            channel_count = new int[3];
            averagedChannelSamples = new int[3];
            channel_mix = new short[3];

            soundSampleCounter = 0;
            noiseOut = 0x01;
            envelopeVolume = 0;
            noiseCount = 0;
            ayTStates = 0;
            envelopeCount = 0;
            randomSeed = 1;
            selectedRegister = 0;
        }

        public void PortWrite(int val) {
            switch (SelectedRegister) {
                case AY_A_FINE:
                break;

                case AY_A_COARSE:
                val &= 0x0f;
                break;

                case AY_B_FINE:
                break;

                case AY_B_COARSE:
                val &= 0x0f;
                break;

                case AY_C_FINE:
                break;

                case AY_C_COARSE:
                val &= 0x0f;
                break;

                case AY_NOISEPER:
                val &= 0x1f;
                break;

                case AY_ENABLE:
                /*
                if ((lastEnable == -1) || ((lastEnable & 0x40) != (regs[AY_ENABLE] & 0x40))) {
                    SelectedRegister = ((regs[AY_ENABLE] & 0x40) > 0 ? regs[AY_PORT_B] : 0xff);
                }

                if ((lastEnable == -1) || ((lastEnable & 0x80) != (regs[AY_ENABLE] & 0x80))) {
                     PortWrite((regs[AY_ENABLE] & 0x80) > 0 ? regs[AY_PORT_B] : 0xff);
                }
                lastEnable = regs[AY_ENABLE];*/
                break;

                case AY_A_VOL:
                val &= 0x1f;
                break;

                case AY_B_VOL:
                val &= 0x1f;
                break;

                case AY_C_VOL:
                val &= 0x1f;
                break;

                case AY_E_FINE:
                break;

                case AY_E_COARSE:
                break;

                case AY_E_SHAPE:
                val &= 0x0f;
                attack = ((val & 0x04) != 0 ? 0x0f : 0x00);
                // envelopeCount = 0;
                if ((val & 0x08) == 0) {
                    /* if Continue = 0, map the shape to the equivalent one which has Continue = 1 */
                    sustain = true;
                    alternate = (attack != 0);
                }
                else {
                    sustain = (val & 0x01) != 0;
                    alternate = (val & 0x02) != 0;
                }
                envelopeStep = 0x0f;
                sustaining = false;
                envelopeVolume = (envelopeStep ^ attack);
                break;

                case AY_PORT_A:
                /*
                if ((regs[AY_ENABLE] & 0x40) > 0) {
                    selectedRegister = regs[AY_PORT_A];
                }*/
                break;

                case AY_PORT_B:
                /*
                if ((regs[AY_ENABLE] & 0x80) > 0) {
                    PortWrite(regs[AY_PORT_A]);
                }*/
                break;
            }

            regs[SelectedRegister] = (byte)val;
        }

        public int PortRead() {
            if (SelectedRegister == AY_PORT_B) {
                if ((regs[AY_ENABLE] & 0x80) == 0)
                    return 0xff;
                else
                    return regs[AY_PORT_B];
            }

            return regs[selectedRegister];
        }

        public void EndSampleFrame() {
            while (soundSampleCounter < 4)
                SampleOutput();

            if (stereoSound) {
                averagedChannelSamples[0] = (short)((averagedChannelSamples[ChannelLeft] + averagedChannelSamples[ChannelCenter]) / soundSampleCounter);
                averagedChannelSamples[1] = (short)((averagedChannelSamples[ChannelRight] + averagedChannelSamples[ChannelCenter]) / soundSampleCounter);
                averagedChannelSamples[2] = 0;
            }
            else {
                short sample = (short)((averagedChannelSamples[ChannelCenter]) / soundSampleCounter);
                averagedChannelSamples[0] = sample;
                averagedChannelSamples[1] = sample;
                averagedChannelSamples[2] = 0;
            }
            soundSampleCounter = 0;
        }

        public void ResetSamples() {
            averagedChannelSamples[0] = 0;
            averagedChannelSamples[1] = 0;
            averagedChannelSamples[2] = 0;
        }

        public void SampleOutput() {
            int ah;

            ah = regs[AY_ENABLE];

            channel_mix[(int)Channel.A] = MixChannel(ah, regs[AY_A_VOL], (int)Channel.A);

            ah >>= 1;
            channel_mix[(int)Channel.B] = MixChannel(ah, regs[AY_B_VOL], (int)Channel.B);

            ah >>= 1;
            channel_mix[(int)Channel.C] = MixChannel(ah, regs[AY_C_VOL], (int)Channel.C);

            if (stereoSound) {
                averagedChannelSamples[0] += channel_mix[(int)Channel.A];
                averagedChannelSamples[1] += channel_mix[(int)Channel.B];
                averagedChannelSamples[2] += channel_mix[(int)Channel.C];
            }
            else {
                //Only need to maintain one channel for mono.
                averagedChannelSamples[2] += channel_mix[(int)Channel.A] + channel_mix[(int)Channel.B] + channel_mix[(int)Channel.C];
            }
            soundSampleCounter++;
        }

        private short MixChannel(int ah, int cl, int chan) {
            int al = channel_out[chan];
            int bl, bh;
            bl = ah;
            bh = ah;
            bh &= 0x1;
            bl >>= 3;

            al |= (bh); //Tone | AY_ENABLE
            bl |= (noiseOut); //Noise | AY_ENABLE
            al &= bl;

            if ((al != 0)) {
                if ((cl & 16) != 0)
                    cl = envelopeVolume;

                cl &= 15;

                //return (AY_Volumes[cl]);
                return (AY_SpecVolumes[cl]);
            }
            return 0;
        }

        private int TonePeriod(int channel) {
            return (regs[(channel) << 1] | ((regs[((channel) << 1) | 1] & 0x0f) << 8));
        }

        private int NoisePeriod() {
            return (regs[AY_NOISEPER] & 0x1f);
        }

        private int EnvelopePeriod() {
            return ((regs[AY_E_FINE] | (regs[AY_E_COARSE] << 8)));
        }

        private int NoiseEnable(int channel) {
            return ((regs[AY_ENABLE] >> (3 + channel)) & 1);
        }

        private int ToneEnable(int channel) {
            return ((regs[AY_ENABLE] >> (channel)) & 1);
        }

        private int ToneEnvelope(int channel) {
            //return ((regs[AY_A_VOL + channel] & 0x10) >> 4);
            return ((regs[AY_A_VOL + channel] >> 4) & 0x1);
        }

        private void UpdateNoise() {
            noiseCount++;
            if ((noiseCount >= (regs[AY_NOISEPER] & 0x1f)) && (noiseCount > 4)) {
                /* Is noise output going to change? */
                if (((randomSeed + 1) & 2) != 0) /* (bit0^bit1)? */ {
                    noiseOut ^= 1;
                }

                /* The Random Number Generator of the 8910 is a 17-bit shift */
                /* register. The input to the shift register is bit0 XOR bit3 */
                /* (bit0 is the output). This was verified on AY-3-8910 and YM2149 chips. */

                /* The following is a fast way to compute bit17 = bit0^bit3. */
                /* Instead of doing all the logic operations, we only check */
                /* bit0, relying on the fact that after three shifts of the */
                /* register, what now is bit3 will become bit0, and will */
                /* invert, if necessary, bit14, which previously was bit17. */
                if ((randomSeed & 1) != 0)
                    randomSeed ^= 0x28000; /* This version is called the "Galois configuration". */
                randomSeed >>= 1;
                noiseCount = 0;
            }
        }

        private void UpdateEnvelope() {
            envelopeClock ^= 1;

            if (envelopeClock == 1) {
                envelopeCount++;

                //if ((((regs[AY_A_VOL + 0] & 0x10) >> 4) & (((regs[AY_A_VOL + 1] & 0x10) >> 4) & ((regs[AY_A_VOL + 2] & 0x10) >> 4))) != 1)
                //if ((((regs[AY_A_VOL + 0] >> 4) & 0x1) & (((regs[AY_A_VOL + 1] >> 4) & 0x1) & ((regs[AY_A_VOL + 2] >> 4) & 0x1))) != 0)
                if (((regs[AY_A_VOL + 0] & 0x10) & (regs[AY_A_VOL + 1] & 0x10) & (regs[AY_A_VOL + 2] & 0x10)) != 1) {
                    /* update envelope */
                    if (!sustaining) {
                        //envelopeClock++;
                        if (envelopeCount >= (regs[AY_E_FINE] | (regs[AY_E_COARSE] << 8))) {
                            envelopeStep--;

                            /* check envelope current position */
                            if (envelopeStep < 0) {
                                if (sustain) {
                                    if (alternate)
                                        attack ^= 0x0f;
                                    sustaining = true;
                                    envelopeStep = 0;
                                }
                                else {
                                    /* if CountEnv has looped an odd number of times (usually 1), */
                                    /* invert the output. */
                                    if (alternate && ((envelopeStep & (0x0f + 1)) != 0) && (envelopeCount > 4))
                                        attack ^= 0x0f;

                                    envelopeStep &= 0x0f;
                                }
                            }
                            envelopeCount = 0;
                        }
                    }
                    envelopeVolume = (envelopeStep ^ attack);
                }
            }
        }

        public void Update(int deltaTstates) {
            ayTStates += deltaTstates;

            while (ayTStates >= AY_SAMPLE_RATE) {
                UpdateEnvelope();

                //Update noise
                if ((regs[AY_ENABLE] & 0x38) != 0x38) {
                    UpdateNoise();
                }

                //Update channels
                channel_count[0]++;
                int regs1 = (regs[1] & 0x0f) << 8;
                if (((regs[0] | regs1) > 4) && (channel_count[0] >= (regs[0] | regs1))) {
                    channel_out[0] ^= 1;
                    channel_count[0] = 0;
                }

                int regs3 = (regs[3] & 0x0f) << 8;
                channel_count[1]++;
                if (((regs[2] | regs3) > 4) && (channel_count[1] >= (regs[2] | regs3))) {
                    channel_out[1] ^= 1;
                    channel_count[1] = 0;
                }

                int regs5 = (regs[5] & 0x0f) << 8;
                channel_count[2]++;
                if (((regs[4] | regs5) > 4) && (channel_count[2] >= (regs[4] | regs5))) {
                    channel_out[2] ^= 1;
                    channel_count[2] = 0;
                }
                ayTStates -= AY_SAMPLE_RATE;
                SampleOutput();
            }
        }
    }
}
