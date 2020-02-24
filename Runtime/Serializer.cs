using System;

namespace Popcron.Intercom
{
    [Serializable]
    public abstract class Serializer
    {
        public abstract object Deserialize(byte[] data);
        public abstract byte[] Serialize(object value);
    }
}