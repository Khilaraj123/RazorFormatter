using System;

namespace RazorFormatter.Core
{
    public class RazorFormatterInternalException : Exception
    {
        public RazorFormatterInternalException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}

