using Netimobiledevice.Exceptions;
using System;

namespace Netimobiledevice.Afc;

public class AfcException : NetimobiledeviceException
{
    public AfcError AfcError { get; } = AfcError.UnknownError;

    public AfcException(AfcError afcError, string? message, Exception? innerException = null) :
    base(message, innerException)
    {
        AfcError = afcError;
    }

    public AfcException(AfcError afcError) :
    this(afcError, null, null)
    {
    }

    public AfcException() : base() { }

    public AfcException(AfcError afcError, Exception? innerException) :
    this(afcError, $"{afcError}", innerException)
    {
    }

    public AfcException(string? message, Exception? inner) : base(message, inner) { }

    public AfcException(string? message) : base(message) { }

}
