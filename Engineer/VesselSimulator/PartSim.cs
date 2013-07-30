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
using UnityEngine;
using Engineer.Extensions;

namespace Engineer.VesselSimulator
{
    public class PartSim
    {
        ResourceContainer resources = new ResourceContainer();
        ResourceContainer resourceConsumptions = new ResourceContainer();
        ResourceContainer resourceDrains = new ResourceContainer();

        List<PartSim> sourceParts = new List<PartSim>();

        public Part part;
        public int decoupledInStage;
        public double thrust = 0d;
        public double actualThrust = 0d;
        public double isp = 0d;
        public bool surfaceMounted = true;

        public PartSim(Part part, double atmosphere)
        {
            this.part = part;

            foreach (PartResource resource in part.Resources)
            {
                resources.Add(resource.info.id, resource.amount);
            }

            foreach (ModuleEngines engine in part.Modules.OfType<ModuleEngines>())
            {
                if (part.vessel != null)
                {
                    actualThrust += engine.requestedThrust;
                    isp = engine.atmosphereCurve.Evaluate((float)part.staticPressureAtm);
                }
                else
                {
                    isp = engine.atmosphereCurve.Evaluate((float)atmosphere);
                }

                thrust += engine.maxThrust;
            }

            decoupledInStage = DecoupledInStage();
        }

        public bool CanDrawNeededResources(List<PartSim> partSims)
        {
            foreach (int type in resourceConsumptions.Types)
            {
                switch (ResourceContainer.GetResourceFlowMode(type))
                {
                    case ResourceFlowMode.NO_FLOW:
                        if (resources[type] < 1d)
                        {
                            return false;
                        }
                        break;

                    case ResourceFlowMode.ALL_VESSEL:
                        foreach (PartSim partSim in partSims)
                        {
                            if (partSim.resources[type] > 1d)
                            {
                                return true;
                            }
                        }
                        return false;

                    case ResourceFlowMode.STACK_PRIORITY_SEARCH:
                        if (!CanSupplyResourceRecursive(type))
                        {
                            return false;
                        }
                        break;

                    default:
                        return false;
                }
            }
            return true;
        }

        public void SetResourceConsumptions()
        {
            foreach (ModuleEngines engine in part.Modules.OfType<ModuleEngines>())
            {
                double flowRate = 0d;
                if (part.vessel != null)
                {
                    if (engine.throttleLocked)
                    {
                        flowRate = engine.maxThrust / (isp * Simulation.STD_GRAVITY);
                    }
                    else
                    {
                        if (part.vessel.Landed)
                        {
                            flowRate = Math.Max(0.000001d, engine.maxThrust * FlightInputHandler.state.mainThrottle) / (isp * Simulation.STD_GRAVITY);
                        }
                        else
                        {
                            if (engine.requestedThrust > 0d)
                            {
                                flowRate = engine.requestedThrust / (isp * Simulation.STD_GRAVITY);
                            }
                            else
                            {
                                flowRate = engine.maxThrust / (isp * Simulation.STD_GRAVITY);
                            }
                        }
                    }
                }
                else
                {
                    flowRate = engine.maxThrust / (isp * Simulation.STD_GRAVITY);
                }

                double flowMass = 0d;

                foreach (ModuleEngines.Propellant propellant in engine.propellants)
                {
                    flowMass += propellant.ratio * ResourceContainer.GetResourceDensity(propellant.id);
                }

                foreach (ModuleEngines.Propellant propellant in engine.propellants)
                { 
                    if (propellant.name == "ElectricCharge" || propellant.name == "IntakeAir")
                    {
                        continue;
                    }

                    double consumptionRate = propellant.ratio * flowRate / flowMass;
                    resourceConsumptions.Add(propellant.id, consumptionRate);
                }
            }
        }

