using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.IO;
using System.Threading;
using Xunit;

namespace ProtoBuf.Test.Issues
{
    public class AllowSurrogateForBuiltInTypes
    {
        [Fact]
        public void BuiltInSurrogatesAreBlockedByDefault()
        {
            var model = RuntimeTypeModel.Create();
            Assert.False(model.AllowSurrogateForBuiltInTypes);

            _ = Assert.Throws<ArgumentException>(() => model[typeof(DateTime)].SetSurrogate(typeof(DateTimeSurrogate)));
            _ = Assert.Throws<ArgumentException>(() => model[typeof(Guid)].SetSurrogate(typeof(GuidSurrogate)));
        }

        [Fact]
        public void BuiltInBehaviorIsPreservedUntilSurrogateIsRegistered()
        {
            var baseline = RuntimeTypeModel.Create();
            var withFlag = RuntimeTypeModel.Create();
            withFlag.AllowSurrogateForBuiltInTypes = true;

            // proving that explicit meta-type registration alone does not override built-in handling
            _ = withFlag[typeof(DateTime)];
            _ = withFlag[typeof(Guid)];

            var value = new BuiltInPayload
            {
                When = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1234),
                Id = Guid.NewGuid(),
            };

            Assert.Equal(ToHex(Serialize(baseline, value)), ToHex(Serialize(withFlag, value)));
            Assert.Equal(value.When, withFlag.DeepClone(value.When));
            Assert.Equal(value.Id, withFlag.DeepClone(value.Id));
        }

        [Fact]
        public void CanOverrideDateTimeAndGuidWithSurrogates()
        {
            var model = RuntimeTypeModel.Create();
            model.AllowSurrogateForBuiltInTypes = true;
            DateTimeSurrogate.Reset();
            GuidSurrogate.Reset();

            model[typeof(DateTime)].SetSurrogate(typeof(DateTimeSurrogate));
            model[typeof(Guid)].SetSurrogate(typeof(GuidSurrogate));

            var value = new BuiltInPayload
            {
                When = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1234),
                Id = Guid.NewGuid(),
            };

            var clone = model.DeepClone(value);

            Assert.Equal(value.When, clone.When);
            Assert.Equal(value.Id, clone.Id);
            Assert.True(DateTimeSurrogate.ToSurrogateCount > 0);
            Assert.True(DateTimeSurrogate.ToModelCount > 0);
            Assert.True(GuidSurrogate.ToSurrogateCount > 0);
            Assert.True(GuidSurrogate.ToModelCount > 0);
        }

        private static byte[] Serialize<T>(TypeModel model, T value)
        {
            using var ms = new MemoryStream();
            model.Serialize(ms, value);
            return ms.ToArray();
        }

        private static string ToHex(byte[] value) => BitConverter.ToString(value);

        [ProtoContract]
        public sealed class BuiltInPayload
        {
            [ProtoMember(1)]
            public DateTime When { get; set; }

            [ProtoMember(2)]
            public Guid Id { get; set; }
        }

        [ProtoContract]
        public struct DateTimeSurrogate
        {
            internal static int ToSurrogateCount;
            internal static int ToModelCount;

            [ProtoMember(1)]
            public long BinaryValue { get; set; }

            internal static void Reset()
            {
                ToSurrogateCount = 0;
                ToModelCount = 0;
            }

            public static implicit operator DateTimeSurrogate(DateTime value)
            {
                Interlocked.Increment(ref ToSurrogateCount);
                return new DateTimeSurrogate { BinaryValue = value.ToBinary() };
            }

            public static implicit operator DateTime(DateTimeSurrogate value)
            {
                Interlocked.Increment(ref ToModelCount);
                return DateTime.FromBinary(value.BinaryValue);
            }
        }

        [ProtoContract]
        public struct GuidSurrogate
        {
            internal static int ToSurrogateCount;
            internal static int ToModelCount;

            [ProtoMember(1)]
            public string Value { get; set; }

            internal static void Reset()
            {
                ToSurrogateCount = 0;
                ToModelCount = 0;
            }

            public static implicit operator GuidSurrogate(Guid value)
            {
                Interlocked.Increment(ref ToSurrogateCount);
                return new GuidSurrogate { Value = value.ToString("N") };
            }

            public static implicit operator Guid(GuidSurrogate value)
            {
                Interlocked.Increment(ref ToModelCount);
                return string.IsNullOrWhiteSpace(value.Value) ? Guid.Empty : Guid.ParseExact(value.Value, "N");
            }
        }
    }
}
