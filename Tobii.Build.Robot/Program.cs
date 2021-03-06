﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Tobii.Build.Robot.Core;
using Tobii.Build.Robot.Rest;
using Tobii.Build.Robot.Rest.Core;
using Tobii.Build.Robot.Telegram;
using Tobii.Build.Robot.Core.Commands;
using Tobii.Build.Robot.Core.Pipeline;
using Tobii.Build.Robot.Rest.TeamCity;
using Tobii.Build.Robot.Rest.TeamCity.Commands;
using ConfigurationProvider = Tobii.Build.Robot.Telegram.ConfigurationProvider;
using System.IO;

namespace Tobii.Build.Robot
{
    class Program
    {
        static void Main(string[] args)
        {
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Console.WriteLine((e.ExceptionObject as Exception).Message);
            };
            var teamcityConfig = new Rest.TeamCity.ConfigurationProvider();
            var restClient = new RestClient(
                teamcityConfig.Host, 
                teamcityConfig.Login, 
                teamcityConfig.Password,
                MediaType.Json);
            var tc = new TeamCityApi(restClient);
            var gateway = new Gateway(new[] { tc });
            var presenterFactory = new PresenterFactory();
            var output = new Output(
                presenterFactory,
                new IOutputStream[] {
                    new ConsoleStream(),
                    new FileStream("log.txt") });
            var cancellationSource = new CancellationTokenSource();
            output.Write("build bot greetings you");
            output.Write("type exit to leave bot");
           
            var client = new TelegramBotClient(new ConfigurationProvider().ApiKey);
            var commands = new List<CommandBase>
            {
                new StartCommand(cancellationSource),
                new ExitCommand(cancellationSource),
                new TeamcityGetBuildQueueCommand(gateway.For<ITeamCity>(), cancellationSource),
                new TeamcityGetProjectsCommand(gateway.For<ITeamCity>(), cancellationSource),
                new TeamcityGetProjectCommand(gateway.For<ITeamCity>(), cancellationSource),
                new TeamcityGetBranchesCommand(gateway.For<ITeamCity>(), cancellationSource),
                new TeamcityGetBuildsCommand(gateway.For<ITeamCity>(), cancellationSource),
                new TeamcityGetBuildTypesCommand(gateway.For<ITeamCity>(), cancellationSource),
                new TeamcityGetBuildCommand(gateway.For<ITeamCity>(), cancellationSource),
                new TeamcityGetAgentsCommand(gateway.For<ITeamCity>(), cancellationSource),
                new TeamcityEnqueueBuild(gateway.For<ITeamCity>(), cancellationSource),
                new TeamcityGetRunningBuildsCommand(gateway.For<ITeamCity>(), cancellationSource),
                new TeamcityEnqueueAgentCommand(gateway.For<ITeamCity>(), cancellationSource),
                new TeamcityGetBranchCommand(gateway.For<ITeamCity>(), cancellationSource)
            };
            var help = new HelpCommand(commands, cancellationSource);
            commands.Add(help);
            var commandsExecutor = new CommandsExecutor(commands);
            var inputStream = new InputPipeline();
            var consoleListener = new ConsoleCommandProducer(inputStream);
            var runLooper = new RunLooper(inputStream, commandsExecutor, consoleListener, output, cancellationSource);
            var store = new MemoryStore();
            using (var botWrapper = new BotWrapper(client, inputStream, presenterFactory, cancellationSource, commandsExecutor, output, store))
            {
                botWrapper.Start();
                runLooper.Run();
            }
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Console.WriteLine(e.Exception.Message);
        }
    }

    internal class MemoryStore : IStore
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

        public void Put<T>(string user, string id, T item)
        {
            _data.Add(id, item);
        }

        public T Get<T>(string chatId, string id)
        {
            if (_data.ContainsKey(id))
            {
                var item = (T) _data[id];
                _data.Remove(id);
                return item;
            }

            return default(T);
        }

        public void Remove(string chatId, string id)
        {
            if (_data.ContainsKey(id))
            {
                _data.Remove(id);
            }
        }
    }

    public class BackupStore : IStore
    {
        private readonly IStore decoratedStore;


        public BackupStore(IStore decoratedStore)
        {
            this.decoratedStore = decoratedStore;
        }

        public T Get<T>(string chatId, string id)
        {
            return decoratedStore.Get<T>(chatId, id);
        }

        public void Put<T>(string user, string id, T item)
        {
            decoratedStore.Put<T>(user, id, item);
            
        }

        public void Remove(string chatId, string id)
        {
            decoratedStore.Remove(chatId, id);
        }
    }
}
