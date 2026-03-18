using org.dmxc.wkdt.Light.RDM;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ArtNetTests")]
namespace ArtNetSharp.Communication;

public sealed class RDMUID_ReceivedBag : INotifyPropertyChanged
{
    public readonly UID Uid;
    public DateTime LastSeen { get; private set; }

    private PortAddress portAddress;
    private byte bindIndex;

    public event PropertyChangedEventHandler PropertyChanged;

    public PortAddress PortAddress
    {
        get { return portAddress; }
        private set
        {
            if (value == portAddress)
                return;
            portAddress = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PortAddress)));
        }
    }
    public byte BIndIndex
    {
        get { return bindIndex; }
        private set
        {
            if (value == bindIndex)
                return;
            bindIndex = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PortAddress)));
        }
    }

    public byte TransactionNumber { get; private set; } = byte.MaxValue;

    public RDMUID_ReceivedBag(in PortAddress portAddress, in byte bindIndex, in UID uid)
    {
        Uid = uid;
        Seen(portAddress, bindIndex);
    }

    internal void Seen(in PortAddress portAddress, in byte bindIndex)
    {
        PortAddress = portAddress;
        BIndIndex = bindIndex;
        LastSeen = DateTime.UtcNow;
    }

    internal bool Timouted()
    {
        var now = DateTime.UtcNow.AddSeconds(-30);
        return LastSeen <= now;
    }

    internal byte NewTransactionNumber()
    {
        TransactionNumber++;
        return TransactionNumber;
    }
}