using System;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace Jarvis.Framework.Shared.Domain.Serialization
{
    public class StringValueBsonSerializer : IBsonSerializer
    {
        public StringValueBsonSerializer(Type t)
        {
            ValueType = t;
        }

        public object Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var bsonReader = context.Reader;
            var nominalType = args.NominalType;
            if (bsonReader.CurrentBsonType == BsonType.Null)
            {
                bsonReader.ReadNull();
                return null;
            }

            var id = bsonReader.ReadString();
            return Activator.CreateInstance(nominalType, new object[] {id});
        }

        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
            var bsonWriter = context.Writer;
            if (value == null)
            {
                bsonWriter.WriteNull();
            }
            else
            {
                bsonWriter.WriteString((StringValue)value);
            }
        }

        public Type ValueType { get; }
    }

    public class StringValueBsonSerializer<T> : StringValueBsonSerializer
    {
        public StringValueBsonSerializer() : base(typeof(T))
        {
        }
    }
}
