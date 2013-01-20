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
            bodies.Add(new Body("Kerbin", 9.8066f, 1.01327f));
            bodies.Add(new Body("Mun", 1.6284f, 0f));
            bodies.Add(new Body("Minmus", 0.785f, 0f));
            bodies.Add(new Body("Moho", 3.92f, 0f));
            bodies.Add(new Body("Eve", 16.671305f, 5.06634f));
            bodies.Add(new Body("Gilly", 0.05f, 0f));
            bodies.Add(new Body("Duna", 2.94f, 0.20265f));
            bodies.Add(new Body("Ike", 1.1f, 0f));
            bodies.Add(new Body("Dres", 1.13f, 0f));
            bodies.Add(new Body("Jool", 7.84f, 15.19902f));
            bodies.Add(new Body("Laythe", 7.85f, 0.81061f));
            bodies.Add(new Body("Vall", 2.31f, 0f));
            bodies.Add(new Body("Tylo", 7.85f, 0f));
            bodies.Add(new Body("Bop", 0.59f, 0f));
            bodies.Add(new Body("Pol", 0.356f, 0f));
        }

        public class Body
        {
            public string name;
            public float gravity;
            public float atmosphere;

            public Body(string name, float gravity, float atmosphere = 1f)
            {
                this.name = name;
                this.gravity = gravity;
                this.atmosphere = atmosphere;
            }
        }
    }
}
