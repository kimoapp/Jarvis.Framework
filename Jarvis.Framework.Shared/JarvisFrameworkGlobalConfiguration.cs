﻿using System;

namespace Jarvis.Framework.Shared
{
    public static class JarvisFrameworkGlobalConfiguration
    {
        public static Boolean KernelMetrics { get; private set; }

        /// <summary>
        /// If true the RepositoryCommandHandler will use SingleAggregateRepository cached
        /// to reuse the same Repository+Aggregate if found in cache. This form of aggressive
        /// cache can really improve performances when lots of commands are send to the very
        /// same aggregate.
        /// </summary>
        public static Boolean SingleAggregateRepositoryCacheEnabled { get; private set; }

        public static Boolean OfflineEventsReadmodelIdempotencyCheck { get; private set; }

        public static Boolean AtomicProjectionEngineOptimizedCatchup { get; private set; }

        /// <summary>
        /// Due to an anomaly in the driver.
        /// </summary>
        public static Boolean MongoDbAsyncDisabled { get; private set; }

        static JarvisFrameworkGlobalConfiguration()
        {
            KernelMetrics = true;
            SingleAggregateRepositoryCacheEnabled = false;
            AtomicProjectionEngineOptimizedCatchup = true;
        }

        public static void DisableMongoDbAsync()
        {
            MongoDbAsyncDisabled = true;
        }

        /// <summary>
        /// Disable kernel metrics.
        /// </summary>
        public static void DisableMetrics()
        {
            KernelMetrics = false;
        }

        /// <summary>
        /// Enable kernel metrics.
        /// </summary>
        public static void EnableMetrics()
        {
            KernelMetrics = true;
        }

        public static void DisableSingleAggregateRepositoryCache()
        {
            SingleAggregateRepositoryCacheEnabled = false;
        }

        public static void EnableSingleAggregateRepositoryCache()
        {
            SingleAggregateRepositoryCacheEnabled = true;
        }

        public static void EnableOfflineEventsReadmodelIdempotencyCheck()
        {
            OfflineEventsReadmodelIdempotencyCheck = true;
        }

        public static void DisableOfflineEventsReadmodelIdempotencyCheck()
        {
            OfflineEventsReadmodelIdempotencyCheck = false;
        }

        public static void EnableAtomicProjectionEngineOptimizedCatchup()
        {
            AtomicProjectionEngineOptimizedCatchup = true;
        }

        public static void DisableAtomicProjectionEngineOptimizedCatchup()
        {
            AtomicProjectionEngineOptimizedCatchup = false;
        }
    }
}
