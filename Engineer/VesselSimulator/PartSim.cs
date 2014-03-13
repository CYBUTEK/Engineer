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
using System.Linq;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Engineer.Extensions;

namespace Engineer.VesselSimulator
{
    public class PartSim
    {
        ResourceContainer resources = new ResourceContainer();
        ResourceContainer resourceFlowStates = new ResourceContainer();
        ResourceContainer resourceConsumptions = new ResourceContainer();
        ResourceContainer resourceDrains = new ResourceContainer();

        Dictionary<int, bool> resourceCanSupply = new Dictionary<int, bool>();

        List<AttachNodeSim> attachNodes = new List<AttachNodeSim>();

        private Part part;              // This is only set while the data structures are being initialised
        public int partId = 0;
        public String name;
        public PartSim parent;
        public PartSim fuelLineTarget;
        public bool hasVessel;
        public bool isLanded;
        public int decoupledInStage;
        public int inverseStage;
        public int cost;
        double baseMass = 0d;
        double startMass = 0d;
        public double thrust = 0;
        public double actualThrust = 0;
        public double isp = 0;
        public String noCrossFeedNodeKey;
        public bool fuelCrossFeed;
        public bool isEngine;
        public bool isFuelLine;
        public bool isFuelTank;
        public bool isDecoupler;
        public bool isDockingNode;
        public bool isStrutOrFuelLine;
        public bool isSolidMotor;
        public bool isSepratron;
        public bool hasMultiModeEngine;
        public bool hasModuleEnginesFX;
        public bool hasModuleEngines;

        public PartSim(Part thePart, int id, double atmosphere)
        {
            part = thePart;
            partId = id;
            name = part.partInfo.name;
            MonoBehaviour.print("Create PartSim for " + name);
            
            parent = null;
            fuelCrossFeed = part.fuelCrossFeed;
            noCrossFeedNodeKey = part.NoCrossFeedNodeKey;
            decoupledInStage = DecoupledInStage(part);
            isDecoupler = IsDecoupler(part);
            isDockingNode = IsDockingNode();
            isFuelLine = part is FuelLine;
            isFuelTank = part is FuelTank;
            isStrutOrFuelLine = IsStrutOrFuelLine();
            isSolidMotor = IsSolidMotor();
            isSepratron = IsSepratron();
            inverseStage = part.inverseStage;
            //MonoBehaviour.print("inverseStage = " + inverseStage);

            cost = part.partInfo.cost;

            if (!part.Modules.Contains("LaunchClamp") && part.physicalSignificance == Part.PhysicalSignificance.FULL)
                baseMass = part.mass;

            foreach (PartResource resource in part.Resources)
            {
                // Make sure it isn't NaN as this messes up the part mass and hence most of the values
                // This can happen if a resource capacity is 0 and tweakable
                if (!Double.IsNaN(resource.amount))
                {
                    //MonoBehaviour.print(resource.resourceName + " = " + resource.amount);
                    resources.Add(resource.info.id, resource.amount);
                    resourceFlowStates.Add(resource.info.id, resource.flowState ? 1 : 0);
                }
                else
                {
                    MonoBehaviour.print(resource.resourceName + " is NaN. Skipping.");
                }
            }

            startMass = GetMass();

            hasVessel = (part.vessel != null);
            isLanded = hasVessel && part.vessel.Landed;

            hasMultiModeEngine = part.HasModule<MultiModeEngine>();
            hasModuleEnginesFX = part.HasModule<ModuleEnginesFX>();
            hasModuleEngines = part.HasModule<ModuleEngines>();

            isEngine = hasMultiModeEngine || hasModuleEnginesFX || hasModuleEngines;

            SetupEngineMembers(atmosphere);

            //MonoBehaviour.print("Created " + name + ". Decoupled in stage " + decoupledInStage);
        }

