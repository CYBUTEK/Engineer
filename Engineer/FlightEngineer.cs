// Kerbal Engineer Redux
// Author:  CYBUTEK
// License: Attribution-NonCommercial-ShareAlike 3.0 Unported

// Thanks to mic_e for impact calculation.

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Engineer.VesselSimulator;

namespace Engineer
{
    public class FlightEngineer : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Sim Timing"),
         UI_FloatRange(minValue = 0.0f, maxValue = 1000.0f, stepIncrement = 10.0f, scene = UI_Scene.Flight)]
        public float minFESimTime = 200.0f;      // The minimum time in ms from the start of one simulation to the start of the next

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Thrust: "),
            UI_Toggle(disabledText = "Scalar", enabledText = "Vector", scene = UI_Scene.Flight)]
        public bool vectoredThrust = false;

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Dump Tree")]
        public void DumpTree()
        {
            SimManager.dumpTree = true;
        }

        public static bool isVisible = true;

        public Settings settings = new Settings();
        Version version = new Version();
        bool showUpdate = true;
        string settingsFile = "flight_engineer.cfg";
        
        Rendezvous rendezvous = new Rendezvous();

        public Rect windowPosition = new Rect(UnityEngine.Screen.width - 275, 0, 0, 0);
        int windowID = Guid.NewGuid().GetHashCode();
        int windowMargin = 25;

        double maxGForce = 0f;

        public GUIStyle headingStyle, dataStyle, windowStyle, buttonStyle, areaStyle;
        bool hasInitStyles = false;

        bool surfaceOpen = false;
#if TERRAINTEST
        double maxDiff = 0;
        double heightMaxDiff = 0;
