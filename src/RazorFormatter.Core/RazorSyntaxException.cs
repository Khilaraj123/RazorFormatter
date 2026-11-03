using System;

namespace RazorFormatter.Core
{
    public class RazorSyntaxException : Exception
    {
        public int Offset { get; }
        public int Line { get; }
        public int Column { get; }

        public RazorSyntaxException(string message, int offset = 0, int line = 0, int column = 0, Exception? innerException = null)
            : base(message, innerException)
        {
            Offset = offset;
            Line = line;
            Column = column;
        }
    }
}

