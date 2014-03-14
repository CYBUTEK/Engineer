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
                            FloatCurve atmosphereCurve,
                            bool throttleLocked,
                            List<Propellant> propellants)
        {
            partSim = theEngine;

            thrust = maxThrust * (thrustPercentage / 100f);

            double flowRate = 0d;
            if (partSim.hasVessel)
            {
                actualThrust = requestedThrust;
                isp = atmosphereCurve.Evaluate((float)partSim.part.staticPressureAtm);

                if (throttleLocked)
                {
                    flowRate = maxThrust * (thrustPercentage / 100f) / (isp * 9.81d);
                }
                else
                {
                    if (partSim.isLanded)
                    {
                        flowRate = Math.Max(0.000001d, maxThrust * (thrustPercentage / 100f) * FlightInputHandler.state.mainThrottle) / (isp * 9.81d);
                    }
                    else
                    {
                        if (requestedThrust > 0)
                            flowRate = requestedThrust / (isp * 9.81d);
                        else
                            flowRate = maxThrust * (thrustPercentage / 100f) / (isp * 9.81d);
                    }
                }
            }
            else
            {
                isp = atmosphereCurve.Evaluate((float)atmosphere);
                flowRate = maxThrust * (thrustPercentage / 100f) / (isp * 9.81d);
            }

            float flowMass = 0f;

            foreach (Propellant propellant in propellants)
                flowMass += propellant.ratio * ResourceContainer.GetResourceDensity(propellant.id);

            foreach (Propellant propellant in propellants)
            {
                if (propellant.name == "ElectricCharge" || propellant.name == "IntakeAir")
                    continue;

                double consumptionRate = propellant.ratio * flowRate / flowMass;
                //MonoBehaviour.print("Add consumption(" + ResourceContainer.GetResourceName(propellant.id) + ", " + name + ":" + partId + ") = " + consumptionRate);
                resourceConsumptions.Add(propellant.id, consumptionRate);
            }
        }


        public bool SetResourceDrains(List<PartSim> allParts, List<PartSim> allFuelLines)
        {
            // A dictionary to hold a set of parts for each resource
            Dictionary<int, HashSet<PartSim>> sourcePartSets = new Dictionary<int, HashSet<PartSim>>();

            foreach (int type in resourceConsumptions.Types)
            {
                HashSet<PartSim> sourcePartSet = null;
                switch (ResourceContainer.GetResourceFlowMode(type))
                {
                    case ResourceFlowMode.NO_FLOW:
                        if (partSim.resources[type] > 1f)
                        {
                            sourcePartSet = new HashSet<PartSim>();
                            //MonoBehaviour.print("SetResourceDrains(" + name + ":" + partId + ") setting sources to just this");
                            sourcePartSet.Add(partSim);
                        }
                        break;

                    case ResourceFlowMode.ALL_VESSEL:
                        foreach (PartSim aPartSim in allParts)
                        {
                            if (aPartSim.resources[type] > 1f)
                            {
                                if (sourcePartSet == null)
                                    sourcePartSet = new HashSet<PartSim>();

                                sourcePartSet.Add(aPartSim);
                            }
                        }
                        break;

                    case ResourceFlowMode.STACK_PRIORITY_SEARCH:
                        HashSet<PartSim> visited = new HashSet<PartSim>();
                        sourcePartSet = partSim.GetSourceSet(type, allParts, allFuelLines, visited);
                        break;

                    default:
                        MonoBehaviour.print("SetResourceDrains(" + partSim.name + ":" + partSim.partId + ") No flow type for " + ResourceContainer.GetResourceName(type) + ")");
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

            // Now we set the drains on the members of the sets
            foreach (int type in resourceConsumptions.Types)
            {
                HashSet<PartSim> sourcePartSet = sourcePartSets[type];
                double amount = resourceConsumptions[type] / sourcePartSet.Count;
                foreach (PartSim partSim in sourcePartSet)
                {
#if LOG
                    MonoBehaviour.print("Adding drain of " + amount + " " + ResourceContainer.GetResourceName(type) + " to " + partSim.name + ":" + partSim.partId);
#endif
                    partSim.resourceDrains.Add(type, amount);
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
            buffer.AppendFormat("[thrust = {0:d}, actual = {1:d}, isp = {2:d}", thrust, actualThrust, isp);
        }
#endif
    }
}
