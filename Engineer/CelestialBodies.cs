// Kerbal Engineer Redux
// Author:  CYBUTEK
// License: Attribution-NonCommercial-ShareAlike 3.0 Unported

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engineer
{
    class CelestialBodies
    {
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
            bodies.Add(new Body("Kerbin", 9.8066d, 1.01327d));
            bodies.Add(new Body("Mun", 1.6284d, 0d));
            bodies.Add(new Body("Minmus", 0.785d, 0d));
            bodies.Add(new Body("Moho", 3.92d, 0d));
            bodies.Add(new Body("Eve", 16.671305d, 5.06634d));
            bodies.Add(new Body("Gilly", 0.05d, 0d));
            bodies.Add(new Body("Duna", 2.94d, 0.20265d));
            bodies.Add(new Body("Ike", 1.1d, 0d));
            bodies.Add(new Body("Dres", 1.13d, 0d));
            bodies.Add(new Body("Jool", 7.84d, 15.19902d));
            bodies.Add(new Body("Laythe", 7.85d, 0.81061d));
            bodies.Add(new Body("Vall", 2.31d, 0d));
            bodies.Add(new Body("Tylo", 7.85d, 0d));
            bodies.Add(new Body("Bop", 0.59d, 0d));
            bodies.Add(new Body("Pol", 0.356d, 0d));
            bodies.Add(new Body("Eeloo", 1.69d, 0d));
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
