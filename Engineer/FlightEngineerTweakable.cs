#region

using Engineer.VesselSimulator;

using UnityEngine;

#endregion

namespace Engineer
{
    public class FlightEngineerTweakable : FlightEngineer
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Thrust: "),
         UI_Toggle(disabledText = "Scalar", enabledText = "Vector", scene = UI_Scene.Flight)]
        public new bool vectoredThrust = false;

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Dump Tree")] public void DumpTree()
        {
            print("FlightEngineer.DumpTree");
            SimManager.dumpTree = true;
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Log Sim")] public void LogSim()
        {
            print("FlightEngineer.LogSim");
            SimManager.logOutput = true;
        }

        public override void Update()
        {
            base.vectoredThrust = this.vectoredThrust;
            base.Update();
        }
    }
}