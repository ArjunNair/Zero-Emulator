using System;
using System.Collections.Generic;
using System.Reflection;
using System.ComponentModel;
using System.IO;

namespace ZeroWin
{
    class Utilities
    {
        public static string GetStringFromEnum(Enum value)
        {
            FieldInfo fi = value.GetType().GetField(value.ToString());

            DescriptionAttribute[] attributes =
                (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);

            if (attributes != null && attributes.Length > 0)
                return attributes[0].Description;
            else
                return value.ToString();
        }

        public static T GetEnumFromString<T>(string s, T defaultValue) where T: struct
        {
            T enumToReturn = defaultValue;

            foreach (var val in Enum.GetValues(typeof(T)))
            {
                string enumDesc = GetStringFromEnum((Enum)val);

                if (enumDesc == s)
                {
                    enumToReturn = (T)val;
                    break;
                }
            }
            return enumToReturn;
        }

        public static IEnumerable<T> EnumToList<T>()
        {
            Type enumType = typeof(T);

            // Can't use generic type constraints on value types,
            // so have to do check like this
            if (enumType.BaseType != typeof(Enum))
                throw new ArgumentException("T must be of type System.Enum");

            Array enumValArray = Enum.GetValues(enumType);
            List<T> enumValList = new List<T>(enumValArray.Length);

            foreach (int val in enumValArray)
            {
                enumValList.Add((T)Enum.Parse(enumType, val.ToString()));
            }

            return enumValList;
        }

        public static int ConvertToInt(string input) {
            bool validInput = false;
            int number = -1;

            if (input[0] == '$' || input[0] == '#')
                validInput = System.Int32.TryParse(input.Substring(1, input.Length - 1), System.Globalization.NumberStyles.HexNumber, null, out number);
            else
                validInput = System.Int32.TryParse(input, out number);

            if (!validInput) {
                System.Windows.Forms.MessageBox.Show(input + " isn't a valid number.", "Invalid input", System.Windows.Forms.MessageBoxButtons.OK);
                number = -1;
            }

            return number;
        }

        public static bool ReadBytesFromFile(string file, out byte[] data) {
            FileStream fs;
            data = new byte[0];

            try {
                fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            } catch {
                return false;
            }

            int length = (int)fs.Length;

            using (BinaryReader r = new BinaryReader(fs)) {
                data = new byte[length];

                int bytesRead = r.Read(data, 0, length);

                if (bytesRead == 0) {
                    fs.Close();
                    return false; //something bad happened!
                }
            }

            return true;
        }
    }
}
