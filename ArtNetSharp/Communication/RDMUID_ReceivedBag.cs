using RDMSharp;
using System;

namespace ArtNetSharp.Communication
{
    public sealed class RDMUID_ReceivedBag
    {
        public readonly RDMUID Uid;
        public DateTime LastSeen { get; private set; }

        public byte TransactionNumber { get; private set; } = byte.MaxValue;

        public RDMUID_ReceivedBag(in RDMUID uid)
        {
            Uid = uid;
            Seen();
        }

        internal void Seen()
        {
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
}