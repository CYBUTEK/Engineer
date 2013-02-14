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
//using System.Linq;
using System.Diagnostics;
using UnityEngine;

namespace Engineer
{
    class Simulator
    {
        List<PartSim> partSims;
        int currentStage = 0;
        bool firstSimulation = true;

        public bool FirstSimulation
        {
            get
            {
                if (firstSimulation)
                {
                    firstSimulation = false;
                    return true;
                }

                return false;
            }
        }

        public Stage[] RunSimulation(List<Part> parts, double gravity, double atmosphere = 0)
        {
            BuildVessel(parts, atmosphere);

            currentStage = Staging.lastStage;
            Stage[] stages = new Stage[currentStage + 1];

            while (currentStage >= 0)
            {
                Stage stage = new Stage();
                double stageTime = 0;
                double stageDeltaV = 0;
                double totalStageThrust = 0;
                double totalStageActualThrust = 0;
                //double totalStageIsp = 0;
                //double totalStageIspThrust = 0;

                double totalStageFlowRate = 0d;
                double totalStageIspFlowRate = 0d;

                foreach (PartSim engine in ActiveEngines)
                {
                    totalStageActualThrust += engine.actualThrust;
                    totalStageThrust += engine.thrust;

                    totalStageFlowRate += engine.ResourceConsumptions.Mass;
                    totalStageIspFlowRate += engine.ResourceConsumptions.Mass * engine.isp;

                    //    if (engine.part.vessel != null)
                    //    {
                    //        if (engine.part.vessel.Landed)
                    //        {
                    //            if (engine.IsSolidMotor)
                    //            {
                    //                totalStageIsp += engine.isp * engine.thrust;
                    //                totalStageIspThrust += engine.thrust;
                    //            }
                    //            else
                    //            {
                    //                totalStageIsp += engine.isp * engine.thrust * Math.Max(0.000001d, FlightInputHandler.state.mainThrottle);
                    //                totalStageIspThrust += engine.thrust * Math.Max(0.000001d, FlightInputHandler.state.mainThrottle);
                    //            }
                    //        }
                    //        else
                    //        {
                    //            if (engine.actualThrust > 0)
                    //            {
                    //                totalStageIsp += engine.isp * engine.actualThrust;
                    //                totalStageIspThrust += engine.actualThrust;
                    //            }
                    //            else
                    //            {
                    //                totalStageIsp += engine.isp * 0.000001d;
                    //                totalStageIspThrust += 0.000001d;
                    //            }
                    //        }
                    //    }
                    //    else
                    //    {
                    //        totalStageIsp += engine.isp * engine.thrust;
                    //        totalStageIspThrust += engine.thrust;
                    //    }
                    //}

                    //if (totalStageThrust > 0 && totalStageIsp > 0)
                    //{
                    //    stage.isp = totalStageIsp / totalStageIspThrust;
                    //}
                }

                if (totalStageFlowRate > 0 && totalStageIspFlowRate > 0)
                {
                    stage.isp = totalStageIspFlowRate / totalStageFlowRate;
                }

                stage.thrust = totalStageThrust;
                stage.thrustToWeight = (double)(totalStageThrust / (ShipMass * gravity));
                stage.actualThrust = totalStageActualThrust;
                stage.actualThrustToWeight = (double)(totalStageActualThrust / (ShipMass * gravity));

                foreach (PartSim partSim in partSims)
                {
                    if (partSim.decoupledInStage == currentStage - 1)
                    {
                        stage.cost += partSim.part.partInfo.cost;
                        stage.mass += partSim.GetStartMass(currentStage);
                    }
                }

                int loopCounter = 0;
                while (!AllowedToStage())
                {
                    loopCounter++;

                    List<PartSim> engines = ActiveEngines;
                    totalStageThrust = 0;
                    foreach (PartSim engine in engines)
                    {
                        if (engine.actualThrust > 0)
                        {
                            totalStageThrust += engine.actualThrust;
                        }
                        else
                        {
                            totalStageThrust += engine.thrust;
                        }
                    }

                    SetResourceDrainRates();

                    double resourceDrainTime = double.MaxValue;
                    foreach (PartSim partSim in partSims)
                    {
                        double time = 0d;
                        time = partSim.TimeToDrainResource();
                        if (time < resourceDrainTime)
                        {
                            resourceDrainTime = time;
                        }
                    }

                    double startMass = ShipMass;
                    foreach (PartSim partSim in partSims)
                    {
                        partSim.DrainResources(resourceDrainTime);
                    }
                    double endMass = ShipMass;
                    stageTime += resourceDrainTime;

                    if (resourceDrainTime > 0 && startMass > endMass && startMass > 0 && endMass > 0)
                    {
                        stageDeltaV += (stage.isp * 9.81d) * Math.Log(startMass / endMass);
                    }

                    if (loopCounter == 1000)
                    {
                        break;
                    }
                }

                stage.deltaV = stageDeltaV;
                if (stageTime < 9999)
                {
                    stage.time = stageTime;
                }
                else
                {
                    stage.time = 0;
                }
                stages[currentStage] = stage;

                currentStage--;
                ActivateStage();
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

                if (stages[i].totalTime > 9999)
                {
                    stages[i].totalTime = 0;
                }
            }

            return stages;
        }

