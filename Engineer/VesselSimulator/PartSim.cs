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
        public ResourceContainer resources = new ResourceContainer();
        public ResourceContainer resourceDrains = new ResourceContainer();
        public ResourceContainer resourceFlowStates = new ResourceContainer();

        List<AttachNodeSim> attachNodes = new List<AttachNodeSim>();
        public List<PartSim> fuelTargets = new List<PartSim>();

        public Part part;              // This is only set while the data structures are being initialised
        public int partId = 0;
        public String name;
        public PartSim parent;
        public bool hasVessel;
        public String vesselName;
        public VesselType vesselType;
        public String initialVesselName;
        public bool isLanded;
        public bool isDecoupler;
        public int decoupledInStage;
        public int inverseStage;
        public float cost;
        public double baseMass = 0d;
        public double startMass = 0d;
        public String noCrossFeedNodeKey;
        public bool fuelCrossFeed;
        public bool isEngine;
        public bool isFuelLine;
        public bool isFuelTank;
        public bool isSepratron;
        public bool hasMultiModeEngine;
        public bool hasModuleEnginesFX;
        public bool hasModuleEngines;
        public bool isNoPhysics;
        public bool localCorrectThrust;

        public PartSim(Part thePart, int id, double atmosphere, LogMsg log)
        {
            part = thePart;
            partId = id;
            name = part.partInfo.name;

            if (log != null)
                log.buf.AppendLine("Create PartSim for " + name);

            parent = null;
            fuelCrossFeed = part.fuelCrossFeed;
            noCrossFeedNodeKey = part.NoCrossFeedNodeKey;
            decoupledInStage = DecoupledInStage(part);
            isFuelLine = part is FuelLine;
            isFuelTank = part is FuelTank;
            isSepratron = IsSepratron();
            inverseStage = part.inverseStage;
            //MonoBehaviour.print("inverseStage = " + inverseStage);

            cost = part.partInfo.cost;
            foreach (PartResource resource in part.Resources)
            {
                cost -= (float)((resource.maxAmount - resource.amount) * resource.info.unitCost);
            }

            // Work out if the part should have no physical significance
            isNoPhysics = part.HasModule<ModuleLandingGear>() ||
                            part.HasModule<LaunchClamp>() ||
                            part.physicalSignificance == Part.PhysicalSignificance.NONE ||
                            part.PhysicsSignificance == 1;

            if (!isNoPhysics)
                baseMass = part.mass;

            if (SimManager.logOutput)
                MonoBehaviour.print((isNoPhysics ? "Ignoring" : "Using") + " part.mass of " + part.mass);

            foreach (PartResource resource in part.Resources)
            {
                // Make sure it isn't NaN as this messes up the part mass and hence most of the values
                // This can happen if a resource capacity is 0 and tweakable
                if (!Double.IsNaN(resource.amount))
                {
                    if (SimManager.logOutput)
                        MonoBehaviour.print(resource.resourceName + " = " + resource.amount);

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
            if (hasVessel)
            {
                vesselName = part.vessel.vesselName;
                vesselType = part.vesselType;
            }
            initialVesselName = part.initialVesselName;

            hasMultiModeEngine = part.HasModule<MultiModeEngine>();
            hasModuleEnginesFX = part.HasModule<ModuleEnginesFX>();
            hasModuleEngines = part.HasModule<ModuleEngines>();

            isEngine = hasMultiModeEngine || hasModuleEnginesFX || hasModuleEngines;

            if (SimManager.logOutput)
                MonoBehaviour.print("Created " + name + ". Decoupled in stage " + decoupledInStage);
        }

        public void CreateEngineSims(List<EngineSim> allEngines, double atmosphere, double velocity, bool vectoredThrust, LogMsg log)
        {
            bool correctThrust = SimManager.DoesEngineUseCorrectedThrust(part);
            if (log != null)
            {
                log.buf.AppendLine("CreateEngineSims for " + name);

                foreach (PartModule partMod in part.Modules)
                {
                    log.buf.AppendLine("Module: " + partMod.moduleName);
                }

                log.buf.AppendLine("correctThrust = " + correctThrust);
            }

            if (hasMultiModeEngine)
            {
                // A multi-mode engine has multiple ModuleEnginesFX but only one is active at any point
                // The mode of the engine is the engineID of the ModuleEnginesFX that is active
                string mode = part.GetModule<MultiModeEngine>().mode;

                foreach (ModuleEnginesFX engine in part.GetModules<ModuleEnginesFX>())
                {
                    if (engine.engineID == mode)
                    {
                        if (log != null)
                            log.buf.AppendLine("Module: " + engine.moduleName);

                        Vector3 thrustvec = CalculateThrustVector(vectoredThrust ? engine.thrustTransforms : null, log);

                        EngineSim engineSim = new EngineSim(this,
                                                            atmosphere,
                                                            velocity,
                                                            engine.maxThrust,
                                                            engine.minThrust, 
                                                            engine.thrustPercentage,
                                                            engine.requestedThrust,
                                                            thrustvec,
                                                            engine.realIsp,
                                                            engine.atmosphereCurve,
                                                            engine.useVelocityCurve ? engine.velocityCurve : null,
                                                            engine.throttleLocked,
                                                            engine.propellants,
                                                            engine.isOperational,
                                                            correctThrust);
                        allEngines.Add(engineSim);
                    }
                }
            }
            else
            {
                if (hasModuleEnginesFX)
                {
                    foreach (ModuleEnginesFX engine in part.GetModules<ModuleEnginesFX>())
                    {
                        if (log != null)
                            log.buf.AppendLine("Module: " + engine.moduleName);

                        Vector3 thrustvec = CalculateThrustVector(vectoredThrust ? engine.thrustTransforms : null, log);
                        
                        EngineSim engineSim = new EngineSim(this,
                                                            atmosphere,
                                                            velocity,
                                                            engine.maxThrust,
                                                            engine.minThrust,
                                                            engine.thrustPercentage,
                                                            engine.requestedThrust,
                                                            thrustvec,
                                                            engine.realIsp,
                                                            engine.atmosphereCurve,
                                                            engine.useVelocityCurve ? engine.velocityCurve : null,
                                                            engine.throttleLocked,
                                                            engine.propellants,
                                                            engine.isOperational,
                                                            correctThrust);
                        allEngines.Add(engineSim);
                    }
                }

                if (hasModuleEngines)
                {
                    foreach (ModuleEngines engine in part.GetModules<ModuleEngines>())
                    {
                        if (log != null)
                            log.buf.AppendLine("Module: " + engine.moduleName);

                        Vector3 thrustvec = CalculateThrustVector(vectoredThrust ? engine.thrustTransforms : null, log);

                        EngineSim engineSim = new EngineSim(this,
                                                            atmosphere,
                                                            velocity,
                                                            engine.maxThrust,
                                                            engine.minThrust,
                                                            engine.thrustPercentage,
                                                            engine.requestedThrust,
                                                            thrustvec,
                                                            engine.realIsp,
                                                            engine.atmosphereCurve,
                                                            engine.useVelocityCurve ? engine.velocityCurve : null,
                                                            engine.throttleLocked,
                                                            engine.propellants,
                                                            engine.isOperational,
                                                            correctThrust);
                        allEngines.Add(engineSim);
                    }
                }
            }

            if (log != null)
                log.Flush();
        }

        private Vector3 CalculateThrustVector(List<Transform> thrustTransforms, LogMsg log)
        {
            if (thrustTransforms == null)
                return Vector3.forward;
            
            Vector3 thrustvec = Vector3.zero;
            foreach (Transform trans in thrustTransforms)
            {
                if (log != null)
                    log.buf.AppendFormat("Transform = ({0:g6}, {1:g6}, {2:g6})   length = {3:g6}\n", trans.forward.x, trans.forward.y, trans.forward.z, trans.forward.magnitude);

                thrustvec -= trans.forward;
            }

            if (log != null)
                log.buf.AppendFormat("ThrustVec  = ({0:g6}, {1:g6}, {2:g6})   length = {3:g6}\n", thrustvec.x, thrustvec.y, thrustvec.z, thrustvec.magnitude);

            thrustvec.Normalize();

            if (log != null)
                log.buf.AppendFormat("ThrustVecN = ({0:g6}, {1:g6}, {2:g6})   length = {3:g6}\n", thrustvec.x, thrustvec.y, thrustvec.z, thrustvec.magnitude);

            return thrustvec;
        }

        public void SetupParent(Dictionary<Part, PartSim> partSimLookup, LogMsg log)
        {
            if (part.parent != null)
            {
                parent = null;
                if (partSimLookup.TryGetValue(part.parent, out parent))
                {
                    if (log != null)
                        log.buf.AppendLine("Parent part is " + parent.name + ":" + parent.partId);
                }
                else
                {
                    if (log != null)
                        log.buf.AppendLine("No PartSim for parent part (" + part.parent.partInfo.name + ")");
                }
            }
        }

        public void SetupAttachNodes(Dictionary<Part, PartSim> partSimLookup, LogMsg log)
        {
            if (log != null)
                log.buf.AppendLine("SetupAttachNodes for " + name + ":" + partId + "");

            attachNodes.Clear();
            foreach (AttachNode attachNode in part.attachNodes)
            {
                if (log != null)
                    log.buf.AppendLine("AttachNode " + attachNode.id + " = " + (attachNode.attachedPart != null ? attachNode.attachedPart.partInfo.name : "null"));

                if (attachNode.attachedPart != null && attachNode.id != "Strut")
                {
                    PartSim attachedSim;
                    if (partSimLookup.TryGetValue(attachNode.attachedPart, out attachedSim))
                    {
                        if (log != null)
                            log.buf.AppendLine("Adding attached node " + attachedSim.name + ":" + attachedSim.partId + "");

                        attachNodes.Add(new AttachNodeSim(attachedSim, attachNode.id, attachNode.nodeType));
                    }
                    else
                    {
                        if (log != null)
                            log.buf.AppendLine("No PartSim for attached part (" + attachNode.attachedPart.partInfo.name + ")");
                    }
                }
            }

            foreach (Part p in part.fuelLookupTargets)
            {
                if (p != null)
                {
                    PartSim targetSim;
                    if (partSimLookup.TryGetValue(p, out targetSim))
                    {
                        if (log != null)
                            log.buf.AppendLine("Fuel target: " + targetSim.name + ":" + targetSim.partId);

                        fuelTargets.Add(targetSim);
                    }
                    else
                    {
                        if (log != null)
                            log.buf.AppendLine("No PartSim for fuel target (" + p.name + ")");
                    }
                }
            }
        }

        private int DecoupledInStage(Part thePart, int stage = -1)
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
            return thePart.HasModule<ModuleDecouple>() ||
                    thePart.HasModule<ModuleAnchoredDecoupler>();
        }

        private bool IsActiveDecoupler(Part thePart)
        {
            return thePart.FindModulesImplementing<ModuleDecouple>().Any(mod => !mod.isDecoupled) ||
                    thePart.FindModulesImplementing<ModuleAnchoredDecoupler>().Any(mod => !mod.isDecoupled);
        }

        private bool IsSepratron()
        {
            if (!part.ActivatesEvenIfDisconnected)
                return false;

            if (part is SolidRocket)
                return true;

            var modList = part.Modules.OfType<ModuleEngines>();
            if (modList.Count() == 0)
                return false;

            if (modList.First().throttleLocked == true)
                return true;

            return false;
        }

        public void ReleasePart()
        {
            part = null;
        }


        // All functions below this point must not rely on the part member (it may be null)
        //

        public HashSet<PartSim> GetSourceSet(int type, List<PartSim> allParts, HashSet<PartSim> visited, LogMsg log, String indent)
        {
            if (log != null)
            {
                log.buf.AppendLine(indent + "GetSourceSet(" + ResourceContainer.GetResourceName(type) + ") for " + name + ":" + partId);
                indent += "  ";
            }

            HashSet<PartSim> allSources = new HashSet<PartSim>();
            HashSet<PartSim> partSources = null;

            // Rule 1: Each part can be only visited once, If it is visited for second time in particular search it returns empty list.
            if (visited.Contains(this))
            {
                if (log != null)
                    log.buf.AppendLine(indent + "Returning empty set, already visited (" + name + ":" + partId + ")");

                return allSources;
            }

            //if (log != null)
            //    log.buf.AppendLine(indent + "Adding this to visited");

            visited.Add(this);

            // Rule 2: Part performs scan on start of every fuel pipe ending in it. This scan is done in order in which pipes were installed. Then it makes an union of fuel tank sets each pipe scan returned. If the resulting list is not empty, it is returned as result.
            //MonoBehaviour.print("foreach fuel line");

            foreach (PartSim partSim in fuelTargets)
            {
                if (visited.Contains(partSim))
                {
                    //if (log != null)
                    //    log.buf.AppendLine(indent + "Fuel target already visited, skipping (" + partSim.name + ":" + partSim.partId + ")");
                }
                else
                {
                    //if (log != null)
                    //    log.buf.AppendLine(indent + "Adding fuel target as source (" + partSim.name + ":" + partSim.partId + ")");

                    partSources = partSim.GetSourceSet(type, allParts, visited, log, indent);
                    if (partSources.Count > 0)
                    {
                        allSources.UnionWith(partSources);
                        partSources.Clear();
                    }
                }
            }

            if (allSources.Count > 0)
            {
                if (log != null)
                    log.buf.AppendLine(indent + "Returning " + allSources.Count + " fuel target sources (" + name + ":" + partId + ")");

                return allSources;
            }

            // Rule 3: This rule has been removed and merged with rules 4 and 7 to fix issue with fuel tanks with disabled crossfeed

            // Rule 4: Part performs scan on each of its axially mounted neighbors. 
            //  Couplers (bicoupler, tricoupler, ...) are an exception, they only scan one attach point on the single attachment side, skip the points on the side where multiple points are. [Experiment]
            //  Again, the part creates union of scan lists from each of its neighbor and if it is not empty, returns this list. 
            //  The order in which mount points of a part are scanned appears to be fixed and defined by the part specification file. [Experiment]
            if (fuelCrossFeed)
            {
                //MonoBehaviour.print("foreach attach node");
                foreach (AttachNodeSim attachSim in attachNodes)
                {
                    if (attachSim.attachedPartSim != null)
                    {
                        if (/*attachSim.nodeType != AttachNode.NodeType.Surface &&*/
                            !(noCrossFeedNodeKey != null && noCrossFeedNodeKey.Length > 0 && attachSim.id.Contains(noCrossFeedNodeKey)))
                        {
                            if (visited.Contains(attachSim.attachedPartSim))
                            {
                                //if (log != null)
                                //    log.buf.AppendLine(indent + "Attached part already visited, skipping (" + attachSim.attachedPartSim.name + ":" + attachSim.attachedPartSim.partId + ")");
                            }
                            else
                            {
                                //if (log != null)
                                //    log.buf.AppendLine(indent + "Adding attached part as source (" + attachSim.attachedPartSim.name + ":" + attachSim.attachedPartSim.partId + ")");

                                partSources = attachSim.attachedPartSim.GetSourceSet(type, allParts, visited, log, indent);
                                if (partSources.Count > 0)
                                {
                                    allSources.UnionWith(partSources);
                                    partSources.Clear();
                                }
                            }
                        }
                        else
                        {
                            //if (log != null)
                            //    log.buf.AppendLine(indent + "AttachNode is noCrossFeedKey, skipping (" + attachSim.attachedPartSim.name + ":" + attachSim.attachedPartSim.partId + ")");
                        }
                    }
                }

                if (allSources.Count > 0)
                {
                    if (log != null)
                        log.buf.AppendLine(indent + "Returning " + allSources.Count + " attached sources (" + name + ":" + partId + ")");

                    return allSources;
                }
            }
            else
            {
                //if (log != null)
                //    log.buf.AppendLine(indent + "Crossfeed disabled, skipping axial connected parts (" + name + ":" + partId + ")");
            }

            // Rule 5: If the part is fuel container for searched type of fuel (i.e. it has capability to contain that type of fuel and the fuel type was not disabled [Experiment]) and it contains fuel, it returns itself.
            // Rule 6: If the part is fuel container for searched type of fuel (i.e. it has capability to contain that type of fuel and the fuel type was not disabled) but it does not contain the requested fuel, it returns empty list. [Experiment]
            if (resources.HasType(type) && resourceFlowStates[type] != 0)
            {
                if (resources[type] > SimManager.RESOURCE_MIN)
                {
                    allSources.Add(this);

                    if (log != null)
                        log.buf.AppendLine(indent + "Returning enabled tank as only source (" + name + ":" + partId + ")");
                }
                else
                {
                    //if (log != null)
                    //    log.buf.AppendLine(indent + "Returning empty set, enabled tank is empty (" + name + ":" + partId + ")");
                }

                return allSources;
            }

            // Rule 7: If the part is radially attached to another part and it is child of that part in the ship's tree structure, it scans its parent and returns whatever the parent scan returned. [Experiment] [Experiment]
            if (parent != null)
            {
                if (fuelCrossFeed)
                {
                    if (visited.Contains(parent))
                    {
                        //if (log != null)
                        //    log.buf.AppendLine(indent + "Parent part already visited, skipping (" + parent.name + ":" + parent.partId + ")");
                    }
                    else
                    {
                        allSources = parent.GetSourceSet(type, allParts, visited, log, indent);
                        if (allSources.Count > 0)
                        {
                            if (log != null)
                                log.buf.AppendLine(indent + "Returning " + allSources.Count + " parent sources (" + name + ":" + partId + ")");

                            return allSources;
                        }
                    }
                }
                else
                {
                    //if (log != null)
                    //    log.buf.AppendLine(indent + "Crossfeed disabled, skipping radial parent (" + name + ":" + partId + ")");
                }
            }

            // Rule 8: If all preceding rules failed, part returns empty list.
            //if (log != null)
            //    log.buf.AppendLine(indent + "Returning empty set, no sources found (" + name + ":" + partId + ")");

            return allSources;
        }


        public void RemoveAttachedParts(HashSet<PartSim> partSims)
        {
            // Loop through the attached parts
            foreach (AttachNodeSim attachSim in attachNodes)
            {
                // If the part is in the set then "remove" it by clearing the PartSim reference
                if (partSims.Contains(attachSim.attachedPartSim))
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
                if (resourceDrains[type] > 0)
                {
                    time = Math.Min(time, resources[type] / resourceDrains[type]);
                    //MonoBehaviour.print("type = " + ResourceContainer.GetResourceName(type) + "  amount = " + resources[type] + "  rate = " + resourceDrains[type] + "  time = " + time);
                }
            }

            //if (time < double.MaxValue)
            //    MonoBehaviour.print("TimeToDrainResource(" + name + ":" + partId + ") = " + time);
            return time;
        }

        public int DecouplerCount()
        {
            int count = 0;
            PartSim partSim = this;
            while (partSim != null)
            {
                if (partSim.isDecoupler)
                    count++;

                partSim = partSim.parent;
            }
            return count;
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

        public ResourceContainer ResourceDrains
        {
            get
            {
                return resourceDrains;
            }
        }

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

            buffer.AppendFormat(", vesselName = '{0}'", vesselName);
            buffer.AppendFormat(", vesselType = {0}", SimManager.GetVesselTypeString(vesselType));
            buffer.AppendFormat(", initialVesselName = '{0}'", initialVesselName);

            buffer.AppendFormat(", fuelCF = {0}", fuelCrossFeed);
            buffer.AppendFormat(", noCFNKey = '{0}'", noCrossFeedNodeKey);

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
    }
}
