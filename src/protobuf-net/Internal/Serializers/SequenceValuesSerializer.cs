using ProtoBuf.Meta;
using ProtoBuf.Serializers;
using System;

namespace ProtoBuf.Internal.Serializers
{
    /// <summary>
    /// The list that is the value of a map (<see cref="RuntimeTypeModel.MapSequenceValues"/>), written as a message: <c>message Values { repeated V items = 1; }</c>.
    /// Protobuf has no <c>map&lt;K, repeated V&gt;</c>. An absent or empty message is an empty list.
    /// </summary>
    internal static class SequenceValuesSerializers
    {
        public static ISerializer<T> Get<T>() => Cache<T>.Instance;

        private static class Cache<T>
        {
            public static readonly ISerializer<T> Instance = Create();

            private static ISerializer<T> Create()
            {
                var repeated = RepeatedSerializers.TryGetRepeatedProvider(typeof(T));
                if (repeated is null) throw new InvalidOperationException(typeof(T).FullName + " is not a list, it cannot be the value of a map as a message");
                return (ISerializer<T>)Activator.CreateInstance(typeof(SequenceValuesSerializer<,>).MakeGenericType(typeof(T), repeated.ItemType), nonPublic: true);
            }
        }
    }

    internal sealed class SequenceValuesSerializer<TCollection, TItem> : ISerializer<TCollection>
    {
        SerializerFeatures ISerializer<TCollection>.Features => SerializerFeatures.WireTypeString | SerializerFeatures.CategoryMessage;

        private static RepeatedSerializer<TCollection, TItem> Repeated
            => (RepeatedSerializer<TCollection, TItem>)RepeatedSerializers.TryGetRepeatedProvider(typeof(TCollection)).Serializer;

        public TCollection Read(ref ProtoReader.State state, TCollection value)
        {
            var repeated = Repeated;
            // an absent value or an empty message is an empty list (never null)
            if (value is null) value = ((IFactory<TCollection>)repeated).Create(state.Context);
            int field;
            while ((field = state.ReadFieldHeader()) > 0)
            {
                if (field == 1)
                {
                    value = repeated.ReadRepeated(ref state, default, value);
                }
                else
                {
                    state.SkipField();
                }
            }
            return value;
        }

        public void Write(ref ProtoWriter.State state, TCollection value)
            // not packed: the same bytes as the same list written as a member of a message (the kit's lists are not packed either)
            => Repeated.WriteRepeated(ref state, 1, SerializerFeatures.OptionPackedDisabled, value);
    }
}
