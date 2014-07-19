// Kerbal Engineer Redux
// Author:  CYBUTEK
// License: Attribution-NonCommercial-ShareAlike 3.0 Unported

using System;

using UnityEngine;

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
                    return ToDistance(number); // Quick and dirty implementation of the new distance formatter from KER 1.0
                    //notation = new string[] { "mm", "m", "km", "Mm", "Gm", "Tm", "Pm", "Em", "Zm", "Ym" };
                    //number *= 1000;

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

        // Quick and dirty implementation of the new distance formatter from KER 1.0
        private static string ToDistance(double value, bool showNotation = true)
        {
            bool negative = value < 0d;

            if (negative) value = -value;

            if (value < 1000000d)
            {
                if (value < 1d)
                {
                    value *= 1000d;

                    if (negative) value = -value;
                    return value.ToString("#,0.") + "mm";
                }
                else
                {
                    if (value < 10d)
                    {
                        if (negative) value = -value;
                        return value.ToString("#,0.000") + "m";
                    }
                    else if (value < 100d)
                    {
                        if (negative) value = -value;
                        return value.ToString("#,0.00") + "m";
                    }
                    else if (value < 1000d)
                    {
                        if (negative) value = -value;
                        return value.ToString("#,0.0") + "m";
                    }
                    else
                    {
                        if (negative) value = -value;
                        return value.ToString("#,0.") + "m";
                    }
                }
            }
            else
            {
                value /= 1000d;
                if (value >= 1000000d)
                {
                    value /= 1000d;
                    if (negative) value = -value;
                    return value.ToString("#,0." + "Mm");
                }
                else
                {
                    if (negative) value = -value;
                    return value.ToString("#,0." + "km");
                }
            }
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

        public static void GetSlopeAngleAndHeading(Vessel vessel, out string result)
        {
            //LogMsg log = new LogMsg();
            CelestialBody mainBody = vessel.mainBody;
            Vector3d rad = (vessel.CoM - mainBody.position).normalized;
            //log.buf.AppendLine("rad = " + rad.ToString() + " len = " + rad.magnitude);
            RaycastHit hit;
            if (Physics.Raycast(vessel.CoM, -rad, out hit))
            {
                Vector3d norm = hit.normal;
                norm = norm.normalized;
                //log.buf.AppendLine("norm = " + norm.ToString() + " len = " + norm.magnitude);
                double raddotnorm = Vector3d.Dot(rad, norm);
                //log.buf.AppendLine("dot = " + raddotnorm);
                if (raddotnorm > 1.0)
                    raddotnorm = 1.0;
                else if (raddotnorm < 0.0)
                    raddotnorm = 0.0;
                double slope = Math.Acos(raddotnorm) * 180 / Math.PI;
                //log.buf.AppendLine("slope = " + slope);
                result = FormatNumber(slope, "°", 1);
                if (slope < 0.05)
                {
                    result += " @ ---°";
                }
                else
                {
                    Vector3d side = Vector3d.Cross(rad, norm).normalized;
                    //log.buf.AppendLine("side = " + side.ToString() + " len = " + side.magnitude);
                    Vector3d east = Vector3d.Cross(rad, Vector3d.up).normalized;
                    //log.buf.AppendLine("east = " + east.ToString() + " len = " + east.magnitude);
                    Vector3d north = Vector3d.Cross(rad, east).normalized;
                    //log.buf.AppendLine("north = " + north.ToString() + " len = " + north.magnitude);
                    double sidedoteast = Vector3d.Dot(side, east);
                    //log.buf.AppendLine("side.east = " + sidedoteast);
                    double direction = Math.Acos(sidedoteast) * 180 / Math.PI;
                    //log.buf.AppendLine("angle side:east = " + direction);
                    double sidedotnorth = Vector3d.Dot(side, north);
                    //log.buf.AppendLine("side.north = " + sidedotnorth);
                    if (sidedotnorth < 0)
                        direction = 360 - direction;
                    result += " @ " + FormatNumber(direction, "°", 0);
                }
            }
            else
            {
                result = "--° @ ---°";
            }
            //log.Flush();
        } 
    }
}
