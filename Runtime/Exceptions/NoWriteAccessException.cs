using System;

namespace Popcron.Intercom
{
    public class NoWriteAccessException : Exception
    {
        public NoWriteAccessException(string message) : base(message)
        {

        }
    }
}
