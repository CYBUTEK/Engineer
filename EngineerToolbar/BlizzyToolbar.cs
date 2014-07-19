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

using Engineer;

using Toolbar;

using UnityEngine;

#endregion

namespace EngineerToolbar
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class BlizzyToolbar : MonoBehaviour
    {
        private const string DisabledTexturePath = "Engineer/BlizzyToolbarDisabled";
        private const string EnabledTexturePath = "Engineer/BlizzyToolbarEnabled";
        private readonly Engineer.Settings settings = new Engineer.Settings();
        private IButton button;

        private void Awake()
        {
            this.settings.Load("toolbar.cfg");
            if (!this.settings.Get("USE_BLIZZY_TOOLBAR", true))
            {
                Destroy(this);
            }
        }

        private void Start()
        {
            if (HighLogic.LoadedScene == GameScenes.EDITOR || HighLogic.LoadedScene == GameScenes.SPH || HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                this.button = ToolbarManager.Instance.add("KER", "engineerButton");
                this.button.ToolTip = "Kerbal Engineer Redux";

                if (HighLogic.LoadedScene == GameScenes.EDITOR || HighLogic.LoadedScene == GameScenes.SPH)
                {
                    this.SetButtonState(BuildEngineer.isVisible);
                    this.button.OnClick += e => this.TogglePluginVisibility(ref BuildEngineer.isVisible);
                }
                else if (HighLogic.LoadedScene == GameScenes.FLIGHT)
                {
                    this.SetButtonState(FlightEngineer.isVisible);
                    this.button.OnClick += e => this.TogglePluginVisibility(ref FlightEngineer.isVisible);
                }
            }
        }

        private void LateUpdate()
        {
            if (HighLogic.LoadedScene == GameScenes.EDITOR || HighLogic.LoadedScene == GameScenes.SPH)
            {
                this.SetButtonState(BuildEngineer.isVisible);
                this.button.Visible = BuildEngineer.hasEngineer;
                BuildEngineer.hasEngineerReset = true;
            }
            else if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                this.SetButtonState(FlightEngineer.isVisible);
                this.button.Visible = FlightEngineer.hasEngineer;
                FlightEngineer.hasEngineerReset = true;
            }
        }

        private void TogglePluginVisibility(ref bool toggle)
        {
            toggle = !toggle;
            this.SetButtonState(toggle);
        }

        private void SetButtonState(bool state)
        {
            this.button.TexturePath = state ? EnabledTexturePath : DisabledTexturePath;
        }

        private void OnDestroy()
        {
            if (this.button != null)
            {
                this.button.Destroy();
            }
        }
    }
}