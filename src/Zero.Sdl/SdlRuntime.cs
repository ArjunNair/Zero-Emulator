using System;
using SDL;
using static SDL.SDL3;

namespace Zero.Sdl
{
    /// <summary>Reference-counted SDL subsystem initialisation shared by the audio and gamepad backends.</summary>
    public static class SdlRuntime
    {
        private static readonly object Sync = new object();
        private static SDL_InitFlags _initialised;

        public static void EnsureInit(SDL_InitFlags flags)
        {
            lock (Sync)
            {
                SDL_InitFlags missing = flags & ~_initialised;
                if (missing == 0) return;
                if (!SDL_InitSubSystem(missing))
                    throw new InvalidOperationException("SDL_InitSubSystem failed: " + SDL_GetError());
                _initialised |= missing;
            }
        }

        public static void Shutdown()
        {
            lock (Sync)
            {
                if (_initialised != 0) SDL_Quit();
                _initialised = 0;
            }
        }
    }
}
