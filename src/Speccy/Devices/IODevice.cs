using SpeccyCommon;

namespace Speccy {
    public interface IODevice {
        // Devices that respond to port read and writes, do so in a priority order
        // that determines who "wins". Highest priority devices win over lower.
        int Priority => 0;              // The lowest priority by default
        bool Responded { get; }
        byte In(ushort port);
        void Out(ushort port, byte val);
        void RegisterDevice(zx_spectrum speccyModel) {}
        void UnregisterDevice(zx_spectrum speccyModel);
        void Reset();
        SPECCY_DEVICE DeviceID { get; }
    }
}
