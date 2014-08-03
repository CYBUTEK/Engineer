// Kerbal Engineer Redux
// Author:  CYBUTEK
// License: Attribution-NonCommercial-ShareAlike 3.0 Unported
//
// This class has taken a lot of inspiration from r4m0n's MuMech FuelFlowSimulator.  Although extremely
// similar to the code used within MechJeb, it is a clean re-write.  The similarities are a testiment
// to how well the MuMech code works and the robustness of the simulation algorithem used.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Engineer.Extensions;

namespace Engineer.VesselSimulator
{
    public class Simulation
    {
        private List<Part> partList;

        private List<PartSim> allParts;
        private List<PartSim> allFuelLines;
        private HashSet<PartSim> drainingParts;
        private List<EngineSim> allEngines;
        private List<EngineSim> activeEngines;
        private HashSet<int> drainingResources;
        private List<PartSim> dontStageParts;

        private int lastStage = 0;
        private int currentStage = 0;
        private bool doingCurrent = false;

        public String vesselName;
        public VesselType vesselType;

        private double stageTime = 0d;
        private Vector3 vecStageDeltaV;
        private double simpleTotalThrust = 0d;
        private double totalStageThrust = 0d;
        private double totalStageActualThrust = 0d;
        private Vector3 vecThrust;
        private Vector3 vecActualThrust;
        private double totalStageFlowRate = 0d;
        private double totalStageIspFlowRate = 0d;
        private double currentisp = 0d;
        private double stageStartMass = 0d;
        private double stepStartMass = 0d;
        private double stepEndMass = 0d;

        private double gravity = 0d;
        private double atmosphere = 0d;
        private double velocity = 0d;
        private Stopwatch _timer = new Stopwatch();
        private const double STD_GRAVITY = 9.81d;
        private const double SECONDS_PER_DAY = 86400d;
        
        public Simulation()
        {
            if (SimManager.logOutput)
                MonoBehaviour.print("Simulation created");
        }

