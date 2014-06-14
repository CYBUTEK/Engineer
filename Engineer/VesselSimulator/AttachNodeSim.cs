using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Engineer.VesselSimulator
{
    class AttachNodeSim
    {
        public PartSim attachedPartSim;
        public AttachNode.NodeType nodeType;
        public String id;

        public AttachNodeSim(PartSim partSim, String newId, AttachNode.NodeType newNodeType)
        {
            attachedPartSim = partSim;
            nodeType = newNodeType;
            id = newId;
        }

        public void DumpToBuffer(StringBuilder buffer)
        {
            if (attachedPartSim == null)
            {
                buffer.Append("<staged>:<n>");
            }
            else
            {
                buffer.Append(attachedPartSim.name);
                buffer.Append(":");
                buffer.Append(attachedPartSim.partId);
            }
            buffer.Append("#");
            buffer.Append(nodeType);
            buffer.Append(":");
            buffer.Append(id);
        }
    }
}
