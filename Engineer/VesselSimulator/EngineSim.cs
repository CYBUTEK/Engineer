// Kerbal Engineer Redux
// Author:  CYBUTEK
// License: Attribution-NonCommercial-ShareAlike 3.0 Unported

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Engineer.Extensions;

namespace Engineer.VesselSimulator
{
    public class EngineSim
    {
        ResourceContainer resourceConsumptions = new ResourceContainer();

        public PartSim partSim;

        public double thrust = 0;
        public double actualThrust = 0;
        public double isp = 0;

        // Add thrust vector to account for directional losses
        //public Vector3d thrustVec;

        public EngineSim(PartSim theEngine, double atmosphere,
                            float maxThrust,
                            float thrustPercentage,
                            float requestedThrust,
                            float realIsp,
                            FloatCurve atmosphereCurve,
                            bool throttleLocked,
                            List<Propellant> propellants,
                            bool correctThrust)
        {
            //MonoBehaviour.print("Create EngineSim for " + theEngine.name);
            //MonoBehaviour.print("maxThrust = " + maxThrust);
            //MonoBehaviour.print("thrustPercentage = " + thrustPercentage);
            //MonoBehaviour.print("requestedThrust = " + requestedThrust);

            partSim = theEngine;

            thrust = maxThrust * (thrustPercentage / 100f);
            //MonoBehaviour.print("thrust = " + thrust);

            double flowRate = 0d;
            if (partSim.hasVessel)
            {
                //MonoBehaviour.print("hasVessel is true");
                actualThrust = requestedThrust;
                isp = atmosphereCurve.Evaluate((float)partSim.part.staticPressureAtm);

                if (correctThrust && realIsp == 0)
                {
                    thrust = thrust * isp / atmosphereCurve.Evaluate(0);
                    //MonoBehaviour.print("corrected thrust = " + thrust);
                }

                if (throttleLocked)
                {
                    //MonoBehaviour.print("throttleLocked is true");
                    flowRate = thrust / (isp * 9.81d);
                }
                else
                {
                    if (partSim.isLanded)
                    {
                        //MonoBehaviour.print("partSim.isLanded is true, mainThrottle = " + FlightInputHandler.state.mainThrottle);
                        flowRate = Math.Max(0.000001d, thrust * FlightInputHandler.state.mainThrottle) / (isp * 9.81d);
                    }
                    else
                    {
                        if (requestedThrust > 0)
                        {
                            //MonoBehaviour.print("requestedThrust > 0");
                            flowRate = requestedThrust / (isp * 9.81d);
                        }
                        else
                        {
                            //MonoBehaviour.print("requestedThrust <= 0");
                            flowRate = thrust / (isp * 9.81d);
                        }
                    }
                }
            }
            else
            {
                //MonoBehaviour.print("hasVessel is false");
                isp = atmosphereCurve.Evaluate((float)atmosphere);
                if (correctThrust)
                {
                    thrust = thrust * isp / atmosphereCurve.Evaluate(0);
                    //MonoBehaviour.print("corrected thrust = " + thrust);
                }
                flowRate = thrust / (isp * 9.81d);
            }
#if LOG
            StringBuilder buffer = new StringBuilder(1024);
            buffer.AppendFormat("flowRate = {0:g6}\n", flowRate);
#endif
            float flowMass = 0f;

            foreach (Propellant propellant in propellants)
                flowMass += propellant.ratio * ResourceContainer.GetResourceDensity(propellant.id);
#if LOG
            buffer.AppendFormat("flowMass = {0:g6}\n", flowMass);
#endif
            foreach (Propellant propellant in propellants)
            {
                if (propellant.name == "ElectricCharge" || propellant.name == "IntakeAir")
                    continue;

                double consumptionRate = propellant.ratio * flowRate / flowMass;
#if LOG
                buffer.AppendFormat("Add consumption({0}, {1}:{2:d}) = {3:g6}\n", ResourceContainer.GetResourceName(propellant.id), theEngine.name, theEngine.partId, consumptionRate);
#endif
                resourceConsumptions.Add(propellant.id, consumptionRate);
            }
#if LOG
            MonoBehaviour.print(buffer);
#endif
        }


