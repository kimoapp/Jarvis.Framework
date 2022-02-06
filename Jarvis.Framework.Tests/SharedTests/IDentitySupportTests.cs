﻿using Jarvis.Framework.Shared.Domain.Serialization;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.IdentitySupport.Serialization;
using Jarvis.Framework.Tests.Support;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.SharedTests
{
    [TestFixture]
    public class IDentitySupportTests
    {
        private TestMapper sut;
        private TestFlatMapper sutFlat;
        private MongoCollection<BsonDocument> _mappingCollection;
        private MongoCollection<BsonDocument> _mappingFlatCollection;

        [TestFixtureSetUp]
        public void TestFixtureSetup()
        {
            BsonSerializer.RegisterSerializer(typeof(TestFlatId), new EventStoreIdentityBsonSerializer());
            EventStoreIdentityCustomBsonTypeMapper.Register<TestFlatId>();
        }

        [SetUp]
        public void SetUp()
        {
           var db = TestHelper.CreateNew(ConfigurationManager.ConnectionStrings["system"].ConnectionString);
            IdentityManager manager = new IdentityManager(new CounterService(db));
            manager.RegisterIdentitiesFromAssembly(Assembly.GetExecutingAssembly());
            _mappingCollection = db.GetCollection<BsonDocument>("map_testid");
            _mappingFlatCollection = db.GetCollection<BsonDocument>("map_testflatid");
            sut = new TestMapper(db, manager);
            sutFlat = new TestFlatMapper(db, manager);
        }

        [Test]
        public void Verify_delete_of_mapping()
        {
            var id = sut.Map("TEST");
            var mapCount = _mappingCollection.FindAll();
            Assert.That(mapCount.Count(), Is.EqualTo(1));

            sut.DeleteAliases(id);
            mapCount = _mappingCollection.FindAll();
            Assert.That(mapCount.Count(), Is.EqualTo(0));
        }


        [Test]
        public void Verify_delete_of_flat_mapping()
        {
            var id = sutFlat.Map("TEST");
            var mapCount = _mappingFlatCollection.FindAll();
            Assert.That(mapCount.Count(), Is.EqualTo(1));

            sutFlat.DeleteAliases(id);
            mapCount = _mappingFlatCollection.FindAll();
            Assert.That(mapCount.Count(), Is.EqualTo(0));
        }

        /// <summary>
        /// The method 'MapIdentity' uses the message of the MongoWriteConcernException
        /// to recognize duplicate keys. We test that this works.
        /// </summary>
        [Test]
        public void Verify_MapIdentity_WhenIdentityAlreadyExists()
        {
            var id = new TestId(1);

            sut.LinkKeysTest("TEST", id);
            var mapCount = _mappingCollection.FindAll();
            Assert.That(mapCount.Count(), Is.EqualTo(1));

            var actualId = sut.LinkKeysTest("TEST", id);

            Assert.AreEqual(id, actualId);

            mapCount = _mappingCollection.FindAll();
            Assert.That(mapCount.Count(), Is.EqualTo(1));
        }
    }

    public class TestMapper : AbstractIdentityTranslator<TestId>
    {
        public TestMapper(MongoDatabase db, IIdentityGenerator identityGenerator) :
            base(db, identityGenerator)
        {

        }

        public void ReplaceAlias(TestId id, String value)
        {
            base.ReplaceAlias(id, value);
        }

        public TestId Map(String value)
        {
            return Translate(value);
        }

        public TestId LinkKeysTest(string externalKey, TestId key)
        {
            return LinkKeys(externalKey, key);
        }
    }

    public class TestFlatMapper : AbstractIdentityTranslator<TestFlatId>
    {
        public TestFlatMapper(MongoDatabase db, IIdentityGenerator identityGenerator) :
            base(db, identityGenerator)
        {

        }

        public void ReplaceAlias(TestFlatId id, String value)
        {
            base.ReplaceAlias(id, value);
        }


        public TestFlatId Map(String value)
        {
            return Translate(value);
        }
    }

    public class TestId : EventStoreIdentity
    {
        public TestId(string id)
            : base(id)
        {
        }

        public TestId(long id)
            : base(id)
        {
        }
    }

    public class TestFlatId : EventStoreIdentity
    {
        public TestFlatId(string id)
            : base(id)
        {
        }

        public TestFlatId(long id)
            : base(id)
        {
        }
    }
}
