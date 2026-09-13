using System;
using System.Collections.Generic;
using SDL;
using Zero.Emulation.Host;
using static SDL.SDL3;

namespace Zero.Sdl
{
    /// <summary>
    /// Polls SDL3 gamepads with hot-plug support. Initialises the gamepad subsystem lazily on the
    /// thread that first calls <see cref="Update"/> (the emulation thread), which keeps SDL's
    /// per-thread HID run loop on macOS happy.
    /// </summary>
    public sealed unsafe class SdlGamepadSource : IGamepadSource
    {
        private readonly List<SDL_JoystickID> _order = new List<SDL_JoystickID>();
        private readonly Dictionary<uint, IntPtr> _open = new Dictionary<uint, IntPtr>();
        private bool _initialised;
        private int _rescanCountdown;

        public int Count => _order.Count;

        public void Update()
        {
            if (!_initialised)
            {
                SdlRuntime.EnsureInit(SDL_InitFlags.SDL_INIT_GAMEPAD);
                SDL_SetJoystickEventsEnabled(false);
                SDL_SetGamepadEventsEnabled(false);
                _initialised = true;
            }

            SDL_UpdateGamepads();

            // Device enumeration is comparatively expensive; every 25 frames (~0.5 s) is plenty for hot-plug.
            if (--_rescanCountdown <= 0)
            {
                _rescanCountdown = 25;
                Rescan();
            }
        }

        private void Rescan()
        {
            int count;
            SDL_JoystickID* ids = SDL_GetGamepads(&count);
            var seen = new HashSet<uint>();
            _order.Clear();
            if (ids != null)
            {
                for (int i = 0; i < count; i++)
                {
                    SDL_JoystickID id = ids[i];
                    uint key = (uint)id;
                    seen.Add(key);
                    if (!_open.ContainsKey(key))
                    {
                        SDL_Gamepad* pad = SDL_OpenGamepad(id);
                        if (pad == null) continue;
                        _open[key] = (IntPtr)pad;
                    }
                    _order.Add(id);
                }
                SDL_free(ids);
            }
            foreach (uint key in new List<uint>(_open.Keys))
            {
                if (!seen.Contains(key))
                {
                    SDL_CloseGamepad((SDL_Gamepad*)_open[key]);
                    _open.Remove(key);
                }
            }
        }

        public string GetName(int index)
        {
            if (index < 0 || index >= _order.Count) return null;
            return SDL_GetGamepadName((SDL_Gamepad*)_open[(uint)_order[index]]);
        }

        public GamepadInput Poll(int index)
        {
            if (index < 0 || index >= _order.Count) return default;
            var pad = (SDL_Gamepad*)_open[(uint)_order[index]];
            uint buttons = 0;
            for (int b = 0; b < 26; b++)
                if (SDL_GetGamepadButton(pad, (SDL_GamepadButton)b)) buttons |= 1u << b;
            return new GamepadInput
            {
                Connected = true,
                LeftX = SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX),
                LeftY = SDL_GetGamepadAxis(pad, SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY),
                Buttons = buttons
            };
        }

        public void Dispose()
        {
            foreach (IntPtr p in _open.Values)
                SDL_CloseGamepad((SDL_Gamepad*)p);
            _open.Clear();
            _order.Clear();
        }
    }
}
