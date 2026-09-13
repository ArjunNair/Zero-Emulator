using System;
using System.Collections.Generic;
using SpeccyCommon;
using Zero.Emulation.Host;

namespace Zero.Emulation.Input
{
    /// <summary>SDL_GamepadButton indices and display names, in SDL order.</summary>
    public static class GamepadButtons
    {
        public static readonly string[] Names =
        {
            "A / Cross (south)", "B / Circle (east)", "X / Square (west)", "Y / Triangle (north)",
            "Back / Select", "Guide", "Start", "Left stick click", "Right stick click",
            "Left shoulder", "Right shoulder", "D-pad up", "D-pad down", "D-pad left", "D-pad right",
            "Misc 1", "Right paddle 1", "Left paddle 1", "Right paddle 2", "Left paddle 2", "Touchpad",
            "Misc 2", "Misc 3", "Misc 4", "Misc 5", "Misc 6"
        };

        public const int South = 0, East = 1, West = 2, North = 3, Back = 4, Guide = 5, Start = 6,
            LeftStick = 7, RightStick = 8, LeftShoulder = 9, RightShoulder = 10,
            DpadUp = 11, DpadDown = 12, DpadLeft = 13, DpadRight = 14;

        public const int Count = 26;
    }

    /// <summary>
    /// What a gamepad button does. Joystick actions feed the emulated interface; "Key:" actions press
    /// a Spectrum key (handy for Space, Enter or a game's pause key).
    /// </summary>
    public static class GamepadActions
    {
        public const string None = "None", Up = "Up", Down = "Down", Left = "Left", Right = "Right",
            Fire1 = "Fire 1", Fire2 = "Fire 2", Fire3 = "Fire 3";
        public const string KeyPrefix = "Key:";

        public static readonly string[] JoystickActions = { None, Fire1, Fire2, Fire3, Up, Down, Left, Right };

        /// <summary>Every selectable action: joystick actions followed by all Spectrum keys.</summary>
        public static IReadOnlyList<string> All { get; } = BuildAll();

        private static IReadOnlyList<string> BuildAll()
        {
            var list = new List<string>(JoystickActions);
            foreach (keyCode k in Enum.GetValues(typeof(keyCode)))
                if (k != keyCode.LAST) list.Add(KeyPrefix + k);
            return list;
        }

        public static bool TryGetKey(string action, out keyCode key)
        {
            key = keyCode.LAST;
            return action != null && action.StartsWith(KeyPrefix, StringComparison.Ordinal)
                   && Enum.TryParse(action.Substring(KeyPrefix.Length), out key) && key != keyCode.LAST;
        }
    }

    /// <summary>One gamepad's button bindings (button index → action name). Serialised in settings.</summary>
    public sealed class GamepadMapping
    {
        public const short AxisThreshold = 12000;

        /// <summary>Button index → action. Unlisted buttons do nothing. Sticks and D-pad always steer.</summary>
        public Dictionary<int, string> Buttons { get; set; } = new Dictionary<int, string>();

        public static GamepadMapping Default() => new GamepadMapping
        {
            Buttons = new Dictionary<int, string>
            {
                [GamepadButtons.South] = GamepadActions.Fire1,
                [GamepadButtons.East] = GamepadActions.Fire2,
                [GamepadButtons.West] = GamepadActions.Fire3,
                [GamepadButtons.North] = GamepadActions.Fire1,
                [GamepadButtons.RightShoulder] = GamepadActions.Fire1,
                [GamepadButtons.DpadUp] = GamepadActions.Up,
                [GamepadButtons.DpadDown] = GamepadActions.Down,
                [GamepadButtons.DpadLeft] = GamepadActions.Left,
                [GamepadButtons.DpadRight] = GamepadActions.Right,
                [GamepadButtons.Start] = GamepadActions.KeyPrefix + keyCode.ENTER,
                [GamepadButtons.Back] = GamepadActions.KeyPrefix + keyCode.SPACE
            }
        };

        public string ActionFor(int button) => Buttons.TryGetValue(button, out string a) ? a : GamepadActions.None;

        public void Set(int button, string action)
        {
            if (string.IsNullOrEmpty(action) || action == GamepadActions.None) Buttons.Remove(button);
            else Buttons[button] = action;
        }

        /// <summary>Turn raw input into joystick state plus any Spectrum keys bound to pressed buttons.</summary>
        public GamepadState Resolve(in GamepadInput raw, List<keyCode> pressedKeys)
        {
            var s = new GamepadState
            {
                Connected = raw.Connected,
                Left = raw.LeftX < -AxisThreshold,
                Right = raw.LeftX > AxisThreshold,
                Up = raw.LeftY < -AxisThreshold,
                Down = raw.LeftY > AxisThreshold
            };
            if (!raw.Connected) return s;

            foreach (KeyValuePair<int, string> b in Buttons)
            {
                if (!raw.IsDown(b.Key)) continue;
                switch (b.Value)
                {
                    case GamepadActions.Up: s.Up = true; break;
                    case GamepadActions.Down: s.Down = true; break;
                    case GamepadActions.Left: s.Left = true; break;
                    case GamepadActions.Right: s.Right = true; break;
                    case GamepadActions.Fire1: s.Fire1 = true; break;
                    case GamepadActions.Fire2: s.Fire2 = true; break;
                    case GamepadActions.Fire3: s.Fire3 = true; break;
                    default:
                        if (pressedKeys != null && GamepadActions.TryGetKey(b.Value, out keyCode key)) pressedKeys.Add(key);
                        break;
                }
            }
            return s;
        }
    }
}
