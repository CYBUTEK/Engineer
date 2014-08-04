// Kerbal Engineer Redux
// Author:  CYBUTEK
// License: Attribution-NonCommercial-ShareAlike 3.0 Unported

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Engineer
{
    public class CelestialBodies
    {
		// Is this object safe to construct?
		public static bool Available
		{
			get { return (PSystemManager.Instance != null); }
		}

        public List<Body> bodies = new List<Body>();

        public Body this[string name]
        {
            get
            {
                foreach (Body body in bodies)
                {
                    if (body.name == name)
                    {
                        return body;
                    }
                }

                return null;
            }
        }

        public CelestialBodies()
        {
			// If this class is not available and we try to construct it, throw an exception
			if (!CelestialBodies.Available) 
			{
				throw new Exception("Engineer.CelestialBodies can't be constructed at this time");
			}

			// Add the local bodies by looking through flight globals
			foreach (CelestialBody body in PSystemManager.Instance.localBodies) 
			{
				// Does this body have atmosphere
				if(body.atmosphere)
				{
					bodies.Add(new Body(body.bodyName, body.GeeASL * 9.8066d, body.atmosphereMultiplier));
				}

				// Otherwise use 0 for no atmosphere
				else
				{
					bodies.Add(new Body(body.bodyName, body.GeeASL * 9.8066d, 0d));
				}
			}
        }

        public class Body
        {
            public string name;
            public double gravity;
            public double atmosphere;

            public Body(string name, double gravity, double atmosphere = 1d)
            {
                this.name = name;
                this.gravity = gravity;
                this.atmosphere = atmosphere;
            }
        }
    }
}
