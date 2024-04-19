namespace ArtNetSharp.Communication
{
    public class OutputPortConfig : PortConfig
    {
        public override EPortType Type
        {
            get
            {
                return base.Type | EPortType.OutputFromArtNet;
            }
            set
            {
                base.Type = value | EPortType.OutputFromArtNet;
            }
        }
        public OutputPortConfig(in byte bindIndex, in Address address) : base(bindIndex, address, true, false)
        {
        }

        public OutputPortConfig(in byte bindIndex, in PortAddress portAddress) : base(bindIndex, portAddress, true, false)
        {
        }

        public OutputPortConfig(in byte bindIndex, in Subnet subnet, in Universe universe) : base(bindIndex, subnet, universe, true, false)
        {
        }

        public OutputPortConfig(in byte bindIndex, in Net net, in Address address) : base(bindIndex, net, address, true, false)
        {
        }

        public OutputPortConfig(in byte bindIndex, in Net net, in Subnet subnet, in Universe universe) : base(bindIndex, net, subnet, universe, true, false)
        {
        }
    }
}