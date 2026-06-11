using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NetworkPresetSwitcher.Services;

internal static class PresetCsvFormat
{
    private const string PresetTypePreset = "Preset";
    private const string PresetTypeSettings = "Settings";

    internal static List<string[]> ReadCsvRows(string path, out CsvEncodingMode encodingMode)
    {
        var text = ReadCsvTextWithFallback(path, out encodingMode);
        text = NormalizeCsvDelimiters(text);
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
                continue;
            }

            if (c == ',')
            {
                row.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (c == '\r' || c == '\n')
            {
                row.Add(field.ToString());
                field.Clear();

                if (row.Any(r => !string.IsNullOrWhiteSpace(r)))
                {
                    rows.Add(row.ToArray());
                }

                row = new List<string>();

                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                continue;
            }

            field.Append(c);
        }

        row.Add(field.ToString());
        if (row.Any(r => !string.IsNullOrWhiteSpace(r)))
        {
            rows.Add(row.ToArray());
        }

        return rows;
    }

    internal static string NormalizeCsvDelimiters(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var firstLineEnd = text.IndexOfAny(new[] { '\r', '\n' });
        var firstLine = firstLineEnd >= 0 ? text[..firstLineEnd] : text;
        if (firstLine.IndexOf('\t') >= 0 && firstLine.IndexOf(',') < 0)
        {
            return ReplaceTabsOutsideQuotes(text);
        }

        return text;
    }

    internal static string ReplaceTabsOutsideQuotes(string text)
    {
        var builder = new StringBuilder(text.Length);
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    builder.Append(c);
                    builder.Append(text[i + 1]);
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                builder.Append(c);
                continue;
            }

            if (c == '\t' && !inQuotes)
            {
                builder.Append(',');
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    internal static string ReadCsvTextWithFallback(string path, out CsvEncodingMode encodingMode)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            encodingMode = CsvEncodingMode.Utf8Bom;
            return Encoding.UTF8.GetString(bytes);
        }

        try
        {
            var utf8 = new UTF8Encoding(false, true);
            encodingMode = CsvEncodingMode.Utf8;
            return utf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
        }

        encodingMode = CsvEncodingMode.Cp932Fallback;
        var cp932 = Encoding.GetEncoding(932);
        return cp932.GetString(bytes);
    }

    internal static bool LooksLikeHeader(string[] row)
    {
        if (row.Length == 0)
        {
            return false;
        }

        var set = row.Select(value => MapHeaderKey(TrimBom(value).Trim()))
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (set.Count == 0)
        {
            return false;
        }

        return (set.Contains("Type") || set.Contains("Name")) &&
               (set.Contains("IP") || set.Contains("Subnet") || set.Contains("DNS1") || set.Contains("DNS2"));
    }

    internal static Dictionary<string, int> BuildHeaderMap(string[] row)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < row.Length; i++)
        {
            var key = MapHeaderKey(TrimBom(row[i]).Trim());
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!map.ContainsKey(key))
            {
                map[key] = i;
            }
        }

        return map;
    }

    internal static string? MapHeaderKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = NormalizeHeaderKey(key);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized switch
        {
            "type" => "Type",
            "name" => "Name",
            "presetname" => "Name",
            "profile" => "Name",
            "profilename" => "Name",
            "group" => "Group",
            "category" => "Group",
            "folder" => "Group",
            "section" => "Group",
            "ip" => "IP",
            "ipaddress" => "IP",
            "ipaddr" => "IP",
            "subnet" => "Subnet",
            "subnetmask" => "Subnet",
            "mask" => "Subnet",
            "gateway" => "Gateway",
            "defaultgateway" => "Gateway",
            "gw" => "Gateway",
            "dns1" => "DNS1",
            "dnsprimary" => "DNS1",
            "primarydns" => "DNS1",
            "dnsserver1" => "DNS1",
            "dns2" => "DNS2",
            "dnssecondary" => "DNS2",
            "secondarydns" => "DNS2",
            "dnsserver2" => "DNS2",
            "comment" => "Comment",
            "memo" => "Comment",
            "note" => "Comment",
            "remarks" => "Comment",
            "language" => "Language",
            "lang" => "Language",
            _ => null
        };
    }

    internal static string NormalizeHeaderKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    internal static string GetField(string[] row, Dictionary<string, int> map, string key)
    {
        if (map.TryGetValue(key, out var index) && index >= 0 && index < row.Length)
        {
            return TrimBom(row[index]);
        }

        return string.Empty;
    }

    internal static string GetFieldTrimmed(string[] row, Dictionary<string, int> map, string key)
    {
        return GetField(row, map, key).Trim();
    }

    internal static string ToCsvLine(IEnumerable<string> fields)
    {
        return string.Join(",", fields.Select(EscapeCsv));
    }

    internal static string EscapeCsv(string value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        var needsQuote = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var sanitized = value.Replace("\"", "\"\"");
        return needsQuote ? $"\"{sanitized}\"" : sanitized;
    }

    internal static string TrimBom(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value[0] == '\uFEFF' ? value.TrimStart('\uFEFF') : value;
    }

    internal static bool IsTypeRow(string typeCandidate)
    {
        return string.Equals(typeCandidate, PresetTypePreset, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(typeCandidate, PresetTypeSettings, StringComparison.OrdinalIgnoreCase);
    }

    internal static string SafeGet(string[] row, int index)
    {
        if (index < 0 || index >= row.Length)
        {
            return string.Empty;
        }

        return TrimBom(row[index]);
    }
}

internal enum CsvEncodingMode
{
    Utf8Bom,
    Utf8,
    Cp932Fallback
}
