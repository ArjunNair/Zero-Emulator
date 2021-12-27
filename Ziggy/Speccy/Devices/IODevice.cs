namespace Speccy {
    public interface IODevice: SpeccyDevice {

        bool Responded { get; }
        byte In(ushort port);
        void Out(ushort port, byte val);
    }
}
