#region

using Engineer.VesselSimulator;

using UnityEngine;

#endregion

namespace Engineer
{
    public class BuildEngineerTweakable : BuildEngineer
    {
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Sim ASP %"),
         UI_FloatRange(minValue = 0.0f, maxValue = 100.0f, stepIncrement = 1.0f, scene = UI_Scene.Editor)]
        public new float percentASP = 100.0f; // The percentage of sea-level pressure to use for "atmospheric stats"

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Thrust: "),
         UI_Toggle(disabledText = "Scalar", enabledText = "Vector", scene = UI_Scene.Editor)]
        public new bool vectoredThrust = false;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Sim Velocity"),
         UI_FloatRange(minValue = 0.0f, maxValue = 2500.0f, stepIncrement = 25.0f, scene = UI_Scene.Editor)]
        public new float velocity = 0.0f; // The velocity to use for "atmospheric stats"

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Dump Tree")] public void DumpTree()
        {
            print("BuildEngineer.DumpTree");
            SimManager.dumpTree = true;
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Log Sim")] public void LogSim()
        {
            print("BuildEngineer.LogSim");
            SimManager.logOutput = true;
        }

        protected override void Update()
        {
            base.percentASP = this.percentASP;
            base.vectoredThrust = this.vectoredThrust;
            base.velocity = this.velocity;
            base.Update();
        }
    }
}