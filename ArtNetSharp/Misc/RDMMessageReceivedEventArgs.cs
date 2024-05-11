using RDMSharp;
using System;

namespace ArtNetSharp.Misc
{
    public class ControllerRDMMessageReceivedEventArgs : EventArgs
    {
        public readonly RDMMessage Request;
        public bool Handled { get => Response != null; }
        public RDMMessage Response;
        public readonly PortAddress PortAddress;
        public ControllerRDMMessageReceivedEventArgs (in RDMMessage request, in PortAddress portAddress)
        { 
            this.Request = request;
            this.PortAddress = portAddress;
        }
        public void SetResponse (RDMMessage response)
        {
            lock (Request)
            {
                if (Handled)
                    return;

                Response = response;
            }
        }
    }
    public class ResponderRDMMessageReceivedEventArgs : EventArgs
    {
        public readonly RDMMessage Response;
        public readonly PortAddress PortAddress;
        public ResponderRDMMessageReceivedEventArgs(RDMMessage response, in PortAddress portAddress)
        {
            this.Response = response;
            this.PortAddress = portAddress;
        }
    }
}
