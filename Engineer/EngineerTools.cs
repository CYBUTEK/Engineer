using System;

/*
  This (non-officially) static class groups methods for
  formatting floating-point values for printing. It relies heavily
  on the ToString method with custom numeric format strings. In
  particular, it uses a suffix to truncate the value with the
  specified number of digits. For more information, refer to
  http://msdn.microsoft.com/en-us/library/0c899ak8%28v=vs.110%29.aspx

  Additionnally, WeightFormatter and SimpleFormatter exist in two
  versions: the seconds prints two numbers separated by a slash (/)
  instead of one. While SimpleFormatter asks for the suffix unit to be
  used (postfix parameter), WeightFormatter also handles non-SI values
  from KSP (in tons).
*/

namespace Engineer
{
    public class EngineerTools
    {
        // place-holder for non-zero values
        public const string BLANK = "----";

        public static string WeightFormatter(double weight)
        {
            weight *= 1000;
            return (weight > 0d) ? weight.ToString("#,0.") + "kg" : BLANK;
        }

        public static string WeightFormatter(double weight1, double weight2)
        {
            weight1 *= 1000;
            weight2 *= 1000;
            string format1 = (weight1 > 0d) ? weight1.ToString("#,0.") : BLANK;
            string format2 = (weight2 > 0d) ? weight2.ToString("#,0.") : BLANK;

            return format1 + " / " + format2 + "kg";
        }

        public static string ForceFormatter(double force)
        {
            return (force > 0d) ? force.ToString("#,0.#") + "kN" : BLANK;
        }

        // see header comment
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

            string format = (number > 0d) ? number.ToString("#,0." + decimalMask) : BLANK;

            return  format + postfix;
        }

        // see header comment
        public static string SimpleFormatter(double number1, double number2, string postfix = "", int decimals = 0, bool alwaysShowDecimals = false)
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

            string format1 = (number1 > 0d) ? number1.ToString("#,0." + decimalMask) : BLANK;
            string format2 = (number2 > 0d) ? number2.ToString("#,0." + decimalMask) : BLANK;

            return format1 + " / " + format2 + postfix;
        }
    }
}