        // This function prepares the simulation by creating all the necessary data structures it will 
        // need during the simulation.  All required data is copied from the core game data structures 
        // so that the simulation itself can be run in a background thread without having issues with 
        // the core game changing the data while the simulation is running.
        public bool PrepareSimulation(List<Part> parts, double theGravity, double theAtmosphere = 0, double theVelocity = 0, bool dumpTree = false, bool vectoredThrust = false)
        {
            LogMsg log = null;
            if (SimManager.logOutput)
            {
                log = new LogMsg();
                log.buf.AppendLine("PrepareSimulation started");
                dumpTree = true;
            }
            _timer.Start();

            // Store the parameters in members for ease of access in other functions
            partList = parts;
            gravity = theGravity;
            atmosphere = theAtmosphere;
            velocity = theVelocity;
            lastStage = Staging.lastStage;
            //MonoBehaviour.print("lastStage = " + lastStage);

            // Create the lists for our simulation parts
            allParts = new List<PartSim>();
            allFuelLines = new List<PartSim>();
            drainingParts = new HashSet<PartSim>();
            allEngines = new List<EngineSim>();
            activeEngines = new List<EngineSim>();
            drainingResources = new HashSet<int>();

            // A dictionary for fast lookup of Part->PartSim during the preparation phase
            Dictionary<Part, PartSim> partSimLookup = new Dictionary<Part, PartSim>();

            if (partList.Count > 0 && partList[0].vessel != null)
            {
                vesselName = partList[0].vessel.vesselName;
                vesselType = partList[0].vessel.vesselType;
            }

            // First we create a PartSim for each Part (giving each a unique id)
            int partId = 1;
            foreach (Part part in partList)
            {
                // If the part is already in the lookup dictionary then log it and skip to the next part
                if (partSimLookup.ContainsKey(part))
                {
                    if (log != null)
                        log.buf.AppendLine("Part " + part.name + " appears in vessel list more than once");
                    continue;
                }

                // Create the PartSim
                PartSim partSim = new PartSim(part, partId, atmosphere, log);

                // Add it to the Part lookup dictionary and the necessary lists
                partSimLookup.Add(part, partSim);
                allParts.Add(partSim);
                if (partSim.isFuelLine)
                    allFuelLines.Add(partSim);
                if (partSim.isEngine)
                    partSim.CreateEngineSims(allEngines, atmosphere, velocity, vectoredThrust, log);

                partId++;
            }

            UpdateActiveEngines();

            // Now that all the PartSims have been created we can do any set up that needs access to other parts
            // First we set up all the parent links
            foreach (PartSim partSim in allParts)
            {
                partSim.SetupParent(partSimLookup, log);
            }
            
            // Then, in the VAB/SPH, we add the parent of each fuel line to the fuelTargets list of their targets
            if (HighLogic.LoadedSceneIsEditor)
            {
                foreach (PartSim partSim in allFuelLines)
                {
                    if ((partSim.part as FuelLine).target != null)
                    {
                        PartSim targetSim;
                        if (partSimLookup.TryGetValue((partSim.part as FuelLine).target, out targetSim))
                        {
                            if (log != null)
                                log.buf.AppendLine("Fuel line target is " + targetSim.name + ":" + targetSim.partId);

                            targetSim.fuelTargets.Add(partSim.parent);
                        }
                        else
                        {
                            if (log != null)
                                log.buf.AppendLine("No PartSim for fuel line target (" + partSim.part.partInfo.name + ")");
                        }
                    }
                    else
                    {
                        if (log != null)
                            log.buf.AppendLine("Fuel line target is null");
                    }
                }
            }

            //MonoBehaviour.print("SetupAttachNodes and count stages");
            foreach (PartSim partSim in allParts)
            {
                partSim.SetupAttachNodes(partSimLookup, log);
                if (partSim.decoupledInStage >= lastStage)
                    lastStage = partSim.decoupledInStage + 1;
            }

            // And finally release the Part references from all the PartSims
            //MonoBehaviour.print("ReleaseParts");
            foreach (PartSim partSim in allParts)
                partSim.ReleasePart();

            // And dereference the core's part list
            partList = null;

            _timer.Stop();
            if (log != null)
            {
                log.buf.AppendLine("PrepareSimulation: " + _timer.ElapsedMilliseconds + "ms");
                log.Flush();
            }

            if (dumpTree)
                Dump();

            return true;
        }

        
        // This function runs the simulation and returns a newly created array of Stage objects
        public Stage[] RunSimulation()
        {
            if (SimManager.logOutput)
                MonoBehaviour.print("RunSimulation started");
            _timer.Start();

            LogMsg log = null;
            if (SimManager.logOutput)
                log = new LogMsg();

            // Start with the last stage to simulate
            // (this is in a member variable so it can be accessed by AllowedToStage and ActivateStage)
            currentStage = lastStage;

            // Work out which engines would be active if just doing the staging and if this is different to the 
            // currently active engines then generate an extra stage
            // Loop through all the engines
            bool anyActive = false;
            foreach (EngineSim engine in allEngines)
            {
                if (log != null)
                    log.buf.AppendLine("Testing engine mod of " + engine.partSim.name + ":" + engine.partSim.partId);
                bool bActive = engine.isActive;
                bool bStage = (engine.partSim.inverseStage >= currentStage);
                if (log != null)
                    log.buf.AppendLine("bActive = " + bActive + "   bStage = " + bStage);
                if (HighLogic.LoadedSceneIsFlight)
                {
                    if (bActive)
                        anyActive = true;
                    if (bActive != bStage)
                    {
                        // If the active state is different to the state due to staging
                        if (log != null)
                            log.buf.AppendLine("Need to do current active engines first");

                        doingCurrent = true;
                    }
                }
                else
                {
                    if (bStage)
                    {
                        if (log != null)
                            log.buf.AppendLine("Marking as active");

                        engine.isActive = true;
                    }
                }
            }

            // If we need to do current because of difference in engine activation and there actually are active engines
            // then we do the extra stage otherwise activate the next stage and don't treat it as current
            if (doingCurrent && anyActive)
            {
                currentStage++;
            }
            else
            {
                ActivateStage();
                doingCurrent = false;
            }

            // Create a list of lists of PartSims that prevent decoupling
            List<List<PartSim>> dontStagePartsLists = BuildDontStageLists(log);
            
            if (log != null)
                log.Flush();

            // Create the array of stages that will be returned
            Stage[] stages = new Stage[currentStage + 1];

            // Loop through the stages
            while (currentStage >= 0)
            {
                if (log != null)
                {
                    log.buf.AppendLine("Simulating stage " + currentStage);
                    log.buf.AppendLine("ShipMass = " + ShipMass);
                    log.Flush();
                    _timer.Reset();
                    _timer.Start();
                }

                // Update active engines and resource drains
                UpdateResourceDrains();

                // Create the Stage object for this stage
                Stage stage = new Stage();

                stageTime = 0d;
                vecStageDeltaV = Vector3.zero;
                stageStartMass = ShipMass;
                stepStartMass = stageStartMass;
                stepEndMass = 0;

                CalculateThrustAndISP();

                // Store various things in the Stage object
                stage.thrust = totalStageThrust;
                //MonoBehaviour.print("stage.thrust = " + stage.thrust);
                stage.thrustToWeight = totalStageThrust / (stageStartMass * gravity);
                stage.maxThrustToWeight = stage.thrustToWeight;
                //MonoBehaviour.print("StageMass = " + stageStartMass);
                //MonoBehaviour.print("Initial maxTWR = " + stage.maxThrustToWeight);
                stage.actualThrust = totalStageActualThrust;
                stage.actualThrustToWeight = totalStageActualThrust / (stageStartMass * gravity);

                // Calculate the cost and mass of this stage and add all engines and tanks that are decoupled
                // in the next stage to the dontStageParts list
                foreach (PartSim partSim in allParts)
                {
                    if (partSim.decoupledInStage == currentStage - 1)
                    {
                        stage.cost += partSim.cost;
                        stage.mass += partSim.GetStartMass();
                    }
                }

                dontStageParts = dontStagePartsLists[currentStage];

                if (log != null)
                {
                    log.buf.AppendLine("Stage setup took " + _timer.ElapsedMilliseconds + "ms");

                    if (dontStageParts.Count > 0)
                    {
                        log.buf.AppendLine("Parts preventing staging:");
                        foreach (PartSim partSim in dontStageParts)
                            partSim.DumpPartToBuffer(log.buf, "");
                    }
                    else
                        log.buf.AppendLine("No parts preventing staging");

                    log.Flush();
                }

                // Now we will loop until we are allowed to stage
                int loopCounter = 0;
                while (!AllowedToStage())
                {
                    loopCounter++;
                    //MonoBehaviour.print("loop = " + loopCounter);

                    // Calculate how long each draining tank will take to drain and run for the minimum time
                    double resourceDrainTime = double.MaxValue;
                    PartSim partMinDrain = null;
                    foreach (PartSim partSim in drainingParts)
                    {
                        double time = partSim.TimeToDrainResource();
                        if (time < resourceDrainTime)
                        {
                            resourceDrainTime = time;
                            partMinDrain = partSim;
                        }
                    }

                    if (log != null)
                        MonoBehaviour.print("Drain time = " + resourceDrainTime + " (" + partMinDrain.name + ":" + partMinDrain.partId + ")");

                    foreach (PartSim partSim in drainingParts)
                        partSim.DrainResources(resourceDrainTime);

                    // Get the mass after draining
                    stepEndMass = ShipMass;
                    stageTime += resourceDrainTime;

                    double stepEndTWR = totalStageThrust / (stepEndMass * gravity);
                    //MonoBehaviour.print("After drain mass = " + stepEndMass);
                    //MonoBehaviour.print("currentThrust = " + totalStageThrust);
                    //MonoBehaviour.print("currentTWR = " + stepEndTWR);
                    if (stepEndTWR > stage.maxThrustToWeight)
                        stage.maxThrustToWeight = stepEndTWR;

                    //MonoBehaviour.print("newMaxTWR = " + stage.maxThrustToWeight);

                    // If we have drained anything and the masses make sense then add this step's deltaV to the stage total
                    if (resourceDrainTime > 0d && stepStartMass > stepEndMass && stepStartMass > 0d && stepEndMass > 0d)
                        vecStageDeltaV += vecThrust * (float)((currentisp * STD_GRAVITY * Math.Log(stepStartMass / stepEndMass)) / simpleTotalThrust);

                    // Update the active engines and resource drains for the next step
                    UpdateResourceDrains();

                    // Recalculate the current thrust and isp for the next step
                    CalculateThrustAndISP();

                    // Check if we actually changed anything
                    if (stepStartMass == stepEndMass)
                    {
                        //MonoBehaviour.print("No change in mass");
                        break;
                    }

                    // Check to stop rampant looping
                    if (loopCounter == 1000)
                    {
                        MonoBehaviour.print("exceeded loop count");
                        MonoBehaviour.print("stageStartMass = " + stageStartMass);
                        MonoBehaviour.print("stepStartMass = " + stepStartMass);
                        MonoBehaviour.print("StepEndMass   = " + stepEndMass);
                        break;
                    }

                    // The next step starts at the mass this one ended at
                    stepStartMass = stepEndMass;
                }

                // Store more values in the Stage object and stick it in the array

                // Store the magnitude of the deltaV vector
                stage.deltaV = vecStageDeltaV.magnitude;
                
                // Recalculate effective stage isp from the stage deltaV (flip the standard deltaV calculation around)
                // Note: If the mass doesn't change then this is a divide by zero
                if (stageStartMass != stepStartMass)
                    stage.isp = stage.deltaV / (STD_GRAVITY * Math.Log(stageStartMass / stepStartMass));
                else
                    stage.isp = 0;

                // Zero stage time if more than a day (this should be moved into the window code)
                stage.time = (stageTime < SECONDS_PER_DAY) ? stageTime : 0d;
                stage.number = doingCurrent ? -1 : currentStage;                // Set the stage number to -1 if doing current engines
                stages[currentStage] = stage;

                // Now activate the next stage
                currentStage--;
                doingCurrent = false;

                if (log != null)
                {
                    // Log how long the stage took
                    _timer.Stop();
                    MonoBehaviour.print("Simulating stage took " + _timer.ElapsedMilliseconds + "ms");
                    stage.Dump();
                    _timer.Reset();
                    _timer.Start();
                }

                // Activate the next stage
                ActivateStage();

                if (log != null)
                {
                    // Log how long it took to activate
                    _timer.Stop();
                    MonoBehaviour.print("ActivateStage took " + _timer.ElapsedMilliseconds + "ms");
                }
            }

            // Now we add up the various total fields in the stages
            for (int i = 0; i < stages.Length; i++)
            {
                // For each stage we total up the cost, mass, deltaV and time for this stage and all the stages above
                for (int j = i; j >= 0; j--)
                {
                    stages[i].totalCost += stages[j].cost;
                    stages[i].totalMass += stages[j].mass;
                    stages[i].totalDeltaV += stages[j].deltaV;
                    stages[i].totalTime += stages[j].time;
                }
                // We also total up the deltaV for stage and all stages below
                for (int j = i; j < stages.Length; j++)
                {
                    stages[i].inverseTotalDeltaV += stages[j].deltaV;
                }

                // Zero the total time if the value will be huge (24 hours?) to avoid the display going weird
                // (this should be moved into the window code)
                if (stages[i].totalTime > SECONDS_PER_DAY)
                    stages[i].totalTime = 0d;
            }

            if (log != null)
            {
                _timer.Stop();
                MonoBehaviour.print("RunSimulation: " + _timer.ElapsedMilliseconds + "ms");
            }

            return stages;
        }

