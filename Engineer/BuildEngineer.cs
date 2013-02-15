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
    public class BuildEngineer : PartModule
    {
        Version version = new Version();
        Settings settings = new Settings();
        bool showUpdate = true;
        string settingsFile = "build_engineer.cfg";
        GUIStyle heading, data;
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
        Stopwatch simTimer = new Stopwatch();
        double simDelay = 0d;

        public bool IsPrimary
        {
            get
            {
                foreach (Part part in EditorLogic.SortedShipList)
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
            if (state == StartState.Editor)
            {
                this.part.OnEditorAttach = OnEditorAttach;
                this.part.OnEditorDetach = OnEditorDetach;
                this.part.OnEditorDestroy = OnEditorDestroy;

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

                //print("BuildEngineer: Start (" + state + ")");
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
                    //print("BuildEngineer: OnSave");
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
                    //print("BuildEngineer: OnLoad");
                }
            }
            catch { }
        }

        private void OnEditorAttach()
        {
            if (IsPrimary && (this.part.parent != null || (this.part is CommandPod)))
            {
                OnLoad(null);
                RenderingManager.AddToPostDrawQueue(0, DrawGUI);
                //print("BuildEngineer: OnEditorAttach");
            }
        }

        private void OnEditorDetach()
        {
            if (IsPrimary)
            {
                OnSave(null);
                RenderingManager.RemoveFromPostDrawQueue(0, DrawGUI);
                //print("BuildEngineer: OnEditorDetach");
            }
        }

        private void OnEditorDestroy()
        {
            if (IsPrimary)
            {
                RenderingManager.RemoveFromPostDrawQueue(0, DrawGUI);
                //print("BuildEngineer: OnEditorDestroy");
            }
        }

        private void DrawGUI()
        {
            if (!this.part.isAttached || !IsPrimary)
            {
                RenderingManager.RemoveFromPostDrawQueue(0, DrawGUI);
                //print("BuildEngineer: OnUpdate - Not Attached || Not Primary");
                return;
            }

            CheckEditorLock();

            GUI.skin = HighLogic.Skin;
            heading = new GUIStyle(GUI.skin.label);
            heading.normal.textColor = Color.white;
            heading.fontStyle = FontStyle.Normal;

            data = new GUIStyle(GUI.skin.label);
            data.fontStyle = FontStyle.Normal;

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
                windowPosition = GUILayout.Window(windowID, windowPosition, Window, title);
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
                settings.Set("_SAVEONCHANGE_SHOW_MAIN", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_MAIN", true), "Main Display", GUI.skin.button));
                settings.Set("_SAVEONCHANGE_SHOW_REFERENCES", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_REFERENCES", true), "Reference Bodies", GUI.skin.button));
                settings.Set("_SAVEONCHANGE_USE_ATMOSPHERE", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_USE_ATMOSPHERE", false), "Atmospheric Stats", GUI.skin.button));
                settings.Set("_SAVEONCHANGE_SHOW_ALL_STAGES", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_SHOW_ALL_STAGES", false), "Show All Stages", GUI.skin.button));
            }
            else
            {
                GUILayout.BeginHorizontal(GUILayout.Width(215));
            }
            settings.Set("_SAVEONCHANGE_COMPACT", GUILayout.Toggle(settings.Get("_SAVEONCHANGE_COMPACT", false), "Compact", GUI.skin.button));
            GUILayout.EndHorizontal();

            if (((TimeWarp.WarpMode == TimeWarp.Modes.LOW) || (TimeWarp.CurrentRate <= TimeWarp.MaxPhysicsRate)) && (simDelay == 0 || simTimer.ElapsedMilliseconds > simDelay))
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                if (settings.Get<bool>("_SAVEONCHANGE_USE_ATMOSPHERE"))
                {
                    stages = new Simulator().RunSimulation(EditorLogic.SortedShipList, referenceBody.gravity, referenceBody.atmosphere);
                }
                else
                {
                    stages = new Simulator().RunSimulation(EditorLogic.SortedShipList, referenceBody.gravity, 0f);
                }
                stopwatch.Stop();
                simDelay = 10 * stopwatch.ElapsedMilliseconds;
                simTimer.Reset();
                simTimer.Start();
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
            GUILayout.BeginHorizontal(GUI.skin.textArea);

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
            GUILayout.BeginHorizontal(GUI.skin.textArea);

            foreach (CelestialBodies.Body body in referenceBodies.bodies)
            {
                if (GUILayout.Toggle(referenceBody == body, body.name, GUI.skin.button))
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
                GUILayout.Label("S " + i, heading);
            }

            GUILayout.EndVertical();
        }

        private void DrawCost(Stage[] stages)
        {
            GUILayout.BeginVertical(GUILayout.Width(120));
            GUILayout.Label("COST", heading);

            for (int i = 0; i < stages.Length; i++)
            {
                if (!settings.Get<bool>("_SAVEONCHANGE_SHOW_ALL_STAGES") && stages[i].deltaV == 0)
                {
                    continue;
                }
                GUILayout.Label(Tools.FormatNumber(stages[i].cost) + " / " + Tools.FormatNumber(stages[i].totalCost), data);
            }

            GUILayout.EndVertical();
        }

        private void DrawMass(Stage[] stages)
        {
            GUILayout.BeginVertical(GUILayout.Width(150));
            GUILayout.Label("MASS", heading);

            for (int i = 0; i < stages.Length; i++)
            {
                if (!settings.Get<bool>("_SAVEONCHANGE_SHOW_ALL_STAGES") && stages[i].deltaV == 0)
                {
                    continue;
                }
                GUILayout.Label(Tools.FormatNumber(stages[i].mass, 3) + " / " + Tools.FormatNumber(stages[i].totalMass, "Mg", 3), data);
            }

            GUILayout.EndVertical();
        }

        private void DrawIsp(Stage[] stages)
        {
            GUILayout.BeginVertical(GUILayout.Width(50));
            GUILayout.Label("ISP", heading);

            for (int i = 0; i < stages.Length; i++)
            {
                if (!settings.Get<bool>("_SAVEONCHANGE_SHOW_ALL_STAGES") && stages[i].deltaV == 0)
                {
                    continue;
                }
                GUILayout.Label(Tools.FormatNumber(stages[i].isp, "s", 0), data);
            }

            GUILayout.EndVertical();
        }

        private void DrawThrust(Stage[] stages)
        {
            GUILayout.BeginVertical(GUILayout.Width(85));
            GUILayout.Label("THRUST", heading);

            for (int i = 0; i < stages.Length; i++)
            {
                if (!settings.Get<bool>("_SAVEONCHANGE_SHOW_ALL_STAGES") && stages[i].deltaV == 0)
                {
                    continue;
                }
                GUILayout.Label(Tools.FormatSI(stages[i].thrust, Tools.SIUnitType.Force), data);
            }

            GUILayout.EndVertical();
        }

        private void DrawDeltaV(Stage[] stages)
        {
            GUILayout.BeginVertical(GUILayout.Width(115));
            GUILayout.Label("DELTA-V", heading);

            for (int i = 0; i < stages.Length; i++)
            {
                if (!settings.Get<bool>("_SAVEONCHANGE_SHOW_ALL_STAGES") && stages[i].deltaV == 0)
                {
                    continue;
                }
                GUILayout.Label(Tools.FormatNumber(stages[i].deltaV, 0) + " / " + Tools.FormatNumber(stages[i].inverseTotalDeltaV, "m/s", 0), data);
            }

            GUILayout.EndVertical();
        }

        private void DrawTWR(Stage[] stages)
        {
            GUILayout.BeginVertical(GUILayout.Width(50));
            GUILayout.Label("TWR", heading);

            for (int i = 0; i < stages.Length; i++)
            {
                if (!settings.Get<bool>("_SAVEONCHANGE_SHOW_ALL_STAGES") && stages[i].deltaV == 0)
                {
                    continue;
                }

                GUILayout.Label(Tools.FormatNumber(stages[i].thrustToWeight, 2), data);
            }

            GUILayout.EndVertical();
        }

        private void DrawTime(Stage[] stages)
        {
            GUILayout.BeginVertical(GUILayout.Width(60));
            GUILayout.Label("TIME", heading);

            for (int i = 0; i < stages.Length; i++)
            {
                if (!settings.Get<bool>("_SAVEONCHANGE_SHOW_ALL_STAGES") && stages[i].deltaV == 0)
                {
                    continue;
                }
                GUILayout.Label(Tools.FormatTime(stages[i].time), data);
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
    }
}
