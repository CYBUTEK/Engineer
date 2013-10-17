using System;

namespace Engineer
{
    public class EngineerTools
    {
        public const string BLANK = "----";

        public static string WeightFormatter(double weight)
        {
            weight *= 1000;
            return (weight > 0d) ? weight.ToString("#,#.") + " kg" : BLANK;
        }

        public static string WeightFormatter(double weight1, double weight2)
        {
            weight1 *= 1000;
            weight2 *= 1000;
            string format1 = (weight1 > 0d) ? weight1.ToString("#,#.") : BLANK;
            string format2 = (weight2 > 0d) ? weight2.ToString("#,#.") : BLANK;

            return format1 + " / " + format2 + " kg";
        }

        public static string ForceFormatter(double force)
        {
            return (force > 0d) ? force.ToString("#,#.#") + " kN" : BLANK;
        }

        public static string SimpleFormatter(double number, string postfix = "", int decimals = -1, bool alwaysShowDecimals = false)
        {
            string decimalMask = (alwaysShowDecimals) ? "0" : "#";

            if (decimals > -1)
            {
                decimalMask = "";
                for (int i = 0; i < decimals; i++)
                {
                    decimalMask += "0";
                }
            }

            string format = (number > 0d) ? number.ToString("#,#." + decimalMask) : BLANK;

            return  format + " " + postfix;
        }

        public static string SimpleFormatter(double number1, double number2, string postfix = "", int decimals = 0, bool alwaysShowDecimals = false)
        {
            string decimalMaskChar = (alwaysShowDecimals) ? "0" : "#";
            string decimalMask = "";

            if (decimals > -1)
            {
                decimalMask = "";
                for (int i = 0; i < decimals; i++)
                {
                    decimalMask += "0";
                }
            }

            string format1 = (number1 > 0d) ? number1.ToString("#,#." + decimalMask) : BLANK;
            string format2 = (number2 > 0d) ? number2.ToString("#,#." + decimalMask) : BLANK;

            return format1 + " / " + format2 + " " + postfix;
        }
    }
}