        private List<List<PartSim>> BuildDontStageLists(LogMsg log)
        {
            if (log != null)
                log.buf.AppendLine("Creating list with capacity of " + (currentStage + 1));
            List<List<PartSim>> lists = new List<List<PartSim>>();
            for (int i = 0; i <= currentStage; i++)
                lists.Add(new List<PartSim>());

            foreach (PartSim partSim in allParts)
            {
                if (partSim.isEngine || !partSim.Resources.Empty)
                {
                    if (log != null)
                        log.buf.AppendLine(partSim.name + ":" + partSim.partId + " is engine or tank, decoupled = " + partSim.decoupledInStage);

                    if (partSim.decoupledInStage < -1 || partSim.decoupledInStage > currentStage - 1)
                    {
                        if (log != null)
                            log.buf.AppendLine("decoupledInStage out of range");
                    }
                    else
                    {
                        lists[partSim.decoupledInStage + 1].Add(partSim);
                    }
                }
            }

            for (int i = 1; i <= lastStage; i++ )
            {
                if (lists[i].Count == 0)
                    lists[i] = lists[i - 1];
            }

            return lists;
        }

        // This function simply rebuilds the active engines by testing the isActive flag of all the engines
        private void UpdateActiveEngines()
        {
            activeEngines.Clear();
            foreach (EngineSim engine in allEngines)
            {
                if (engine.isActive)
                    activeEngines.Add(engine);
            }
        }

