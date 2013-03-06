// Kerbal Engineer Redux
// Author:  CYBUTEK
// License: Attribution-NonCommercial-ShareAlike 3.0 Unported

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace Engineer
{
    class FlightEngineer : PartModule
    {
        public Settings settings = new Settings();
        Version version = new Version();
        bool showUpdate = true;
        string settingsFile = "flight_engineer.cfg";

        Rendezvous rendezvous = new Rendezvous();

        public Rect windowPosition = new Rect(UnityEngine.Screen.width - 275, 0, 0, 0);
        int windowID = new System.Random().Next();
        int windowMargin = 25;

        double maxGForce = 0f;

        public GUIStyle winStyle, heading, data;

        Simulator simulator = new Simulator();
        Stage[] stages = new Stage[0];
        double stageDeltaV = 0d;
        int numberOfStages = 0;
        int numberOfStagesUseful = 0;

        Stopwatch simTimer = new Stopwatch();
        double simDelay = 0d;

        [KSPEvent(guiActive = true, guiName = "Toggle Flight Engineer", active = false)]
        public void ShowWindow()
        {
            settings.Set<bool>("_TOGGLE_FLIGHT_ENGINEER", !settings.Get<bool>("_TOGGLE_FLIGHT_ENGINEER", true));

            if (part.Modules.Contains("TapeDriveAnimator"))
            {
                TapeDriveAnimator tapeAnimator = (TapeDriveAnimator)part.Modules["TapeDriveAnimator"];
                tapeAnimator.Enabled = settings.Get<bool>("_TOGGLE_FLIGHT_ENGINEER");
            }
        }

        public bool IsPrimary
        {
            get
            {
                foreach (Part part in this.vessel.parts)
                {
                    if (part.Modules.Contains(this.ClassID))
                    {
                        if (this.part == part)
                        {
                            return true;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                return false;
            }
        }

        public override void OnStart(StartState state)
        {
            if (state != StartState.Editor)
            {
                rendezvous.FlightEngineer = this;

                RenderingManager.AddToPostDrawQueue(0, DrawGUI);

                //print("FlightEngineer: OnStart (" + state + ")");
            }
        }

        public override void OnSave(ConfigNode node)
        {
            try
            {
                if (IsPrimary)
                {
                    //settings.Set("_WINDOW_POSITION", settings.ConvertToString(windowPosition));
                    settings.Save(settingsFile);
                    settings.Changed = true;
                    //print("FlightEngineer: OnSave");
                }
            }
            catch { }
        }

        public override void OnLoad(ConfigNode node)
        {
            try
            {
                if (IsPrimary)
                {
                    settings.Load(settingsFile);
                    settings.Changed = true;
                    //windowPosition = settings.ConvertToRect(settings.Get("_SAVEONCHANGE_NOCHANGEUPDATE_WINDOW_POSITION", settings.ConvertToString(windowPosition)));
                    //print("FlightEngineer: OnLoad");
                }
            }
            catch { }
        }

        private void DrawGUI()
        {
            if (this.vessel != null)
            {
                if (this.vessel == FlightGlobals.ActiveVessel)
                {
                    if (IsPrimary)
                    {
                        Events["ShowWindow"].active = true;

                        if (settings.Get<bool>("_TOGGLE_FLIGHT_ENGINEER", true))
                        {
                            if (settings.Get<bool>("Use old GUI style", false))
                            {
                                GUI.skin = AssetBase.GetGUISkin("KSP window 2");
                            }
                            else
                            {
                                GUI.skin = HighLogic.Skin;
                            }
                            winStyle = new GUIStyle(GUI.skin.window);
                            winStyle.fixedWidth = 275;
                            heading = new GUIStyle(GUI.skin.label);
                            heading.normal.textColor = Color.white;
                            heading.fontStyle = FontStyle.Normal;
                            data = new GUIStyle(GUI.skin.label);
                            data.fontStyle = FontStyle.Normal;
                            data.alignment = TextAnchor.UpperRight;
                            data.stretchWidth = true;

                            if (!settings.IsDrawing)
                            {
                                windowPosition = settings.ConvertToRect(settings.Get("_SAVEONCHANGE_NOCHANGEUPDATE_WINDOW_POSITION", settings.ConvertToString(windowPosition)));
                                if (settings.Changed)
                                {
                                    windowPosition.height = 0;
                                }
                                windowPosition = GUILayout.Window(windowID, windowPosition, Window, "Flight Engineer  -  Version " + Version.VERSION, winStyle);
                                CheckWindowMargin();
                                settings.Set("_SAVEONCHANGE_NOCHANGEUPDATE_WINDOW_POSITION", settings.ConvertToString(windowPosition));
                            }
                            else
                            {
                                settings.DrawWindow();
                            }
                        }
                    }
                }
            }
        }

        private void Window(int windowId)
        {
            GUILayout.BeginHorizontal();
            settings.Set("_SAVEONCHANGE_SHOW_ORBITAL", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_ORBITAL", false), "ORB", GUI.skin.button));
            settings.Set("_SAVEONCHANGE_SHOW_SURFACE", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_SURFACE", false), "SUR", GUI.skin.button));
            settings.Set("_SAVEONCHANGE_SHOW_VESSEL", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_VESSEL", false), "VES", GUI.skin.button));
            settings.Set("_SAVEONCHANGE_SHOW_RENDEZVOUS", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_RENDEZVOUS", false), "RDV", GUI.skin.button));
            GUILayout.EndHorizontal();

            if (settings.Get<bool>("_SAVEONCHANGE_SHOW_ORBITAL"))
            {
                DrawOrbital();
            }

            if (settings.Get<bool>("_SAVEONCHANGE_SHOW_SURFACE"))
            {
                DrawSurface();
            }

            if (settings.Get<bool>("_SAVEONCHANGE_SHOW_VESSEL"))
            {
                DrawVessel();
            }

            if (settings.Get<bool>("_SAVEONCHANGE_SHOW_RENDEZVOUS"))
            {
                DrawRendezvous();
            }

            GUILayout.Label("Goto the Settings Configurator...", heading);
            if ((Event.current.type == EventType.repaint) && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition) && Input.GetMouseButtonDown(0))
            {
                settings.IsDrawing = true;
            }

            if (version.Newer && showUpdate)
            {
                GUILayout.Label("UPDATE AVAILABLE:  Version " + version.Remote + "!");
                if ((Event.current.type == EventType.repaint) && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition) && Input.GetMouseButtonDown(0))
                {
                    showUpdate = false;
                    settings.Changed = true;
                }
            }

            GUI.DragWindow();
        }

        private void DrawOrbital()
        {
            GUILayout.Label("ORBITAL DISPLAY", heading);
            GUILayout.BeginHorizontal(GUI.skin.textArea);
            GUILayout.BeginVertical();
            settings.Set("*SPACER_ORBITAL", "");
            settings.Set("*HEADING_ORBITAL", "ORBITAL DISPLAY");
            if (settings.Get<bool>("Orbital: Show Grouped Ap/Pe Readouts", false))
            {
                if (settings.Get<bool>("Orbital: Apoapsis Height", true)) GUILayout.Label("Apoapsis Height", heading);
                if (settings.Get<bool>("Orbital: Time to Apoapsis", true)) GUILayout.Label("Time to Apoapsis", heading);
                if (settings.Get<bool>("Orbital: Periapsis Height", true)) GUILayout.Label("Periapsis Height", heading);
                if (settings.Get<bool>("Orbital: Time to Periapsis", true)) GUILayout.Label("Time to Periapsis", heading);
            }
            else
            {
                if (settings.Get<bool>("Orbital: Apoapsis Height", true)) GUILayout.Label("Apoapsis Height", heading);
                if (settings.Get<bool>("Orbital: Periapsis Height", true)) GUILayout.Label("Periapsis Height", heading);
                if (settings.Get<bool>("Orbital: Time to Apoapsis", true)) GUILayout.Label("Time to Apoapsis", heading);
                if (settings.Get<bool>("Orbital: Time to Periapsis", true)) GUILayout.Label("Time to Periapsis", heading);
            }
            if (settings.Get<bool>("Orbital: Inclination", true)) GUILayout.Label("Inclination", heading);
            if (settings.Get<bool>("Orbital: Eccentricity", true)) GUILayout.Label("Eccentricity", heading);
            if (settings.Get<bool>("Orbital: Period", true)) GUILayout.Label("Orbital Period", heading);
            if (settings.Get<bool>("Orbital: Longitude of AN", true)) GUILayout.Label("Longitude of AN", heading);
            if (settings.Get<bool>("Orbital: Longitude of Pe", true)) GUILayout.Label("Longitude of Pe", heading);
            if (settings.Get<bool>("Orbital: Semi-major Axis", true)) GUILayout.Label("Semi-major Axis", heading);
            if (settings.Get<bool>("Orbital: Semi-minor Axis", true)) GUILayout.Label("Semi-minor Axis", heading);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (settings.Get<bool>("Orbital: Show Grouped Ap/Pe Readouts"))
            {
                if (settings.Get<bool>("Orbital: Apoapsis Height")) GUILayout.Label(Tools.FormatSI(this.vessel.orbit.ApA, Tools.SIUnitType.Distance), data);
                if (settings.Get<bool>("Orbital: Time to Apoapsis")) GUILayout.Label(Tools.FormatTime(this.vessel.orbit.timeToAp), data);
                if (settings.Get<bool>("Orbital: Periapsis Height")) GUILayout.Label(Tools.FormatSI(this.vessel.orbit.PeA, Tools.SIUnitType.Distance), data);
                if (settings.Get<bool>("Orbital: Time to Periapsis")) GUILayout.Label(Tools.FormatTime(this.vessel.orbit.timeToPe), data);
            }
            else
            {
                if (settings.Get<bool>("Orbital: Apoapsis Height")) GUILayout.Label(Tools.FormatSI(this.vessel.orbit.ApA, Tools.SIUnitType.Distance), data);
                if (settings.Get<bool>("Orbital: Periapsis Height")) GUILayout.Label(Tools.FormatSI(this.vessel.orbit.PeA, Tools.SIUnitType.Distance), data);
                if (settings.Get<bool>("Orbital: Time to Apoapsis")) GUILayout.Label(Tools.FormatTime(this.vessel.orbit.timeToAp), data);
                if (settings.Get<bool>("Orbital: Time to Periapsis")) GUILayout.Label(Tools.FormatTime(this.vessel.orbit.timeToPe), data);
            }
            if (settings.Get<bool>("Orbital: Inclination")) GUILayout.Label(Tools.FormatNumber(this.vessel.orbit.inclination, "°", 6), data);
            if (settings.Get<bool>("Orbital: Eccentricity")) GUILayout.Label(Tools.FormatNumber(this.vessel.orbit.eccentricity, "°", 6), data);
            if (settings.Get<bool>("Orbital: Period")) GUILayout.Label(Tools.FormatTime(this.vessel.orbit.period), data);
            if (settings.Get<bool>("Orbital: Longitude of AN")) GUILayout.Label(Tools.FormatNumber(this.vessel.orbit.LAN, "°", 6), data);
            if (settings.Get<bool>("Orbital: Longitude of Pe")) GUILayout.Label(Tools.FormatNumber(this.vessel.orbit.LAN + this.vessel.orbit.argumentOfPeriapsis, "°", 6), data);
            if (settings.Get<bool>("Orbital: Semi-major Axis")) GUILayout.Label(Tools.FormatSI(this.vessel.orbit.semiMajorAxis, Tools.SIUnitType.Distance), data);
            if (settings.Get<bool>("Orbital: Semi-minor Axis")) GUILayout.Label(Tools.FormatSI(this.vessel.orbit.semiMinorAxis, Tools.SIUnitType.Distance), data);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawSurface()
        {
            if (this.vessel.geeForce > maxGForce) maxGForce = this.vessel.geeForce;

            GUILayout.Label("SURFACE DISPLAY", heading);
            GUILayout.BeginHorizontal(GUI.skin.textArea);
            GUILayout.BeginVertical();
            settings.Set("*SPACER_SURFACE", "");
            settings.Set("*HEADING_SURFACE", "SURFACE DISPLAY");
            if (settings.Get<bool>("Surface: Altitude (Sea Level)", true)) GUILayout.Label("Altitude (Sea Level)", heading);
            if (settings.Get<bool>("Surface: Altitude (Terrain)", true)) GUILayout.Label("Altitude (Terrain)", heading);
            if (settings.Get<bool>("Surface: Vertical Speed", true)) GUILayout.Label("Vertical Speed", heading);
            if (settings.Get<bool>("Surface: Horizontal Speed", true)) GUILayout.Label("Horizontal Speed", heading);
            if (settings.Get<bool>("Surface: Longitude", true)) GUILayout.Label("Longitude", heading);
            if (settings.Get<bool>("Surface: Latitude", true)) GUILayout.Label("Latitude", heading);
            if (settings.Get<bool>("Surface: G-Force", true)) GUILayout.Label("G-Force", heading);

            if (settings.Get<bool>("Surface: Terminal Velocity", true)) GUILayout.Label("Terminal Velocity", heading);
            if (settings.Get<bool>("Surface: Atmospheric Efficiency", true)) GUILayout.Label("Atmospheric Efficiency", heading);
            if (settings.Get<bool>("Surface: Atmospheric Drag", true)) GUILayout.Label("Atmospheric Drag", heading);
            if (settings.Get<bool>("Surface: Atmospheric Density", true)) GUILayout.Label("Atmospheric Density", heading);
            if (settings.Get<bool>("Surface: Atmospheric Pressure", true)) GUILayout.Label("Atmospheric Pressure", heading);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (settings.Get<bool>("Surface: Altitude (Sea Level)")) GUILayout.Label(Tools.FormatSI(this.vessel.mainBody.GetAltitude(this.vessel.CoM), Tools.SIUnitType.Distance), data);
            if (settings.Get<bool>("Surface: Altitude (Terrain)")) GUILayout.Label(Tools.FormatSI(this.vessel.mainBody.GetAltitude(this.vessel.CoM) - this.vessel.terrainAltitude, Tools.SIUnitType.Distance), data);
            if (settings.Get<bool>("Surface: Vertical Speed")) GUILayout.Label(Tools.FormatSI(this.vessel.verticalSpeed, Tools.SIUnitType.Speed), data);
            if (settings.Get<bool>("Surface: Horizontal Speed")) GUILayout.Label(Tools.FormatSI(this.vessel.horizontalSrfSpeed, Tools.SIUnitType.Speed), data);
            if (settings.Get<bool>("Surface: Longitude")) GUILayout.Label(Tools.FormatNumber(this.vessel.longitude, "°", 6), data);
            if (settings.Get<bool>("Surface: Latitude")) GUILayout.Label(Tools.FormatNumber(this.vessel.latitude, "°", 6), data);
            if (settings.Get<bool>("Surface: G-Force")) GUILayout.Label(Tools.FormatNumber(this.vessel.geeForce, 3) + " / " + Tools.FormatNumber(maxGForce, "g", 3), data);

            double totalMass = 0d;
            double massDrag = 0d;
            foreach (Part part in this.vessel.parts)
            {
                if (part.physicalSignificance != Part.PhysicalSignificance.NONE)
                {
                    double partMass = part.mass + part.GetResourceMass();
                    totalMass += partMass;
                    massDrag += partMass * part.maximum_drag;
                }
            }

            double gravity = FlightGlobals.getGeeForceAtPosition(this.vessel.CoM).magnitude;
            double atmosphere = this.vessel.atmDensity;

            double terminalVelocity = 0d;
            if (atmosphere > 0)
            {
                terminalVelocity = Math.Sqrt((2 * totalMass * gravity) / (atmosphere * massDrag * FlightGlobals.DragMultiplier));
            }

            double atmosphericEfficiency = 0d;
            if (terminalVelocity > 0)
            {
                atmosphericEfficiency = FlightGlobals.ship_srfSpeed / terminalVelocity;
            }

            double dragForce = 0.5 * atmosphere * Math.Pow(FlightGlobals.ship_srfSpeed, 2) * massDrag * FlightGlobals.DragMultiplier;

            if (settings.Get<bool>("Surface: Terminal Velocity")) GUILayout.Label(Tools.FormatSI(terminalVelocity, Tools.SIUnitType.Speed), data);
            if (settings.Get<bool>("Surface: Atmospheric Efficiency")) GUILayout.Label(Tools.FormatNumber(atmosphericEfficiency * 100, "%", 2), data);
            if (settings.Get<bool>("Surface: Atmospheric Drag")) GUILayout.Label(Tools.FormatSI(dragForce, Tools.SIUnitType.Force), data);
            if (settings.Get<bool>("Surface: Atmospheric Pressure")) GUILayout.Label(Tools.FormatSI(this.part.dynamicPressureAtm * 100, Tools.SIUnitType.Pressure), data);
            if (settings.Get<bool>("Surface: Atmospheric Density")) GUILayout.Label(Tools.FormatSI(this.vessel.atmDensity, Tools.SIUnitType.Density), data);

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private double GetDrag()
        {
            double drag = 0d;

            foreach (Part part in this.vessel.parts)
            {
                drag += part.maximum_drag;
            }

            return drag;
        }

        private void DrawVessel()
        {
            if (((TimeWarp.WarpMode == TimeWarp.Modes.LOW) || (TimeWarp.CurrentRate <= TimeWarp.MaxPhysicsRate)) && (simDelay == 0 || simTimer.ElapsedMilliseconds > simDelay))
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                stages = simulator.RunSimulation(this.vessel.parts, FlightGlobals.getGeeForceAtPosition(this.vessel.rigidbody.position).magnitude);
                stopwatch.Stop();
                simDelay = 10 * stopwatch.ElapsedMilliseconds;
                simTimer.Reset();
                simTimer.Start();
            }

            int stage = stages.Length - 1;

            GUILayout.Label("VESSEL DISPLAY", heading);
            GUILayout.BeginHorizontal(GUI.skin.textArea);
            GUILayout.BeginVertical();
            settings.Set("*SPACER_VESSEL", "");
            settings.Set("*HEADING_VESSEL", "VESSEL DISPLAY");

            int stageCount = stages.Length;
            int stageCountUseful = 0;
            if (settings.Get<bool>("Vessel: Show All DeltaV Stages", true))
            {
                for (int i = stageCount - 1; i >= 0; i--)
                {
                    stageDeltaV = stages[i].deltaV;
                    if (stageDeltaV > 0)
                    {
                        if (settings.Get<bool>("Vessel: DeltaV (Stage)", true)) GUILayout.Label("DeltaV (S" + i + ")", heading);
                        stageCountUseful++;
                    }
                }

                if (stageCount != numberOfStages || stageCountUseful != numberOfStagesUseful)
                {
                    numberOfStages = stageCount;
                    numberOfStagesUseful = stageCountUseful;
                    settings.Changed = true;
                }
            }
            else
            {
                if (settings.Get<bool>("Vessel: DeltaV (Stage)", true)) GUILayout.Label("DeltaV (Stage)", heading);
            }
            if (settings.Get<bool>("Vessel: DeltaV (Total)", true)) GUILayout.Label("DeltaV (Total)", heading);
            if (settings.Get<bool>("Vessel: Specific Impulse", true)) GUILayout.Label("Specific Impulse", heading);
            if (settings.Get<bool>("Vessel: Mass", true)) GUILayout.Label("Mass", heading);
            if (settings.Get<bool>("Vessel: Thrust (Maximum)", true)) GUILayout.Label("Thrust (Maximum)", heading);
            if (settings.Get<bool>("Vessel: Thrust (Throttle)", true)) GUILayout.Label("Thrust (Throttle)", heading);
            if (settings.Get<bool>("Vessel: Thrust to Weight (Throttle)", true)) GUILayout.Label("TWR (Throttle)", heading);
            if (settings.Get<bool>("Vessel: Thrust to Weight (Current)", true)) GUILayout.Label("TWR (Current)", heading);
            if (settings.Get<bool>("Vessel: Thrust to Weight (Surface)", true)) GUILayout.Label("TWR (Surface)", heading);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (settings.Get<bool>("Vessel: Show All DeltaV Stages"))
            {
                for (int i = stageCount - 1; i >= 0; i--)
                {
                    stageDeltaV = stages[i].deltaV;
                    if (stageDeltaV > 0)
                    {
                        if (settings.Get<bool>("Vessel: DeltaV (Stage)")) GUILayout.Label(Tools.FormatNumber(stages[i].deltaV, "m/s", 0) + " (" + Tools.FormatTime(stages[i].time) + ")", data);
                    }
                }
            }
            else
            {
                if (settings.Get<bool>("Vessel: DeltaV (Stage)")) GUILayout.Label(Tools.FormatNumber(stages[Staging.lastStage].deltaV, "m/s", 0) + " (" + Tools.FormatTime(stages[Staging.lastStage].time)  + ")", data);
            }
            if (settings.Get<bool>("Vessel: DeltaV (Total)")) GUILayout.Label(Tools.FormatNumber(stages[Staging.lastStage].totalDeltaV, "m/s", 0) + " (" + Tools.FormatTime(stages[Staging.lastStage].totalTime) + ")", data);
            if (settings.Get<bool>("Vessel: Specific Impulse")) GUILayout.Label(Tools.FormatNumber(stages[Staging.lastStage].isp, "s", 3), data);
            if (settings.Get<bool>("Vessel: Mass")) GUILayout.Label(Tools.FormatNumber(stages[Staging.lastStage].mass, 3) + " / " + Tools.FormatNumber(stages[Staging.lastStage].totalMass, "Mg", 3), data);
            if (settings.Get<bool>("Vessel: Thrust (Maximum)")) GUILayout.Label(Tools.FormatSI(stages[Staging.lastStage].thrust, Tools.SIUnitType.Force), data);
            if (settings.Get<bool>("Vessel: Thrust (Throttle)")) GUILayout.Label(Tools.FormatSI(stages[Staging.lastStage].actualThrust, Tools.SIUnitType.Force), data);
            if (settings.Get<bool>("Vessel: Thrust to Weight (Throttle)")) GUILayout.Label(Tools.FormatNumber(stages[Staging.lastStage].actualThrustToWeight, 3), data);
            if (settings.Get<bool>("Vessel: Thrust to Weight (Current)")) GUILayout.Label(Tools.FormatNumber(stages[Staging.lastStage].thrustToWeight, 3), data);
            if (settings.Get<bool>("Vessel: Thrust to Weight (Surface)", true)) GUILayout.Label(Tools.FormatNumber(stages[Staging.lastStage].thrust / (stages[Staging.lastStage].totalMass * (this.vessel.mainBody.gravParameter / Math.Pow(this.vessel.mainBody.Radius, 2))), 3), data);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawRendezvous()
        {
            rendezvous.Draw();
        }

        //public enum SIUnitType { Speed, Distance, Pressure, Density, Force, Mass };

        //public string For5matSI(double number, Tools.SIUnitType type)
        //{
        //    // Assign memory for storing the notations.
        //    string[] notation = { "" };

        //    // Select the Tools.SIUnitType used and populate the notation array.
        //    switch (type)
        //    {
        //        case Tools.SIUnitType.Distance:
        //            notation = new string[] { "mm", "m", "km", "Mm", "Gm", "Tm", "Pm", "Em", "Zm", "Ym" };
        //            number *= 1000;
        //            break;
        //        case Tools.SIUnitType.Speed:
        //            notation = new string[] { "mm/s", "m/s", "km/s", "Mm/s", "Gm/s", "Tm/s", "Pm/s", "Em/s", "Zm/s", "Ym/s" };
        //            number *= 1000;
        //            break;
        //        case Tools.SIUnitType.Pressure:
        //            notation = new string[] { "Pa", "kPa", "MPa", "GPa", "TPa", "PPa", "EPa", "ZPa", "YPa" };
        //            number *= 1000;
        //            break;
        //        case Tools.SIUnitType.Density:
        //            notation = new string[] { "mg/m³", "g/m³", "kg/m³", "Mg/m³", "Gg/m³", "Tg/m³", "Pg/m³", "Eg/m³", "Zg/m³", "Yg/m³" };
        //            number *= 1000000;
        //            break;
        //        case Tools.SIUnitType.Force:
        //            notation = new string[] { "N", "kN", "MN", "GN", "TN", "PT", "EN", "ZN", "YN" };
        //            number *= 1000;
        //            break;
        //        case Tools.SIUnitType.Mass:
        //            notation = new string[] { "g", "kg", "Mg", "Gg", "Tg", "Pg", "Eg", "Zg", "Yg" };
        //            number *= 1000;
        //            break;
        //    }

        //    int notationIndex = 0;  // Index that is used to select the notation to display.

        //    // Loop through the notations until the smallest usable one is found.
        //    for (notationIndex = 0; notationIndex < notation.Length; notationIndex++)
        //    {
        //        if (number > 1000 || number < -1000) { number /= 1000; } else { break; }
        //    }

        //    // Return a spacing string if number is 0;
        //    if (number == 0)
        //    {
        //        return "-----";
        //    }

        //    // Return a string of the concatinated number and selected notation.
        //    return number.ToString("0.000") + notation[notationIndex];
        //}

        //public string Tools.FormatTime(double seconds)
        //{
        //    double s = seconds;
        //    int m = 0;
        //    int h = 0;

        //    while (s >= 60)
        //    {
        //        m++;
        //        s -= 60;
        //    }

        //    while (m >= 60)
        //    {
        //        h++;
        //        m -= 60;
        //    }

        //    if (h > 0)
        //    {
        //        return h + ":" + m.ToString("00") + ":" + s.ToString("00.0") + "s";
        //    }

        //    if (m > 0)
        //    {
        //        return m + ":" + s.ToString("00.0") + "s";
        //    }
        //    return s.ToString("0.0") + "s";
        //}

        private void CheckWindowMargin()
        {
            if (windowMargin > 0 && windowPosition.width > 0 && windowPosition.height > 0)
            {
                // Left
                if (windowPosition.x < windowMargin - windowPosition.width)
                {
                    windowPosition.x = windowMargin - windowPosition.width;
                }

                // Right
                if (windowPosition.x > Screen.width - windowMargin)
                {
                    windowPosition.x = Screen.width - windowMargin;
                }

                // Top
                if (windowPosition.y < windowMargin - windowPosition.height)
                {
                    windowPosition.y = windowMargin - windowPosition.height;
                }

                // Bottom
                if (windowPosition.y > Screen.height - windowMargin)
                {
                    windowPosition.y = Screen.height - windowMargin;
                }
            }
        }
    }
}
