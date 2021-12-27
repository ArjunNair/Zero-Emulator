namespace Speccy
{
    public interface AudioDevice: IODevice
    {
        void Update(int deltaTstates);
        void SampleOutput();
        void EndSampleFrame();
        void ResetSamples();
        void EnableStereoSound(bool is_stereo);
        void SetChannelsACB(bool is_acb);

        // We only support stereo or mono sound.
        // If the audio device only supports mono, it sends the same
        // data for both channels.
        int SoundChannel1 { get; set; }
        int SoundChannel2 { get; set; }
    }
}
