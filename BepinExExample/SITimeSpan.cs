using Newtonsoft.Json;

namespace CrowdControl;

[Serializable, JsonConverter(typeof(Converter))]
public struct SITimeSpan :
    IEquatable<SITimeSpan>, IEquatable<TimeSpan>, IEquatable<double>,
    IComparable<SITimeSpan>, IComparable<TimeSpan>, IComparable<double>
{
    public static readonly SITimeSpan Zero = new(TimeSpan.Zero);
    public static readonly SITimeSpan MinValue = new(TimeSpan.MinValue);
    public static readonly SITimeSpan MaxValue = new(TimeSpan.MaxValue);

    private readonly TimeSpan _value;

    public static bool TryParse(string s, out SITimeSpan result)
    {
        bool r0 = TimeSpan.TryParse(s, out TimeSpan r1);
        result = new(r1);
        return r0;
    }

    public static int Compare(SITimeSpan t1, SITimeSpan t2) => TimeSpan.Compare(t1._value, t2._value);
    public static int Compare(TimeSpan t1, SITimeSpan t2) => TimeSpan.Compare(t1, t2._value);
    public static int Compare(SITimeSpan t1, TimeSpan t2) => TimeSpan.Compare(t1._value, t2);
    public static int Compare(double t1, SITimeSpan t2)
    {
        if (t1 > t2.TotalSeconds) return 1;
        return t1 < t2.TotalSeconds ? -1 : 0;
    }
    public static int Compare(SITimeSpan t1, double t2)
    {
        if (t1.TotalSeconds > t2) return 1;
        return t1.TotalSeconds < t2 ? -1 : 0;
    }

    public static bool Equals(SITimeSpan t1, SITimeSpan t2) => TimeSpan.Equals(t1._value, t2._value);
    public static bool Equals(TimeSpan t1, SITimeSpan t2) => TimeSpan.Equals(t1, t2._value);
    public static bool Equals(SITimeSpan t1, TimeSpan t2) => TimeSpan.Equals(t1._value, t2);
    public static bool Equals(double t1, SITimeSpan t2) => double.Equals(t1, (double)t2);
    public static bool Equals(SITimeSpan t1, double t2) => double.Equals((double)t1, t2);

    public static SITimeSpan FromTicks(long value) => new(TimeSpan.FromTicks(value));
    public static SITimeSpan FromMilliseconds(double value) => new(TimeSpan.FromMilliseconds(value));
    public static SITimeSpan FromSeconds(double value) => new(TimeSpan.FromSeconds(value));
    public static SITimeSpan FromMinutes(double value) => new(TimeSpan.FromMinutes(value));
    public static SITimeSpan FromHours(double value) => new(TimeSpan.FromHours(value));
    public static SITimeSpan FromDays(double value) => new(TimeSpan.FromDays(value));
    public long Ticks => _value.Ticks;

    public int Milliseconds => _value.Milliseconds;
    public int Seconds => _value.Seconds;
    public int Minutes => _value.Minutes;
    public int Hours => _value.Hours;
    public int Days => _value.Days;

    public double TotalMilliseconds => _value.TotalMilliseconds;
    public double TotalSeconds => _value.TotalSeconds;
    public double TotalMinutes => _value.TotalMinutes;
    public double TotalHours => _value.TotalHours;
    public double TotalDays => _value.TotalDays;

    public SITimeSpan Duration() => new(_value.Duration());

    public SITimeSpan Add(SITimeSpan other) => new(_value.Add(other._value));
    public SITimeSpan Subtract(SITimeSpan other) => new(_value.Subtract(other._value));
    public SITimeSpan Negate() => new(_value.Negate());

    private SITimeSpan(TimeSpan value) => _value = value;
    private SITimeSpan(double value) => _value = TimeSpan.FromSeconds(value);
    private SITimeSpan(long value) => _value = TimeSpan.FromSeconds(value);

    public SITimeSpan? NullIfZero() => (_value == TimeSpan.Zero) ? (SITimeSpan?)null : this;


    public static implicit operator SITimeSpan(double value) => new(value);

    public static implicit operator SITimeSpan?(double? value)
    {
        if (!value.HasValue) return null;
        return new(value.Value);
    }

    public static implicit operator SITimeSpan(TimeSpan value) => new(value);

    public static implicit operator SITimeSpan?(TimeSpan? value)
    {
        if (!value.HasValue) return null;
        return new(value.Value);
    }

    /*public static implicit operator SITimeSpan(Func<TimeSpan> value) => new(value);

    public static implicit operator SITimeSpan?(Func<TimeSpan>? value)
    {
        if (value == null) return null;
        return new(value);
    }

    public static implicit operator SITimeSpan(Func<SITimeSpan> value) => new(value);

    public static implicit operator SITimeSpan?(Func<SITimeSpan>? value)
    {
        if (value == null) return null;
        return new(value);
    }*/

    public static implicit operator SITimeSpan(Func<TimeSpan> value) => new(value());

    public static implicit operator SITimeSpan?(Func<TimeSpan>? value)
    {
        if (value == null) return null;
        return new(value());
    }

    public static implicit operator SITimeSpan(Func<SITimeSpan> value) => new(value()._value);

    public static implicit operator SITimeSpan?(Func<SITimeSpan>? value)
    {
        if (value == null) return null;
        return new(value()._value);
    }

    public static explicit operator double(SITimeSpan value)
        => value._value.TotalSeconds;

    public static explicit operator double?(SITimeSpan? value)
        => value?._value.TotalSeconds;

    public static explicit operator long(SITimeSpan value)
        => (long)value._value.TotalSeconds;

    public static explicit operator long?(SITimeSpan? value)
        => (long?)value?._value.TotalSeconds;

    public static explicit operator TimeSpan(SITimeSpan value)
        => value._value;

    public static explicit operator TimeSpan?(SITimeSpan? value)
        => value?._value;

    public static explicit operator Func<TimeSpan>(SITimeSpan value)
        => () => value._value;

    public static explicit operator Func<TimeSpan?>(SITimeSpan? value)
        => () => value?._value;

    public static explicit operator Func<SITimeSpan>(SITimeSpan value)
        => () => value;

    public static explicit operator Func<SITimeSpan?>(SITimeSpan? value)
        => () => value;

    public override bool Equals(object? obj)
    {
        if (obj is SITimeSpan s) return Equals(s);
        if (obj is TimeSpan t) return Equals(t);
        if (obj is double d) return Equals(d);
        return false;
    }

    public override int GetHashCode() => _value.GetHashCode();

    public bool Equals(SITimeSpan other) => _value.Equals(other._value);
    public int CompareTo(SITimeSpan other) => _value.CompareTo(other._value);
    public static bool operator ==(SITimeSpan a, SITimeSpan b) => a._value.Equals(b._value);
    public static bool operator !=(SITimeSpan a, SITimeSpan b) => !a._value.Equals(b._value);
    public static bool operator <(SITimeSpan a, SITimeSpan b) => a._value < b._value;
    public static bool operator <=(SITimeSpan a, SITimeSpan b) => a._value <= b._value;
    public static bool operator >(SITimeSpan a, SITimeSpan b) => a._value > b._value;
    public static bool operator >=(SITimeSpan a, SITimeSpan b) => a._value >= b._value;

    public bool Equals(TimeSpan other) => _value.Equals(other);
    public int CompareTo(TimeSpan other) => _value.CompareTo(other);
    public static bool operator ==(SITimeSpan a, TimeSpan b) => a.Equals(b);
    public static bool operator ==(TimeSpan a, SITimeSpan b) => b.Equals(a);
    public static bool operator !=(SITimeSpan a, TimeSpan b) => !a.Equals(b);
    public static bool operator !=(TimeSpan a, SITimeSpan b) => !b.Equals(a);
    public static bool operator <(SITimeSpan a, TimeSpan b) => a._value < b;
    public static bool operator <(TimeSpan a, SITimeSpan b) => a < b._value;
    public static bool operator <=(SITimeSpan a, TimeSpan b) => a._value <= b;
    public static bool operator <=(TimeSpan a, SITimeSpan b) => a <= b._value;
    public static bool operator >(SITimeSpan a, TimeSpan b) => a._value > b;
    public static bool operator >(TimeSpan a, SITimeSpan b) => a > b._value;
    public static bool operator >=(SITimeSpan a, TimeSpan b) => a._value >= b;
    public static bool operator >=(TimeSpan a, SITimeSpan b) => a >= b._value;

    public static SITimeSpan operator +(TimeSpan a, SITimeSpan b) => a + b._value;
    public static SITimeSpan operator -(TimeSpan a, SITimeSpan b) => a - b._value;
    public static SITimeSpan operator +(SITimeSpan a, TimeSpan b) => a._value + b;
    public static SITimeSpan operator -(SITimeSpan a, TimeSpan b) => a._value - b;
    public static SITimeSpan operator +(SITimeSpan a, SITimeSpan b) => a._value + b._value;
    public static SITimeSpan operator -(SITimeSpan a, SITimeSpan b) => a._value - b._value;

    public static DateTime operator +(DateTime a, SITimeSpan b) => a + b._value;
    public static DateTime operator -(DateTime a, SITimeSpan b) => a - b._value;
    public static DateTimeOffset operator +(DateTimeOffset a, SITimeSpan b) => a + b._value;
    public static DateTimeOffset operator -(DateTimeOffset a, SITimeSpan b) => a - b._value;

    //public bool Equals(double other) => _value.Equals(TimeSpan.FromSeconds(other));
    //public int CompareTo(double other) => _value.CompareTo(TimeSpan.FromSeconds(other));
    public bool Equals(double other) => _value.TotalSeconds.Equals(other);
    public int CompareTo(double other) => _value.TotalSeconds.CompareTo(other);
    public static bool operator ==(SITimeSpan a, double b) => a.Equals(b);
    public static bool operator ==(double a, SITimeSpan b) => b.Equals(a);
    public static bool operator !=(SITimeSpan a, double b) => !a.Equals(b);
    public static bool operator !=(double a, SITimeSpan b) => !b.Equals(a);
    public static bool operator <(SITimeSpan a, double b) => a._value.TotalSeconds < b;
    public static bool operator <(double a, SITimeSpan b) => a < b._value.TotalSeconds;
    public static bool operator <=(SITimeSpan a, double b) => a._value.TotalSeconds <= b;
    public static bool operator >=(SITimeSpan a, double b) => a._value.TotalSeconds >= b;
    public static bool operator >(SITimeSpan a, double b) => a._value.TotalSeconds > b;
    public static bool operator >(double a, SITimeSpan b) => a > b._value.TotalSeconds;
    public static bool operator <=(double a, SITimeSpan b) => a <= b._value.TotalSeconds;
    public static bool operator >=(double a, SITimeSpan b) => a >= b._value.TotalSeconds;

    private class Converter : JsonConverter<SITimeSpan>
    {
        public override void WriteJson(JsonWriter writer, SITimeSpan value, JsonSerializer serializer)
            => writer.WriteValue(value._value.TotalSeconds);

        public override SITimeSpan ReadJson(JsonReader reader, Type objectType, SITimeSpan existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.Value is TimeSpan ts) return ts;
            if (reader.Value is string st)
            {
                if (TimeSpan.TryParse(st, out TimeSpan tts)) return tts;
                if (double.TryParse(st, out double td)) return td;
            }
            return Convert.ToDouble(reader.Value);
        }
    }
}
