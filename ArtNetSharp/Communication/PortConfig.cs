using Microsoft.Extensions.Logging;
using RDMSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ArtNetSharp.Communication
{
    public class PortConfig
    {
        private static ILogger Logger = ApplicationLogging.CreateLogger<PortConfig>();
        public virtual PortAddress PortAddress { get; set; }
        public Net Net { get => PortAddress.Net; }
        public Subnet Subnet { get => PortAddress.Subnet; }
        public Universe Universe { get => PortAddress.Universe; }
        public Address Address { get { return PortAddress.Address; } }

        public virtual byte PortNumber { get; set; }
        public byte BindIndex { get; internal set; }

        public virtual bool Output { get; set; }
        public virtual bool Input { get; set; }
        public virtual EPortType Type { get; set; }
        public virtual EGoodInput GoodInput { get; set; }
        public virtual EGoodOutput GoodOutput { get; set; }

        public virtual bool ForceBroadcast { get; set; }

        private List<IPv4Address> additionalIPEndpoints;
        public IReadOnlyCollection<IPv4Address> AdditionalIPEndpoints { get; private set; }


        private ConcurrentDictionary<RDMUID, RDMUID_ReceivedBag> knownRDMUIDs = new ConcurrentDictionary<RDMUID, RDMUID_ReceivedBag>();
        public IReadOnlyCollection<RDMUID_ReceivedBag> KnownRDMUIDs;
        public event EventHandler<RDMUID_ReceivedBag> RDMUIDReceived;
        public event EventHandler<byte[]> RDMMessageReceived;

        public PortConfig(in Net net, in Subnet subnet, in Universe universe, bool output, bool input)
            : this(new PortAddress(net, subnet, universe), output, input)
        {
        }
        public PortConfig(in Subnet subnet, in Universe universe, bool output, bool input)
            : this(new PortAddress(subnet, universe), output, input)
        {
        }
        public PortConfig(in Net net, in Address address, bool output, bool input)
            : this(new PortAddress(net, address.Subnet, address.Universe), output, input)
        {
        }
        public PortConfig(in Address address, bool output, bool input)
            : this(new PortAddress(address), output, input)
        {
        }
        public PortConfig(PortAddress portAddress, bool output, bool input)
        {
            PortAddress = portAddress;
            Output = output;
            Input = input;

            additionalIPEndpoints = new List<IPv4Address>();
            AdditionalIPEndpoints = additionalIPEndpoints.AsReadOnly();

            KnownRDMUIDs = knownRDMUIDs.Values.ToList().AsReadOnly();
        }

        public void AddAdditionalIPEndpoints(params IPv4Address[] addresses) 
        {
            foreach (IPv4Address address in addresses)
                if (!additionalIPEndpoints.Contains(address))
                    additionalIPEndpoints.Add(address);
            AdditionalIPEndpoints = additionalIPEndpoints.AsReadOnly();
        }
        public void RemoveAdditionalIPEndpoints(params IPv4Address[] addresses)
        {
            foreach (IPv4Address address in addresses)
                if (additionalIPEndpoints.Contains(address))
                    additionalIPEndpoints.Remove(address);
            AdditionalIPEndpoints = additionalIPEndpoints.AsReadOnly();
        }
        public void ClearAdditionalIPEndpoints()
        {
            additionalIPEndpoints.Clear();
            AdditionalIPEndpoints = additionalIPEndpoints.AsReadOnly();
        }

        internal void AddRdmUIDs(params RDMUID[] rdmuids)
        {
            if (rdmuids.Length == 0)
                return;

            foreach (RDMUID rdmuid in rdmuids)
            {
                RDMUID_ReceivedBag bag;
                if (knownRDMUIDs.TryGetValue(rdmuid, out bag))
                    bag.Seen();
                else
                {
                    bag = new RDMUID_ReceivedBag(rdmuid);
                    if (knownRDMUIDs.TryAdd(rdmuid, bag))
                    {
                        RDMUIDReceived?.Invoke(this, bag);
                        Logger.LogTrace($"#{BindIndex} PortAddress: {PortAddress.Combined:x4} Cached UID: {bag.Uid}");
                    }
                }
            }
            KnownRDMUIDs = knownRDMUIDs.Values.ToList().AsReadOnly();
        }
        public void RemoveOutdatedRdmUIDs()
        {
            var outdated = knownRDMUIDs.Where(uid => uid.Value.Timouted()).ToList();
            bool removed = false;
            foreach (var remove in outdated)
                removed |= knownRDMUIDs.TryRemove(remove.Key, out _);
            if (removed)
                KnownRDMUIDs = knownRDMUIDs.Values.ToList().AsReadOnly();
        }
        public RDMUID[] GetReceivedRDMUIDs()
        {
            return KnownRDMUIDs.Where(k => !k.Timouted()).Select(k => k.Uid).ToArray();
        }

        internal void ProcessArtRDM(ArtRDM artRDM)
        {
            if (!KnownRDMUIDs.Any(k => k.Uid.Equals(artRDM.Source)))
                return;

            AddRdmUIDs(artRDM.Source);
            RDMMessageReceived?.Invoke(this, artRDM.Data);
        }

        public override string ToString()
        {
            if (Output && Input)
                return $"{nameof(PortConfig)}: In & Output Port {PortAddress.Combined}";
            if (Output)
                return $"{nameof(PortConfig)}: Output Port {PortAddress.Combined}";
            if (Input)
                return $"{nameof(PortConfig)}: Input Port {PortAddress.Combined}";

            return $"{nameof(PortConfig)}: Unknown Port {PortAddress.Combined}";
        }
    }
}