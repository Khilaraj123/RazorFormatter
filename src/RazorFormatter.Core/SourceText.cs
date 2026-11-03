using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace RazorFormatter.Core
{
    public sealed class SourceText
    {
        private readonly string _text;
        private readonly int[] _lineStarts;

        public SourceText(string text)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _lineStarts = ComputeLineStarts(text);
        }

        public int Length => _text.Length;
        public int LineCount => _lineStarts.Length;
        public string ToString(int start, int length) => _text.Substring(start, length);
        public char this[int offset] => offset >= 0 && offset < _text.Length ? _text[offset] : '\0';
        public string GetText() => _text;

        public int GetLineFromOffset(int offset)
        {
            if (offset < 0) return 0;
            if (offset >= _text.Length) return _lineStarts.Length - 1;
            var idx = Array.BinarySearch(_lineStarts, offset);
            return idx < 0 ? ~idx - 1 : idx;
        }

        public (int Line, int Column) GetLineColumn(int offset)
        {
            var line = GetLineFromOffset(offset);
            return (line, offset - _lineStarts[line]);
        }

        public (int Line, int Column) GetSequentialLineColumn(int offset, ref int lastLine)
        {
            if (offset < 0) return (0, 0);
            if (offset >= _text.Length) return (_lineStarts.Length - 1, offset - _lineStarts[_lineStarts.Length - 1]);

            var line = lastLine;
            if (line < 0 || line >= _lineStarts.Length) line = 0;

            while (line + 1 < _lineStarts.Length && _lineStarts[line + 1] <= offset)
            {
                line++;
            }

            while (line > 0 && _lineStarts[line] > offset)
            {
                line--;
            }

            lastLine = line;
            return (line, offset - _lineStarts[line]);
        }

        public int GetOffset(int line, int column)
        {
            if (line < 0 || line >= _lineStarts.Length) return -1;
            return _lineStarts[line] + column;
        }

        public string GetLineText(int line)
        {
            if (line < 0 || line >= _lineStarts.Length) return string.Empty;
            var start = _lineStarts[line];
            var end = line + 1 < _lineStarts.Length ? _lineStarts[line + 1] : _text.Length;
            if (end > start && (_text[end - 1] == '\n' || _text[end - 1] == '\r'))
            {
                end--;
                if (end > start && _text[end - 1] == '\r') end--;
            }
            return _text.Substring(start, end - start);
        }

        public string DetectLineEnding()
        {
            var crlf = 0;
            var lf = 0;
            for (var i = 0; i < _text.Length; i++)
            {
                if (_text[i] == '\n') lf++;
                else if (_text[i] == '\r' && i + 1 < _text.Length && _text[i + 1] == '\n') crlf++;
            }
            return crlf >= lf ? "\r\n" : "\n";
        }

        private static int[] ComputeLineStarts(string text)
        {
            if (text.Length == 0) return new[] { 0 };
            var starts = new List<int>(Math.Max(8, text.Length / 64)) { 0 };
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\n') starts.Add(i + 1);
                else if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    starts.Add(i + 2);
                    i++;
                }
                else if (c == '\r') starts.Add(i + 1);
            }
            return starts.ToArray();
        }
    }
}
