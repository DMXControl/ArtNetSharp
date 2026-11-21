using org.dmxc.wkdt.Light.RDM;
using RDMSharp;
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
            onPropertyChanged(nameof(PortAddress));
        }
    }
    public byte BindIndex
    {
        get { return bindIndex; }
        private set
        {
            if (value == bindIndex)
                return;
            bindIndex = value;
            onPropertyChanged(nameof(BindIndex)); // was reported as PortAddress
        }
    }

    /// <summary>Event queue of the owning instance (null if created standalone).</summary>
    internal EventDispatcher Events { get; set; }
    private void onPropertyChanged(string propertyName)
    {
        var args = new PropertyChangedEventArgs(propertyName);
        // Was a plain Invoke: an exception in an application handler propagated into the receive path.
        EventDispatcher.Raise(Events, () => PropertyChanged?.InvokeFailSafe(this, args));
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
        BindIndex = bindIndex;
        LastSeen = DateTime.UtcNow;
    }

    /// <summary>A UID not seen for this long counts as gone. Settable for tests only.</summary>
    internal static TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    internal bool Timouted()
    {
        return LastSeen <= DateTime.UtcNow - Timeout;
    }

    internal byte NewTransactionNumber()
    {
        TransactionNumber++;
        return TransactionNumber;
    }
}