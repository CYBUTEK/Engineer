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
        Version version = new Version();
        Settings settings = new Settings();
        bool showUpdate = true;
        string settingsFile = "build_engineer.cfg";
        GUIStyle headingStyle, dataStyle, windowStyle, buttonStyle, areaStyle;
        bool hasInitStyles = false;
        Rect windowPosition = new Rect(300, 70, 0, 0);
        int windowID = new System.Random().Next();
        int windowMargin = 25;
        string windowTitle = "Kerbal Engineer Redux - Build Engineer Version " + Version.VERSION;
        string windowTitleCompact = "Kerbal Engineer Redux - Compact";
        bool isEditorLocked = false;
        CelestialBodies referenceBodies = new CelestialBodies();
        CelestialBodies.Body referenceBody;
        Stage[] stages;
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
            if (state == StartState.Editor)
            {
                this.part.OnEditorAttach += OnEditorAttach;
                this.part.OnEditorDetach += OnEditorDetach;
                this.part.OnEditorDestroy += OnEditorDestroy;

                referenceBody = referenceBodies["Kerbin"];
                OnEditorAttach();

                if (this.part.Modules.Contains("FlightEngineer"))
                {
                    windowTitle = "Kerbal Engineer Redux - Build Engineer (inc. Flight Engineer) Version " + Version.VERSION;
                }
                else
                {
                    windowTitle = "Kerbal Engineer Redux - Build Engineer Version " + Version.VERSION;
                }

                print("BuildEngineer: Start (" + state + ")");
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
                    print("BuildEngineer: OnSave");
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
                    windowPosition = settings.ConvertToRect(settings.Get("_WINDOW_POSITION", settings.ConvertToString(windowPosition)));
                    print("BuildEngineer: OnLoad");
                }
            }
            catch { }
        }

        private void OnEditorAttach()
        {
            if (IsPrimary && (this.part.parent != null || this.part.HasModule("ModuleCommand")))
            {
                OnLoad(null);
                RenderingManager.AddToPostDrawQueue(0, DrawGUI);
                print("BuildEngineer: OnEditorAttach");
            }
        }

        private void OnEditorDetach()
        {
            if (IsPrimary)
            {
                OnSave(null);
                RenderingManager.RemoveFromPostDrawQueue(0, DrawGUI);
                print("BuildEngineer: OnEditorDetach");
            }
        }

        private void OnEditorDestroy()
        {
            if (IsPrimary)
            {
                RenderingManager.RemoveFromPostDrawQueue(0, DrawGUI);
                print("BuildEngineer: OnEditorDestroy");
            }
        }

        private void Update()
        {
            if (IsPrimary)
            {
                if (SimManager.Instance.Stages != null)
                {
                    stages = SimManager.Instance.Stages;
                }
                SimManager.Instance.TryStartSimulation();
            }
        }

        private void DrawGUI()
        {
            if (!this.part.isAttached || !IsPrimary)
            {
                RenderingManager.RemoveFromPostDrawQueue(0, DrawGUI);
                print("BuildEngineer: OnUpdate - Not Attached || Not Primary");
                return;
            }

            if (!hasInitStyles) InitStyles();

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

        private void Window(int windowID)
        {
            if (!settings.Get("_SAVEONCHANGE_COMPACT", false))
            {
                GUILayout.BeginHorizontal(GUILayout.Width(700));
                settings.Set("_SAVEONCHANGE_SHOW_MAIN", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_MAIN", true), "Main Display", buttonStyle));
                settings.Set("_SAVEONCHANGE_SHOW_REFERENCES", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_REFERENCES", true), "Reference Bodies", buttonStyle));
                settings.Set("_SAVEONCHANGE_USE_ATMOSPHERE", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_USE_ATMOSPHERE", false), "Atmospheric Stats", buttonStyle));
                settings.Set("_SAVEONCHANGE_SHOW_ALL_STAGES", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_ALL_STAGES", false), "Show All Stages", buttonStyle));
            }
            else
            {
                GUILayout.BeginHorizontal(GUILayout.Width(215));
            }
            settings.Set("_SAVEONCHANGE_COMPACT", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_COMPACT", false), "Compact", buttonStyle));
            GUILayout.EndHorizontal();

            SimManager.Instance.Gravity = referenceBody.gravity;

            if (settings.Get<bool>("_SAVEONCHANGE_USE_ATMOSPHERE"))
            {
                SimManager.Instance.Atmosphere = referenceBody.atmosphere;
            }
            else
            {
                SimManager.Instance.Atmosphere = 0d;

            }

            SimManager.Instance.RequestSimulation();

            if (stages == null)
            {
                //print("BuildEngineer: Not drawing Window because stages == null");
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
                DrawThrust();
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

        private void DrawThrust()
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
            GUILayout.BeginVertical(GUILayout.Width(50));
            GUILayout.Label("TWR", headingStyle);

            for (int i = 0; i < stages.Length; i++)
            {
                if (!settings.Get<bool>("_SAVEONCHANGE_SHOW_ALL_STAGES") && stages[i].deltaV == 0)
                {
                    continue;
                }

                GUILayout.Label(EngineerTools.SimpleFormatter(stages[i].thrustToWeight, "", 2), dataStyle);
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
            if (windowPosition.Contains(mousePos) && !isEditorLocked && !EditorLogic.editorLocked)
            {
                EditorLogic.fetch.Lock(true, true, true);
                isEditorLocked = true;
            }
            else if (!windowPosition.Contains(mousePos) && isEditorLocked && EditorLogic.editorLocked)
            {
                EditorLogic.fetch.Unlock();
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
