using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Project2015To2017.Definition;
using Project2015To2017.Transforms;

namespace Project2015To2017.Migrate2017.Transforms
{
	public sealed class AssemblyFilterPackageKeepTopLevelOnlyTransformation : ILegacyOnlyProjectTransformation
	{
		public void Transform(Project definition)
		{
			// We need a nuget settings for this transform
			if (definition.NuGetSettings == null)
				return;

			// Get the NuGet source repositories
			var packageSources = SettingsUtility.GetEnabledSources(definition.NuGetSettings);
			var factory = NuGet.Protocol.Core.Types.Repository.Factory;
			var sourceRepos = packageSources.Select(x => factory.GetCoreV2(x));
			var resources = sourceRepos.Select(x => x.GetResource<DependencyInfoResource>());

			// Get parsed .NET frameworks for NuGet
			var netFrameworks = definition.TargetFrameworks.Select(NuGetFramework.Parse);

			// TODO HACK, only using first repository
			var resource = resources.First();
			// TODO HACK, only using first .NET framework version
			var netFramework = netFrameworks.First();

			// Cache nuget results since we likely reference the same packages across projects
			using (var sourceCacheContext = new SourceCacheContext())
			{
				// Distinct list of resolved dependencies from NuGet
				var removeList = new HashSet<PackageDependency>();

				foreach (var assemblyPackage in definition.PackageReferences)
				{
					var package = new PackageIdentity(assemblyPackage.Id, NuGetVersion.Parse(assemblyPackage.Version));

					// TODO Not really fond of wrapping an async in a Task
					var results = Task.Run(() => resource.ResolvePackage(package, netFramework, sourceCacheContext, new ConsoleNugetLogger(),
						CancellationToken.None)).Result;

					// If we have a result, add it's dependencies to the remove list
					if (results != null && results.Listed)
					{
						foreach (var dep in results.Dependencies)
							removeList.Add(dep);
					}

					// Remove any system nugets that were added to simplify nuget package consolidation
					if (assemblyPackage.Id.StartsWith("System"))
					{
						if(!assemblyPackage.Id.StartsWith("System.Data.SQLite"))
							removeList.Add(new PackageDependency(assemblyPackage.Id));
					}
				}

				if (removeList.Count <= 0)
					return;

				// Filter out any dependencies from the packages that are in the removeList
				var ids = removeList.Select(x => x.Id).ToList();
				var references = definition.PackageReferences.Where(x => !ids.Contains(x.Id)).ToList();
				definition.PackageReferences = new ReadOnlyCollection<PackageReference>(references);
			}
		}
	}

	// TODO Hacky console logger for Nuget. 'resource.ResolvePackage' throws if a null logger is provided
	public class ConsoleNugetLogger : ILogger
	{
		public void LogDebug(string data)
		{
			Console.WriteLine(data);
		}

		public void LogVerbose(string data)
		{
			Console.WriteLine(data);
		}

		public void LogInformation(string data)
		{
			Console.WriteLine(data);
		}

		public void LogMinimal(string data)
		{
			Console.WriteLine(data);
		}

		public void LogWarning(string data)
		{
			Console.WriteLine(data);
		}

		public void LogError(string data)
		{
			Console.WriteLine(data);
		}

		public void LogInformationSummary(string data)
		{
			Console.WriteLine(data);
		}

		public void Log(LogLevel level, string data)
		{
			Console.WriteLine(data);
		}

		public Task LogAsync(LogLevel level, string data)
		{
			Console.WriteLine(data);
			return Task.CompletedTask;
		}

		public void Log(ILogMessage message)
		{
			Console.WriteLine(message.Message);
		}

		public Task LogAsync(ILogMessage message)
		{
			Console.WriteLine(message.Message);
			return Task.CompletedTask;
		}
	}
}