        private void CalculateThrustAndISP()
        {
            // Reset all the values
            vecThrust = Vector3.zero;
            vecActualThrust = Vector3.zero;
            simpleTotalThrust = 0d;
            totalStageThrust = 0d;
            totalStageActualThrust = 0d;
            totalStageFlowRate = 0d;
            totalStageIspFlowRate = 0d;

            // Loop through all the active engines totalling the thrust, actual thrust and mass flow rates
            // The thrust is totalled as vectors
            foreach (EngineSim engine in activeEngines)
            {
                simpleTotalThrust += engine.thrust;
                vecThrust += ((float)engine.thrust * engine.thrustVec);
                vecActualThrust += ((float)engine.actualThrust * engine.thrustVec);

                totalStageFlowRate += engine.ResourceConsumptions.Mass;
                totalStageIspFlowRate += engine.ResourceConsumptions.Mass * engine.isp;
            }

            //MonoBehaviour.print("vecThrust = " + vecThrust.ToString() + "   magnitude = " + vecThrust.magnitude);

            totalStageThrust = vecThrust.magnitude;
            totalStageActualThrust = vecActualThrust.magnitude;

            // Calculate the effective isp at this point
            if (totalStageFlowRate > 0d && totalStageIspFlowRate > 0d)
                currentisp = totalStageIspFlowRate / totalStageFlowRate;
            else
                currentisp = 0;
        }

