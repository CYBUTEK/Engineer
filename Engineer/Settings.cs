// Kerbal Engineer Redux
// Author:  CYBUTEK
// License: Attribution-NonCommercial-ShareAlike 3.0 Unported

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP.IO;

namespace Engineer
{
    public class Settings
    {
        Dictionary<string, string> settings = new Dictionary<string, string>();
        Vessel vessel = null;
        string filename = "";
        bool changed = false;
        bool firstStart = true;

        public bool Changed
        {
            get
            {
                if (changed)
                {
                    changed = false;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                changed = value;
            }
        }

        public void Clear()
        {
            settings.Clear();
        }

        public void Load(string filename, Vessel vessel = null)
        {
            this.filename = filename;
            this.vessel = vessel;
            bool loaded = false;

            if (File.Exists<Settings>(filename))
            {
                //settings.Clear();

                string[] lines = File.ReadAllLines<Settings>(filename, vessel);

                for (int i = 0; i < lines.Length; i++)
                {
                    string[] line = lines[i].Split('=');
                    if (line.Length == 2)
                    {
                        string key = line[0].Trim();
                        string val = line[1].Trim();
                        if (settings.ContainsKey(key))
                            settings[key] = val;
                        else
                            settings.Add(key, val);
                        loaded = true;
                    }
                    else
                        MonoBehaviour.print("[KER] Ignoring invalid line in settings: '" + lines[i] + "'");
                }
            }

            firstStart = false;
            if (!loaded)
                MonoBehaviour.print("[KER] No valid settings in " + filename);
        }

        public void Save(string filename, Vessel vessel = null)
        {
            this.filename = filename;
            this.vessel = vessel;

            if (firstStart)
            {
                Load(filename, vessel);
            }

            if (settings.Count > 0)
            {
                TextWriter file = File.CreateText<Settings>(filename, vessel);

                foreach (KeyValuePair<string, string> setting in settings)
                {
                    file.WriteLine(setting.Key + " = " + setting.Value);
                }

                file.Close();
            }
        }

        public string Get(string key, string value = null)
        {
            if (settings.ContainsKey(key))
            {
                return settings[key];
            }
            else
            {
                Set(key, value);
                return value;
            }
        }

        public T Get<T>(string key)
        {
            if (settings.ContainsKey(key))
            {
                return (T)Convert.ChangeType(settings[key], typeof(T));
            }
            else
            {
                return (T)Convert.ChangeType(null, typeof(T));
            }
        }

        public T Get<T>(string key, T value)
        {
            if (settings.ContainsKey(key))
            {
                return (T)Convert.ChangeType(settings[key], typeof(T));
            }
            else
            {
                Set<T>(key, value);
                return (T)Convert.ChangeType(value, typeof(T));
            }
        }

        public void Set(string key, string value)
        {
            Set<string>(key, value);
        }

        public void Set<T>(string key, T value)
        {
            if (settings.ContainsKey(key))
            {
                if (settings[key] != (string)Convert.ChangeType(value, typeof(string)))
                {

                    if (key.Contains("_LOADONCHANGE_"))
                    {
                        Load(filename, vessel);
                    }

                    settings[key] = (string)Convert.ChangeType(value, typeof(string));
                    if (!key.Contains("_NOCHANGEUPDATE_"))
                    {
                        changed = true;
                    }

                    if (key.Contains("_SAVEONCHANGE_") && filename != "")
                    {
                        Save(filename, vessel);
                    }
                }
            }
            else
            {
                settings.Add(key, (string)Convert.ChangeType(value, typeof(string)));
                changed = true;
                if (filename != "")
                {
                    Save(filename, vessel);
                }
            }
        }

        public Rect ConvertToRect(string value)
        {
            string[] args = ConvertToArgs(value);
            return new Rect(Convert.ToSingle(args[0]), Convert.ToSingle(args[1]), Convert.ToSingle(args[2]), Convert.ToSingle(args[3]));
        }

        public string ConvertToString(Rect rectangle)
        {
            return ConvertFromArgs(new string[] { rectangle.x.ToString(), rectangle.y.ToString(), rectangle.width.ToString(), rectangle.height.ToString() });
        }

        public string[] ConvertToArgs(string value)
        {
            string[] args = value.Split(',');

            for (int i = 0; i < args.Length; i++)
            {
                args[i] = args[i].Trim();
            }

            return args;
        }

        public string ConvertFromArgs(string[] args)
        {
            string value = "";

            for (int i = 0; i < args.Length; i++)
            {
                value += args[i];
                if (i < args.Length - 1)
                {
                    value += ", ";
                }
            }

            return value;
        }

        public Rect windowPosition = new Rect((UnityEngine.Screen.width / 2) - 300, (UnityEngine.Screen.height / 2) - 250, 600, 500);
        int windowID = new System.Random().Next();
        Vector2 scrollPosition = Vector2.zero;
        GUIStyle heading, data;
        bool isDrawing = false;
        bool hasLoaded = false;

        public void DrawWindow()
        {
            if (!hasLoaded)
            {
                if (filename != "")
                {
                    Load(filename);
                }
                hasLoaded = true;
            }

            GUI.skin = HighLogic.Skin;
            heading = new GUIStyle(GUI.skin.label);
            heading.normal.textColor = Color.white;
            heading.fontStyle = FontStyle.Normal;
            data = new GUIStyle(GUI.skin.label);
            data.fontStyle = FontStyle.Normal;
            data.fixedWidth = 400;
            windowPosition = GUILayout.Window(windowID, windowPosition, Window, "Kerbal Engineer Redux - Settings Configurator");
        }

        private void Window(int windowId)
        {
            bool first = true;

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            foreach (KeyValuePair<string, string> setting in settings)
            {
                GUILayout.BeginHorizontal();
                if (!setting.Key.StartsWith("_") && !setting.Key.StartsWith("*"))
                {
                    GUILayout.BeginVertical();
                    GUILayout.Label(setting.Key, data);
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();
                    Set(setting.Key, GUILayout.Toggle(Get<bool>(setting.Key), "ENABLED", GUI.skin.button));
                    GUILayout.EndVertical();
                    first = false;
                }
                if (setting.Key.StartsWith("*HEADING"))
                {
                    GUILayout.Label(setting.Value.Trim(), heading);
                }
                if (setting.Key.StartsWith("*SPACER"))
                {
                    if (!first)
                    {
                        if (setting.Value.Trim() != "")
                        {
                            GUILayout.Space(Convert.ToSingle(setting.Value.Trim()));
                        }
                        else
                        {
                            GUILayout.Label("");
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            if (GUILayout.Button("CLOSE"))
            {
                changed = true;
                isDrawing = false;
                hasLoaded = false;
                if (filename != "")
                {
                    Save(filename, vessel);
                }
            }
        }

        public bool IsDrawing
        {
            get
            {
                return isDrawing;
            }
            set
            {
                isDrawing = value;
            }
        }
    }
}
