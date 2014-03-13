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

        public PartSim engine;

        public double thrust = 0;
        public double actualThrust = 0;
        public double isp = 0;

        public EngineSim(PartSim theEngine)
        {
            MonoBehaviour.print("Create EngineSim for " + theEngine);
            engine = theEngine;            

            MonoBehaviour.print("Done");
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