        // This function does all the hard work of working out which engines are burning, which tanks are being drained 
        // and setting the drain rates
        private void UpdateResourceDrains()
        {
            // Update the active engines
            UpdateActiveEngines();
            
            // Empty the draining resources set
            drainingResources.Clear();

            // Reset the resource drains of all draining parts
            foreach (PartSim partSim in drainingParts)
                partSim.ResourceDrains.Reset();

            // Empty the draining parts set
            drainingParts.Clear();

            // Loop through all the active engine modules
            foreach (EngineSim engine in activeEngines)
            {
                // Set the resource drains for this engine
                if (engine.SetResourceDrains(allParts, allFuelLines, drainingParts))
                {
                    // If it is active then add the consumed resource types to the set
                    foreach (int type in engine.ResourceConsumptions.Types)
                        drainingResources.Add(type);
                }
            }

            // Update the active engines again to remove any engines that have no fuel supply
            UpdateActiveEngines();

            if (SimManager.logOutput)
            {
                StringBuilder buffer = new StringBuilder(1024);
                buffer.AppendFormat("Active engines = {0:d}\n", activeEngines.Count);
                int i = 0;
                foreach (EngineSim engine in activeEngines)
                    engine.DumpEngineToBuffer(buffer, "Engine " + (i++) + ":");
                MonoBehaviour.print(buffer);
            }
        }

