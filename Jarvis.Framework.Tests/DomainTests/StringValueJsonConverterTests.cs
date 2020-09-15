﻿namespace Jarvis.Framework.Tests.DomainTests
{
    [TestFixture]
    public class StringValueJsonConverterTests
    {
        JsonSerializerSettings _settings;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            _settings = new JsonSerializerSettings()
            {
                Converters = new JsonConverter[]
                {
                    new StringValueJsonConverter()
                }
            };
        }

        [Test]
        public void should_serialize_lowercase()
        {
            var instance = new ClassWithTypedStringValueLowerCase { Value = new TypedStringValueLowerCase("abc_123") };
            var json = JsonConvert.SerializeObject(instance, _settings);

            Assert.AreEqual("{\"Value\":\"abc_123\"}", json);
        }

        [Test]
        public void should_serialize()
        {
            var instance = new ClassWithTypedStringValue { Value = new TypedStringValue("aBc_123") };
            var json = JsonConvert.SerializeObject(instance, _settings);

            Assert.AreEqual("{\"Value\":\"aBc_123\"}", json);
        }

        [Test]
        public void should_deserialize_lowercase()
        {
            var instance = JsonConvert.DeserializeObject<ClassWithTypedStringValueLowerCase>("{ Value:\"abc_123\"}", _settings);
            Assert.AreEqual("abc_123", (string)instance.Value);
        }

        [Test]
        public void should_deserialize()
        {
            var instance = JsonConvert.DeserializeObject<ClassWithTypedStringValue>("{ Value:\"aBC_123\"}", _settings);
            Assert.AreEqual("aBC_123", (string)instance.Value);
        }

        [Test]
        public void should_serialize_null_lowercase()
        {
            var instance = new ClassWithTypedStringValue();
            var json = JsonConvert.SerializeObject(instance, _settings);
            Assert.AreEqual("{\"Value\":null}", json);
        }

        [Test]
        public void should_serialize_null()
        {
            var instance = new ClassWithTypedStringValueLowerCase();
            var json = JsonConvert.SerializeObject(instance, _settings);
            Assert.AreEqual("{\"Value\":null}", json);
        }

        [Test]
        public void should_deserialize_null()
        {
            var instance = JsonConvert.DeserializeObject<ClassWithTypedStringValueLowerCase>("{ AnId:null}", _settings);
            Assert.IsNull(instance.Value);
        }

    }
}