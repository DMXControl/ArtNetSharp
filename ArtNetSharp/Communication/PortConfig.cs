﻿using Microsoft.Extensions.Logging;
using org.dmxc.wkdt.Light.RDM;
using RDMSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ArtNetTests")]
namespace ArtNetSharp.Communication
{
    public class PortConfig
    {
        private static readonly ILogger Logger = Logging.CreateLogger<PortConfig>();
        public virtual PortAddress PortAddress { get; set; }
        public Net Net { get => PortAddress.Net; }
        public Subnet Subnet { get => PortAddress.Subnet; }
        public Universe Universe { get => PortAddress.Universe; }
        public Address Address { get { return PortAddress.Address; } }

        public virtual byte PortNumber { get; set; }
        public readonly byte BindIndex;

        public virtual bool Output { get => this.Type.HasFlag(EPortType.OutputFromArtNet); }
        public virtual bool Input { get => this.Type.HasFlag(EPortType.InputToArtNet); }
        public virtual EPortType Type { get; set; }
        public virtual GoodInput GoodInput { get; set; }
        public virtual GoodOutput GoodOutput { get; set; }

        public virtual bool ForceBroadcast { get; set; }

        private readonly List<IPv4Address> additionalIPEndpoints;
        public IReadOnlyCollection<IPv4Address> AdditionalIPEndpoints { get; private set; }


        private readonly ConcurrentDictionary<UID, RDMUID_ReceivedBag> discoveredRDMUIDs = new ConcurrentDictionary<UID, RDMUID_ReceivedBag>();
        public IReadOnlyCollection<RDMUID_ReceivedBag> DiscoveredRDMUIDs;
        private readonly ConcurrentDictionary<UID, UID> additionalRDMUIDs = new ConcurrentDictionary<UID, UID>();
        public IReadOnlyCollection<UID> AdditionalRDMUIDs;
        public event EventHandler<RDMUID_ReceivedBag> RDMUIDReceived;
        //public event EventHandler<RDMMessage> RDMMessageReceived;

        public PortConfig(in byte bindIndex, in Net net, in Subnet subnet, in Universe universe, in bool output, in bool input)
            : this(bindIndex, new PortAddress(net, subnet, universe), output, input)
        {
        }
        public PortConfig(in byte bindIndex, in Subnet subnet, in Universe universe, in bool output, in bool input)
            : this(bindIndex, new PortAddress(subnet, universe), output, input)
        {
        }
        public PortConfig(in byte bindIndex, in Net net, in Address address, in bool output, in bool input)
            : this(bindIndex, new PortAddress(net, address.Subnet, address.Universe), output, input)
        {
        }
        public PortConfig(in byte bindIndex, in Address address, in bool output, in bool input)
            : this(bindIndex, new PortAddress(address), output, input)
        {
        }
        public PortConfig(in byte bindIndex, in PortAddress portAddress, in bool output, in bool input)
        {
            if (bindIndex == 0)
                throw new ArgumentOutOfRangeException("Value has to bee within 1 and 255", nameof(bindIndex));

            BindIndex = bindIndex;
            PortAddress = portAddress;
            if (output)
                this.Type |= EPortType.OutputFromArtNet;
            if (input)
                this.Type |= EPortType.InputToArtNet;

            additionalIPEndpoints = new List<IPv4Address>();
            AdditionalIPEndpoints = additionalIPEndpoints.AsReadOnly();

            DiscoveredRDMUIDs = discoveredRDMUIDs.Values.ToList().AsReadOnly();
            AdditionalRDMUIDs = additionalRDMUIDs.Values.ToList().AsReadOnly();
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

        internal void AddDiscoveredRdmUIDs(params UID[] rdmuids)
        {
            if (rdmuids.Length == 0)
                return;

            foreach (UID rdmuid in rdmuids)
            {
                if (discoveredRDMUIDs.TryGetValue(rdmuid, out RDMUID_ReceivedBag bag))
                    bag.Seen();
                else
                {
                    bag = new RDMUID_ReceivedBag(rdmuid);
                    if (discoveredRDMUIDs.TryAdd(rdmuid, bag))
                    {
                        RDMUIDReceived?.InvokeFailSafe(this, bag);
                        Logger.LogTrace($"#{BindIndex} PortAddress: {PortAddress.Combined:x4} Cached UID: {bag.Uid}");
                    }
                }
            }
            DiscoveredRDMUIDs = discoveredRDMUIDs.Values.ToList().AsReadOnly();
        }
        public void AddAdditionalRdmUIDs(params UID[] rdmuids)
        {
            if (rdmuids.Length == 0)
                return;

            foreach (UID rdmuid in rdmuids)
                additionalRDMUIDs.TryAdd(rdmuid, rdmuid);

            AdditionalRDMUIDs = additionalRDMUIDs.Values.ToList().AsReadOnly();
        }
        public void RemoveAdditionalRdmUIDs(params UID[] rdmuids)
        {
            if (additionalRDMUIDs.Count == 0)
                return;

            foreach (UID rdmuid in rdmuids)
                additionalRDMUIDs.TryRemove(rdmuid, out _);

            AdditionalRDMUIDs = additionalRDMUIDs.Values.ToList().AsReadOnly();
        }
        public void RemoveOutdatedRdmUIDs()
        {
            var outdated = discoveredRDMUIDs.Where(uid => uid.Value.Timouted()).ToList();
            bool removed = false;
            foreach (var remove in outdated)
                removed |= discoveredRDMUIDs.TryRemove(remove.Key, out _);
            if (removed)
                DiscoveredRDMUIDs = discoveredRDMUIDs.Values.ToList().AsReadOnly();
        }
        public UID[] GetReceivedRDMUIDs()
        {
            return DiscoveredRDMUIDs.Where(k => !k.Timouted()).Select(k => k.Uid).ToArray();
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