        private void SetupEngineMembers(double atmosphere)
        {
            if (hasMultiModeEngine)
            {
                string mode = part.GetModule<MultiModeEngine>().mode;

                foreach (ModuleEnginesFX engine in part.GetModules<ModuleEnginesFX>())
                {
                    if (engine.engineID == mode)
                    {
                        thrust = engine.maxThrust * (engine.thrustPercentage / 100f);

                        double flowRate = 0d;
                        if (hasVessel)
                        {
                            actualThrust = engine.requestedThrust;
                            isp = engine.atmosphereCurve.Evaluate((float)part.staticPressureAtm);
                            
                            if (engine.throttleLocked)
                            {
                                flowRate = engine.maxThrust * (engine.thrustPercentage / 100f) / (isp * 9.81d);
                            }
                            else
                            {
                                if (isLanded)
                                {
                                    flowRate = Math.Max(0.000001d, engine.maxThrust * (engine.thrustPercentage / 100f) * FlightInputHandler.state.mainThrottle) / (isp * 9.81d);
                                }
                                else
                                {
                                    if (engine.requestedThrust > 0)
                                        flowRate = engine.requestedThrust / (isp * 9.81d);
                                    else
                                        flowRate = engine.maxThrust * (engine.thrustPercentage / 100f) / (isp * 9.81d);
                                }
                            }
                        }
                        else
                        {
                            isp = engine.atmosphereCurve.Evaluate((float)atmosphere);
                            flowRate = engine.maxThrust * (engine.thrustPercentage / 100f) / (isp * 9.81d);
                        }

                        float flowMass = 0f;

                        foreach (Propellant propellant in engine.propellants)
                            flowMass += propellant.ratio * ResourceContainer.GetResourceDensity(propellant.id);

                        foreach (Propellant propellant in engine.propellants)
                        {
                            if (propellant.name == "ElectricCharge" || propellant.name == "IntakeAir")
                                continue;

                            double consumptionRate = propellant.ratio * flowRate / flowMass;
                            //MonoBehaviour.print("Add consumption(" + ResourceContainer.GetResourceName(propellant.id) + ", " + name + ":" + partId + ") = " + consumptionRate);
                            resourceConsumptions.Add(propellant.id, consumptionRate);
                        }
                    }
                }
            }
            else if (hasModuleEnginesFX)
            {
                foreach (ModuleEnginesFX engine in part.GetModules<ModuleEnginesFX>())
                {
                    thrust = engine.maxThrust * (engine.thrustPercentage / 100f);
                    
                    double flowRate = 0d;
                    if (hasVessel)
                    {
                        actualThrust = engine.requestedThrust;
                        isp = engine.atmosphereCurve.Evaluate((float)part.staticPressureAtm);

                        if (engine.throttleLocked)
                        {
                            flowRate = engine.maxThrust * (engine.thrustPercentage / 100f) / (isp * 9.81d);
                        }
                        else
                        {
                            if (isLanded)
                            {
                                flowRate = Math.Max(0.000001d, engine.maxThrust * (engine.thrustPercentage / 100f) * FlightInputHandler.state.mainThrottle) / (isp * 9.81d);
                            }
                            else
                            {
                                if (engine.requestedThrust > 0)
                                    flowRate = engine.requestedThrust / (isp * 9.81d);
                                else
                                    flowRate = engine.maxThrust * (engine.thrustPercentage / 100f) / (isp * 9.81d);
                            }
                        }
                    }
                    else
                    {
                        isp = engine.atmosphereCurve.Evaluate((float)atmosphere);
                        flowRate = engine.maxThrust * (engine.thrustPercentage / 100f) / (isp * 9.81d);
                    }

                    float flowMass = 0f;

                    foreach (Propellant propellant in engine.propellants)
                        flowMass += propellant.ratio * ResourceContainer.GetResourceDensity(propellant.id);

                    foreach (Propellant propellant in engine.propellants)
                    {
                        if (propellant.name == "ElectricCharge" || propellant.name == "IntakeAir")
                            continue;

                        double consumptionRate = propellant.ratio * flowRate / flowMass;
                        //MonoBehaviour.print("Add consumption(" + ResourceContainer.GetResourceName(propellant.id) + ", " + name + ":" + partId + ") = " + consumptionRate);
                        resourceConsumptions.Add(propellant.id, consumptionRate);
                    }
                }
            }
            else if (hasModuleEngines)
            {
                foreach (ModuleEngines engine in part.GetModules<ModuleEngines>())
                {
                    thrust = engine.maxThrust * (engine.thrustPercentage / 100f);

                    double flowRate = 0d;
                    if (hasVessel)
                    {
                        actualThrust = engine.requestedThrust;
                        isp = engine.atmosphereCurve.Evaluate((float)part.staticPressureAtm);

                        if (engine.throttleLocked)
                        {
                            flowRate = engine.maxThrust * (engine.thrustPercentage / 100f) / (isp * 9.81d);
                        }
                        else
                        {
                            if (part.vessel.Landed)
                            {
                                flowRate = Math.Max(0.000001d, engine.maxThrust * (engine.thrustPercentage / 100f) * FlightInputHandler.state.mainThrottle) / (isp * 9.81d);
                            }
                            else
                            {
                                if (engine.requestedThrust > 0)
                                    flowRate = engine.requestedThrust / (isp * 9.81d);
                                else
                                    flowRate = engine.maxThrust * (engine.thrustPercentage / 100f) / (isp * 9.81d);
                            }
                        }
                    }
                    else
                    {
                        isp = engine.atmosphereCurve.Evaluate((float)atmosphere);
                        flowRate = engine.maxThrust * (engine.thrustPercentage / 100f) / (isp * 9.81d);
                    }

                    float flowMass = 0f;

                    foreach (Propellant propellant in engine.propellants)
                        flowMass += propellant.ratio * ResourceContainer.GetResourceDensity(propellant.id);

                    foreach (Propellant propellant in engine.propellants)
                    {
                        if (propellant.name == "ElectricCharge" || propellant.name == "IntakeAir")
                            continue;

                        double consumptionRate = propellant.ratio * flowRate / flowMass;
                        //MonoBehaviour.print("Add consumption(" + ResourceContainer.GetResourceName(propellant.id) + ", " + name + ":" + partId + ") = " + consumptionRate);
                        resourceConsumptions.Add(propellant.id, consumptionRate);
                    }
                }
            }
        }

