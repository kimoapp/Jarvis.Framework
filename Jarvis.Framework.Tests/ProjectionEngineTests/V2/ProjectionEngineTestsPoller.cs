﻿using Jarvis.Framework.Tests.EngineTests;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.V2
{
    [TestFixture("2")]
    public class ProjectionEngineTestsPoller : ProjectionEngineBasicTestBase
    {
        public ProjectionEngineTestsPoller(String pollingClientVersion) : base(pollingClientVersion)
        {

        }

        protected override void RegisterIdentities(IdentityManager identityConverter)
        {
            identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleAggregateId).Assembly);
        }

        protected override string GetConnectionString()
        {
            return ConfigurationManager.ConnectionStrings["engine"].ConnectionString;
        }

        protected override IEnumerable<IProjection> BuildProjections()
        {
            var writer = new CollectionWrapper<SampleReadModel, string>(StorageFactory, new NotifyToNobody());
            yield return new Projection(writer);
        }

        [Test]
        public async Task run_poller()
        {
            var reader = new MongoReader<SampleReadModel, string>(Database);
            var aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(1)).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);

            await Engine.UpdateAndWaitAsync().ConfigureAwait(false);
            Assert.AreEqual(1, reader.AllSortedById.Count());

            aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(2)).ConfigureAwait(false);
            aggregate.Create();
            await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);

            await Engine.UpdateAndWaitAsync().ConfigureAwait(false);

            Assert.AreEqual(2, reader.AllSortedById.Count());
        }
    }
}