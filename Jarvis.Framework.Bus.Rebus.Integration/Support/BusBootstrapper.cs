﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.ConfigurationService.Client;
using Jarvis.Framework.Bus.Rebus.Integration.Logging;
using Jarvis.Framework.Bus.Rebus.Integration.Serializers;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.ReadModel;
using Newtonsoft.Json;
using Rebus;
using Rebus.Bus;
using Rebus.Castle.Windsor;
using Rebus.Configuration;
using Rebus.Messages;
using Rebus.MongoDb;
using Rebus.Shared;
using Rebus.Transports.Msmq;

namespace Jarvis.Framework.Bus.Rebus.Integration.Support
{
    public class BusBootstrapper
    {
        private readonly IWindsorContainer _container;
        private readonly string _connectionString;
        private readonly string _prefix;
        private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            ContractResolver = new MessagesContractResolver(),
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
        };

        public IMessagesTracker MessagesTracker { get; set; }

        public BusBootstrapper(
            IWindsorContainer container,
            string connectionString,
            string prefix,
            IMessagesTracker messagesTracker)
        {
            this._container = container;
            this._connectionString = connectionString;
            _prefix = prefix;
            MessagesTracker = messagesTracker;
        }

        public void StartWithAppConfig()
        {
            var busConfiguration = CreateDefaultBusConfiguration();

            var bus = busConfiguration
                .Transport(t => t.UseMsmqAndGetInputQueueNameFromAppConfig())
                .MessageOwnership(d => d.FromRebusConfigurationSection())
                .CreateBus();

            FixTiemoutHack();

            bus.Start();
        }

        RebusConfigurer CreateDefaultBusConfiguration()
        {
            var busConfiguration = Configure.With(new WindsorContainerAdapter(_container))
                .Logging(l => l.Log4Net())
                .Serialization(c => c.Use(new CustomJsonSerializer()))
                .Timeouts(t => t.StoreInMongoDb(_connectionString, _prefix + "-timeouts"))
                .Subscriptions(s => s.StoreInMongoDb(_connectionString, _prefix + "-subscriptions"))
                .Events(e => e.MessageSent += OnMessageSent)
                .Events(e => e.PoisonMessage += OnPoisonMessage)
                .SpecifyOrderOfHandlers(pipeline => pipeline.Use(new RemoveDefaultTimeoutReplyHandlerFilter()));
            return busConfiguration;
        }

        void FixTiemoutHack()
        {
            _container.Register(Component
                .For<IHandleMessages<TimeoutReply>>()
                .ImplementedBy<CustomTimeoutReplyHandler>()
                );
        }

        public void StartWithConfigurationManager()
        {
            String configurationPrefix = _prefix + "-rebus";
            String inputQueueName = ConfigurationServiceClient.Instance.GetSetting(configurationPrefix + ".inputQueue");
            String errorQueueName = ConfigurationServiceClient.Instance.GetSetting(configurationPrefix + ".errorQueue");

            Int32 workersNumber = 3;
            ConfigurationServiceClient.Instance.WithSetting(configurationPrefix + ".workers", setting => workersNumber = Int32.Parse(setting));
            ConfigurationServiceClient.Instance.WithSetting(configurationPrefix + ".maxRetries", setting => Int32.Parse(setting));

            var busConfiguration = CreateDefaultBusConfiguration();

            //now it is time to load endpoints configuration mapping
            Dictionary<String, String> endpointsMap = new Dictionary<string, string>();
            ConfigurationServiceClient.Instance.WithArraySetting(configurationPrefix + ".endpoints", s =>
            {
                foreach (dynamic element in s)
                {
                    var endpoint = (string)element.endpoint;
                    var messageTypeString = (String)element.messageType;
                    if (String.IsNullOrEmpty(endpoint))
                        return
                            @"Missing endpoint property. Rebus endpoints configuration should contain an array of valid endpoints: es 
    rebus : 
	{
		inputQueue : 'xxx.input',
		errorQueue : 'xxx.health',
		workers    : 2,
		maxRetries : 3,
		endpoints : [
			{
				message : 'Auth.Shared.Model.Authentication.Group.Commands.CreateOrUpdateGroupFromActiveDirectoryInfo, Auth.Shared',
				endpoint : 'xxx.input'
			}
		]
	}";
                    endpointsMap.Add(messageTypeString, endpoint);
                }
                return ""; //Everything is ok
            });

            var bus = busConfiguration
                .Transport(t => t.UseMsmq(inputQueueName, errorQueueName))
                .MessageOwnership(mo => mo.Use(new JarvisDetermineMessageOwnershipFromConfigurationManager(endpointsMap)))
                .CreateBus();

            FixTiemoutHack();

            bus.Start(workersNumber);
        }

