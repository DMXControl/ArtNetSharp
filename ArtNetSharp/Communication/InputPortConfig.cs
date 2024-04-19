namespace ArtNetSharp.Communication
{
    public class InputPortConfig : PortConfig
    {
        public override EPortType Type
        {
            get
            {
                return base.Type | EPortType.InputToArtNet;
            }
            set
            {
                base.Type = (value | EPortType.InputToArtNet) & ~EPortType.OutputFromArtNet;
            }
        }
        public InputPortConfig(in byte bindIndex, in Address address) : base(bindIndex, address, false, true)
        {
        }

        public InputPortConfig(in byte bindIndex, in PortAddress portAddress) : base(bindIndex, portAddress, false, true)
        {
        }

        public InputPortConfig(in byte bindIndex, in Subnet subnet, in Universe universe) : base(bindIndex, subnet, universe, false, true)
        {
        }

        public InputPortConfig(in byte bindIndex, in Net net, in Address address) : base(bindIndex, net, address, false, true)
        {
        }

        public InputPortConfig(in byte bindIndex, in Net net, in Subnet subnet, in Universe universe) : base(bindIndex, net, subnet, universe, false, true)
        {
        }
    }
}