        public void SetResourceDrainRates(List<PartSim> partSims)
        {
            foreach (int type in resourceConsumptions.Types)
            {
                double amount = resourceConsumptions[type];

                switch (ResourceContainer.GetResourceFlowMode(type))
                {
                    case ResourceFlowMode.NO_FLOW:
                        this.resourceDrains.Add(type, amount);
                        break;

                    case ResourceFlowMode.ALL_VESSEL:
                        SetResourceDrainRateAllVessel(type, amount, partSims);
                        break;

                    case ResourceFlowMode.STACK_PRIORITY_SEARCH:
                        SetResourceDrainRateRecursive(type, amount);
                        break;
                }
            }
        }

        public void SetSourceNodes(Hashtable partSimLookup)
        {
            foreach (PartSim partSim in partSimLookup.Values)
            {
                if (partSim.part is FuelLine && (partSim.part as FuelLine).target == this.part)
                {
                    sourceParts.Add(partSim);
                }
            }

            foreach (AttachNode attachNode in this.part.attachNodes)
            {
                if (attachNode.attachedPart != null && attachNode.nodeType == AttachNode.NodeType.Stack &&
                    !(this.part.NoCrossFeedNodeKey.Length > 0 && attachNode.id.Contains(this.part.NoCrossFeedNodeKey)))
                {
                    if (part.fuelCrossFeed) sourceParts.Add((PartSim)partSimLookup[attachNode.attachedPart]);
                    if (attachNode.attachedPart == part.parent) surfaceMounted = false;
                }
            }

            if (part.parent != null && (this.part.fuelCrossFeed || this.part.IsEngine())) sourceParts.Add((PartSim)partSimLookup[part.parent]);
        }

        public void RemoveSourcePart(PartSim part)
        {
            if (sourceParts.Contains(part))
            {
                sourceParts.Remove(part);
            }
        }

        public int DecoupledInStage(Part part = null, int stage = -1)
        {
            if (part == null)
            {
                part = this.part;
            }

            if (part.IsDecoupler() || part.IsLaunchClamp())
            {
                if (part.inverseStage > stage)
                {
                    stage = part.inverseStage;
                }
            }

            if (part.parent != null)
            {
                stage = DecoupledInStage(part.parent, stage);
            }

            return stage;
        }

        public void DrainResources(double time)
        {
            foreach (int type in resourceDrains.Types)
            {
                resources.Add(type, -time * resourceDrains[type]);
            }
        }

        public double TimeToDrainResource()
        {
            double time = double.MaxValue;

            foreach (int type in resourceDrains.Types)
            {
                if (resourceDrains[type] > 0d)
                {
                    time = Math.Min(time, resources[type] / resourceDrains[type]);
                }
            }

            return time;
        }

        public bool IsDecoupler(Part part = null)
        {

            if (part == null)
            {
                part = this.part;
            }
           
            return part is Decoupler || part is RadialDecoupler || part.Modules.OfType<ModuleDecouple>().Count() > 0 || part.Modules.OfType<ModuleAnchoredDecoupler>().Count() > 0;
        }

        public bool IsDockingNode(Part part = null)
        {
            if (part == null)
            {
                part = this.part;
            }

            return part.Modules.OfType<ModuleDockingNode>().Count() > 0;
        }

        public bool IsStrutFuelLine(Part part = null)
        {
            if (part == null)
            {
                part = this.part;
            }

            return (part is StrutConnector || part is FuelLine) ? true : false;
        }

        private void SetResourceDrainRateAllVessel(int type, double amount, List<PartSim> partSims)
        {
            PartSim source = null;

            foreach (PartSim partSim in partSims)
            {
                if (partSim.resources[type] > 1d)
                {
                    if (source == null || partSim.InverseStage > source.InverseStage)
                    {
                        source = partSim;
                    }
                }
            }

            if (source != null)
            {
                source.resourceDrains.Add(type, amount);
            }
        }

