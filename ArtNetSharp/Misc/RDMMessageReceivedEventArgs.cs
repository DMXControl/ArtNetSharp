using RDMSharp;
using System;

namespace ArtNetSharp.Misc
{
    public class RequestRDMMessageReceivedEventArgs : EventArgs
    {
        public readonly RDMMessage Request;
        public bool Handled { get => Response != null; }
        public RDMMessage Response;
        public readonly PortAddress PortAddress;
        public RequestRDMMessageReceivedEventArgs(in RDMMessage request, in PortAddress portAddress)
        {
            this.Request = request;
            this.PortAddress = portAddress;
        }
        public void SetResponse(RDMMessage response)
        {
            lock (Request)
            {
                if (Handled)
                    return;

                Response = response;
            }
        }
    }
    public class ResponseRDMMessageReceivedEventArgs : EventArgs
    {
        public readonly RDMMessage Response;
        public readonly PortAddress PortAddress;
        public ResponseRDMMessageReceivedEventArgs(RDMMessage response, in PortAddress portAddress)
        {
            this.Response = response;
            this.PortAddress = portAddress;
        }
    }
}