        private void BuildVessel(List<Part> parts, double atmosphere)
        {
            partSims = new List<PartSim>();
            Hashtable partSimLookup = new Hashtable();
            foreach (Part part in parts)
            {
                PartSim partSim = new PartSim(part, atmosphere);

                //if (partSim.part.vessel != null)
                //{
                //    if (partSim.part.vessel.Landed)
                //    {
                //        partSim.SetResourceConsumptions(Math.Max(0.000001d, partSim.thrust * FlightInputHandler.state.mainThrottle));
                //    }
                //    else
                //    {
                //        if (partSim.actualThrust > 0)
                //        {
                //            partSim.SetResourceConsumptions(partSim.actualThrust);
                //        }
                //        else
                //        {
                //            partSim.SetResourceConsumptions(0.000001d);
                //        }
                //    }
                //}
                //else
                //{
                //    partSim.SetResourceConsumptions();
                //}

                partSim.SetResourceConsumptions();

                partSims.Add(partSim);
                partSimLookup.Add(part, partSim);
            }

            foreach (PartSim partSim in partSims)
            {
                partSim.SetSourceNodes(partSimLookup);
            }
        }

        private bool AllowedToStage()
        {
            List<PartSim> engines = ActiveEngines;

            if (engines.Count == 0)
            {
                return true;
            }

            foreach (PartSim partSim in partSims)
            {
                if (partSim.decoupledInStage == (currentStage - 1))
                {
                    if (!partSim.IsSepratron)
                    {
                        if (!partSim.Resources.Empty || engines.Contains(partSim))
                        {
                            return false;
                        }
                    }
                }
            }

            if (currentStage > 0)
            {
                return true;
            }

            return false;
        }

        private void SetResourceDrainRates()
        {
            foreach (PartSim partSim in partSims)
            {
                partSim.ResourceDrains.Reset();
            }

            List<PartSim> engines = ActiveEngines;

            foreach (PartSim engine in engines)
            {
                engine.SetResourceDrainRates(partSims);
            }
        }

        private void ActivateStage()
        {
            List<PartSim> decoupledParts = new List<PartSim>();

            foreach (PartSim partSim in partSims)
            {
                if (partSim.decoupledInStage == currentStage)
                {
                    decoupledParts.Add(partSim);
                }
            }

            foreach (PartSim partSim in decoupledParts)
            {
                partSims.Remove(partSim);
            }

            foreach (PartSim partSim in partSims)
            {
                foreach (PartSim decoupledPart in decoupledParts)
                {
                    partSim.RemoveSourcePart(decoupledPart);
                }
            }
        }

        private List<PartSim> ActiveEngines
        {
            get
            {
                List<PartSim> engines = new List<PartSim>();
                {
                    foreach (PartSim partSim in partSims)
                    {
                        if (partSim.IsEngine && partSim.InverseStage >= currentStage && partSim.CanDrawNeededResources(partSims))
                        {
                            engines.Add(partSim);
                        }
                    }
                }

                return engines;
            }
        }

        private bool StageHasSolids
        {
            get
            {
                foreach (PartSim engine in ActiveEngines)
                {
                    if (engine.IsSolidMotor)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private double ShipStartMass
        {
            get
            {
                double mass = 0f;

                foreach (PartSim partSim in partSims)
                {
                    mass += partSim.GetStartMass(currentStage);
                }

                return mass;
            }
        }

        private double ShipMass
        {
            get
            {
                double mass = 0f;

                foreach (PartSim partSim in partSims)
                {
                    mass += partSim.GetMass(currentStage);
                }

                return mass;
            }
        }
    }
}