        private void SetResourceDrainRateRecursive(int type, double amount, List<PartSim> visited = null)
        {
            if (visited == null)
            {
                visited = new List<PartSim>();
            }

            List<PartSim> newVisited = new List<PartSim>(visited);
            newVisited.Add(this);

            List<PartSim> fuelLines = new List<PartSim>();
            foreach (PartSim partSim in sourceParts)
            {
                if (partSim.part is FuelLine && !visited.Contains(partSim))
                {
                    if (partSim.CanSupplyResourceRecursive(type, newVisited))
                    {
                        fuelLines.Add(partSim);
                    }
                }
            }

            if (fuelLines.Count > 0)
            {
                foreach (PartSim fuelLine in fuelLines)
                {
                    fuelLine.SetResourceDrainRateRecursive(type, amount / fuelLines.Count, newVisited);
                }
                return;
            }

            foreach (PartSim partSim in sourceParts)
            {
                if (!visited.Contains(partSim) && partSim.CanSupplyResourceRecursive(type, newVisited))
                {
                    if (DrainFromSourceBeforeSelf(type, partSim))
                    {
                        partSim.SetResourceDrainRateRecursive(type, amount, newVisited);
                        return;
                    }
                }
            }

            if (this.resources[type] > 0d)
            {
                resourceDrains.Add(type, amount);
            }
        }

        private bool CanSupplyResourceRecursive(int type, List<PartSim> visited = null)
        {
            if (visited == null)
            {
                visited = new List<PartSim>();
            }

            if (this.resources[type] > 1d)
            {
                return true;
            }

            List<PartSim> newVisited = new List<PartSim>(visited);
            newVisited.Add(this);

            foreach (PartSim partSim in sourceParts)
            {
                if (!visited.Contains(partSim))
                {
                    if (partSim.CanSupplyResourceRecursive(type, newVisited))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool DrainFromSourceBeforeSelf(int type, PartSim source)
        {
            if (resources[type] < 1d)
            {
                return true;
            }

            if (source.part != this.part.parent)
            {
                return true;
            }

            if (this.part.parent == null)
            {
                return true;
            }

            foreach (AttachNode attachNode in this.part.parent.attachNodes)
            {
                if (attachNode.attachedPart == this.part && attachNode.nodeType != AttachNode.NodeType.Stack)
                {
                    return false;
                }
            }

            return true;
        }

        public double GetStartMass(int currentStage)
        {
            double mass = 0d;

            if (part.Modules.Contains("LaunchClamp"))
            {
                return 0d;
            }

            if (part.physicalSignificance == Part.PhysicalSignificance.NONE)
            {
                mass = part.GetResourceMass();
            }
            else
            {
                mass = part.mass + part.GetResourceMass();
            }

            return mass;
        }

        public double GetMass(int currentStage)
        {
            double mass = 0d;

            if (part.Modules.Contains("LaunchClamp"))
            {
                return 0d;
            }

            if (part.physicalSignificance == Part.PhysicalSignificance.FULL)
            {
                mass = part.mass;
            }

            foreach (int type in resources.Types)
            {
                mass += resources.GetResourceMass(type);
            }

            return mass;
        }

        public ResourceContainer Resources
        {
            get
            {
                return this.resources;
            }
        }

        public ResourceContainer ResourceConsumptions
        {
            get
            {
                return this.resourceConsumptions;
            }
        }

        public ResourceContainer ResourceDrains
        {
            get
            {
                return this.resourceDrains;
            }
        }

        public int InverseStage
        {
            get
            {
                return part.inverseStage;
            }
        }

        public bool IsEngine
        {
            get
            {
                return thrust > 0d;
            }
        }

        public bool IsSolidMotor
        {
            get
            {
                foreach (ModuleEngines engine in part.Modules.OfType<ModuleEngines>())
                {
                    if (engine.throttleLocked)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsSepratron
        {
            get
            {
                if (!part.ActivatesEvenIfDisconnected)
                {
                    return false;
                }

                if (part is SolidRocket)
                {
                    return true;
                }

                if (part.Modules.OfType<ModuleEngines>().Count() == 0)
                {
                    return false;
                }

                if (part.Modules.OfType<ModuleEngines>().First().throttleLocked == true)
                {
                    return true;
                }

                return false;
            }
        }
    }
}