        public bool SetResourceDrains(List<PartSim> allParts, List<PartSim> allFuelLines, HashSet<PartSim> drainingParts)
        {
            // A dictionary to hold a set of parts for each resource
            Dictionary<int, HashSet<PartSim>> sourcePartSets = new Dictionary<int, HashSet<PartSim>>();

            foreach (int type in resourceConsumptions.Types)
            {
                HashSet<PartSim> sourcePartSet = null;
                switch (ResourceContainer.GetResourceFlowMode(type))
                {
                    case ResourceFlowMode.NO_FLOW:
                        if (partSim.resources[type] > SimManager.RESOURCE_MIN)
                        {
                            sourcePartSet = new HashSet<PartSim>();
                            //MonoBehaviour.print("SetResourceDrains(" + name + ":" + partId + ") setting sources to just this");
                            sourcePartSet.Add(partSim);
                        }
                        break;

                    case ResourceFlowMode.ALL_VESSEL:
                        foreach (PartSim aPartSim in allParts)
                        {
                            if (aPartSim.resources[type] > SimManager.RESOURCE_MIN)
                            {
                                if (sourcePartSet == null)
                                    sourcePartSet = new HashSet<PartSim>();

                                sourcePartSet.Add(aPartSim);
                            }
                        }
                        break;

                    case ResourceFlowMode.STAGE_PRIORITY_FLOW:
                        {
                            Dictionary<int, HashSet<PartSim>> stagePartSets = new Dictionary<int, HashSet<PartSim>>();
                            int maxStage = -1;
                            foreach (PartSim aPartSim in allParts)
                            {
                                if (aPartSim.resources[type] > SimManager.RESOURCE_MIN)
                                {
                                    int stage = aPartSim.decoupledInStage;
                                    if (stage > maxStage)
                                        maxStage = stage;
                                    if (stagePartSets.ContainsKey(stage))
                                    {
                                        sourcePartSet = stagePartSets[stage];
                                    }
                                    else
                                    {
                                        sourcePartSet = new HashSet<PartSim>();
                                        stagePartSets.Add(stage, sourcePartSet);
                                    }
                                    
                                    sourcePartSet.Add(aPartSim);
                                }
                            }

                            while (maxStage >= 0)
                            {
                                if (stagePartSets.ContainsKey(maxStage))
                                {
                                    if (stagePartSets[maxStage].Count() > 0)
                                    {
                                        sourcePartSet = stagePartSets[maxStage];
                                        break;
                                    }
                                }
                                maxStage--;
                            }
                        }
                        break;

                    case ResourceFlowMode.STACK_PRIORITY_SEARCH:
                        HashSet<PartSim> visited = new HashSet<PartSim>();
                        sourcePartSet = partSim.GetSourceSet(type, allParts, allFuelLines, visited);
                        break;

                    default:
                        MonoBehaviour.print("SetResourceDrains(" + partSim.name + ":" + partSim.partId + ") Unexpected flow type for " + ResourceContainer.GetResourceName(type) + ")");
                        break;
                }

                if (sourcePartSet != null && sourcePartSet.Count > 0)
                    sourcePartSets[type] = sourcePartSet;
            }

            // If we don't have sources for all the needed resources then return false without setting up any drains
            foreach (int type in resourceConsumptions.Types)
            {
                if (!sourcePartSets.ContainsKey(type))
                    return false;
            }

            // Now we set the drains on the members of the sets and update the draining parts set
            foreach (int type in resourceConsumptions.Types)
            {
                HashSet<PartSim> sourcePartSet = sourcePartSets[type];
                // Loop through the members of the set 
                double amount = resourceConsumptions[type] / sourcePartSet.Count;
                foreach (PartSim partSim in sourcePartSet)
                {
#if LOG
                    MonoBehaviour.print("Adding drain of " + amount + " " + ResourceContainer.GetResourceName(type) + " to " + partSim.name + ":" + partSim.partId);
#endif
                    partSim.resourceDrains.Add(type, amount);
                    drainingParts.Add(partSim);
                }
            }

            return true;
        }


        public ResourceContainer ResourceConsumptions
        {
            get
            {
                return resourceConsumptions;
            }
        }

#if LOG
        public void DumpEngineToBuffer(StringBuilder buffer, String prefix)
        {
            buffer.Append(prefix);
            buffer.AppendFormat("[thrust = {0:g6}, actual = {1:g6}, isp = {2:g6}\n", thrust, actualThrust, isp);
        }
#endif
    }
}
