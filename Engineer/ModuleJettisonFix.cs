// Kerbal Engineer Redux
// Author:  CYBUTEK
// License: Attribution-NonCommercial-ShareAlike 3.0 Unported

// NOTES: Currently only works within flight by adding engine fairing mass where required.
//        Does not work within the VAB/SPH as of yet, so is not activated as default.

using System;
using System.Linq;

namespace Engineer
{
    class ModuleJettisonFix : PartModule
    {
        private bool _hasAddedMass = false;

        public override void OnUpdate()
        {
            if (part.Modules.OfType<ModuleJettison>().Count() > 0)
            {
                ModuleJettison jettison = (ModuleJettison)part.Modules["ModuleJettison"];
                if (part.findAttachNode(jettison.bottomNodeName).attachedPart != null && !_hasAddedMass)
                {
                    part.mass += jettison.jettisonedObjectMass;
                    _hasAddedMass = true;
                }

                if (part.findAttachNode(jettison.bottomNodeName).attachedPart == null && _hasAddedMass)
                {
                    part.mass -= jettison.jettisonedObjectMass;
                    _hasAddedMass = false;
                }
            }
        }
    }
}