        public void SetupAttachNodes(Dictionary<Part, PartSim> partSimLookup)
        {
            attachNodes.Clear();
            foreach (AttachNode attachNode in part.attachNodes)
            {
                if (attachNode.attachedPart != null)
                {
                    PartSim attachedSim;
                    if (partSimLookup.TryGetValue(attachNode.attachedPart, out attachedSim))
                    {
                        attachNodes.Add(new AttachNodeSim(attachedSim, attachNode.id, attachNode.nodeType));
                    }
                    else
                    {
                        MonoBehaviour.print("No PartSim for attached part (" + attachNode.attachedPart.partInfo.name + ")");
                    }
                }
            }

            if (isFuelLine)
            {
                if ((this.part as FuelLine).target != null)
                {
                    PartSim targetSim;
                    if (partSimLookup.TryGetValue((this.part as FuelLine).target, out targetSim))
                    {
                        fuelLineTarget = targetSim;
                    }
                }
                else
                {
                    fuelLineTarget = null;
                }
            }

            if (part.parent != null)
            {
                parent = null;
                if (!partSimLookup.TryGetValue(part.parent, out parent))
                {
                    MonoBehaviour.print("No PartSim for parent part (" + part.parent.partInfo.name + ")");
                }
            }
        }

        public int DecoupledInStage(Part thePart, int stage = -1)
        {
            if (IsDecoupler(thePart))
            {
                if (thePart.inverseStage > stage)
                {
                    stage = thePart.inverseStage;
                }
            }

            if (thePart.parent != null)
            {
                stage = DecoupledInStage(thePart.parent, stage);
            }

            return stage;
        }

