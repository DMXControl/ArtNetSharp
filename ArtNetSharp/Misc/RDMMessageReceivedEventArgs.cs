using RDMSharp;
using System;

namespace ArtNetSharp.Misc
{
    public class ControllerRDMMessageReceivedEventArgs : EventArgs
    {
        public readonly RDMMessage Request;
        public bool Handled { get => Response != null; }
        public RDMMessage Response;
        public ControllerRDMMessageReceivedEventArgs (RDMMessage Request)
        { 
            this.Request = Request;
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
}