        void OnPoisonMessage(IBus bus, ReceivedTransportMessage message, PoisonMessageInfo poisonmessageinfo)
        {
            if (message.Headers.ContainsKey("command.id"))
            {
                Guid commandId = Guid.Parse(message.Headers["command.id"].ToString());
                var description = message.Headers["command.description"].ToString();

                var ex = poisonmessageinfo.Exceptions.Last();
                string exMessage = description;

                Exception exception = ex.Value;
                while (exception is TargetInvocationException)
                {
                    exception = exception.InnerException;
                }

                if (exception != null)
                {
                    exMessage = exception.Message;
                }

                this.MessagesTracker.Failed(commandId, ex.Time, exception);

                var command = GetCommandFromMessage(message);

                if (command != null)
                {
                    var notifyTo = command.GetContextData("reply-to");

                    if (!string.IsNullOrEmpty(notifyTo))
                    {
                        bus.Advanced.Routing.Send(
                            message.Headers["rebus-return-address"].ToString(),
                            new CommandHandled(
                                notifyTo,
                                commandId,
                                CommandHandled.CommandResult.Failed,
                                description,
                                exMessage
                                )
                            );
                    }
                }
            }
        }

        /// <summary>
        /// http://mookid.dk/oncode/archives/2966
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="destination"></param>
        /// <param name="message"></param>
        private void OnMessageSent(IBus bus, string destination, object message)
        {
            if (MessagesTracker != null)
            {
                var msg = message as IMessage;
                if (msg != null)
                {
                    MessagesTracker.Started(msg);
                }
            }

            var attribute = message.GetType()
                       .GetCustomAttributes(typeof(TimeToBeReceivedAttribute), false)
                       .Cast<TimeToBeReceivedAttribute>()
                       .SingleOrDefault();

            if (attribute == null) return;

            bus.AttachHeader(message, Headers.TimeToBeReceived, attribute.HmsString);
        }

        private ICommand GetCommandFromMessage(ReceivedTransportMessage message)
        {
            string body;
            switch (message.Headers["rebus-encoding"].ToString().ToLowerInvariant())
            {
                case "utf-7":
                    body = Encoding.UTF7.GetString(message.Body);
                    break;
                case "utf-8":
                    body = Encoding.UTF8.GetString(message.Body);
                    break;
                case "utf-32":
                    body = Encoding.UTF32.GetString(message.Body);
                    break;
                case "ascii":
                    body = Encoding.ASCII.GetString(message.Body);
                    break;
                case "unicode":
                    body = Encoding.Unicode.GetString(message.Body);
                    break;
                default:
                    return null;
            }

            var msg = JsonConvert.DeserializeObject(body, _jsonSerializerSettings);
            var array = msg as Object[];
            if (array != null)
            {
                return array[0] as ICommand;
            }

            return null;
        }
    }

    internal class JarvisDetermineMessageOwnershipFromConfigurationManager : IDetermineMessageOwnership
    {
        private readonly Dictionary<String, String> _endpointsMap = new Dictionary<String, String>();
        private readonly ConcurrentDictionary<Type, String> _mapCache = new ConcurrentDictionary<Type, string>();

        public JarvisDetermineMessageOwnershipFromConfigurationManager(Dictionary<String, String> endpointsMap)
        {
            _endpointsMap = endpointsMap;
            _mapCache = new ConcurrentDictionary<Type, string>();
        }

        public string GetEndpointFor(Type messageType)
        {
            String returnValue = "";
            if (!_mapCache.ContainsKey(messageType))
            {
                //we can have in endpoints configured a namespace or a fully qualified name
                Debug.Assert(messageType != null, "messageType != null");
                var asqn = messageType.FullName + ", " + messageType.Assembly.GetName().Name;
                if (_endpointsMap.ContainsKey(asqn))
                {
                    //exact match
                    returnValue = _endpointsMap[asqn];
                }
                else
                {
                    //find the most specific namespace that contains the type to dispatch.
                    var endpointElement =
                        _endpointsMap
                            .Where(e => asqn.StartsWith(e.Key, StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(e => e.Key.Length)
                            .FirstOrDefault();
                    if (!String.IsNullOrEmpty(endpointElement.Key))
                    {
                        returnValue = endpointElement.Value;
                    }
                }
                _mapCache.TryAdd(messageType, returnValue);
            }
            else
            {
                returnValue = _mapCache[messageType];
            }

            if (!String.IsNullOrEmpty(returnValue)) return returnValue;
            var message = string.Format(@"No endpoint mapping configured for messages of type {0}.

JarvisDetermineMessageOwnershipFromConfigurationManager offers the ability to specify endpoint mappings
in configuration file handled by configuration-manager, you need to configure rebus with endpoints 
section that offer the ability to specify mapping from type to relative endpoint:

 	'pm-rebus' : {
		'inputQueue' : 'Intranet.ProcessManager.input',
		'errorQueue' : 'intranet.health',
		'workers' : 2,
		'maxRetries' : 2,
		'endpoints' : [{
				'messageType' : 'Intranet.Shared',
				'endpoint' : 'intranet.events.input'
			},
			{
				'messageType' : 'Auth.Shared',
				'endpoint' : 'intranet.events.input'
			},
			{
				'messageType' : 'DMS.Shared',
				'endpoint' : 'intranet.events.input'
			},
			{
				'messageType' : 'DMS.Engine',
				'endpoint' : 'intranet.events.input'
			},

", messageType);
            throw new InvalidOperationException(message);
        }

    }
}
