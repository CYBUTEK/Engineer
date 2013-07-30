// Kerbal Engineer Redux
// Author:  CYBUTEK
// License: Attribution-NonCommercial-ShareAlike 3.0 Unported

using System;

namespace Engineer
{
    public class Tools
    {
        public enum SIUnitType { Speed, Distance, Pressure, Density, Force, Mass };

        public static string FormatSI(double number, SIUnitType type)
        {
            // Return a spacing string if number is 0;
            if (number == 0)
            {
                return "-----";
            }

            // Assign memory for storing the notations.
            string[] notation = { "" };

            // Select the SIUnitType used and populate the notation array.
            switch (type)
            {
                case SIUnitType.Distance:
                    notation = new string[] { "mm", "m", "km", "Mm", "Gm", "Tm", "Pm", "Em", "Zm", "Ym" };
                    number *= 1000;
                    break;
                case SIUnitType.Speed:
                    notation = new string[] { "mm/s", "m/s", "km/s", "Mm/s", "Gm/s", "Tm/s", "Pm/s", "Em/s", "Zm/s", "Ym/s" };
                    number *= 1000;
                    break;
                case SIUnitType.Pressure:
                    notation = new string[] { "Pa", "kPa", "MPa", "GPa", "TPa", "PPa", "EPa", "ZPa", "YPa" };
                    number *= 1000;
                    break;
                case SIUnitType.Density:
                    notation = new string[] { "mg/m³", "g/m³", "kg/m³", "Mg/m³", "Gg/m³", "Tg/m³", "Pg/m³", "Eg/m³", "Zg/m³", "Yg/m³" };
                    number *= 1000000;
                    break;
                case SIUnitType.Force:
                    notation = new string[] { "N", "kN", "MN", "GN", "TN", "PT", "EN", "ZN", "YN" };
                    number *= 1000;
                    break;
                case SIUnitType.Mass:
                    notation = new string[] { "g", "kg", "Mg", "Gg", "Tg", "Pg", "Eg", "Zg", "Yg" };
                    number *= 1000;
                    break;
            }

            int notationIndex = 0;  // Index that is used to select the notation to display.

            // Loop through the notations until the smallest usable one is found.
            for (notationIndex = 0; notationIndex < notation.Length; notationIndex++)
            {
                if (number > 1000 || number < -1000) { number /= 1000; } else { break; }
            }

            // Return a string of the concatinated number and selected notation.
            return number.ToString("0.000") + notation[notationIndex];
        }

        public static string FormatTime(double seconds)
        {
            double s = seconds;
            int m = 0;
            int h = 0;
            double d = 0d;
            double y = 0d;

            if (s >= 31536000)
            {
                while (s >= 31536000)
                {
                    y++;
                    s -= 31536000;
                }

                y += (s / 31536000);
                return y.ToString("0.000") + "y";
            }

            if (s >= 86400)
            {
                while (s >= 86400)
                {
                    d++;
                    s -= 86400;
                }

                d += (s / 86400);
                return d.ToString("0.000") + "d";
            }

            while (s >= 60)
            {
                m++;
                s -= 60;
            }

            while (m >= 60)
            {
                h++;
                m -= 60;
            }

            while (h >= 24)
            {
                d++;
                h -= 24;
            }

            if (h > 0)
            {
                return h + ":" + m.ToString("00") + ":" + s.ToString("00.0") + "s";
            }

            if (m > 0)
            {
                return m + ":" + s.ToString("00.0") + "s";
            }

            return s.ToString("0.0") + "s";
        }

        public static string FormatNumber(double number, int decimals = -1)
        {
            if (number == 0)
            {
                return "-----";
            }

            if (decimals == -1)
            {
                return number.ToString();
            }
            if (decimals == 0)
            {
                return number.ToString("0");
            }

            string numberFormat = "0.";

            for (int i = 0; i < decimals; i++)
            {
                numberFormat += "0";
            }

            return number.ToString(numberFormat);
        }

        public static string FormatNumber(double number, string postfix, int decimals = -1)
        {
            if (number == 0)
            {
                return "-----";
            }

            return FormatNumber(number, decimals) + postfix;
        }
    }
}
