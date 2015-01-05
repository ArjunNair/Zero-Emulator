using System;
using System.Collections.Generic;
using System.Reflection;
using System.ComponentModel;

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
    }
}
