﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jarvis.Framework.Shared.Store;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Options;

namespace Jarvis.Framework.Shared.Storage
{
    public static class MongoRegistration
    {
        static MongoRegistration()
        {
            //Code copied from the newer versions of Jarvis!

            var protectedAssemblies = new[] {
                "NEventStore.Persistence.MongoDB"
            };
            var guidConversion = new ConventionPack();
            guidConversion.Add(new GuidAsStringRepresentationConvention(protectedAssemblies.ToList()));
            ConventionRegistry.Register("guidstring", guidConversion, _ => true);
        }

        private class AliasClassMap : BsonClassMap
        {
            public AliasClassMap(Type classType)
                : base(classType)
            {
                AutoMap();
                var version = 1;

                var alias = (VersionInfoAttribute)Attribute.GetCustomAttribute(classType, typeof(VersionInfoAttribute));
                var name = classType.Name;

                if (name.EndsWith("Command"))
                {
                    name = name.Remove(name.Length - "Command".Length);
                }
                else if (name.EndsWith("Event"))
                {
                    name = name.Remove(name.Length - "Event".Length);
                }

                if (alias != null)
                {
                    version = alias.Version;
                    if (!string.IsNullOrWhiteSpace(alias.Name))
                    {
                        name = alias.Name;
                    }
                }

                SetDiscriminator(String.Format("{0}_{1}", name, version));
            }
        }


        public static void RegisterTypes(IEnumerable<Type> types, bool useAlias = true)
        {
            if (useAlias)
            {
                foreach (var t in types)
                {
                    if (!BsonClassMap.IsClassMapRegistered(t))
                        BsonClassMap.RegisterClassMap(new AliasClassMap(t));
                }
            }
            else
            {
                foreach (var t in types)
                {
                    BsonClassMap.LookupClassMap(t);
                }
            }
        }

    }

    /// <summary>
    /// A convention that allows you to set the serialization representation of guid to a simple string
    /// </summary>
    public class GuidAsStringRepresentationConvention : ConventionBase, IMemberMapConvention
    {
        private readonly List<string> protectedAssemblies;

        // constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="GuidAsStringRepresentationConvention" /> class.
        /// </summary>  
        /// <param name="protectedAssemblies"></param>
        public GuidAsStringRepresentationConvention(List<string> protectedAssemblies)
        {
            this.protectedAssemblies = protectedAssemblies;
        }

        /// <summary>
        /// Applies a modification to the member map.
        /// </summary>
        /// <param name="memberMap">The member map.</param>
        public void Apply(BsonMemberMap memberMap)
        {
            var memberTypeInfo = memberMap.MemberType.GetTypeInfo();
            if (memberTypeInfo == typeof(Guid))
            {
                var declaringTypeAssembly = memberMap.ClassMap.ClassType.Assembly;
                var asmName = declaringTypeAssembly.GetName().Name;
                if (protectedAssemblies.Any(a => a.Equals(asmName, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                var serializer = memberMap.GetSerializer();
                var representationConfigurableSerializer = serializer as IRepresentationConfigurable;
                if (representationConfigurableSerializer != null)
                {
                    var _representation = BsonType.String;
                    var reconfiguredSerializer = representationConfigurableSerializer.WithRepresentation(_representation);
                    memberMap.SetSerializer(reconfiguredSerializer);
                }
            }
        }
    }
}