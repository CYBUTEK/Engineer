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

        private int lastStage = 0;
        private int currentStage = 0;
        private bool doingCurrent = false;

        private double gravity = 0;
        private double atmosphere = 0;
#if LOG || TIMERS
        private Stopwatch _timer = new Stopwatch();
#endif
        private const double STD_GRAVITY = 9.81d;
        private const double SECONDS_PER_DAY = 86400;
        
        public Simulation()
        {
#if LOG
            MonoBehaviour.print("Simulation created");
#endif
        }

        // This function prepares the simulation by creating all the necessary data structures it will 
        // need during the simulation.  All required data is copied from the core game data structures 
        // so that the simulation itself can be run in a background thread without having issues with 
        // the core game changing the data while the simulation is running.
        public bool PrepareSimulation(List<Part> parts, double theGravity, double theAtmosphere = 0)
        {
#if LOG
            MonoBehaviour.print("PrepareSimulation started");
#endif
#if LOG || TIMERS
            _timer.Start();
#endif
            // Store the parameters in members for ease of access in other functions
            partList = parts;
            gravity = theGravity;
            atmosphere = theAtmosphere;
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

            // First we create a PartSim for each Part (giving each a unique id)
            int partId = 1;
            foreach (Part part in partList)
            {
                // If the part is already in the lookup dictionary then log it and skip to the next part
                if (partSimLookup.ContainsKey(part))
                {
                    MonoBehaviour.print("Part " + part.name + " appears in vessel list more than once");
                    continue;
                }

                // Create the PartSim
                PartSim partSim = new PartSim(part, partId, atmosphere);

                // Add it to the Part lookup dictionary and the necessary lists
                partSimLookup.Add(part, partSim);
                allParts.Add(partSim);
                if (partSim.isFuelLine)
                    allFuelLines.Add(partSim);
                if (partSim.isEngine)
                    partSim.CreateEngineSims(allEngines, atmosphere);

                partId++;
            }

            UpdateActiveEngines();

            // Now that all the PartSims have been created we can do any set up that needs access to other parts
            //MonoBehaviour.print("SetupAttachNodes and count stages");
            foreach (PartSim partSim in allParts)
            {
                partSim.SetupAttachNodes(partSimLookup);
                if (partSim.decoupledInStage >= lastStage)
                    lastStage = partSim.decoupledInStage + 1;
            }

            // And finally release the Part references from all the PartSims
            //MonoBehaviour.print("ReleaseParts");
            foreach (PartSim partSim in allParts)
                partSim.ReleasePart();

            // And dereference the core's part list
            partList = null;

#if LOG || TIMERS
            _timer.Stop();
            MonoBehaviour.print("PrepareSimulation: " + _timer.ElapsedMilliseconds + "ms");
#endif
#if LOG
            Dump();
#endif
            return true;
        }

        
        // This function runs the simulation and returns a newly created array of Stage objects
        public Stage[] RunSimulation()
        {
#if LOG
            MonoBehaviour.print("RunSimulation started");
#endif
#if LOG || TIMERS
            _timer.Start();
#endif
            // Start with the last stage to simulate
            // (this is in a member variable so it can be accessed by AllowedToStage and ActivateStage)
            currentStage = lastStage;

#if LOG
            LogMsg log = new LogMsg();
#endif
            // Work out which engines would be active if just doing the staging and if this is different to the 
            // currently active engines then generate an extra stage
            // Loop through all the engines
            foreach (EngineSim engine in allEngines)
            {
#if LOG
                log.buf.AppendLine("Testing engine mod of " + engine.partSim.name + ":" + engine.partSim.partId);
#endif
                bool bActive = engine.isActive;
                bool bStage = (engine.partSim.inverseStage >= currentStage);
#if LOG
                log.buf.AppendLine("bActive = " + bActive + "   bStage = " + bStage);
#endif
                if (HighLogic.LoadedSceneIsFlight)
                {
                    if (bActive != bStage)
                    {
                        // If the active state is different to the state due to staging
#if LOG
                        log.buf.AppendLine("Need to do current active engines first");
#endif
                        doingCurrent = true;
                        currentStage++;
                        break;
                    }
                }
                else
                {
                    if (bStage)
                    {
#if LOG
                        log.buf.AppendLine("Marking as active");
#endif
                        engine.isActive = true;
                    }
                }
            }
#if LOG
            MonoBehaviour.print(log.buf);
#endif
            // Create the array of stages that will be returned
            Stage[] stages = new Stage[currentStage + 1];

            // Loop through the stages
            while (currentStage >= 0)
            {
#if LOG
                MonoBehaviour.print("Simulating stage " + currentStage);
                MonoBehaviour.print("ShipMass = " + ShipMass);
                _timer.Reset();
                _timer.Start();
#endif
                // Update active engines and resource drains
                UpdateResourceDrains();

                // Create the Stage object for this stage
                Stage stage = new Stage();

                double stageTime = 0d;
                double stageDeltaV = 0d;            
                double totalStageThrust = 0d;
                double totalStageActualThrust = 0d;

                double totalStageFlowRate = 0d;
                double totalStageIspFlowRate = 0d;
                double currentisp = 0;
                double stageStartMass = ShipMass;
                double stepStartMass = stageStartMass;
                double stepEndMass = 0;

                // Loop through all the active engines totalling the thrust, actual thrust and mass flow rates
                foreach (EngineSim engine in activeEngines)
                {
                    totalStageActualThrust += engine.actualThrust;
                    totalStageThrust += engine.thrust;

                    totalStageFlowRate += engine.ResourceConsumptions.Mass;
                    totalStageIspFlowRate += engine.ResourceConsumptions.Mass * engine.isp;
                }

                // Calculate the effective isp at this point
                if (totalStageFlowRate > 0d && totalStageIspFlowRate > 0d)
                    currentisp = totalStageIspFlowRate / totalStageFlowRate;
                else
                    currentisp = 0;

                // Store various things in the Stage object
                stage.thrust = totalStageThrust;
                //MonoBehaviour.print("stage.thrust = " + stage.thrust);
                stage.thrustToWeight = totalStageThrust / (stageStartMass * gravity);
                stage.maxThrustToWeight = stage.thrustToWeight;
                //MonoBehaviour.print("StageMass = " + stageStartMass);
                //MonoBehaviour.print("Initial maxTWR = " + stage.maxThrustToWeight);
                stage.actualThrust = totalStageActualThrust;
                stage.actualThrustToWeight = totalStageActualThrust / (stageStartMass * gravity);

                // Calculate the cost and mass of this stage
                foreach (PartSim partSim in allParts)
                {
                    if (partSim.decoupledInStage == currentStage - 1)
                    {
                        stage.cost += partSim.cost;
                        stage.mass += partSim.GetStartMass();
                    }
                }
#if LOG
                MonoBehaviour.print("Stage setup took " + _timer.ElapsedMilliseconds + "ms");
#endif
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
#if LOG
                    MonoBehaviour.print("Drain time = " + resourceDrainTime + " (" + partMinDrain.name + ":" + partMinDrain.partId + ")");
#endif
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
                        stageDeltaV += (currentisp * STD_GRAVITY) * Math.Log(stepStartMass / stepEndMass);

                    // Update the active engines and resource drains for the next step
                    UpdateResourceDrains();

                    // Recalculate the current thrust and isp for the next step
                    totalStageThrust = 0d;
                    totalStageActualThrust = 0d;
                    totalStageFlowRate = 0d;
                    totalStageIspFlowRate = 0d;
                    foreach (EngineSim engine in activeEngines)
                    {
                        totalStageActualThrust += engine.actualThrust;
                        totalStageThrust += engine.thrust;

                        totalStageFlowRate += engine.ResourceConsumptions.Mass;
                        totalStageIspFlowRate += engine.ResourceConsumptions.Mass * engine.isp;
                    }

                    //MonoBehaviour.print("next step thrust = " + totalStageThrust);

                    if (totalStageFlowRate > 0d && totalStageIspFlowRate > 0d)
                        currentisp = totalStageIspFlowRate / totalStageFlowRate;
                    else
                        currentisp = 0;

                    // Check if we actually changed anything
                    if (stepStartMass == stepEndMass)
                    {
                        MonoBehaviour.print("No change in mass");
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
                // Recalculate effective stage isp from the stageDeltaV (flip the standard deltaV calculation around)
                // Note: If the mass doesn't change then this is a divide by zero
                if (stageStartMass != stepStartMass)
                    stage.isp = stageDeltaV / (STD_GRAVITY * Math.Log(stageStartMass / stepStartMass));
                else
                    stage.isp = 0;
                stage.deltaV = stageDeltaV;
                // Zero stage time if more than a day (this should be moved into the window code)
                stage.time = (stageTime < SECONDS_PER_DAY) ? stageTime : 0d;
                stage.number = doingCurrent ? -1 : currentStage;                // Set the stage number to -1 if doing current engines
                stages[currentStage] = stage;

                // Now activate the next stage
                currentStage--;
                doingCurrent = false;
#if LOG
                // Log how long the stage took
                _timer.Stop();
                MonoBehaviour.print("Simulating stage took " + _timer.ElapsedMilliseconds + "ms");
                stage.Dump();
                _timer.Reset();
                _timer.Start();
#endif
                // Activate the next stage
                ActivateStage();
#if LOG
                // Log home long it took to activate
                _timer.Stop();
                MonoBehaviour.print("ActivateStage took " + _timer.ElapsedMilliseconds + "ms");
#endif
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
#if LOG || TIMERS
            _timer.Stop();
            MonoBehaviour.print("RunSimulation: " + _timer.ElapsedMilliseconds + "ms");
#endif
            return stages;
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

#if LOG
            StringBuilder buffer = new StringBuilder(1024);
            buffer.AppendFormat("Active engines = {0:d}\n", activeEngines.Count);
            int i = 0;
            foreach (EngineSim engine in activeEngines)
                engine.DumpEngineToBuffer(buffer, "Engine " + (i++) + ":");
            MonoBehaviour.print(buffer);
#endif
        }

        // This function works out if it is time to stage
        private bool AllowedToStage()
        {
#if LOG
            StringBuilder buffer = new StringBuilder(1024);
            buffer.AppendLine("AllowedToStage");
            buffer.AppendFormat("currentStage = {0:d}\n", currentStage);
#endif
            if (activeEngines.Count == 0)
            {
#if LOG
                buffer.AppendLine("No active engines => true");
                MonoBehaviour.print(buffer);
#endif
                return true;
            }

            bool nothingDecoupled = true;

            foreach (PartSim partSim in allParts)
            {
                //partSim.DumpPartToBuffer(buffer, "Testing: ", allParts);
                //buffer.AppendFormat("isSepratron = {0}\n", partSim.isSepratron ? "true" : "false");
                if (partSim.decoupledInStage == (currentStage - 1) && (!partSim.isSepratron || partSim.decoupledInStage < partSim.inverseStage))
                {
                    nothingDecoupled = false;
                    
                    if (!partSim.Resources.EmptyOf(drainingResources))
                    {
#if LOG
                        partSim.DumpPartToBuffer(buffer, "Decoupled part not empty => false: ");
                        MonoBehaviour.print(buffer);
#endif
                        return false;
                    }
                    foreach (EngineSim engine in activeEngines)
                    {
                        if (engine.partSim == partSim)
                        {
#if LOG
                            partSim.DumpPartToBuffer(buffer, "Decoupled part is active engine => false: ");
                            MonoBehaviour.print(buffer);
#endif
                            return false;
                        }
                    }
                }
            }

            if (nothingDecoupled)
            {
#if LOG
                buffer.AppendLine("Nothing decoupled => false");
                MonoBehaviour.print(buffer);
#endif
                return false;
            }

            if (currentStage > 0 && !doingCurrent)
            {
#if LOG
                buffer.AppendLine("Current stage > 0 && !doingCurrent => true");
                MonoBehaviour.print(buffer);
#endif
                return true;
            }

#if LOG
            buffer.AppendLine("Returning false");
            MonoBehaviour.print(buffer);
#endif
            return false;
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
#if LOG
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

                root.DumpPartToBuffer(buffer, "", allParts);
            }

            MonoBehaviour.print(buffer);
        }
#endif
    }
}
