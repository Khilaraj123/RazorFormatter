using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RazorFormatter.Core;
using RazorFormatter.Core.Abstractions;
using RazorFormatter.Core.Configuration;
using RazorFormatter.Core.Enums;

namespace RazorFormatter.Cli.Server
{
    public sealed class ServerRequest
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public ServerParams? Params { get; set; }
    }

    public sealed class ServerParams
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("filePath")]
        public string? FilePath { get; set; }

        [JsonPropertyName("startOffset")]
        public int? StartOffset { get; set; }

        [JsonPropertyName("endOffset")]
        public int? EndOffset { get; set; }

        [JsonPropertyName("indentSize")]
        public int? IndentSize { get; set; }

        [JsonPropertyName("indentStyle")]
        public string? IndentStyle { get; set; }

        [JsonPropertyName("endOfLine")]
        public string? EndOfLine { get; set; }

        [JsonPropertyName("attributeLayout")]
        public string? AttributeLayout { get; set; }
    }

    public sealed class ServerResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("result")]
        public object? Result { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    public sealed class ServerFormatResult
    {
        [JsonPropertyName("formattedText")]
        public string FormattedText { get; set; } = string.Empty;

        [JsonPropertyName("wasSkipped")]
        public bool WasSkipped { get; set; }

        [JsonPropertyName("changes")]
        public List<ServerTextChange> Changes { get; set; } = new();

        [JsonPropertyName("diagnostics")]
        public List<ServerDiagnostic> Diagnostics { get; set; } = new();
    }

    public sealed class ServerTextChange
    {
        [JsonPropertyName("start")]
        public int Start { get; set; }

        [JsonPropertyName("end")]
        public int End { get; set; }

        [JsonPropertyName("newText")]
        public string NewText { get; set; } = string.Empty;
    }

    public sealed class ServerDiagnostic
    {
        [JsonPropertyName("severity")]
        public string Severity { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("line")]
        public int Line { get; set; }

        [JsonPropertyName("column")]
        public int Column { get; set; }
    }

    public sealed class FormatterServer
    {
        private readonly IRazorFormatter _engine = RazorFormatterFactory.Create();

        public int Run()
        {
            // Server handshake message on stdout
            Console.Out.WriteLine("{\"jsonrpc\":\"2.0\",\"ready\":true}");
            Console.Out.Flush();

            string? line;
            while ((line = Console.ReadLine()) != null)
            {
                var trimmed = line.Trim().Trim('\uFEFF');
                if (trimmed.Length == 0) continue;

                ServerRequest? req = null;
                try
                {
                    req = JsonSerializer.Deserialize<ServerRequest>(trimmed);
                }
                catch (Exception ex)
                {
                    SendResponse(new ServerResponse { Id = -1, Error = $"Malformed JSON: {ex.Message}" });
                    continue;
                }

                if (req == null) continue;

                if (string.Equals(req.Method, "shutdown", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(req.Method, "exit", StringComparison.OrdinalIgnoreCase))
                {
                    SendResponse(new ServerResponse { Id = req.Id, Result = "ok" });
                    break;
                }

                if (string.Equals(req.Method, "ping", StringComparison.OrdinalIgnoreCase))
                {
                    SendResponse(new ServerResponse { Id = req.Id, Result = "pong" });
                    continue;
                }

                try
                {
                    if (string.Equals(req.Method, "format", StringComparison.OrdinalIgnoreCase))
                    {
                        var res = HandleFormat(req.Params);
                        SendResponse(new ServerResponse { Id = req.Id, Result = res });
                    }
                    else if (string.Equals(req.Method, "formatRange", StringComparison.OrdinalIgnoreCase))
                    {
                        var res = HandleFormatRange(req.Params);
                        SendResponse(new ServerResponse { Id = req.Id, Result = res });
                    }
                    else
                    {
                        SendResponse(new ServerResponse { Id = req.Id, Error = $"Unknown method: {req.Method}" });
                    }
                }
                catch (Exception ex)
                {
                    SendResponse(new ServerResponse { Id = req.Id, Error = ex.ToString() });
                }
            }

            return 0;
        }

        private ServerFormatResult HandleFormat(ServerParams? p)
        {
            var text = p?.Text ?? string.Empty;
            var options = BuildOptions(p);

            var result = _engine.Format(text, options);
            return ToServerResult(result);
        }

        private ServerFormatResult HandleFormatRange(ServerParams? p)
        {
            var text = p?.Text ?? string.Empty;
            var start = p?.StartOffset ?? 0;
            var end = p?.EndOffset ?? text.Length;
            var options = BuildOptions(p);

            var result = _engine.FormatRange(text, start, end, options);
            return ToServerResult(result);
        }

        private RazorFormatterOptions BuildOptions(ServerParams? p)
        {
            var options = new RazorFormatterOptions();

            if (!string.IsNullOrEmpty(p?.FilePath))
            {
                options.FilePath = p.FilePath;
            }

            if (p?.IndentSize.HasValue == true)
            {
                options.IndentSize = p.IndentSize.Value;
            }

            if (!string.IsNullOrEmpty(p?.IndentStyle))
            {
                if (string.Equals(p.IndentStyle, "tab", StringComparison.OrdinalIgnoreCase))
                    options.IndentStyle = IndentStyle.Tab;
                else
                    options.IndentStyle = IndentStyle.Space;
            }

            if (!string.IsNullOrEmpty(p?.EndOfLine))
            {
                if (string.Equals(p.EndOfLine, "lf", StringComparison.OrdinalIgnoreCase))
                    options.EndOfLine = EndOfLine.Lf;
                else if (string.Equals(p.EndOfLine, "crlf", StringComparison.OrdinalIgnoreCase))
                    options.EndOfLine = EndOfLine.Crlf;
            }

            if (!string.IsNullOrEmpty(p?.AttributeLayout))
            {
                var lower = p.AttributeLayout.ToLowerInvariant().Replace("-", "_");
                if (lower is "one_per_line" or "oneperline")
                    options.AttributeLayout = AttributeLayout.OnePerLine;
                else if (lower is "single_line" or "singleline")
                    options.AttributeLayout = AttributeLayout.SingleLine;
                else if (lower == "preserve")
                    options.AttributeLayout = AttributeLayout.Preserve;
            }

            return options;
        }

        private static ServerFormatResult ToServerResult(FormatResult result)
        {
            var sResult = new ServerFormatResult
            {
                FormattedText = result.FormattedText,
                WasSkipped = result.WasSkipped,
            };

            foreach (var ch in result.Changes)
            {
                sResult.Changes.Add(new ServerTextChange
                {
                    Start = ch.Start,
                    End = ch.End,
                    NewText = ch.NewText,
                });
            }

            foreach (var diag in result.Diagnostics)
            {
                sResult.Diagnostics.Add(new ServerDiagnostic
                {
                    Severity = diag.Severity.ToString(),
                    Message = diag.Message,
                    Offset = diag.Offset,
                    Line = diag.Line,
                    Column = diag.Column,
                });
            }

            return sResult;
        }

        private static void SendResponse(ServerResponse res)
        {
            var json = JsonSerializer.Serialize(res);
            Console.Out.WriteLine(json);
            Console.Out.Flush();
        }
    }
}
