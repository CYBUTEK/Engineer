// Kerbal Engineer Redux
// Author:  CYBUTEK
// License: Attribution-NonCommercial-ShareAlike 3.0 Unported

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Engineer.VesselSimulator;
using Engineer.Extensions;

namespace Engineer
{
    public class BuildEngineer : PartModule
    {
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Sim Timing"),
         UI_FloatRange(minValue = 0.0f, maxValue = 1000.0f, stepIncrement = 10.0f, scene = UI_Scene.Editor)]
        public float minBESimTime = 200.0f;      // The minimum time in ms from the start of one simulation to the start of the next

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Sim ASP %"),
         UI_FloatRange(minValue = 0.0f, maxValue = 100.0f, stepIncrement = 1.0f, scene = UI_Scene.Editor)]
        public float percentASP = 100.0f;      // The percentage of sea-level pressure to use for "atmospheric stats"

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Sim Velocity"),
         UI_FloatRange(minValue = 0.0f, maxValue = 2500.0f, stepIncrement = 25.0f, scene = UI_Scene.Editor)]
        public float velocity = 0.0f;      // The velocity to use for "atmospheric stats"

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Thrust: "),
            UI_Toggle(disabledText = "Scalar", enabledText = "Vector", scene = UI_Scene.Editor)]
        public bool vectoredThrust = false;

        public static bool isVisible = true;

        Version version = new Version();
        Settings settings = new Settings();
        bool showUpdate = true;
        string settingsFile = "build_engineer.cfg";
        GUIStyle headingStyle, dataStyle, windowStyle, buttonStyle, areaStyle;
        bool hasInitStyles = false;
        Rect windowPosition = new Rect(300, 70, 0, 0);
        int windowID = Guid.NewGuid().GetHashCode();
        int windowMargin = 25;
        string windowTitle = "Kerbal Engineer Redux - Build Engineer Version " + Version.VERSION + Version.SUFFIX;
        string windowTitleCompact = "Kerbal Engineer Redux - Compact";
        bool isEditorLocked = false;
        CelestialBodies referenceBodies = new CelestialBodies();
        CelestialBodies.Body referenceBody;
        Stage[] stages = null;
        String failMessage = "";
        int stageCount;
        int stageCountAll;

        public bool IsPrimary
        {
            get
            {
                if (EditorLogic.fetch != null)
                {
                    foreach (Part part in EditorLogic.fetch.ship.Parts)
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
            try
            {
                print("BuildEngineer: OnStart (" + state + ")");
                if (state == StartState.Editor)
                {
                    this.part.OnEditorAttach += OnEditorAttach;
                    this.part.OnEditorDetach += OnEditorDetach;
                    this.part.OnEditorDestroy += OnEditorDestroy;

                    referenceBody = referenceBodies["Kerbin"];
                    OnEditorAttach();

                    if (this.part.Modules.Contains("FlightEngineer"))
                    {
                        windowTitle = "Kerbal Engineer Redux - Build Engineer (inc. Flight Engineer) Version " + Version.VERSION + Version.SUFFIX;
                    }
                    else
                    {
                        windowTitle = "Kerbal Engineer Redux - Build Engineer Version " + Version.VERSION + Version.SUFFIX;
                    }
                }
            }
            catch (Exception e)
            {
                print("Exception in BuildEng.OnStart: " + e);
            }
        }

        public override void OnSave(ConfigNode node)
        {
            try
            {
                if (IsPrimary)
                {
                    print("BuildEngineer: OnSave");
                    settings.Set("_WINDOW_POSITION", settings.ConvertToString(windowPosition));
                    settings.Save(settingsFile);
                }
            }
            catch (Exception e)
            {
                print("Exception in BuildEng.OnSave: " + e);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            try
            {
                if (IsPrimary)
                {
                    print("BuildEngineer: OnLoad");
                    settings.Load(settingsFile);
                    windowPosition = settings.ConvertToRect(settings.Get("_WINDOW_POSITION", settings.ConvertToString(windowPosition)));
                }
            }
            catch (Exception e)
            {
                print("Exception in BuildEng.OnLoad: " + e);
            }
        }

        private void OnEditorAttach()
        {
            if (IsPrimary && (this.part.parent != null || this.part.HasModule("ModuleCommand")))
            {
                print("BuildEngineer: OnEditorAttach");
                OnLoad(null);
                RenderingManager.AddToPostDrawQueue(0, DrawGUI);
            }
        }

        private void OnEditorDetach()
        {
            if (IsPrimary)
            {
                print("BuildEngineer: OnEditorDetach");
                OnSave(null);
                RenderingManager.RemoveFromPostDrawQueue(0, DrawGUI);
            }
        }

        private void OnEditorDestroy()
        {
            if (IsPrimary)
            {
                print("BuildEngineer: OnEditorDestroy");
                RenderingManager.RemoveFromPostDrawQueue(0, DrawGUI);
            }
        }

        private void Update()
        {
            if (IsPrimary)
            {
                // Update the simulation timing from the tweakable
                SimManager.minSimTime = (long)minBESimTime;

                // If the results are ready then read them and start the simulation again (will be delayed by minSimTime)
                if (SimManager.ResultsReady())
                {
                    stages = SimManager.Stages;
                    failMessage = SimManager.failMessage;

                    SimManager.Gravity = referenceBody.gravity;

                    if (settings.Get<bool>("_SAVEONCHANGE_USE_ATMOSPHERE"))
                    {
                        SimManager.Atmosphere = referenceBody.atmosphere * percentASP / 100.0;
                    }
                    else
                    {
                        SimManager.Atmosphere = 0d;
                    }
                    SimManager.Velocity = velocity;
                    SimManager.vectoredThrust = vectoredThrust;
                    SimManager.TryStartSimulation();
                }
            }
        }

        private void DrawGUI()
        {         
            if (!part.isAttached || !IsPrimary)
            {
                print("BuildEngineer: DrawGUI - Not Attached || Not Primary");
                RenderingManager.RemoveFromPostDrawQueue(0, DrawGUI);
                return;
            }

            if (!hasInitStyles)
                InitStyles();

            if (isVisible)
            {
                CheckEditorLock();

                if (!settings.IsDrawing)
                {
                    string title = "";

                    if (!settings.Get("_SAVEONCHANGE_COMPACT", false))
                    {
                        title = windowTitle;
                    }
                    else
                    {
                        title = windowTitleCompact;
                    }

                    windowPosition = GUILayout.Window(windowID, windowPosition, Window, title, windowStyle);
                }
                else
                {
                    settings.DrawWindow();
                }

                CheckWindowMargin();
            }
        }

        private void Window(int windowID)
        {
            if (settings.Get("_SAVEONCHANGE_COMPACT", false))
            {
                GUILayout.BeginHorizontal(GUILayout.Width(255));
            }
            else
            {
                GUILayout.BeginHorizontal(GUILayout.Width(740));
                settings.Set("_SAVEONCHANGE_SHOW_MAIN", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_MAIN", true), "Main Display", buttonStyle));
                settings.Set("_SAVEONCHANGE_SHOW_REFERENCES", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_REFERENCES", true), "Reference Bodies", buttonStyle));
                settings.Set("_SAVEONCHANGE_USE_ATMOSPHERE", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_USE_ATMOSPHERE", false), "Atmospheric Stats", buttonStyle));
                settings.Set("_SAVEONCHANGE_SHOW_ALL_STAGES", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_ALL_STAGES", false), "Show All Stages", buttonStyle));
            }
            settings.Set("_SAVEONCHANGE_COMPACT", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_COMPACT", false), "Compact", buttonStyle));
            GUILayout.EndHorizontal();

            SimManager.RequestSimulation();

            if (stages == null)
            {
                DrawFailMessage();
                return;
            }

            int stageArrayLength = stages.Length;
            int stageCountUseful = 0;
            for (int i = 0; i < stageArrayLength; i++)
            {
                if (stages[i].deltaV > 0)
                {
                    stageCountUseful++;
                }
            }

            if (stageCountUseful != stageCount || stageArrayLength != stageCountAll || settings.Changed)
            {
                stageCount = stageCountUseful;
                stageCountAll = stageArrayLength;
                windowPosition.width = 0;
                windowPosition.height = 0;
            }

            if (settings.Get<bool>("_SAVEONCHANGE_SHOW_MAIN"))
            {
                DrawStandard();
            }

            if (settings.Get<bool>("_SAVEONCHANGE_SHOW_REFERENCES") && !settings.Get<bool>("_SAVEONCHANGE_COMPACT"))
            {
                DrawRefBodies();
            }

            if (version.Newer && showUpdate)
            {

                if (!settings.Get<bool>("_SAVEONCHANGE_COMPACT"))
                {
                    GUILayout.Label("UPDATE AVAILABLE:  Your version is now obsolete, please update to " + version.Remote + " for the best gameplay experience!");
                }
                else
                {
                    GUILayout.Label("UPDATE AVAILABLE:  " + version.Remote + "!");
                }

                if ((Event.current.type == EventType.repaint) && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition) && Input.GetMouseButtonDown(0))
                {
                    showUpdate = false;
                    settings.Changed = true;
                }
            }

            GUI.DragWindow();
        }

        private void DrawFailMessage()
        {
            GUILayout.BeginHorizontal(areaStyle);
            GUILayout.BeginVertical();

            GUILayout.Label("Simulation failed:", headingStyle);
            GUILayout.Label(failMessage == "" ? "No fail message" : failMessage, dataStyle);

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawStandard()
        {
            GUILayout.BeginHorizontal(areaStyle);

            DrawStage(stages);
            if (!settings.Get<bool>("_SAVEONCHANGE_COMPACT"))
            {
                DrawCost(stages);
                DrawMass(stages);
                DrawIsp(stages);
                DrawThrust(stages);
                DrawDeltaV(stages);
                DrawTWR(stages);
                DrawTime(stages);
            }
            else
            {
                DrawDeltaV(stages);
                DrawTWR(stages);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawRefBodies()
        {
            GUILayout.BeginHorizontal(areaStyle);

            foreach (CelestialBodies.Body body in referenceBodies.bodies)
            {
                if (GUILayout.Toggle(referenceBody == body, body.name, buttonStyle))
                {
                    referenceBody = body;
                }
            }

            GUILayout.EndHorizontal();
        }

        private void DrawStage(Stage[] stages)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("");

            for (int i = 0; i < stages.Length; i++)
            {
                if (!settings.Get<bool>("_SAVEONCHANGE_SHOW_ALL_STAGES") && stages[i].deltaV == 0)
                {
                    continue;
                }
                GUILayout.Label("S " + i, headingStyle);
            }

            GUILayout.EndVertical();
        }

        private void DrawCost(Stage[] stages)
        {
            GUILayout.BeginVertical(GUILayout.Width(120));
            GUILayout.Label("COST", headingStyle);

            for (int i = 0; i < stages.Length; i++)
            {
                if (!settings.Get<bool>("_SAVEONCHANGE_SHOW_ALL_STAGES") && stages[i].deltaV == 0)
                {
                    continue;
                }
                GUILayout.Label(EngineerTools.SimpleFormatter(stages[i].cost, stages[i].totalCost), dataStyle);
            }

            GUILayout.EndVertical();
        }

        private void DrawMass(Stage[] stages)
        {
            GUILayout.BeginVertical(GUILayout.Width(150));
            GUILayout.Label("MASS", headingStyle);

            for (int i = 0; i < stages.Length; i++)
            {
                if (!settings.Get<bool>("_SAVEONCHANGE_SHOW_ALL_STAGES") && stages[i].deltaV == 0)
                {
                    continue;
                }
                GUILayout.Label(EngineerTools.WeightFormatter(stages[i].mass, stages[i].totalMass), dataStyle);
            }

            GUILayout.EndVertical();
        }

        private void DrawIsp(Stage[] stages)
        {
            GUILayout.BeginVertical(GUILayout.Width(50));
            GUILayout.Label("ISP", headingStyle);

            for (int i = 0; i < stages.Length; i++)
            {
                if (!settings.Get<bool>("_SAVEONCHANGE_SHOW_ALL_STAGES") && stages[i].deltaV == 0)
                {
                    continue;
                }
                GUILayout.Label(EngineerTools.SimpleFormatter(stages[i].isp, "s"), dataStyle);
            }

            GUILayout.EndVertical();
        }

        private void DrawThrust(Stage[] stages)
        {
            GUILayout.BeginVertical(GUILayout.Width(85));
            GUILayout.Label("THRUST", headingStyle);

            for (int i = 0; i < stages.Length; i++)
            {
                if (!settings.Get<bool>("_SAVEONCHANGE_SHOW_ALL_STAGES") && stages[i].deltaV == 0)
                {
                    continue;
                }
                GUILayout.Label(EngineerTools.ForceFormatter(stages[i].thrust), dataStyle);
            }

            GUILayout.EndVertical();
        }

        private void DrawDeltaV(Stage[] stages)
        {
            GUILayout.BeginVertical(GUILayout.Width(115));
            GUILayout.Label("DELTA-V", headingStyle);

            for (int i = 0; i < stages.Length; i++)
            {
                if (!settings.Get<bool>("_SAVEONCHANGE_SHOW_ALL_STAGES") && stages[i].deltaV == 0)
                {
                    continue;
                }
                GUILayout.Label(EngineerTools.SimpleFormatter(stages[i].deltaV, stages[i].inverseTotalDeltaV, "m/s"), dataStyle);
            }

            GUILayout.EndVertical();
        }

        private void DrawTWR(Stage[] stages)
        {
            GUILayout.BeginVertical(GUILayout.Width(90));
            GUILayout.Label("TWR (Max)", headingStyle);

            for (int i = 0; i < stages.Length; i++)
            {
                if (!settings.Get<bool>("_SAVEONCHANGE_SHOW_ALL_STAGES") && stages[i].deltaV == 0)
                {
                    continue;
                }

                GUILayout.Label(EngineerTools.SimpleFormatter(stages[i].thrustToWeight, "", 2) + " (" + EngineerTools.SimpleFormatter(stages[i].maxThrustToWeight, "", 2) + ")", dataStyle);
            }

            GUILayout.EndVertical();
        }

        private void DrawTime(Stage[] stages)
        {
            GUILayout.BeginVertical(GUILayout.Width(60));
            GUILayout.Label("TIME", headingStyle);

            for (int i = 0; i < stages.Length; i++)
            {
                if (!settings.Get<bool>("_SAVEONCHANGE_SHOW_ALL_STAGES") && stages[i].deltaV == 0)
                {
                    continue;
                }
                GUILayout.Label(Tools.FormatTime(stages[i].time), dataStyle);
            }

            GUILayout.EndVertical();
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

        private void CheckEditorLock()
        {
            Vector2 mousePos = Input.mousePosition;
            mousePos.y = Screen.height - mousePos.y;
            if (windowPosition.Contains(mousePos) && !isEditorLocked)
            {
                EditorLogic.fetch.Lock(true, true, true, windowID.ToString());
                print("Lock");
                isEditorLocked = true;
            }
            else if (!windowPosition.Contains(mousePos) && isEditorLocked)
            {
                EditorLogic.fetch.Unlock(windowID.ToString());
                print("Unlock");
                isEditorLocked = false;
            }
        }

        private void InitStyles()
        {
            windowStyle = new GUIStyle(HighLogic.Skin.window);

            buttonStyle = new GUIStyle(HighLogic.Skin.button);

            areaStyle = new GUIStyle(HighLogic.Skin.textArea);
            areaStyle.active = areaStyle.hover = areaStyle.normal;

            headingStyle = new GUIStyle(HighLogic.Skin.label);
            headingStyle.normal.textColor = Color.white;
            headingStyle.fontStyle = FontStyle.Normal;
            headingStyle.alignment = TextAnchor.MiddleCenter;
            headingStyle.stretchWidth = true;

            dataStyle = new GUIStyle(HighLogic.Skin.label);
            dataStyle.fontStyle = FontStyle.Normal;
            dataStyle.alignment = TextAnchor.MiddleCenter;
            dataStyle.stretchWidth = true;
        }
    }
}
