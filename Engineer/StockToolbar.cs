// 
//     Copyright (C) 2014 CYBUTEK
// 
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
// 
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
// 
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>.
// 

#region Using Directives

using System.IO;
using System.Reflection;

using UnityEngine;

#endregion

namespace Engineer
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class StockToolbar : MonoBehaviour
    {
        private static Texture2D texture;
        private readonly Settings settings = new Settings();
        private ApplicationLauncherButton buildButton;
        private ApplicationLauncherButton flightButton;

        private void Awake()
        {
            settings.Load("toolbar.cfg");
            if (!this.settings.Get("USE_STOCK_TOOLBAR", true))
            {
                Destroy(this);
                return;
            }
            if (texture == null)
            {
                texture = new Texture2D(36, 36, TextureFormat.RGBA32, false);
                texture.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "StockToolbar.png")));
            }
        }

        private void CreateButtons()
        {
            if (HighLogic.LoadedScene == GameScenes.EDITOR && this.buildButton == null)
            {
                this.buildButton = ApplicationLauncher.Instance.AddModApplication(
                    this.BuildOn,
                    this.BuildOff,
                    null,
                    null,
                    null,
                    null,
                    ApplicationLauncher.AppScenes.ALWAYS,
                    texture
                    );
            }

            if (HighLogic.LoadedScene == GameScenes.FLIGHT && this.flightButton == null)
            {
                this.flightButton = ApplicationLauncher.Instance.AddModApplication(
                    this.FlightOn,
                    this.FlightOff,
                    null,
                    null,
                    null,
                    null,
                    ApplicationLauncher.AppScenes.ALWAYS,
                    texture
                    );
            }
        }

        private void BuildOn()
        {
            BuildEngineer.isVisible = true;
        }

        private void BuildOff()
        {
            BuildEngineer.isVisible = false;
        }

        private void FlightOn()
        {
            FlightEngineer.isVisible = true;
        }

        private void FlightOff()
        {
            FlightEngineer.isVisible = false;
        }

        private void VoidMethod() { }

        private void LateUpdate()
        {
            if (HighLogic.LoadedScene == GameScenes.EDITOR)
            {
                if (this.buildButton != null)
                {
                    if (BuildEngineer.hasEngineer)
                    {
                        if (BuildEngineer.isVisible && this.buildButton.State != RUIToggleButton.ButtonState.TRUE)
                        {
                            this.buildButton.SetTrue();
                        }
                        else if (!BuildEngineer.isVisible && this.buildButton.State != RUIToggleButton.ButtonState.FALSE)
                        {
                            this.buildButton.SetFalse();
                        }
                    }
                    else
                    {
                        ApplicationLauncher.Instance.RemoveModApplication(this.buildButton);
                    }
                }
                else if (BuildEngineer.hasEngineer)
                {
                    this.CreateButtons();
                }

                BuildEngineer.hasEngineerReset = true;
            }
            else if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                if (this.flightButton != null)
                {
                    if (FlightEngineer.hasEngineer)
                    {
                        if (FlightEngineer.isVisible && this.flightButton.State != RUIToggleButton.ButtonState.TRUE)
                        {
                            this.flightButton.SetTrue();
                        }
                        else if (!FlightEngineer.isVisible && this.flightButton.State != RUIToggleButton.ButtonState.FALSE)
                        {
                            this.flightButton.SetFalse();
                        }
                    }
                    else
                    {
                        ApplicationLauncher.Instance.RemoveModApplication(this.flightButton);
                    }
                }
                else if (FlightEngineer.hasEngineer)
                {
                    this.CreateButtons();
                }

                FlightEngineer.hasEngineerReset = true;
            }
        }

        private void OnDestroy()
        {
            BuildEngineer.hasEngineer = false;
            FlightEngineer.hasEngineer = false;

            if (this.buildButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(this.buildButton);
            }

            if (this.flightButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(this.flightButton);
            }
            settings.Save("toolbar.cfg");
        }
    }
}