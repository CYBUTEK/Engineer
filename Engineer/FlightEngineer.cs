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

        public enum SIUnitType { Speed, Distance };
        Rendezvous rendezvous = new Rendezvous();

        public Rect windowPosition = new Rect(UnityEngine.Screen.width - 275, 0, 0, 0);
        int windowID = new System.Random().Next();
        int windowMargin = 25;

        double maxGForce = 0f;

        public GUIStyle winStyle, heading, data;

        Simulator simulator = new Simulator();
        Stage[] stages = new Stage[0];

        Stopwatch simTimer = new Stopwatch();
        double simDelay = 0d;

        [KSPEvent(guiActive = true, guiName = "Toggle Flight Engineer", active = false)]
        public void ShowWindow()
        {
            settings.Set<bool>("_TOGGLE_FLIGHT_ENGINEER", !settings.Get<bool>("_TOGGLE_FLIGHT_ENGINEER", true));
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
                            heading.fontStyle = FontStyle.Bold;
                            data = new GUIStyle(GUI.skin.label);
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
                if (settings.Get<bool>("Orbital: Apoapsis Height")) GUILayout.Label(FormatSI(this.vessel.orbit.ApA, SIUnitType.Distance), data);
                if (settings.Get<bool>("Orbital: Time to Apoapsis")) GUILayout.Label(this.vessel.orbit.timeToAp.ToString("0.000") + "s", data);
                if (settings.Get<bool>("Orbital: Periapsis Height")) GUILayout.Label(FormatSI(this.vessel.orbit.PeA, SIUnitType.Distance), data);
                if (settings.Get<bool>("Orbital: Time to Periapsis")) GUILayout.Label(this.vessel.orbit.timeToPe.ToString("0.000") + "s", data);
            }
            else
            {
                if (settings.Get<bool>("Orbital: Apoapsis Height")) GUILayout.Label(FormatSI(this.vessel.orbit.ApA, SIUnitType.Distance), data);
                if (settings.Get<bool>("Orbital: Periapsis Height")) GUILayout.Label(FormatSI(this.vessel.orbit.PeA, SIUnitType.Distance), data);
                if (settings.Get<bool>("Orbital: Time to Apoapsis")) GUILayout.Label(this.vessel.orbit.timeToAp.ToString("0.000") + "s", data);
                if (settings.Get<bool>("Orbital: Time to Periapsis")) GUILayout.Label(this.vessel.orbit.timeToPe.ToString("0.000") + "s", data);
            }
            if (settings.Get<bool>("Orbital: Inclination")) GUILayout.Label(this.vessel.orbit.inclination.ToString("0.000000") + "°", data);
            if (settings.Get<bool>("Orbital: Eccentricity")) GUILayout.Label(this.vessel.orbit.eccentricity.ToString("0.000000"), data);
            if (settings.Get<bool>("Orbital: Period")) GUILayout.Label(this.vessel.orbit.period.ToString("0.000") + "s", data);
            if (settings.Get<bool>("Orbital: Longitude of AN")) GUILayout.Label(this.vessel.orbit.LAN.ToString("0.000000") + "°", data);
            if (settings.Get<bool>("Orbital: Longitude of Pe")) GUILayout.Label((this.vessel.orbit.LAN + this.vessel.orbit.argumentOfPeriapsis).ToString("0.000000") + "°", data);
            if (settings.Get<bool>("Orbital: Semi-major Axis")) GUILayout.Label(FormatSI(this.vessel.orbit.semiMajorAxis, SIUnitType.Distance), data);
            if (settings.Get<bool>("Orbital: Semi-minor Axis")) GUILayout.Label(FormatSI(this.vessel.orbit.semiMinorAxis, SIUnitType.Distance), data);
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
            if (settings.Get<bool>("Surface: Atmospheric Pressure", true)) GUILayout.Label("Atmospheric Pressure", heading);
            if (settings.Get<bool>("Surface: Atmospheric Density", true)) GUILayout.Label("Atmospheric Density", heading);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (settings.Get<bool>("Surface: Altitude (Sea Level)")) GUILayout.Label(FormatSI(this.vessel.mainBody.GetAltitude(this.vessel.CoM), SIUnitType.Distance), data);
            if (settings.Get<bool>("Surface: Altitude (Terrain)")) GUILayout.Label(FormatSI(this.vessel.mainBody.GetAltitude(this.vessel.CoM) - this.vessel.terrainAltitude, SIUnitType.Distance), data);
            if (settings.Get<bool>("Surface: Vertical Speed")) GUILayout.Label(FormatSI(this.vessel.verticalSpeed, SIUnitType.Speed), data);
            if (settings.Get<bool>("Surface: Horizontal Speed")) GUILayout.Label(FormatSI(this.vessel.horizontalSrfSpeed, SIUnitType.Speed), data);
            if (settings.Get<bool>("Surface: Longitude")) GUILayout.Label(this.vessel.longitude.ToString("0.000000") + "°", data);
            if (settings.Get<bool>("Surface: Latitude")) GUILayout.Label(this.vessel.latitude.ToString("0.000000") + "°", data);
            if (settings.Get<bool>("Surface: G-Force")) GUILayout.Label(this.vessel.geeForce.ToString("0.000") + " / " + maxGForce.ToString("0.000"), data);
            if (settings.Get<bool>("Surface: Atmospheric Pressure")) GUILayout.Label((this.part.dynamicPressureAtm * 100).ToString("0.000") + "kPa", data);
            if (settings.Get<bool>("Surface: Atmospheric Density")) GUILayout.Label(this.vessel.atmDensity.ToString("0.000000"), data);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawVessel()
        {
            if (((TimeWarp.WarpMode == TimeWarp.Modes.LOW) || (TimeWarp.CurrentRate <= TimeWarp.MaxPhysicsRate)) && (simDelay == 0 || simTimer.ElapsedMilliseconds > simDelay))
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                stages = simulator.RunSimulation(this.vessel.parts, this.vessel.mainBody.gravParameter / Math.Pow(this.vessel.orbit.radius, 2));
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
            if (settings.Get<bool>("Vessel: DeltaV (Stage)", true)) GUILayout.Label("DeltaV (Stage)", heading);
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
            if (settings.Get<bool>("Vessel: DeltaV (Stage)")) GUILayout.Label(stages[Staging.lastStage].deltaV.ToString("0") + "m/s (" + stages[Staging.lastStage].time.ToString("0") + "s)", data);
            if (settings.Get<bool>("Vessel: DeltaV (Total)")) GUILayout.Label(stages[Staging.lastStage].totalDeltaV.ToString("0") + "m/s (" + stages[Staging.lastStage].totalTime.ToString("0") + "s)", data);
            if (settings.Get<bool>("Vessel: Specific Impulse")) GUILayout.Label(stages[Staging.lastStage].isp.ToString("0.000") + "s", data);
            if (settings.Get<bool>("Vessel: Mass")) GUILayout.Label(stages[Staging.lastStage].mass.ToString("0.000") + " / " + stages[Staging.lastStage].totalMass.ToString("0.000"), data);
            if (settings.Get<bool>("Vessel: Thrust (Maximum)")) GUILayout.Label(stages[Staging.lastStage].thrust.ToString("0.000") + "kN", data);
            if (settings.Get<bool>("Vessel: Thrust (Throttle)")) GUILayout.Label(stages[Staging.lastStage].actualThrust.ToString("0.000") + "kN", data);
            if (settings.Get<bool>("Vessel: Thrust to Weight (Throttle)")) GUILayout.Label(stages[Staging.lastStage].actualThrustToWeight.ToString("0.000"), data);
            if (settings.Get<bool>("Vessel: Thrust to Weight (Current)")) GUILayout.Label(stages[Staging.lastStage].thrustToWeight.ToString("0.000"), data);
            if (settings.Get<bool>("Vessel: Thrust to Weight (Surface)", true)) GUILayout.Label((stages[Staging.lastStage].thrust / (stages[Staging.lastStage].totalMass * (this.vessel.mainBody.gravParameter / Math.Pow(this.vessel.mainBody.Radius, 2)))).ToString("0.000"), data);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawRendezvous()
        {
            rendezvous.Draw();
        }

        public string FormatSI(double number, SIUnitType type)
        {
            // Assign memory for storing the notations.
            string[] notation = { "" };

            // Select the SIUnitType used and populate the notation array.
            switch (type)
            {
                case SIUnitType.Distance:
                    notation = new string[] { "m", "km", "Mm", "Gm", "Tm", "Pm", "Em", "Ym", "Zm" };
                    break;
                case SIUnitType.Speed:
                    notation = new string[] { "mm/s", "m/s", "km/s", "Mm/s", "Gm/s", "Tm/s", "Pm/s", "Em/s", "Ym/s", "Zm/s" };
                    number *= 1000;
                    break;
            }

            int notationIndex = 0;  // Index that is used to select the notation to display.

            // Loop through the notations until the smallest usable one is found.
            for (notationIndex = 0; notationIndex < notation.Length; notationIndex++)
            {
                if (number > 1000 || number < -1000) { number /= 1000; } else { break; }
            }

            // Return a spacing string if number is 0;
            if (number == 0)
            {
                return "-----";
            }

            // Return a string of the concatinated number and selected notation.
            return number.ToString("0.000") + notation[notationIndex];
        }

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