        // This function works out if it is time to stage
        private bool AllowedToStage()
        {
            StringBuilder buffer = null;
            if (SimManager.logOutput)
            {
                buffer = new StringBuilder(1024);
                buffer.AppendLine("AllowedToStage");
                buffer.AppendFormat("currentStage = {0:d}\n", currentStage);
            }

            if (activeEngines.Count > 0)
            {
                foreach (PartSim partSim in dontStageParts)
                {
                    if (SimManager.logOutput)
                        partSim.DumpPartToBuffer(buffer, "Testing: ");
                    //buffer.AppendFormat("isSepratron = {0}\n", partSim.isSepratron ? "true" : "false");

                    if (!partSim.Resources.EmptyOf(drainingResources))
                    {
                        if (SimManager.logOutput)
                        {
                            partSim.DumpPartToBuffer(buffer, "Decoupled part not empty => false: ");
                            MonoBehaviour.print(buffer);
                        }

                        return false;
                    }

                    if (partSim.isEngine)
                    {
                        foreach (EngineSim engine in activeEngines)
                        {
                            if (engine.partSim == partSim)
                            {
                                if (SimManager.logOutput)
                                {
                                    partSim.DumpPartToBuffer(buffer, "Decoupled part is active engine => false: ");
                                    MonoBehaviour.print(buffer);
                                }
                                return false;
                            }
                        }
                    }
                }
               
            }

            if (currentStage == 0 && doingCurrent)
            {
                if (SimManager.logOutput)
                {
                    buffer.AppendLine("Current stage == 0 && doingCurrent => false");
                    MonoBehaviour.print(buffer);
                }
                return false;
            }

            if (SimManager.logOutput)
            {
                buffer.AppendLine("Returning true");
                MonoBehaviour.print(buffer);
            }
            return true;
        }

        // This function activates the next stage
        // currentStage must be updated before calling this function
        private void ActivateStage()
        {
            // Build a set of all the parts that will be decoupled
            HashSet<PartSim> decoupledParts = new HashSet<PartSim>();
            foreach (PartSim partSim in allParts)
            {
                if (partSim.decoupledInStage >= currentStage)
                    decoupledParts.Add(partSim);
            }

            foreach (PartSim partSim in decoupledParts)
            {
                // Remove it from the all parts list
                allParts.Remove(partSim);
                if (partSim.isEngine)
                {
                    // If it is an engine then loop through all the engine modules and remove all the ones from this engine part
                    for (int i = allEngines.Count - 1; i >= 0; i--)
                    {
                        if (allEngines[i].partSim == partSim)
                            allEngines.RemoveAt(i);
                    }
                }
                // If it is a fuel line then remove it from the list of all fuel lines
                if (partSim.isFuelLine)
                    allFuelLines.Remove(partSim);
            }

            // Loop through all the (remaining) parts
            foreach (PartSim partSim in allParts)
            {
                // Ask the part to remove all the parts that are decoupled
                partSim.RemoveAttachedParts(decoupledParts);
            }

            // Now we loop through all the engines and activate those that are ignited in this stage
            foreach(EngineSim engine in allEngines)
            {
                if (engine.partSim.inverseStage == currentStage)
                    engine.isActive = true;
            }
        }

        private double ShipStartMass
        {
            get
            {
                double mass = 0d;

                foreach (PartSim partSim in allParts)
                {
                    mass += partSim.GetStartMass();
                }

                return mass;
            }
        }

        private double ShipMass
        {
            get
            {
                double mass = 0d;

                foreach (PartSim partSim in allParts)
                {
                    mass += partSim.GetMass();
                }

                return mass;
            }
        }

        public void Dump()
        {
            StringBuilder buffer = new StringBuilder(1024);
            buffer.AppendFormat("Part count = {0:d}\n", allParts.Count);

            // Output a nice tree view of the rocket
            if (allParts.Count > 0)
            {
                PartSim root = allParts[0];
                while (root.parent != null)
                    root = root.parent;

                if (root.hasVessel)
                    buffer.AppendFormat("vesselName = '{0}'  vesselType = {1}\n", vesselName, SimManager.GetVesselTypeString(vesselType));

                root.DumpPartToBuffer(buffer, "", allParts);
            }

            MonoBehaviour.print(buffer);
        }
    }
}
