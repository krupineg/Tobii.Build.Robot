﻿using System.Threading;
using System.Threading.Tasks;
using Tobii.Build.Robot.Core;
using System.Linq;
using Tobii.Build.Robot.Core.Commands;

namespace Tobii.Build.Robot.Rest.Command
{
    public class TeamcityGetProjectsCommand : CommandBase
    {
        private readonly ITeamCity _teamCity;

        public TeamcityGetProjectsCommand(ITeamCity teamCity, CancellationTokenSource cancellationTokenSource) : base(cancellationTokenSource)
        {
            _teamCity = teamCity;
        }

        public override string Name { get { return "projects"; }}

        public override async Task Do(Output output, string[] parameters)
        {
            var projects = await _teamCity.GetProjectsAsync();
            var clickables = projects.Project.Select(x
                => new Clickable(x.Name, Name, "", TeamcityGetProjectCommand.Project, x.Id));
            output.Ask("available projects: ", clickables.ToArray());
        }
    }
}