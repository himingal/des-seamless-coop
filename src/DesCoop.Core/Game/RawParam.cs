using System.Buffers.Binary;
using System.Reflection;
using SoulsFormats;
using static SoulsFormats.PARAMDEF;

namespace DesCoop.Game;

/// <summary>
/// Byte-level view of a PARAM: reads/writes single fields in place, leaving every other byte of the
/// file untouched (SoulsFormats' full re-serialization is not byte-identical for Demon's Souls).
/// Field offsets come from the game's own paramdef when available, else from a built-in table.
/// </summary>
public sealed class RawParam
{
    static readonly FieldInfo RowDataOffset =
        typeof(PARAM.Row).GetField("DataOffset", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new MissingFieldException("PARAM.Row.DataOffset");

    public byte[] Bytes { get; }
    public string ParamType { get; }
    public bool BigEndian { get; }
    public int RowSize { get; }
    readonly Dictionary<int, long> _rows = [];
    readonly Dictionary<string, (int offset, DefType type)> _fields;

    public IEnumerable<int> RowIds => _rows.Keys;
    public bool Has(int id) => _rows.ContainsKey(id);
    public bool HasField(string name) => _fields.ContainsKey(name);

    public RawParam(byte[] bytes, Dictionary<string, (int, DefType)> fields)
    {
        Bytes = bytes.ToArray();
        var p = PARAM.Read(Bytes);
        ParamType = p.ParamType;
        BigEndian = p.BigEndian;
        foreach (var r in p.Rows) _rows[r.ID] = (long)RowDataOffset.GetValue(r)!;
        _fields = fields;
        var offs = _rows.Values.Where(o => o > 0).Distinct().OrderBy(o => o).ToList();
        RowSize = offs.Count > 1 ? (int)offs.Zip(offs.Skip(1), (a, b) => b - a).Min() : 0;
        foreach (var (name, (off, type)) in fields)
            if (RowSize > 0 && off + SizeOf(type) > RowSize)
                throw new InvalidDataException($"{ParamType}.{name} at +{off} is outside the {RowSize}-byte row.");
    }

    public static int SizeOf(DefType t) => t switch
    {
        DefType.s8 or DefType.u8 or DefType.dummy8 or DefType.fixstr => 1,
        DefType.s16 or DefType.u16 or DefType.fixstrW => 2,
        DefType.f64 => 8,
        _ => 4,
    };

    /// <summary>Byte offset of every field in a paramdef (handles arrays and packed bitfields).</summary>
    public static Dictionary<string, (int, DefType)> Offsets(PARAMDEF def)
    {
        var map = new Dictionary<string, (int, DefType)>();
        int off = 0, bitsUsed = 0, bitUnit = 0;
        foreach (var f in def.Fields)
        {
            int size = SizeOf(f.DisplayType);
            if (f.BitSize > 0 && f.DisplayType != DefType.dummy8)
            {
                int unitBits = size * 8;
                if (bitUnit != unitBits || bitsUsed + f.BitSize > unitBits) { if (bitUnit > 0) off += bitUnit / 8; bitUnit = unitBits; bitsUsed = 0; }
                bitsUsed += f.BitSize;
                continue; // bitfields are not addressable by this helper
            }
            if (bitUnit > 0) { off += bitUnit / 8; bitUnit = 0; bitsUsed = 0; }
            if (f.DisplayType == DefType.dummy8 && f.BitSize > 0) { off += (f.BitSize + 7) / 8; continue; }
            map[f.InternalName] = (off, f.DisplayType);
            off += size * Math.Max(1, f.ArrayLength);
        }
        return map;
    }

    long Pos(int id, string field, out DefType type)
    {
        if (!_rows.TryGetValue(id, out var row)) throw new KeyNotFoundException($"{ParamType} has no row {id}");
        if (!_fields.TryGetValue(field, out var f)) throw new KeyNotFoundException($"{ParamType} has no field {field}");
        type = f.type;
        return row + f.offset;
    }

    public double Get(int id, string field)
    {
        long p = Pos(id, field, out var t);
        var s = Bytes.AsSpan((int)p);
        return t switch
        {
            DefType.s8 => (sbyte)s[0],
            DefType.u8 => s[0],
            DefType.s16 => BigEndian ? BinaryPrimitives.ReadInt16BigEndian(s) : BinaryPrimitives.ReadInt16LittleEndian(s),
            DefType.u16 => BigEndian ? BinaryPrimitives.ReadUInt16BigEndian(s) : BinaryPrimitives.ReadUInt16LittleEndian(s),
            DefType.s32 => BigEndian ? BinaryPrimitives.ReadInt32BigEndian(s) : BinaryPrimitives.ReadInt32LittleEndian(s),
            DefType.u32 => BigEndian ? BinaryPrimitives.ReadUInt32BigEndian(s) : BinaryPrimitives.ReadUInt32LittleEndian(s),
            DefType.f32 => BigEndian ? BinaryPrimitives.ReadSingleBigEndian(s) : BinaryPrimitives.ReadSingleLittleEndian(s),
            _ => throw new NotSupportedException(t.ToString()),
        };
    }

    public int GetInt(int id, string field) => (int)Get(id, field);

    /// <summary>Writes a value; returns true when the bytes actually changed.</summary>
    public bool Set(int id, string field, double value)
    {
        long p = Pos(id, field, out var t);
        var s = Bytes.AsSpan((int)p, SizeOf(t));
        var before = s.ToArray();
        switch (t)
        {
            case DefType.s8: s[0] = (byte)(sbyte)Math.Clamp(value, sbyte.MinValue, sbyte.MaxValue); break;
            case DefType.u8: s[0] = (byte)Math.Clamp(value, 0, byte.MaxValue); break;
            case DefType.s16: var v16 = (short)Math.Clamp(value, short.MinValue, short.MaxValue); if (BigEndian) BinaryPrimitives.WriteInt16BigEndian(s, v16); else BinaryPrimitives.WriteInt16LittleEndian(s, v16); break;
            case DefType.u16: var u16 = (ushort)Math.Clamp(value, 0, ushort.MaxValue); if (BigEndian) BinaryPrimitives.WriteUInt16BigEndian(s, u16); else BinaryPrimitives.WriteUInt16LittleEndian(s, u16); break;
            case DefType.s32: var v32 = (int)Math.Clamp(value, int.MinValue, int.MaxValue); if (BigEndian) BinaryPrimitives.WriteInt32BigEndian(s, v32); else BinaryPrimitives.WriteInt32LittleEndian(s, v32); break;
            case DefType.u32: var u32 = (uint)Math.Clamp(value, 0, uint.MaxValue); if (BigEndian) BinaryPrimitives.WriteUInt32BigEndian(s, u32); else BinaryPrimitives.WriteUInt32LittleEndian(s, u32); break;
            case DefType.f32: if (BigEndian) BinaryPrimitives.WriteSingleBigEndian(s, (float)value); else BinaryPrimitives.WriteSingleLittleEndian(s, (float)value); break;
            default: throw new NotSupportedException(t.ToString());
        }
        return !s.SequenceEqual(before);
    }
}
