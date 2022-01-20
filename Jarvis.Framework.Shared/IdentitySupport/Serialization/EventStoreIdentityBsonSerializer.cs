using System;
using Jarvis.NEventStoreEx.CommonDomainEx;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace Jarvis.Framework.Shared.IdentitySupport.Serialization
{
    public class EventStoreIdentityBsonSerializer: IBsonSerializer 
    {
        public static IIdentityConverter IdentityConverter { get; set; }


        public object Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var bsonReader = context.Reader;
            if (bsonReader.CurrentBsonType == BsonType.Null)
            {
                bsonReader.ReadNull();
                return null;
            }

            var id = bsonReader.ReadString();

            if (IdentityConverter == null)
                throw new Exception("Identity converter not set in EventStoreIdentityBsonSerializer");

            return IdentityConverter.ToIdentity(id);
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
                bsonWriter.WriteString((EventStoreIdentity)value);
            }
        }

        public Type ValueType => typeof(EventStoreIdentity);
    }
}
