using System;

namespace Popcron.Intercom
{
    public class NoReadAccessException : Exception
    {
        public NoReadAccessException(string message) : base(message)
        {

        }
    }
}
