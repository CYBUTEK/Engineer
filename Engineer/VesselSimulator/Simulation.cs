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
        private List<PartSim> drainingParts;
        private List<EngineSim> allEngines;
        private List<EngineSim> activeEngines;

        private int lastStage = 0;
        private int currentStage = 0;

        private double gravity = 0;
        private double atmosphere = 0;
#if LOG
        private Stopwatch _timer = new Stopwatch();
#endif
        private const double STD_GRAVITY = 9.81d;


        
        public Simulation()
        {
#if LOG
            MonoBehaviour.print("Simulation created");
#endif
        }

        public bool PrepareSimulation(List<Part> parts, double theGravity, double theAtmosphere = 0)
        {
#if LOG
            MonoBehaviour.print("PrepareSimulation started");
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
            drainingParts = new List<PartSim>();
            allEngines = new List<EngineSim>();
            activeEngines = new List<EngineSim>();

            // A dictionary for fast lookup of Part->PartSim during the preparation phase
            Dictionary<Part, PartSim> partSimLookup = new Dictionary<Part, PartSim>();

            // First we create a PartSim for each Part (giving each a unique id)
            int partId = 1;
            foreach (Part part in partList)
            {
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
#if LOG
            _timer.Stop();
            MonoBehaviour.print("PrepareSimulation took " + _timer.ElapsedMilliseconds + "ms");
            Dump();
#endif
            return true;
        }

        

        public Stage[] RunSimulation()
        {
#if LOG
            MonoBehaviour.print("RunSimulation started");
#endif
            currentStage = lastStage;
            Stage[] stages = new Stage[currentStage + 1];
            List<PartSim> allDrains = new List<PartSim>();

            while (currentStage >= 0)
            {
#if LOG
                MonoBehaviour.print("Simulating stage " + currentStage);
                MonoBehaviour.print("ShipMass = " + ShipMass);
                _timer.Reset();
                _timer.Start();
#endif
                UpdateResourceDrains();

                Stage stage = new Stage();
                double stageTime = 0d;
                double stageDeltaV = 0d;
                double totalStageThrust = 0d;
                double totalStageActualThrust = 0d;

                double totalStageFlowRate = 0d;
                double totalStageIspFlowRate = 0d;

                foreach (EngineSim engine in activeEngines)
                {
                    totalStageActualThrust += engine.actualThrust;
                    totalStageThrust += engine.thrust;

                    totalStageFlowRate += engine.ResourceConsumptions.Mass;
                    totalStageIspFlowRate += engine.ResourceConsumptions.Mass * engine.isp;
                }

                if (totalStageFlowRate > 0d && totalStageIspFlowRate > 0d)
                {
                    stage.isp = totalStageIspFlowRate / totalStageFlowRate;
                }

                stage.thrust = totalStageThrust;
                stage.thrustToWeight = (double)(totalStageThrust / (ShipMass * gravity));
                stage.actualThrust = totalStageActualThrust;
                stage.actualThrustToWeight = (double)(totalStageActualThrust / (ShipMass * gravity));

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
                double currentisp = stage.isp;

                int loopCounter = 0;
                while (!AllowedToStage())
                {
                    loopCounter++;
                    //MonoBehaviour.print("loop = " + loopCounter);

                    double resourceDrainTime = double.MaxValue;
                    foreach (PartSim partSim in allParts)
                    {
                        double time = partSim.TimeToDrainResource();
                        if (time < resourceDrainTime)
                            resourceDrainTime = time;
                    }
#if LOG
                    MonoBehaviour.print("Drain time = " + resourceDrainTime);
#endif
                    double startMass = ShipMass;
                    foreach (PartSim partSim in allParts)
                        partSim.DrainResources(resourceDrainTime);

                    double endMass = ShipMass;
                    stageTime += resourceDrainTime;

                    if (resourceDrainTime > 0d && startMass > endMass && startMass > 0d && endMass > 0d)
                    {
                        stageDeltaV += (currentisp * STD_GRAVITY) * Math.Log(startMass / endMass);
                    }

                    UpdateResourceDrains();

                    totalStageFlowRate = 0d;
                    totalStageIspFlowRate = 0d;
                    foreach (EngineSim engine in activeEngines)
                    {
                        totalStageFlowRate += engine.ResourceConsumptions.Mass;
                        totalStageIspFlowRate += engine.ResourceConsumptions.Mass * engine.isp;
                    }

                    if (totalStageFlowRate > 0d && totalStageIspFlowRate > 0d)
                    {
                        currentisp = totalStageIspFlowRate / totalStageFlowRate;
                    }

                    if (startMass == endMass)
                    {
                        MonoBehaviour.print("No change in mass");
                        break;
                    }

                    if (loopCounter == 1000)
                    {
                        MonoBehaviour.print("exceeded loop count");
                        MonoBehaviour.print("startMass = " + startMass);
                        MonoBehaviour.print("endMass   = " + endMass);
                        break;
                    }
                }

                stage.deltaV = stageDeltaV;
                stage.time = (stageTime < 9999) ? stageTime : 0d;
                stage.number = currentStage;
                stages[currentStage] = stage;

                currentStage--;
#if LOG
                _timer.Stop();
                MonoBehaviour.print("Simulating stage took " + _timer.ElapsedMilliseconds + "ms");

                stage.Dump();

                _timer.Reset();
                _timer.Start();
#endif
                ActivateStage();
#if LOG
                _timer.Stop();
                MonoBehaviour.print("ActivateStage took " + _timer.ElapsedMilliseconds + "ms");
#endif
            }

            for (int i = 0; i < stages.Length; i++)
            {
                for (int j = i; j >= 0; j--)
                {
                    stages[i].totalCost += stages[j].cost;
                    stages[i].totalMass += stages[j].mass;
                    stages[i].totalDeltaV += stages[j].deltaV;
                    stages[i].totalTime += stages[j].time;
                }
                for (int j = i; j < stages.Length; j++)
                {
                    stages[i].inverseTotalDeltaV += stages[j].deltaV;
                }

                if (stages[i].totalTime > 9999d)
                {
                    stages[i].totalTime = 0d;
                }
            }

            ResetPartRefs();

            return stages;
        }

        private void UpdateResourceDrains()
        {
            activeEngines.Clear();
            foreach (PartSim partSim in allParts)
                partSim.ResourceDrains.Reset();

            foreach (EngineSim engine in allEngines)
            {
                if (engine.partSim.inverseStage >= currentStage)
                {
                    if (engine.SetResourceDrains(allParts, allFuelLines))
                        activeEngines.Add(engine);
                }
            }
#if LOG
            StringBuilder buffer = new StringBuilder(1024);
            buffer.AppendFormat("Active engines = {0:d}\n", activeEngines.Count);
            //int i = 0;
            //foreach (PartSim engine in activeEngines)
            //    engine.DumpPartAndParentsToBuffer(buffer, "Engine " + (i++) + ":");
            MonoBehaviour.print(buffer);
#endif
        }

        private bool AllowedToStage()
        {
            //StringBuilder buffer = new StringBuilder(1024);
            //buffer.Append("AllowedToStage\n");
            //buffer.AppendFormat("currentStage = {0:d}\n", currentStage);

            if (activeEngines.Count == 0)
            {
                //buffer.Append("No active engines => true\n");
                //MonoBehaviour.print(buffer);
                return true;
            }

            foreach (PartSim partSim in allParts)
            {
                //partSim.DumpPartToBuffer(buffer, "Testing: ");
                //buffer.AppendFormat("isSepratron = {0}\n", partSim.isSepratron ? "true" : "false");
                if (partSim.decoupledInStage == (currentStage - 1) && (!partSim.isSepratron || partSim.decoupledInStage < partSim.inverseStage))
                {
                    if (!partSim.Resources.Empty)
                    {
                        //buffer.Append("Decoupled part not empty => false\n");
                        //MonoBehaviour.print(buffer);
                        return false;
                    }
                    foreach (EngineSim engine in activeEngines)
                    {
                        if (engine.partSim == partSim)
                        {
                            //buffer.Append("Decoupled part is active engine => false\n");
                            //MonoBehaviour.print(buffer);
                            return false;
                        }
                    }
                }
            }

            if (currentStage > 0)
            {
                //buffer.Append("Current stage > 0 => true\n");
                //MonoBehaviour.print(buffer);
                return true;
            }

            //buffer.Append("Returning false\n");
            //MonoBehaviour.print(buffer);
            return false;
        }

        private void ResetPartRefs()
        {
            foreach (PartSim partSim in allParts)
                partSim.ClearRefs();
        }

        private void ActivateStage()
        {
            List<PartSim> decoupledParts = new List<PartSim>();

            foreach (PartSim partSim in allParts)
            {
                if (partSim.decoupledInStage >= currentStage)
                {
                    decoupledParts.Add(partSim);
                }
            }

            foreach (PartSim partSim in decoupledParts)
            {
                allParts.Remove(partSim);
                if (partSim.isEngine)
                {
                    for (int i = allEngines.Count - 1; i >= 0; i--)
                    {
                        if (allEngines[i].partSim == partSim)
                            allEngines.RemoveAt(i);
                    }
                }
                if (partSim.isFuelLine)
                    allFuelLines.Remove(partSim);
            }

            foreach (PartSim partSim in allParts)
            {
                foreach (PartSim decoupledPart in decoupledParts)
                {
                    partSim.RemoveAttachedPart(decoupledPart);
                }
            }
        }


        private bool StageHasSolids
        {
            get
            {
                foreach (EngineSim engine in activeEngines)
                {
                    if (engine.partSim.isSolidMotor)
                        return true;
                }

                return false;
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
