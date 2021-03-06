using System;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.checkpoint_strategy
{
    [TestFixture]
    public class the_from_all_checkpoint_strategy
    {
        private CheckpointStrategy _strategy;

        [SetUp]
        public void setup()
        {
            var builder = new CheckpointStrategy.Builder();
            builder.FromAll();
            builder.AllEvents();
            _strategy = builder.Build(ProjectionConfig.GetTest());
        }

    }
}