using SpeccyCommon;

namespace Speccy
{
    public interface SpeccyDevice
    {
        void RegisterDevice(zx_spectrum speccyModel);
        void UnregisterDevice(zx_spectrum speccyModel);
        void Reset();
        SPECCY_DEVICE DeviceID { get; }
    }
}
