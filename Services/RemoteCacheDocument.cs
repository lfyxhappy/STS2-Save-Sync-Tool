using System.Globalization;
using System.IO;
using System.Text;

namespace Sts2SaveSyncTool.Services;

internal sealed class RemoteCacheDocument
{
    private readonly VdfDocument _document;

    private RemoteCacheDocument(string filePath, VdfDocument document)
    {
        FilePath = filePath;
        _document = document;
    }

    public string FilePath { get; }

    public static RemoteCacheDocument Load(string filePath)
    {
        string text = File.ReadAllText(filePath);
        VdfDocument document = VdfDocument.Parse(text);

        if (!string.Equals(document.RootName, "2868840", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{filePath} 的根节点不是 2868840。");
        }

        return new RemoteCacheDocument(filePath, document);
    }

    public bool TryGetEntry(string relativePath, out RemoteCacheEntry? entry)
    {
        if (_document.RootObject.TryGetObject(relativePath, out VdfObject? value) && value is not null)
        {
            entry = new RemoteCacheEntry(relativePath, value);
            return true;
        }

        entry = null;
        return false;
    }

    public RemoteCacheEntry GetOrAddEntry(string relativePath)
    {
        VdfObject value = _document.RootObject.GetOrAddObject(relativePath);
        return new RemoteCacheEntry(relativePath, value);
    }

    public byte[] ToBytes()
    {
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(_document.Serialize());
    }
}

internal sealed class RemoteCacheEntry
{
    private readonly VdfObject _value;

    public RemoteCacheEntry(string relativePath, VdfObject value)
    {
        RelativePath = relativePath;
        _value = value;
    }

    public string RelativePath { get; }

    public long? Size => ParseLong("size");

    public string? Sha1 => _value.GetString("sha");

    public long? LocalTime => ParseLong("localtime");

    public long? Time => ParseLong("time");

    public long? RemoteTime => ParseLong("remotetime");

    public void ApplyMetadata(long size, string sha1, long timestamp)
    {
        EnsureScalar("root", "0");
        EnsureScalar("syncstate", "1");
        EnsureScalar("persiststate", "0");
        EnsureScalar("platformstosync2", "-1");

        _value.SetString("size", size.ToString(CultureInfo.InvariantCulture));
        _value.SetString("sha", sha1.ToLowerInvariant());
        _value.SetString("localtime", timestamp.ToString(CultureInfo.InvariantCulture));
        _value.SetString("time", timestamp.ToString(CultureInfo.InvariantCulture));
        _value.SetString("remotetime", timestamp.ToString(CultureInfo.InvariantCulture));
    }

    private void EnsureScalar(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(_value.GetString(key)))
        {
            _value.SetString(key, value);
        }
    }

    private long? ParseLong(string key)
    {
        string? value = _value.GetString(key);
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
            ? parsed
            : null;
    }
}

internal sealed class VdfDocument
{
    internal VdfDocument(string rootName, VdfObject rootObject)
    {
        RootName = rootName;
        RootObject = rootObject;
    }

    public string RootName { get; }

    public VdfObject RootObject { get; }

    public static VdfDocument Parse(string text)
    {
        VdfParser parser = new(text);
        return parser.Parse();
    }

    public string Serialize()
    {
        StringBuilder builder = new();
        WriteQuoted(builder, RootName);
        builder.AppendLine();
        WriteObject(builder, RootObject, 0);
        builder.AppendLine();
        return builder.ToString();
    }

    private static void WriteObject(StringBuilder builder, VdfObject value, int indentLevel)
    {
        WriteIndent(builder, indentLevel);
        builder.AppendLine("{");

        foreach ((string key, VdfValue entryValue) in value.Entries)
        {
            WriteIndent(builder, indentLevel + 1);
            WriteQuoted(builder, key);

            if (entryValue is VdfString stringValue)
            {
                builder.Append('\t').Append('\t');
                WriteQuoted(builder, stringValue.Value);
                builder.AppendLine();
                continue;
            }

            builder.AppendLine();
            WriteObject(builder, (VdfObject)entryValue, indentLevel + 1);
        }

        WriteIndent(builder, indentLevel);
        builder.Append('}');
    }

    private static void WriteIndent(StringBuilder builder, int indentLevel)
    {
        for (int i = 0; i < indentLevel; i++)
        {
            builder.Append('\t');
        }
    }

