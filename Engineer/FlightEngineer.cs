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
		private Type FARWingAerodynamicModelType = null;
		private System.Reflection.MethodInfo FARWingAerodynamicModelCdMethod;
		private System.Reflection.FieldInfo FARWingAerodynamicModelSField;
		private Type FARBasicDragModelType = null;
		private System.Reflection.FieldInfo FARBasicDragModelCdField;
		private System.Reflection.FieldInfo FARBasicDragModelSField;
		private Type FARAeroUtilType = null;
		private System.Reflection.MethodInfo FARGetCurrentDensityMethod;

        public static bool isVisible = true;
        public static bool isActive = false;

        public Settings settings = new Settings();
        Version version = new Version();
        bool showUpdate = true;
        string settingsFile = "flight_engineer.cfg";
        
        Rendezvous rendezvous = new Rendezvous();

        public Rect windowPosition = new Rect(UnityEngine.Screen.width - 275, 0, 0, 0);
        int windowID = Guid.NewGuid().GetHashCode();
        int windowMargin = 25;

        double maxGForce = 0f;
		double dragLosses = 0f;
		double maxQ = 0f;

        public GUIStyle headingStyle, dataStyle, windowStyle, buttonStyle, areaStyle;
        bool hasInitStyles = false;

        Stage[] stages = new Stage[0];
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
                if (this.vessel != null)
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
                    settings.Set("_WINDOW_POSITION", settings.ConvertToString(windowPosition));
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
                    windowPosition = settings.ConvertToRect(settings.Get("_SAVEONCHANGE_NOCHANGEUPDATE_WINDOW_POSITION", settings.ConvertToString(windowPosition)));
                    //print("FlightEngineer: OnLoad");
                }
            }
            catch { }
        }

        public void Update()
        {
            if (this.vessel != null && this.vessel == FlightGlobals.ActiveVessel)
            {
                if (IsPrimary)
                {
                    if (SimManager.Instance.Stages != null)
                    {
                        stages = SimManager.Instance.Stages;
                    }
                    SimManager.Instance.Gravity = this.vessel.mainBody.gravParameter / Math.Pow(this.vessel.mainBody.Radius + this.vessel.mainBody.GetAltitude(this.vessel.CoM), 2);
                    SimManager.Instance.TryStartSimulation();
                    isActive = true;
                }
            }
        }

        private void DrawGUI()
        {
            if (this.vessel != null && this.vessel == FlightGlobals.ActiveVessel)
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
                            windowPosition = GUILayout.Window(windowID, windowPosition, Window, "Flight Engineer  -  Version " + Version.VERSION, windowStyle);
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
                if (settings.Get<bool>("Orbital: Apoapsis Height")) GUILayout.Label(Tools.FormatSI(this.vessel.orbit.ApA, Tools.SIUnitType.Distance), dataStyle);
                if (settings.Get<bool>("Orbital: Time to Apoapsis")) GUILayout.Label(Tools.FormatTime(this.vessel.orbit.timeToAp), dataStyle);
                if (settings.Get<bool>("Orbital: Periapsis Height")) GUILayout.Label(Tools.FormatSI(this.vessel.orbit.PeA, Tools.SIUnitType.Distance), dataStyle);
                if (settings.Get<bool>("Orbital: Time to Periapsis")) GUILayout.Label(Tools.FormatTime(this.vessel.orbit.timeToPe), dataStyle);
            }
            else
            {
                if (settings.Get<bool>("Orbital: Apoapsis Height")) GUILayout.Label(Tools.FormatSI(this.vessel.orbit.ApA, Tools.SIUnitType.Distance), dataStyle);
                if (settings.Get<bool>("Orbital: Periapsis Height")) GUILayout.Label(Tools.FormatSI(this.vessel.orbit.PeA, Tools.SIUnitType.Distance), dataStyle);
                if (settings.Get<bool>("Orbital: Time to Apoapsis")) GUILayout.Label(Tools.FormatTime(this.vessel.orbit.timeToAp), dataStyle);
                if (settings.Get<bool>("Orbital: Time to Periapsis")) GUILayout.Label(Tools.FormatTime(this.vessel.orbit.timeToPe), dataStyle);
            }
            if (settings.Get<bool>("Orbital: Inclination")) GUILayout.Label(Tools.FormatNumber(this.vessel.orbit.inclination, "°", 6), dataStyle);
            if (settings.Get<bool>("Orbital: Eccentricity")) GUILayout.Label(Tools.FormatNumber(this.vessel.orbit.eccentricity, "", 6), dataStyle);
            if (settings.Get<bool>("Orbital: Period")) GUILayout.Label(Tools.FormatTime(this.vessel.orbit.period), dataStyle);
            if (settings.Get<bool>("Orbital: Longitude of AN")) GUILayout.Label(Tools.FormatNumber(this.vessel.orbit.LAN, "°", 6), dataStyle);
            if (settings.Get<bool>("Orbital: Longitude of Pe")) GUILayout.Label(Tools.FormatNumber(this.vessel.orbit.LAN + this.vessel.orbit.argumentOfPeriapsis, "°", 6), dataStyle);
            if (settings.Get<bool>("Orbital: Semi-major Axis")) GUILayout.Label(Tools.FormatSI(this.vessel.orbit.semiMajorAxis, Tools.SIUnitType.Distance), dataStyle);
            if (settings.Get<bool>("Orbital: Semi-minor Axis")) GUILayout.Label(Tools.FormatSI(this.vessel.orbit.semiMinorAxis, Tools.SIUnitType.Distance), dataStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        // Code by: mic_e
		//
		// normalizes an angle to the range [-180; 180]
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
		//
		// calculates the radius unit vector for a given true anomaly
        private Vector3d radiusdirection(double theta)
        {
            theta = Math.PI * theta / 180;
            double omega = Math.PI * this.vessel.orbit.argumentOfPeriapsis / 180;
            double incl = Math.PI * this.vessel.orbit.inclination / 180;

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
		//
		// calculates the time to periapsis for a given true anomaly 
        private double timetoperiapsis(double theta)
        {
            double e = this.vessel.orbit.eccentricity;
            double a = this.vessel.orbit.semiMajorAxis;
            double rp = this.vessel.orbit.PeR;
            double mu = this.vessel.mainBody.gravParameter;

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

		//Code by mic_e
		//
		//this method simply assumes a spherical body, and calculates the intersection point of the orbit with that body.
		//if an intersect is found, the actual altitude at that point is read and the method repeated with a sphere of that new
		//radius.
		private bool FindImpact (out double impacttime, out double impactlong, out double impactlat, out double impactalt)
		{
			impacttime = 0;
			impactlong = 0;
			impactlat = 0;

			if (this.vessel.mainBody.pqsController == null) {
				impactalt = 0;
			} else {
				impactalt = this.vessel.mainBody.pqsController.radiusMin - this.vessel.mainBody.Radius;
			}

			double e = this.vessel.orbit.eccentricity;
			//get current position direction vector
			Vector3d currentpos = radiusdirection (this.vessel.orbit.trueAnomaly);
			//calculate longitude in inertial reference frame from that
			double currentirflong = 180 * Math.Atan2 (currentpos.x, currentpos.y) / Math.PI;

			//experimentally determined; even for very flat trajectories, the errors go into the sub-millimeter area after 5 iterations or so
			const int impactiterations = 6;

			//do a few iterations of impact site calculations
			for (int i = 0; i < impactiterations; i++) {
				if (this.vessel.orbit.PeA >= impactalt) {
					//periapsis must be lower than impact alt
					return false;
				}
				if ((this.vessel.orbit.eccentricity < 1) && (this.vessel.orbit.ApA <= impactalt)) {
					//apoapsis must be higher than impact alt
					return false;
				}
				if ((this.vessel.orbit.eccentricity >= 1) && (this.vessel.orbit.timeToPe <= 0)) {
					//if currently escaping, we still need to be before periapsis
					return false;
				}

				double impacttheta = 0;
				if (e > 0) {
					//in this step, we are using the calculated impact altitude of the last step, to refine the impact site position
					impacttheta = -180 * Math.Acos ((this.vessel.orbit.PeR * (1 + e) / (this.vessel.mainBody.Radius + impactalt) - 1) / e) / Math.PI;
				}

				//calculate time to impact
				impacttime = this.vessel.orbit.timeToPe - timetoperiapsis (impacttheta);
				//calculate position vector of impact site
				Vector3d impactpos = radiusdirection (impacttheta);
				//calculate longitude of impact site in inertial reference frame
				double impactirflong = 180 * Math.Atan2 (impactpos.x, impactpos.y) / Math.PI;
				double deltairflong = impactirflong - currentirflong;
				//get body rotation until impact
				double bodyrot = 360 * impacttime / this.vessel.mainBody.rotationPeriod;
				//get current longitude in body coordinates
				double currentlong = this.vessel.longitude;
				//finally, calculate the impact longitude in body coordinates
				impactlong = normangle (currentlong - deltairflong - bodyrot);
				//calculate impact latitude from impact position
				impactlat = 180 * Math.Asin (impactpos.z / impactpos.magnitude) / Math.PI;
				//calculate the actual altitude of the impact site
				//altitude for long/lat code stolen from some ISA MapSat forum post; who knows why this works, but it seems to.
				if(this.vessel.mainBody.pqsController != null) {
					Vector3d rad = QuaternionD.AngleAxis (impactlong, Vector3d.down) * QuaternionD.AngleAxis (impactlat, Vector3d.forward) * Vector3d.right;
					impactalt = this.vessel.mainBody.pqsController.GetSurfaceHeight(rad) - this.vessel.mainBody.pqsController.radius;
					if ((impactalt < 0) && (this.vessel.mainBody.ocean == true)) {
						impactalt = 0;
					}
				} else {
					impactalt = 0;
				}
			}

			return true;
		}

		// Code by: mic_e
		private bool checkNotNull (object o, string errormessage)
		{
			if (o == null) {
				print ("[KerbalEngineer]: " + errormessage);
				return false;
			} else {
				return true;
			}
		}

		// Code by: mic_e
		private void CheckForFAR () {
			hasCheckedForFAR = true;

			foreach (AssemblyLoader.LoadedAssembly assembly in AssemblyLoader.loadedAssemblies) {
				if (assembly.assembly.ToString ().Split (',') [0] == "FerramAerospaceResearch") {

					print ("[KerbalEngineer]: FAR detected!");

					FARWingAerodynamicModelType = assembly.assembly.GetType ("ferram4.FARWingAerodynamicModel");
					FARBasicDragModelType = assembly.assembly.GetType ("ferram4.FARBasicDragModel");
					FARAeroUtilType = assembly.assembly.GetType("ferram4.FARAeroUtil");

					if (!checkNotNull(FARWingAerodynamicModelType, "Could not load FARWingAerodynamicModel type")) break;
					if (!checkNotNull(FARBasicDragModelType, "Could not load FARBasicDragModel type")) break;
					if (!checkNotNull(FARAeroUtilType, "Could not load FARAeroUtil type")) break;

					FARBasicDragModelCdField = FARBasicDragModelType.GetField ("Cd");
					FARBasicDragModelSField = FARBasicDragModelType.GetField ("S");
					FARWingAerodynamicModelCdMethod = FARWingAerodynamicModelType.GetMethod ("GetCd");
					FARWingAerodynamicModelSField = FARWingAerodynamicModelType.GetField ("S");
					FARGetCurrentDensityMethod = FARAeroUtilType.GetMethod("GetCurrentDensity", new[] {typeof(CelestialBody), typeof(float)});

					if (!checkNotNull(FARBasicDragModelCdField, "Could not load BasicDragModel.Cd field")) break;
					if (!checkNotNull(FARBasicDragModelSField, "Could not load BasicDragModel.S field")) break;
					if (!checkNotNull(FARWingAerodynamicModelCdMethod, "Could not load WingAerodynamicDragModel.Cd() method")) break;
					if (!checkNotNull(FARWingAerodynamicModelSField, "Could not load WingAerodynamicDragModel.S field")) break;
					if (!checkNotNull(FARGetCurrentDensityMethod, "Could no load AeroUtil.GeCurrentDensity(CelestialBody, float) method")) break;

					print ("[KerbalEngineer]: Turning on awesome FAR atmospheric details!");

					hasInstalledFAR = true;
					break;
				}
			}
		}

		// Impact, FAR and some atmospheric drag code by: mic_e
        private void DrawSurface ()
		{
			try {

				double impacttime;
				double impactlong;
				double impactlat;
				double impactalt;
				bool impacthappening = FindImpact (out impacttime, out impactlong, out impactlat, out impactalt);

				if (this.vessel.geeForce > maxGForce)
					maxGForce = this.vessel.geeForce;

				if (!hasCheckedForFAR) {
					CheckForFAR ();
				}

				GUILayout.Label ("SURFACE DISPLAY", headingStyle);
				GUILayout.BeginHorizontal (areaStyle);
				GUILayout.BeginVertical ();
				settings.Set ("*SPACER_SURFACE", "");
				settings.Set ("*headingStyle_SURFACE", "SURFACE DISPLAY");
				if (settings.Get<bool> ("Surface: Altitude (Sea Level)", true))
					GUILayout.Label ("Altitude (Sea Level)", headingStyle);
				if (settings.Get<bool> ("Surface: Altitude (Terrain)", true))
					GUILayout.Label ("Altitude (Terrain)", headingStyle);
				if (settings.Get<bool> ("Surface: Vertical Speed", true))
					GUILayout.Label ("Vertical Speed", headingStyle);
				if (settings.Get<bool> ("Surface: Horizontal Speed", true))
					GUILayout.Label ("Horizontal Speed", headingStyle);
				if (settings.Get<bool> ("Surface: Longitude", true))
					GUILayout.Label ("Longitude", headingStyle);
				if (settings.Get<bool> ("Surface: Latitude", true))
					GUILayout.Label ("Latitude", headingStyle);

				if (impacthappening) {
					if (settings.Get<bool> ("Surface: Impact Time", true))
						GUILayout.Label ("Impact Time", headingStyle);
					if (settings.Get<bool> ("Surface: Impact Longitude", true))
						GUILayout.Label ("Impact Longitude", headingStyle);
					if (settings.Get<bool> ("Surface: Impact Latitude", true))
						GUILayout.Label ("Impact Latitude", headingStyle);
					if (settings.Get<bool> ("Surface: Impact Altitude", true))
						GUILayout.Label ("Impact Altitude", headingStyle);
				}

				if (settings.Get<bool> ("Surface: G-Force", true))
					GUILayout.Label ("G-Force", headingStyle);

				if (settings.Get<bool> ("Surface: Terminal Velocity", true))
					GUILayout.Label ("Terminal Velocity", headingStyle);
				if (settings.Get<bool> ("Surface: Atmospheric Efficiency", true))
					GUILayout.Label ("Atmospheric Efficiency", headingStyle);
				if (settings.Get<bool> ("Surface: Static Pressure", true))
					GUILayout.Label ("Static Pressure", headingStyle);
				if (settings.Get<bool> ("Surface: Dynamic Pressure", true))
					GUILayout.Label ("Dynamic Pressure", headingStyle);
				if (settings.Get<bool> ("Surface: Max-Q", true))
					GUILayout.Label ("Max-Q", headingStyle);
				if (settings.Get<bool> ("Surface: Atmospheric Density", true))
					GUILayout.Label ("Atmospheric Density", headingStyle);
				if (settings.Get<bool> ("Surface: Drag Coefficient", true))
					GUILayout.Label ("Drag Coefficient", headingStyle);
				if (settings.Get<bool> ("Surface: Drag Force", true))
					GUILayout.Label ("Drag Force", headingStyle);
				if (settings.Get<bool> ("Surface: Drag Deceleration", true))
					GUILayout.Label ("Drag Deceleration", headingStyle);
				if (settings.Get<bool> ("Surface: Drag Losses", true))
					GUILayout.Label ("Drag Losses", headingStyle);

				GUILayout.EndVertical ();

				GUILayout.BeginVertical ();
				if (settings.Get<bool> ("Surface: Altitude (Sea Level)"))
					GUILayout.Label (Tools.FormatSI (this.vessel.mainBody.GetAltitude (this.vessel.CoM), Tools.SIUnitType.Distance), dataStyle);
				if (settings.Get<bool> ("Surface: Altitude (Terrain)"))
					GUILayout.Label (Tools.FormatSI (this.vessel.mainBody.GetAltitude (this.vessel.CoM) - this.vessel.terrainAltitude, Tools.SIUnitType.Distance), dataStyle);
				if (settings.Get<bool> ("Surface: Vertical Speed"))
					GUILayout.Label (Tools.FormatSI (this.vessel.verticalSpeed, Tools.SIUnitType.Speed), dataStyle);
				if (settings.Get<bool> ("Surface: Horizontal Speed"))
					GUILayout.Label (Tools.FormatSI (this.vessel.horizontalSrfSpeed, Tools.SIUnitType.Speed), dataStyle);
				if (settings.Get<bool> ("Surface: Longitude"))
					GUILayout.Label (Tools.FormatNumber (this.vessel.longitude, "°", 6), dataStyle);
				if (settings.Get<bool> ("Surface: Latitude"))
					GUILayout.Label (Tools.FormatNumber (normangle (this.vessel.latitude), "°", 6), dataStyle);

				if (impacthappening) {
					if (settings.Get<bool> ("Surface: Impact Time", true))
						GUILayout.Label (Tools.FormatTime (impacttime), dataStyle);
					if (settings.Get<bool> ("Surface: Impact Longitude", true))
						GUILayout.Label (Tools.FormatNumber (impactlong, "°", 6), dataStyle);
					if (settings.Get<bool> ("Surface: Impact Latitude", true))
						GUILayout.Label (Tools.FormatNumber (impactlat, "°", 6), dataStyle);
					if (settings.Get<bool> ("Surface: Impact Altitude", true))
						GUILayout.Label (Tools.FormatSI (impactalt, Tools.SIUnitType.Distance), dataStyle);
				}

				if (settings.Get<bool> ("Surface: G-Force"))
					GUILayout.Label (Tools.FormatNumber (this.vessel.geeForce, 3) + " / " + Tools.FormatNumber (maxGForce, "g", 3), dataStyle);

				//drag calculations (work for both FAR and Stock aerodynamics)

				//get true FAR air density value (if applies)
				float airDensity = (float) vessel.atmDensity;
				float airDensityAdjustmentFactor = 1f;
				if (hasInstalledFAR) {
					object airDensityObject = FARGetCurrentDensityMethod.Invoke(null, new object[] {this.vessel.mainBody, (object) ((float) this.vessel.altitude)});
					if (airDensityObject is float) {
						airDensity = (float) airDensityObject;
						if (airDensity > 0) {
							airDensityAdjustmentFactor = (float) vessel.atmDensity / airDensity;
						}
					}
				}
				float classicalDragAdjustmentFactor = FlightGlobals.DragMultiplier * airDensityAdjustmentFactor;

				//drag factor: the 'c_d * A' in F_D = q * c_d * A
				//(unit: newtons per pascal)
				double dragForceFactor = 0;
				//total vessel mass
				//(unit: kg)
				double totalMass = 0;

				foreach (Part p in vessel.parts) {
					if (p == null /*|| p.physicalSignificance == Part.PhysicalSignificance.NONE*/) {
						continue;
					}

					//mass
					double partMass = (p.mass + p.GetResourceMass()) * 1000;
					totalMass += partMass;

					//classical drag
					dragForceFactor += partMass * p.maximum_drag * classicalDragAdjustmentFactor;

					//FAR drag
					if (hasInstalledFAR) {
						foreach (PartModule m in p.Modules) {
							if (m == null) {
								continue;
							}

							//part drag coefficient
							object CdObj = null;
							//part area
							object SObj = null;
							if (FARWingAerodynamicModelType.IsAssignableFrom (m.GetType ())) {
								CdObj = FARWingAerodynamicModelCdMethod.Invoke (m, null);
								SObj = FARWingAerodynamicModelSField.GetValue (m);
							} else if (FARBasicDragModelType.IsAssignableFrom (m.GetType ())) {
								CdObj = FARBasicDragModelCdField.GetValue (m);
								SObj = FARBasicDragModelSField.GetValue (m);
							}
							if ((CdObj != null) && (SObj != null) && (CdObj is float) && (SObj is float)) {
								dragForceFactor += ((float)CdObj) * ((float)SObj);
								break;
							}
						}
					}
				}

				//drag deceleration per dynamic pressure
				//the 'c_d * A / m' in a_D = q * c_d * A / m
				//(unit: m/s² per pascal)
				double dragDecelFactor = 0;

				if (totalMass > 0.001) {
					dragDecelFactor = dragForceFactor / totalMass;
				}

				//dynamic pressure
				//(unit: pascal)
				double dynamicPressure = 0.5 * airDensity * FlightGlobals.ship_srfSpeed * FlightGlobals.ship_srfSpeed;

				if (dynamicPressure > maxQ) {
					maxQ = dynamicPressure;
				}

				//drag deceleration
				double dragDecel = dynamicPressure * dragDecelFactor;
				//drag force
				double dragForce = dynamicPressure * dragForceFactor;

				//local gravity
				//(unit: m/s²)ty))
				double gravity = FlightGlobals.getGeeForceAtPosition(this.vessel.CoM).magnitude;

				//terminal velocity (i.e. F_d = F_g)
				//(unit: m/s)
				double terminalVelocity = 0d;
				//calculate terminal velocity only if specific drag not close to 0, to prevent crashes
				if (airDensity > 0.000000001 && dragDecelFactor > 0.000001 && gravity > 0) {
					terminalVelocity = Math.Sqrt((2 * gravity) / (dragDecelFactor * airDensity));
				}

				//atospheric efficiency (v/v_term)
				double atmosphericEfficiency = 0d;
				if (terminalVelocity > 0.000001) {
					atmosphericEfficiency = FlightGlobals.ship_srfSpeed / terminalVelocity;
				}

				//integrate drag Deceleration for aerodynamic d-v losses
				if (!FlightDriver.Pause) {
					dragLosses += dragDecel * Time.fixedDeltaTime;
				}

				//calculate stock-equivalent drag coefficient
				double stockEquivalentDragCoefficient = 0;
				if (classicalDragAdjustmentFactor > 0.000001) {
					stockEquivalentDragCoefficient = dragDecelFactor / classicalDragAdjustmentFactor;
				}

				/*
				 * bug?: Pressure and Force SIUnitTypes accept kN/kPa as arguments, instead of SI N/Pa
				 */

				if (settings.Get<bool>("Surface: Terminal Velocity")) GUILayout.Label(Tools.FormatSI(terminalVelocity, Tools.SIUnitType.Speed), dataStyle);
	            if (settings.Get<bool>("Surface: Atmospheric Efficiency")) GUILayout.Label(Tools.FormatNumber(atmosphericEfficiency * 100, "%", 2), dataStyle);
	            if (settings.Get<bool>("Surface: Static Pressure")) GUILayout.Label(Tools.FormatSI(this.part.dynamicPressureAtm * 100, Tools.SIUnitType.Pressure), dataStyle);
				if (settings.Get<bool>("Surface: Dynamic Pressure")) GUILayout.Label(Tools.FormatSI(dynamicPressure / 1000, Tools.SIUnitType.Pressure), dataStyle);
				if (settings.Get<bool>("Surface: Max-Q")) GUILayout.Label(Tools.FormatSI(maxQ / 1000, Tools.SIUnitType.Pressure), dataStyle);
				if (settings.Get<bool>("Surface: Atmospheric Density")) GUILayout.Label(Tools.FormatSI(airDensity, Tools.SIUnitType.Density), dataStyle);
				if (settings.Get<bool>("Surface: Drag Coefficient")) GUILayout.Label(Tools.FormatNumber(stockEquivalentDragCoefficient, 5), dataStyle);
				if (settings.Get<bool>("Surface: Drag Force")) GUILayout.Label(Tools.FormatSI(dragForce / 1000, Tools.SIUnitType.Force), dataStyle);
				if (settings.Get<bool>("Surface: Drag Deceleration")) GUILayout.Label(Tools.FormatNumber(dragDecel / 9.81, "g", 3), dataStyle);
				if (settings.Get<bool>("Surface: Drag Losses")) GUILayout.Label(Tools.FormatSI(dragLosses, Tools.SIUnitType.Speed), dataStyle);

	            GUILayout.EndVertical();
	            GUILayout.EndHorizontal();

			} catch(Exception e) {
				print("Caught exception in DrawSurface. Info (Message, StackTrace):");
				print(e.Message);
				print(e.StackTrace);
			}
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
            if ((TimeWarp.WarpMode == TimeWarp.Modes.LOW) || (TimeWarp.CurrentRate <= TimeWarp.MaxPhysicsRate))
            {
                SimManager.Instance.RequestSimulation();
            }

            int stage = stages.Length - 1;

            GUILayout.Label("VESSEL DISPLAY", headingStyle);
            GUILayout.BeginHorizontal(areaStyle);
            GUILayout.BeginVertical();
            settings.Set("*SPACER_VESSEL", "");
            settings.Set("*headingStyle_VESSEL", "VESSEL DISPLAY");

            int stageCount = stages.Length;
            int stageCountUseful = 0;
            if (settings.Get<bool>("Vessel: Show All DeltaV Stages", true))
            {
                for (int i = stageCount - 1; i >= 0; i--)
                {
                    stageDeltaV = stages[i].deltaV;
                    if (stageDeltaV > 0)
                    {
                        if (settings.Get<bool>("Vessel: DeltaV (Stage)", true)) GUILayout.Label("DeltaV (S" + i + ")", headingStyle);
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
            if (settings.Get<bool>("Vessel: Thrust to Weight (Surface)", true)) GUILayout.Label(Tools.FormatNumber(stages[Staging.lastStage].thrust / (stages[Staging.lastStage].totalMass * (this.vessel.mainBody.gravParameter / Math.Pow(this.vessel.mainBody.Radius, 2))), 3), dataStyle);
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
