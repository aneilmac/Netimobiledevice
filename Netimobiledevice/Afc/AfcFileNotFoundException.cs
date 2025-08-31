using System;

namespace Netimobiledevice.Afc
{
    public class AfcFileNotFoundException : AfcException
    {
        public AfcFileNotFoundException() : base(AfcError.ObjectNotFound)
        {
        }

        public AfcFileNotFoundException(string? message, Exception? innerException = null) :
        base(AfcError.ObjectNotFound, message, innerException)
        {
        }
    }
}