    private static void WriteQuoted(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (char ch in value)
        {
            if (ch is '"' or '\\')
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        builder.Append('"');
    }
}

internal abstract class VdfValue;

internal sealed class VdfString : VdfValue
{
    public VdfString(string value)
    {
        Value = value;
    }

    public string Value { get; set; }
}

internal sealed class VdfObject : VdfValue
{
    private readonly List<KeyValuePair<string, VdfValue>> _entries = [];

    public IReadOnlyList<KeyValuePair<string, VdfValue>> Entries => _entries;

    public void Add(string key, VdfValue value)
    {
        _entries.Add(new KeyValuePair<string, VdfValue>(key, value));
    }

    public string? GetString(string key)
    {
        if (TryGetValue(key, out VdfValue? value) && value is VdfString stringValue)
        {
            return stringValue.Value;
        }

        return null;
    }

    public bool TryGetObject(string key, out VdfObject? value)
    {
        if (TryGetValue(key, out VdfValue? existing) && existing is VdfObject objectValue)
        {
            value = objectValue;
            return true;
        }

        value = null;
        return false;
    }

    public VdfObject GetOrAddObject(string key)
    {
        if (TryGetObject(key, out VdfObject? value) && value is not null)
        {
            return value;
        }

        VdfObject created = new();
        SetValue(key, created);
        return created;
    }

    public void SetString(string key, string value)
    {
        SetValue(key, new VdfString(value));
    }

    private bool TryGetValue(string key, out VdfValue? value)
    {
        foreach ((string existingKey, VdfValue existingValue) in _entries)
        {
            if (string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase))
            {
                value = existingValue;
                return true;
            }
        }

        value = null;
        return false;
    }

    private void SetValue(string key, VdfValue value)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (string.Equals(_entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                _entries[i] = new KeyValuePair<string, VdfValue>(key, value);
                return;
            }
        }

        _entries.Add(new KeyValuePair<string, VdfValue>(key, value));
    }
}

internal sealed class VdfParser
{
    private readonly string _text;
    private int _index;

    public VdfParser(string text)
    {
        _text = text;
    }

    public VdfDocument Parse()
    {
        SkipTrivia();
        string rootName = ReadQuotedString();
        SkipTrivia();
        VdfObject rootObject = ReadObject();
        SkipTrivia();

        if (_index != _text.Length)
        {
            throw new InvalidOperationException("VDF 末尾存在未解析内容。");
        }

        return new VdfDocument(rootName, rootObject);
    }

    private VdfObject ReadObject()
    {
        Expect('{');
        VdfObject value = new();

        while (true)
        {
            SkipTrivia();
            if (TryConsume('}'))
            {
                return value;
            }

            string key = ReadQuotedString();
            SkipTrivia();

            if (Peek() == '{')
            {
                value.Add(key, ReadObject());
            }
            else
            {
                value.Add(key, new VdfString(ReadQuotedString()));
            }
        }
    }

    private string ReadQuotedString()
    {
        SkipTrivia();
        Expect('"');
        StringBuilder builder = new();

        while (_index < _text.Length)
        {
            char current = _text[_index++];
            if (current == '\\')
            {
                if (_index >= _text.Length)
                {
                    throw new InvalidOperationException("VDF 字符串转义不完整。");
                }

                builder.Append(_text[_index++]);
                continue;
            }

            if (current == '"')
            {
                return builder.ToString();
            }

            builder.Append(current);
        }

        throw new InvalidOperationException("VDF 字符串缺少结束引号。");
    }

    private void SkipTrivia()
    {
        while (_index < _text.Length)
        {
            if (char.IsWhiteSpace(_text[_index]))
            {
                _index++;
                continue;
            }

            if (_text[_index] == '/' && _index + 1 < _text.Length && _text[_index + 1] == '/')
            {
                _index += 2;
                while (_index < _text.Length && _text[_index] is not '\r' and not '\n')
                {
                    _index++;
                }

                continue;
            }

            break;
        }
    }

    private char Peek()
    {
        if (_index >= _text.Length)
        {
            return '\0';
        }

        return _text[_index];
    }

    private bool TryConsume(char expected)
    {
        if (Peek() != expected)
        {
            return false;
        }

        _index++;
        return true;
    }

    private void Expect(char expected)
    {
        if (!TryConsume(expected))
        {
            throw new InvalidOperationException($"VDF 解析失败，预期字符 {expected}。");
        }
    }
}