#endif
        Stage[] stages = null;
        String failMessage;
        double stageDeltaV = 0d;
        int numberOfStages = 0;
        int numberOfStagesUseful = 0;

        bool hasCheckedForFAR = false;
        bool hasInstalledFAR = false;

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
                if (vessel != null)
                {
                    foreach (Part thePart in vessel.parts)
                    {
                        if (thePart.Modules.Contains(ClassID))
                        {
                            if (thePart == part)
                            {
                                return true;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
                return false;
            }
        }

        public override void OnStart(StartState state)
        {
            try
            {
                print("FlightEngineer: OnStart (" + state + ")");
                if (state != StartState.Editor)
                {
                    rendezvous.FlightEngineer = this;
                    RenderingManager.AddToPostDrawQueue(0, DrawGUI);
                }
            }
            catch (Exception e)
            {
                print("Exception in FlightEng.OnStart: " + e);
            }
        }

        public override void OnSave(ConfigNode node)
        {
            try
            {
                if (IsPrimary)
                {
                    print("FlightEngineer: OnSave");
                    settings.Set("_WINDOW_POSITION", settings.ConvertToString(windowPosition));
                    settings.Save(settingsFile);
                    settings.Changed = true;
                }
            }
            catch (Exception e)
            {
                print("Exception in FlightEng.OnSave: " + e);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            try
            {
                if (IsPrimary)
                {
                    print("FlightEngineer: OnLoad");
                    settings.Load(settingsFile);
                    settings.Changed = true;
                    windowPosition = settings.ConvertToRect(settings.Get("_SAVEONCHANGE_NOCHANGEUPDATE_WINDOW_POSITION", settings.ConvertToString(windowPosition)));
                }
            }
            catch (Exception e)
            {
                print("Exception in FlightEng.OnLoad: " + e);
            }
        }

        public void Update()
        {
            if (vessel != null && vessel == FlightGlobals.ActiveVessel)
            {
                if (IsPrimary)
                {
                    // Update the simulation timing from the tweakable
                    SimManager.minSimTime = (long)minFESimTime;

                    // If the results are ready then read them and start the simulation again (will be delayed by minSimTime)
                    if (SimManager.ResultsReady())
                    {
                        // Get the results into our members
                        stages = SimManager.Stages;
                        failMessage = SimManager.failMessage;

                        SimManager.Gravity = vessel.mainBody.gravParameter / Math.Pow(vessel.mainBody.Radius + vessel.mainBody.GetAltitude(vessel.CoM), 2);
                        SimManager.Velocity = vessel.srfSpeed;
                        SimManager.vectoredThrust = vectoredThrust;
                        SimManager.TryStartSimulation();
                    }
                }
            }
        }

        private void DrawGUI()
        {
            if (vessel != null && vessel == FlightGlobals.ActiveVessel)
            {
                if (IsPrimary)
                {
                    Events["ShowWindow"].active = true;

                    if (settings.Get<bool>("_TOGGLE_FLIGHT_ENGINEER", true) && isVisible)
                    {
                        if (!hasInitStyles) InitStyles();

                        if (!settings.IsDrawing)
                        {
                            windowPosition = settings.ConvertToRect(settings.Get("_SAVEONCHANGE_NOCHANGEUPDATE_WINDOW_POSITION", settings.ConvertToString(windowPosition)));
                            if (settings.Changed)
                            {
                                windowPosition.height = 0;
                            }
                            windowPosition = GUILayout.Window(windowID, windowPosition, Window, "Flight Engineer  -  Version " + Version.VERSION + Version.SUFFIX, windowStyle);
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

        private void Window(int windowId)
        {
            GUILayout.BeginHorizontal();
            settings.Set("_SAVEONCHANGE_SHOW_ORBITAL", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_ORBITAL", false), "ORB", buttonStyle));
            settings.Set("_SAVEONCHANGE_SHOW_SURFACE", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_SURFACE", false), "SUR", buttonStyle));
            settings.Set("_SAVEONCHANGE_SHOW_VESSEL", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_VESSEL", false), "VES", buttonStyle));
            settings.Set("_SAVEONCHANGE_SHOW_RENDEZVOUS", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_RENDEZVOUS", false), "RDV", buttonStyle));
            GUILayout.EndHorizontal();

            if (settings.Get<bool>("_SAVEONCHANGE_SHOW_ORBITAL"))
            {
                DrawOrbital();
            }

            if (settings.Get<bool>("_SAVEONCHANGE_SHOW_SURFACE"))
            {
                if (!surfaceOpen)
                {
                    // Reset any appropriate values
#if TERRAINTEST
                    maxDiff = 0;
                    heightMaxDiff = 0;
#endif
                }
                surfaceOpen = true;
                DrawSurface();
            }
            else
            {
                surfaceOpen = false;
            }

            if (settings.Get<bool>("_SAVEONCHANGE_SHOW_VESSEL"))
            {
                DrawVessel();
            }

            if (settings.Get<bool>("_SAVEONCHANGE_SHOW_RENDEZVOUS"))
            {
                DrawRendezvous();
            }

            GUILayout.Label("Goto the Settings Configurator...", headingStyle);
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
            GUILayout.Label("ORBITAL DISPLAY", headingStyle);
            GUILayout.BeginHorizontal(areaStyle);
            GUILayout.BeginVertical();
            settings.Set("*SPACER_ORBITAL", "");
            settings.Set("*headingStyle_ORBITAL", "ORBITAL DISPLAY");
            if (settings.Get<bool>("Orbital: Show Grouped Ap/Pe Readouts", false))
            {
                if (settings.Get<bool>("Orbital: Apoapsis Height", true)) GUILayout.Label("Apoapsis Height", headingStyle);
                if (settings.Get<bool>("Orbital: Time to Apoapsis", true)) GUILayout.Label("Time to Apoapsis", headingStyle);
                if (settings.Get<bool>("Orbital: Periapsis Height", true)) GUILayout.Label("Periapsis Height", headingStyle);
                if (settings.Get<bool>("Orbital: Time to Periapsis", true)) GUILayout.Label("Time to Periapsis", headingStyle);
            }
            else
            {
                if (settings.Get<bool>("Orbital: Apoapsis Height", true)) GUILayout.Label("Apoapsis Height", headingStyle);
                if (settings.Get<bool>("Orbital: Periapsis Height", true)) GUILayout.Label("Periapsis Height", headingStyle);
                if (settings.Get<bool>("Orbital: Time to Apoapsis", true)) GUILayout.Label("Time to Apoapsis", headingStyle);
                if (settings.Get<bool>("Orbital: Time to Periapsis", true)) GUILayout.Label("Time to Periapsis", headingStyle);
            }
            if (settings.Get<bool>("Orbital: Inclination", true)) GUILayout.Label("Inclination", headingStyle);
            if (settings.Get<bool>("Orbital: Eccentricity", true)) GUILayout.Label("Eccentricity", headingStyle);
            if (settings.Get<bool>("Orbital: Period", true)) GUILayout.Label("Orbital Period", headingStyle);
            if (settings.Get<bool>("Orbital: Longitude of AN", true)) GUILayout.Label("Longitude of AN", headingStyle);
            if (settings.Get<bool>("Orbital: Longitude of Pe", true)) GUILayout.Label("Longitude of Pe", headingStyle);
            if (settings.Get<bool>("Orbital: Semi-major Axis", true)) GUILayout.Label("Semi-major Axis", headingStyle);
            if (settings.Get<bool>("Orbital: Semi-minor Axis", true)) GUILayout.Label("Semi-minor Axis", headingStyle);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (settings.Get<bool>("Orbital: Show Grouped Ap/Pe Readouts"))
            {
                if (settings.Get<bool>("Orbital: Apoapsis Height")) GUILayout.Label(Tools.FormatSI(vessel.orbit.ApA, Tools.SIUnitType.Distance), dataStyle);
                if (settings.Get<bool>("Orbital: Time to Apoapsis")) GUILayout.Label(Tools.FormatTime(vessel.orbit.timeToAp), dataStyle);
                if (settings.Get<bool>("Orbital: Periapsis Height")) GUILayout.Label(Tools.FormatSI(vessel.orbit.PeA, Tools.SIUnitType.Distance), dataStyle);
                if (settings.Get<bool>("Orbital: Time to Periapsis")) GUILayout.Label(Tools.FormatTime(vessel.orbit.timeToPe), dataStyle);
            }
            else
            {
                if (settings.Get<bool>("Orbital: Apoapsis Height")) GUILayout.Label(Tools.FormatSI(vessel.orbit.ApA, Tools.SIUnitType.Distance), dataStyle);
                if (settings.Get<bool>("Orbital: Periapsis Height")) GUILayout.Label(Tools.FormatSI(vessel.orbit.PeA, Tools.SIUnitType.Distance), dataStyle);
                if (settings.Get<bool>("Orbital: Time to Apoapsis")) GUILayout.Label(Tools.FormatTime(vessel.orbit.timeToAp), dataStyle);
                if (settings.Get<bool>("Orbital: Time to Periapsis")) GUILayout.Label(Tools.FormatTime(vessel.orbit.timeToPe), dataStyle);
            }
            if (settings.Get<bool>("Orbital: Inclination")) GUILayout.Label(Tools.FormatNumber(vessel.orbit.inclination, "°", 6), dataStyle);
            if (settings.Get<bool>("Orbital: Eccentricity")) GUILayout.Label(Tools.FormatNumber(vessel.orbit.eccentricity, "°", 6), dataStyle);
            if (settings.Get<bool>("Orbital: Period")) GUILayout.Label(Tools.FormatTime(vessel.orbit.period), dataStyle);
            if (settings.Get<bool>("Orbital: Longitude of AN")) GUILayout.Label(Tools.FormatNumber(vessel.orbit.LAN, "°", 6), dataStyle);
            if (settings.Get<bool>("Orbital: Longitude of Pe")) GUILayout.Label(Tools.FormatNumber(vessel.orbit.LAN + vessel.orbit.argumentOfPeriapsis, "°", 6), dataStyle);
            if (settings.Get<bool>("Orbital: Semi-major Axis")) GUILayout.Label(Tools.FormatSI(vessel.orbit.semiMajorAxis, Tools.SIUnitType.Distance), dataStyle);
            if (settings.Get<bool>("Orbital: Semi-minor Axis")) GUILayout.Label(Tools.FormatSI(vessel.orbit.semiMinorAxis, Tools.SIUnitType.Distance), dataStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        // Code by: mic_e
        private double normangle(double ang)
        {
            if (ang > 180)
            {
                ang -= 360 * Math.Ceiling((ang - 180) / 360);
            }
            if (ang <= -180)
            {
                ang -= 360 * Math.Floor((ang + 180) / 360);
            }

            return ang;
        }

        // Code by: mic_e
        private Vector3d radiusdirection(double theta)
        {
            theta = Math.PI * theta / 180;
            double omega = Math.PI * vessel.orbit.argumentOfPeriapsis / 180;
            double incl = Math.PI * vessel.orbit.inclination / 180;

            double costheta = Math.Cos(theta);
            double sintheta = Math.Sin(theta);
            double cosomega = Math.Cos(omega);
            double sinomega = Math.Sin(omega);
            double cosincl = Math.Cos(incl);
            double sinincl = Math.Sin(incl);

            Vector3d result;

            result.x = cosomega * costheta - sinomega * sintheta;
            result.y = cosincl * (sinomega * costheta + cosomega * sintheta);
            result.z = sinincl * (sinomega * costheta + cosomega * sintheta);

            return result;
        }

        // Code by: mic_e
        public static double ACosh(double x)
        {
            return (Math.Log(x + Math.Sqrt((x * x) - 1.0)));
        }

        // Code by: mic_e
        private double timetoperiapsis(double theta)
        {
            double e = vessel.orbit.eccentricity;
            double a = vessel.orbit.semiMajorAxis;
            double rp = vessel.orbit.PeR;
            double mu = vessel.mainBody.gravParameter;

            if (e == 1.0)
            {
                double D = Math.Tan(Math.PI * theta / 360.0);
                double M = D + D * D * D / 3.0;
                return (Math.Sqrt(2.0 * rp * rp * rp / mu) * M);
            }
            else if (a > 0)
            {
                double cosTheta = Math.Cos(Math.PI * theta / 180.0);
                double cosE = (e + cosTheta) / (1.0 + e * cosTheta);
                double radE = Math.Acos(cosE);
                double M = radE - e * Math.Sin(radE);
                return (Math.Sqrt(a * a * a / mu) * M);
            }
            else if (a < 0)
            {
                double cosTheta = Math.Cos(Math.PI * theta / 180.0);
                double coshF = (e + cosTheta) / (1.0 + e * cosTheta);
                double radF = ACosh(coshF);
                double M = e * Math.Sinh(radF) - radF;
                return (Math.Sqrt(-a * a * a / mu) * M);
            }

            return 0;
        }

        // Impact Code by: mic_e
        private void DrawSurface()
        {
            bool impacthappening = false;
            double impacttime = 0;
            double impactlong = 0;
            double impactlat = 0;
            double impactalt = 0;

            if (FlightGlobals.ActiveVessel.mainBody.pqsController != null)
            {
                //do impact site calculations
                impacthappening = true;
                impacttime = 0;
                impactlong = 0;
                impactlat = 0;
                impactalt = 0;
                double e = vessel.orbit.eccentricity;
                //get current position direction vector
                Vector3d currentpos = radiusdirection(vessel.orbit.trueAnomaly);
                //calculate longitude in inertial reference frame from that
                double currentirflong = 180 * Math.Atan2(currentpos.x, currentpos.y) / Math.PI;

                //experimentally determined; even for very flat trajectories, the errors go into the sub-millimeter area after 5 iterations or so
                const int impactiterations = 6;

                //do a few iterations of impact site calculations
                for (int i = 0; i < impactiterations; i++)
                {
                    if (vessel.orbit.PeA >= impactalt)
                    {
                        //periapsis must be lower than impact alt
                        impacthappening = false;
                    }
                    if ((vessel.orbit.eccentricity < 1) && (vessel.orbit.ApA <= impactalt))
                    {
                        //apoapsis must be higher than impact alt
                        impacthappening = false;
                    }
                    if ((vessel.orbit.eccentricity >= 1) && (vessel.orbit.timeToPe <= 0))
                    {
                        //if currently escaping, we still need to be before periapsis
                        impacthappening = false;
                    }
                    if (!impacthappening)
                    {
                        impacttime = 0;
                        impactlong = 0;
                        impactlat = 0;
                        impactalt = 0;
                        break;
                    }

                    double impacttheta = 0;
                    if (e > 0)
                    {
                        //in this step, we are using the calculated impact altitude of the last step, to refine the impact site position
                        impacttheta = -180 * Math.Acos((vessel.orbit.PeR * (1 + e) / (vessel.mainBody.Radius + impactalt) - 1) / e) / Math.PI;
                    }

                    //calculate time to impact
                    impacttime = vessel.orbit.timeToPe - timetoperiapsis(impacttheta);
                    //calculate position vector of impact site
                    Vector3d impactpos = radiusdirection(impacttheta);
                    //calculate longitude of impact site in inertial reference frame
                    double impactirflong = 180 * Math.Atan2(impactpos.x, impactpos.y) / Math.PI;
                    double deltairflong = impactirflong - currentirflong;
                    //get body rotation until impact
                    double bodyrot = 360 * impacttime / vessel.mainBody.rotationPeriod;
                    //get current longitude in body coordinates
                    double currentlong = vessel.longitude;
                    //finally, calculate the impact longitude in body coordinates
                    impactlong = normangle(currentlong - deltairflong - bodyrot);
                    //calculate impact latitude from impact position
                    impactlat = 180 * Math.Asin(impactpos.z / impactpos.magnitude) / Math.PI;
                    //calculate the actual altitude of the impact site
                    //altitude for long/lat code stolen from some ISA MapSat forum post; who knows why this works, but it seems to.
                    Vector3d rad = QuaternionD.AngleAxis(impactlong, Vector3d.down) * QuaternionD.AngleAxis(impactlat, Vector3d.forward) * Vector3d.right;
                    impactalt = vessel.mainBody.pqsController.GetSurfaceHeight(rad) - vessel.mainBody.pqsController.radius;
                    if ((impactalt < 0) && (vessel.mainBody.ocean == true))
                    {
                        impactalt = 0;
                    }
                }
            }

            if (vessel.geeForce > maxGForce) maxGForce = vessel.geeForce;

            if (!hasCheckedForFAR)
            {
                hasCheckedForFAR = true;

                foreach (AssemblyLoader.LoadedAssembly assembly in AssemblyLoader.loadedAssemblies)
                {
                    if (assembly.assembly.ToString().Split(',')[0] == "FerramAerospaceResearch")
                    {
                        hasInstalledFAR = true;
                        print("[KerbalEngineer]: FAR detected!  Turning off atmospheric details!");
                    }
                }
            }

            GUILayout.Label("SURFACE DISPLAY", headingStyle);
            GUILayout.BeginHorizontal(areaStyle);
            GUILayout.BeginVertical();
            settings.Set("*SPACER_SURFACE", "");
            settings.Set("*headingStyle_SURFACE", "SURFACE DISPLAY");
            if (settings.Get<bool>("Surface: Altitude (Sea Level)", true)) GUILayout.Label("Altitude (Sea Level)", headingStyle);
            if (settings.Get<bool>("Surface: Altitude (Terrain)", true)) GUILayout.Label("Altitude (Terrain)", headingStyle);
#if TERRAINTEST
            if (settings.Get<bool>("Surface: terrainAltitude", true)) GUILayout.Label("terrainAltitude", headingStyle);
            if (settings.Get<bool>("Surface: pqsAltitude", true)) GUILayout.Label("pqsAltitude", headingStyle);
            if (settings.Get<bool>("Surface: diff", true)) GUILayout.Label("diff", headingStyle);
            if (settings.Get<bool>("Surface: maxDiff", true)) GUILayout.Label("maxDiff", headingStyle);
            if (settings.Get<bool>("Surface: heightFromTerrain", true)) GUILayout.Label("heightFromTerrain", headingStyle);
            if (settings.Get<bool>("Surface: heightDiff", true)) GUILayout.Label("heightDiff", headingStyle);
            if (settings.Get<bool>("Surface: heightMaxDiff", true)) GUILayout.Label("heightMaxDiff", headingStyle);
#endif
            if (settings.Get<bool>("Surface: Vertical Speed", true)) GUILayout.Label("Vertical Speed", headingStyle);
            if (settings.Get<bool>("Surface: Horizontal Speed", true)) GUILayout.Label("Horizontal Speed", headingStyle);
            if (settings.Get<bool>("Surface: Longitude", true)) GUILayout.Label("Longitude", headingStyle);
            if (settings.Get<bool>("Surface: Latitude", true)) GUILayout.Label("Latitude", headingStyle);

            if (impacthappening)
            {
                if (settings.Get<bool>("Surface: Impact Time", true)) GUILayout.Label("Impact Time", headingStyle);
                if (settings.Get<bool>("Surface: Impact Longitude", true)) GUILayout.Label("Impact Longitude", headingStyle);
                if (settings.Get<bool>("Surface: Impact Latitude", true)) GUILayout.Label("Impact Latitude", headingStyle);
                if (settings.Get<bool>("Surface: Impact Altitude", true)) GUILayout.Label("Impact Altitude", headingStyle);
            }

            if (settings.Get<bool>("Surface: G-Force", true)) GUILayout.Label("G-Force", headingStyle);

            if (!hasInstalledFAR)
            {
                if (settings.Get<bool>("Surface: Terminal Velocity", true)) GUILayout.Label("Terminal Velocity", headingStyle);
                if (settings.Get<bool>("Surface: Atmospheric Efficiency", true)) GUILayout.Label("Atmospheric Efficiency", headingStyle);
                if (settings.Get<bool>("Surface: Atmospheric Drag", true)) GUILayout.Label("Atmospheric Drag", headingStyle);
                if (settings.Get<bool>("Surface: Atmospheric Pressure", true)) GUILayout.Label("Atmospheric Pressure", headingStyle);
                if (settings.Get<bool>("Surface: Atmospheric Density", true)) GUILayout.Label("Atmospheric Density", headingStyle);
            }
            GUILayout.EndVertical();

            double altSL = vessel.mainBody.GetAltitude(vessel.CoM);
            double altT = altSL - vessel.terrainAltitude;

            GUILayout.BeginVertical();
            if (settings.Get<bool>("Surface: Altitude (Sea Level)")) GUILayout.Label(Tools.FormatSI(altSL, Tools.SIUnitType.Distance), dataStyle);
            if (settings.Get<bool>("Surface: Altitude (Terrain)")) GUILayout.Label(Tools.FormatSI(altT, Tools.SIUnitType.Distance), dataStyle);
#if TERRAINTEST
            double terrainAltitude = vessel.terrainAltitude;
            double pqsAltitude = vessel.pqsAltitude;
            //double heightFromSurface = vessel.heightFromSurface;
            double heightFromTerrain = vessel.heightFromTerrain;
            double diff = terrainAltitude - pqsAltitude;
            if (Math.Abs(diff) > Math.Abs(maxDiff))
                maxDiff = diff;
            double heightDiff = 0;
            if (heightFromTerrain != -1)
            {
                heightDiff = altT - heightFromTerrain;
                if (Math.Abs(heightDiff) > Math.Abs(heightMaxDiff))
                    maxDiff = diff;
            }
            if (settings.Get<bool>("Surface: terrainAltitude")) GUILayout.Label(Tools.FormatSI(terrainAltitude, Tools.SIUnitType.Distance), dataStyle);
            if (settings.Get<bool>("Surface: pqsAltitude")) GUILayout.Label(Tools.FormatSI(pqsAltitude, Tools.SIUnitType.Distance), dataStyle);
            if (settings.Get<bool>("Surface: diff")) GUILayout.Label(Tools.FormatSI(diff, Tools.SIUnitType.Distance), dataStyle);
            if (settings.Get<bool>("Surface: maxDiff")) GUILayout.Label(Tools.FormatSI(maxDiff, Tools.SIUnitType.Distance), dataStyle);
            if (settings.Get<bool>("Surface: heightFromTerrain")) GUILayout.Label(Tools.FormatSI(heightFromTerrain, Tools.SIUnitType.Distance), dataStyle);
            if (settings.Get<bool>("Surface: heightDiff")) GUILayout.Label(Tools.FormatSI(heightDiff, Tools.SIUnitType.Distance), dataStyle);
            if (settings.Get<bool>("Surface: heightMaxDiff")) GUILayout.Label(Tools.FormatSI(heightMaxDiff, Tools.SIUnitType.Distance), dataStyle);
#endif
            if (settings.Get<bool>("Surface: Vertical Speed")) GUILayout.Label(Tools.FormatSI(vessel.verticalSpeed, Tools.SIUnitType.Speed), dataStyle);
            if (settings.Get<bool>("Surface: Horizontal Speed")) GUILayout.Label(Tools.FormatSI(vessel.horizontalSrfSpeed, Tools.SIUnitType.Speed), dataStyle);
            if (settings.Get<bool>("Surface: Longitude")) GUILayout.Label(Tools.FormatNumber(vessel.longitude, "°", 6), dataStyle);
            if (settings.Get<bool>("Surface: Latitude")) GUILayout.Label(Tools.FormatNumber(vessel.latitude, "°", 6), dataStyle);

            if (impacthappening)
            {
                if (settings.Get<bool>("Surface: Impact Time", true)) GUILayout.Label(Tools.FormatTime(impacttime), dataStyle);
                if (settings.Get<bool>("Surface: Impact Longitude", true)) GUILayout.Label(Tools.FormatNumber(impactlong, "°", 6), dataStyle);
                if (settings.Get<bool>("Surface: Impact Latitude", true)) GUILayout.Label(Tools.FormatNumber(impactlat, "°", 6), dataStyle);
                if (settings.Get<bool>("Surface: Impact Altitude", true)) GUILayout.Label(Tools.FormatSI(impactalt, Tools.SIUnitType.Distance), dataStyle);
            }

            if (settings.Get<bool>("Surface: G-Force")) GUILayout.Label(Tools.FormatNumber(vessel.geeForce, 3) + " / " + Tools.FormatNumber(maxGForce, "g", 3), dataStyle);

            if (!hasInstalledFAR)
            {
                double totalMass = 0d;
                double massDrag = 0d;
                foreach (Part thePart in vessel.parts)
                {
                    if (thePart.physicalSignificance != Part.PhysicalSignificance.NONE)
                    {
                        double partMass = thePart.mass + thePart.GetResourceMass();
                        totalMass += partMass;
                        massDrag += partMass * thePart.maximum_drag;
                    }
                }

                double gravity = FlightGlobals.getGeeForceAtPosition(vessel.CoM).magnitude;
                double atmosphere = vessel.atmDensity;

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

                if (settings.Get<bool>("Surface: Terminal Velocity")) GUILayout.Label(Tools.FormatSI(terminalVelocity, Tools.SIUnitType.Speed), dataStyle);
                if (settings.Get<bool>("Surface: Atmospheric Efficiency")) GUILayout.Label(Tools.FormatNumber(atmosphericEfficiency * 100, "%", 2), dataStyle);
                if (settings.Get<bool>("Surface: Atmospheric Drag")) GUILayout.Label(Tools.FormatSI(dragForce, Tools.SIUnitType.Force), dataStyle);
                if (settings.Get<bool>("Surface: Atmospheric Pressure")) GUILayout.Label(Tools.FormatSI(part.dynamicPressureAtm * 100, Tools.SIUnitType.Pressure), dataStyle);
                if (settings.Get<bool>("Surface: Atmospheric Density")) GUILayout.Label(Tools.FormatSI(vessel.atmDensity, Tools.SIUnitType.Density), dataStyle);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private double GetDrag()
        {
            double drag = 0d;

            foreach (Part part in vessel.parts)
            {
                drag += part.maximum_drag;
            }

            return drag;
        }

        private void DrawVessel()
        {
            if ((TimeWarp.WarpMode == TimeWarp.Modes.LOW) || (TimeWarp.CurrentRate <= TimeWarp.MaxPhysicsRate))
            {
                SimManager.RequestSimulation();
            }

            GUILayout.Label("VESSEL DISPLAY", headingStyle);
            GUILayout.BeginHorizontal(areaStyle);
            GUILayout.BeginVertical();
            settings.Set("*SPACER_VESSEL", "");
            settings.Set("*headingStyle_VESSEL", "VESSEL DISPLAY");

            if (stages == null)
            {
                GUILayout.Label("Simulation failed:", headingStyle);
                GUILayout.Label(failMessage == "" ? "No fail message" : failMessage, dataStyle);
            }
            else
            {
                int stageCount = stages.Length;
                int stageCountUseful = 0;
                int currentStage = FlightGlobals.ActiveVessel.currentStage;
                if (settings.Get<bool>("Vessel: Show All DeltaV Stages", true))
                {
                    for (int i = stageCount - 1; i >= 0; i--)
                    {
                        stageDeltaV = stages[i].deltaV;
                        if (stageDeltaV > 0)
                        {
                            if (settings.Get<bool>("Vessel: DeltaV (Stage)", true))
                            {
                                if (stages[i].number == -1)
                                    GUILayout.Label("DeltaV (active)", headingStyle);
                                else
                                    GUILayout.Label("DeltaV (S" + i + ")", headingStyle);
                            }
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
                    if (settings.Get<bool>("Vessel: DeltaV (Stage)", true)) GUILayout.Label("DeltaV (Stage)", headingStyle);
                }
                if (settings.Get<bool>("Vessel: DeltaV (Total)", true)) GUILayout.Label("DeltaV (Total)", headingStyle);
                if (settings.Get<bool>("Vessel: Specific Impulse", true)) GUILayout.Label("Specific Impulse", headingStyle);
                if (settings.Get<bool>("Vessel: Mass", true)) GUILayout.Label("Mass", headingStyle);
                if (settings.Get<bool>("Vessel: Thrust (Maximum)", true)) GUILayout.Label("Thrust (Maximum)", headingStyle);
                if (settings.Get<bool>("Vessel: Thrust (Throttle)", true)) GUILayout.Label("Thrust (Throttle)", headingStyle);
                if (settings.Get<bool>("Vessel: Thrust to Weight (Throttle)", true)) GUILayout.Label("TWR (Throttle)", headingStyle);
                if (settings.Get<bool>("Vessel: Thrust to Weight (Current)", true)) GUILayout.Label("TWR (Current)", headingStyle);
                if (settings.Get<bool>("Vessel: Thrust to Weight (Surface)", true)) GUILayout.Label("TWR (Surface)", headingStyle);
                GUILayout.EndVertical();

                GUILayout.BeginVertical();
                if (settings.Get<bool>("Vessel: Show All DeltaV Stages"))
                {
                    for (int i = stageCount - 1; i >= 0; i--)
                    {
                        stageDeltaV = stages[i].deltaV;
                        if (stageDeltaV > 0)
                        {
                            if (settings.Get<bool>("Vessel: DeltaV (Stage)")) GUILayout.Label(Tools.FormatNumber(stages[i].deltaV, "m/s", 0) + " (" + Tools.FormatTime(stages[i].time) + ")", dataStyle);
                        }
                    }
                }
                else
                {
                    if (settings.Get<bool>("Vessel: DeltaV (Stage)")) GUILayout.Label(Tools.FormatNumber(stages[Staging.lastStage].deltaV, "m/s", 0) + " (" + Tools.FormatTime(stages[Staging.lastStage].time) + ")", dataStyle);
                }
                if (settings.Get<bool>("Vessel: DeltaV (Total)")) GUILayout.Label(Tools.FormatNumber(stages[Staging.lastStage].totalDeltaV, "m/s", 0) + " (" + Tools.FormatTime(stages[Staging.lastStage].totalTime) + ")", dataStyle);
                if (settings.Get<bool>("Vessel: Specific Impulse")) GUILayout.Label(Tools.FormatNumber(stages[Staging.lastStage].isp, "s", 3), dataStyle);
                if (settings.Get<bool>("Vessel: Mass")) GUILayout.Label(EngineerTools.WeightFormatter(stages[Staging.lastStage].mass, stages[Staging.lastStage].totalMass), dataStyle);
                if (settings.Get<bool>("Vessel: Thrust (Maximum)")) GUILayout.Label(Tools.FormatSI(stages[Staging.lastStage].thrust, Tools.SIUnitType.Force), dataStyle);
                if (settings.Get<bool>("Vessel: Thrust (Throttle)")) GUILayout.Label(Tools.FormatSI(stages[Staging.lastStage].actualThrust, Tools.SIUnitType.Force), dataStyle);
                if (settings.Get<bool>("Vessel: Thrust to Weight (Throttle)")) GUILayout.Label(Tools.FormatNumber(stages[Staging.lastStage].actualThrustToWeight, 3), dataStyle);
                if (settings.Get<bool>("Vessel: Thrust to Weight (Current)")) GUILayout.Label(Tools.FormatNumber(stages[Staging.lastStage].thrustToWeight, 3), dataStyle);
                if (settings.Get<bool>("Vessel: Thrust to Weight (Surface)", true)) GUILayout.Label(Tools.FormatNumber(stages[Staging.lastStage].thrust / (stages[Staging.lastStage].totalMass * (vessel.mainBody.gravParameter / Math.Pow(vessel.mainBody.Radius, 2))), 3), dataStyle);
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawRendezvous()
        {
            rendezvous.Draw();
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

        private void InitStyles()
        {
            windowStyle = new GUIStyle(HighLogic.Skin.window);
            windowStyle.fixedWidth = 275;

            areaStyle = new GUIStyle(HighLogic.Skin.textArea);
            areaStyle.active = areaStyle.hover = areaStyle.normal;

            buttonStyle = new GUIStyle(HighLogic.Skin.button);

            headingStyle = new GUIStyle(HighLogic.Skin.label);
            headingStyle.normal.textColor = Color.white;
            headingStyle.fontStyle = FontStyle.Normal;
            headingStyle.alignment = TextAnchor.MiddleLeft;
            headingStyle.stretchWidth = true;

            dataStyle = new GUIStyle(HighLogic.Skin.label);
            dataStyle.fontStyle = FontStyle.Normal;
            dataStyle.alignment = TextAnchor.MiddleRight;
            dataStyle.stretchWidth = true;
        }
    }
}
