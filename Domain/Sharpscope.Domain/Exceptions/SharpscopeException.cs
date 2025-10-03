using System;
using System.Runtime.Serialization;

namespace Sharpscope.Domain.Exceptions;

/// <summary>
/// Base domain exception for Sharpscope.
/// Use this to signal domain-level errors (invalid model, calculation issues, etc.).
/// </summary>
[Serializable]
public class SharpscopeException : Exception
{
    #region Constructors

    public SharpscopeException() { }

    public SharpscopeException(string message)
        : base(message) { }

    public SharpscopeException(string message, Exception innerException)
        : base(message, innerException) { }

    protected SharpscopeException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }

    #endregion
}
