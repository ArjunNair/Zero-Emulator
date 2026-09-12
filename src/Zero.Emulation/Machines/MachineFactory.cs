using System;
using Speccy;
using SpeccyCommon;

namespace Zero.Emulation.Machines
{
    public static class MachineFactory
    {
        public static readonly MachineModel[] Supported =
        {
            MachineModel._48k, MachineModel._128k, MachineModel._128ke, MachineModel._plus3, MachineModel._pentagon
        };

        public static zx_spectrum Create(MachineModel model, IAudioOutput audio, bool lateTimings)
        {
            switch (model)
            {
                case MachineModel._48k: return new zx_48k(audio, lateTimings);
                case MachineModel._128k: return new zx_128k(audio, lateTimings);
                case MachineModel._128ke: return new zx_128ke(audio, lateTimings);
                case MachineModel._plus3: return new zx_plus3(audio, lateTimings);
                case MachineModel._pentagon: return new Pentagon_128k(audio, lateTimings);
                default: throw new ArgumentOutOfRangeException(nameof(model), model, "Unsupported machine");
            }
        }

        public static string DisplayName(MachineModel model)
        {
            switch (model)
            {
                case MachineModel._48k: return "ZX Spectrum 48K";
                case MachineModel._128k: return "ZX Spectrum 128K";
                case MachineModel._128ke: return "ZX Spectrum 128Ke";
                case MachineModel._plus3: return "ZX Spectrum +3";
                case MachineModel._pentagon: return "Pentagon 128K";
                default: return model.ToString();
            }
        }
    }
}