        private bool IsDecoupler(Part thePart)
        {
            return thePart is Decoupler || thePart is RadialDecoupler || thePart.Modules.OfType<ModuleDecouple>().Count() > 0 || thePart.Modules.OfType<ModuleAnchoredDecoupler>().Count() > 0;
        }

        private bool IsDockingNode()
        {
            return part.Modules.OfType<ModuleDockingNode>().Count() > 0;
        }

        private bool IsStrutOrFuelLine()
        {
            return (part is StrutConnector || part is FuelLine) ? true : false;
        }

        private bool IsSolidMotor()
        {
            foreach (ModuleEngines engine in part.Modules.OfType<ModuleEngines>())
            {
                if (engine.throttleLocked)
                    return true;
            }

            return false;
        }

        private bool IsSepratron()
        {
            if (!part.ActivatesEvenIfDisconnected)
                return false;

            if (part is SolidRocket)
                return true;

            if (part.Modules.OfType<ModuleEngines>().Count() == 0)
                return false;

            if (part.Modules.OfType<ModuleEngines>().First().throttleLocked == true)
                return true;

            return false;
        }

        public void ReleasePart()
        {
            part = null;
        }


        // All functions below this point must not rely on the part member (it may be null)
        //

        public bool SetResourceDrains(List<PartSim> allParts, List<PartSim> allFuelLines)
        {
            if (!isEngine)
                return false;

            // A dictionary to hold a set of parts for each resource
            Dictionary<int, HashSet<PartSim>> sourcePartSets = new Dictionary<int, HashSet<PartSim>>();

            foreach (int type in resourceConsumptions.Types)
            {
                HashSet<PartSim> sourcePartSet = null;
                switch (ResourceContainer.GetResourceFlowMode(type))
                {
                    case ResourceFlowMode.NO_FLOW:
                        if (resources[type] > 1f)
                        {
                            sourcePartSet = new HashSet<PartSim>();
                            //MonoBehaviour.print("SetResourceDrains(" + name + ":" + partId + ") setting sources to just this");
                            sourcePartSet.Add(this);
                        }
                        break;

                    case ResourceFlowMode.ALL_VESSEL:
                        foreach (PartSim partSim in allParts)
                        {
                            if (partSim.resources[type] > 1f)
                            {
                                if (sourcePartSet == null)
                                    sourcePartSet = new HashSet<PartSim>();

                                sourcePartSet.Add(partSim);
                            }
                        }
                        break;

                    case ResourceFlowMode.STACK_PRIORITY_SEARCH:
                        HashSet<PartSim> visited = new HashSet<PartSim>();
                        sourcePartSet = GetSourceSet(type, allParts, allFuelLines, visited);
                        break;

                    default:
                        MonoBehaviour.print("SetResourceDrains(" + name + ":" + partId + ") No flow type for " + ResourceContainer.GetResourceName(type) + ")");
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

        public HashSet<PartSim> GetSourceSet(int type, List<PartSim> allParts, List<PartSim> allFuelLines, HashSet<PartSim> visited)
        {
            //MonoBehaviour.print("GetSourceSet(" + ResourceContainer.GetResourceName(type) + ") for " + name + ":" + partId);

            HashSet<PartSim> allSources = new HashSet<PartSim>();
            HashSet<PartSim> partSources = new HashSet<PartSim>();

            // Rule 1: Each part can be only visited once, If it is visited for second time in particular search it returns empty list.
            if (visited.Contains(this))
            {
                //MonoBehaviour.print("Returning empty set, already visited (" + name + ":" + partId + ")");
                return allSources;
            }

            //MonoBehaviour.print("Adding this to visited");
            visited.Add(this);

            // Rule 2: Part performs scan on start of every fuel pipe ending in it. This scan is done in order in which pipes were installed. Then it makes an union of fuel tank sets each pipe scan returned. If the resulting list is not empty, it is returned as result.
            //MonoBehaviour.print("foreach fuel line");
            foreach (PartSim partSim in allFuelLines)
            {
                if (partSim.fuelLineTarget == this)
                {
                    //MonoBehaviour.print("Adding fuel line as source (" + partSim.name + ":" + partSim.partId + ")");
                    partSources = partSim.GetSourceSet(type, allParts, allFuelLines, visited);
                    if (partSources.Count > 0)
                    {
                        allSources.UnionWith(partSources);
                        partSources.Clear();
                    }
                }
            }

            if (allSources.Count > 0)
            {
                //MonoBehaviour.print("Returning " + allSources.Count + " fuel line sources (" + name + ":" + partId + ")");
                return allSources;
            }

            // Rule 3: If the part is not crossfeed capable, it returns empty list.
            //MonoBehaviour.print("Test crossfeed");
            if (!fuelCrossFeed)
            {
                //MonoBehaviour.print("Returning empty set, no cross feed (" + name + ":" + partId + ")");
                return allSources;
            }

            // Rule 4: Part performs scan on each of its axially mounted neighbors. 
            //  Couplers (bicoupler, tricoupler, ...) are an exception, they only scan one attach point on the single attachment side, skip the points on the side where multiple points are. [Experiment]
            //  Again, the part creates union of scan lists from each of its neighbor and if it is not empty, returns this list. 
            //  The order in which mount points of a part are scanned appears to be fixed and defined by the part specification file. [Experiment]
            //MonoBehaviour.print("foreach attach node");
            foreach (AttachNodeSim attachSim in attachNodes)
            {
                if (attachSim.attachedPartSim != null)
                {
                    if (attachSim.nodeType == AttachNode.NodeType.Stack &&
                        (attachSim.attachedPartSim.fuelCrossFeed || attachSim.attachedPartSim.isFuelTank) &&
                        !(noCrossFeedNodeKey != null && noCrossFeedNodeKey.Length > 0 && attachSim.id.Contains(noCrossFeedNodeKey)))
                    {
                        //MonoBehaviour.print("Adding attached part as source (" + attachSim.attachedPartSim.name + ":" + attachSim.attachedPartSim.partId + ")");
                        partSources = attachSim.attachedPartSim.GetSourceSet(type, allParts, allFuelLines, visited);
                        if (partSources.Count > 0)
                        {
                            allSources.UnionWith(partSources);
                            partSources.Clear();
                        }
                    }
                }
            }

            if (allSources.Count > 0)
            {
                //MonoBehaviour.print("Returning " + allSources.Count + " attached sources (" + name + ":" + partId + ")");
                return allSources;
            }

            // Rule 5: If the part is fuel container for searched type of fuel (i.e. it has capability to contain that type of fuel and the fuel type was not disabled [Experiment]) and it contains fuel, it returns itself.
            // Rule 6: If the part is fuel container for searched type of fuel (i.e. it has capability to contain that type of fuel and the fuel type was not disabled) but it does not contain the requested fuel, it returns empty list. [Experiment]
            //MonoBehaviour.print("testing enabled container");
            if (resources.HasType(type) && resourceFlowStates[type] != 0)
            {
                if (resources[type] > 1f)
                    allSources.Add(this);

                //MonoBehaviour.print("Returning this as only source (" + name + ":" + partId + ")");
                return allSources;
            }

            // Rule 7: If the part is radially attached to another part and it is child of that part in the ship's tree structure, it scans its parent and returns whatever the parent scan returned. [Experiment] [Experiment]
            if (parent != null)
            {
                allSources = parent.GetSourceSet(type, allParts, allFuelLines, visited);
                if (allSources.Count > 0)
                {
                    //MonoBehaviour.print("Returning " + allSources.Count + " parent sources (" + name + ":" + partId + ")");
                    return allSources;
                }
            }

            // Rule 8: If all preceding rules failed, part returns empty list.
            //MonoBehaviour.print("Returning empty set, no sources found (" + name + ":" + partId + ")");
            return allSources;
        }

        public void RemoveAttachedPart(PartSim partSim)
        {
            foreach (AttachNodeSim attachSim in attachNodes)
            {
                if (attachSim.attachedPartSim == partSim)
                    attachSim.attachedPartSim = null;
            }
        }


        public void DrainResources(double time)
        {
            //MonoBehaviour.print("DrainResources(" + name + ":" + partId + ", " + time + ")");
            foreach (int type in resourceDrains.Types)
            {
                //MonoBehaviour.print("draining " + (time * resourceDrains[type]) + " " + ResourceContainer.GetResourceName(type));
                resources.Add(type, -time * resourceDrains[type]);
                //MonoBehaviour.print(ResourceContainer.GetResourceName(type) + " left = " + resources[type]);
            }
        }

        public double TimeToDrainResource()
        {
            //MonoBehaviour.print("TimeToDrainResource(" + name + ":" + partId + ")");
            double time = double.MaxValue;

            foreach (int type in resourceDrains.Types)
            {
                //MonoBehaviour.print("type = " + ResourceContainer.GetResourceName(type) + "  amount = " + resources[type] + "  rate = " + resourceDrains[type]);
                if (resourceDrains[type] > 0)
                    time = Math.Min(time, resources[type] / resourceDrains[type]);
            }

            //if (time < double.MaxValue)
            //    MonoBehaviour.print("TimeToDrainResource(" + name + ":" + partId + ") = " + time);
            return time;
        }

        public double GetStartMass()
        {
            return startMass;
        }

        public double GetMass()
        {
            double mass = baseMass;

            foreach (int type in resources.Types)
                mass += resources.GetResourceMass(type);

            return mass;
        }

        public ResourceContainer Resources
        {
            get
            {
                return resources;
            }
        }

        public ResourceContainer ResourceConsumptions
        {
            get
            {
                return resourceConsumptions;
            }
        }

        public ResourceContainer ResourceDrains
        {
            get
            {
                return resourceDrains;
            }
        }

        public int InverseStage
        {
            get
            {
                return part.inverseStage;
            }
        }

        public void ClearRefs()
        {
            part = null;
            parent = null;
            attachNodes.Clear();
        }

#if LOG
        public String DumpPartAndParentsToBuffer(StringBuilder buffer, String prefix)
        {
            if (parent != null)
            {
                prefix = parent.DumpPartAndParentsToBuffer(buffer, prefix) + " ";
            }

            DumpPartToBuffer(buffer, prefix);

            return prefix;
        }

        public void DumpPartToBuffer(StringBuilder buffer, String prefix, List<PartSim> allParts = null)
        {
            buffer.Append(prefix);
            buffer.Append(name);
            buffer.AppendFormat(":[id = {0:d}, decouple = {1:d}, invstage = {2:d}", partId, decoupledInStage, inverseStage);

            buffer.AppendFormat(", isSep = {0}", isSepratron);

            foreach (int type in resources.Types)
                buffer.AppendFormat(", {0} = {1:g6}", ResourceContainer.GetResourceName(type), resources[type]);

            if (attachNodes.Count > 0)
            {
                buffer.Append(", attached = <");
                attachNodes[0].DumpToBuffer(buffer);
                for (int i = 1; i < attachNodes.Count; i++)
                {
                    buffer.Append(", ");
                    attachNodes[i].DumpToBuffer(buffer);
                }
                buffer.Append(">");
            }

            // Add more info here

            buffer.Append("]\n");

            if (allParts != null)
            {
                String newPrefix = prefix + " ";
                foreach (PartSim partSim in allParts)
                {
                    if (partSim.parent == this)
                        partSim.DumpPartToBuffer(buffer, newPrefix, allParts);
                }
            }
        }
#endif
    }
}
