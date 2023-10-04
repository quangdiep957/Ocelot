#tool "dotnet:?package=GitVersion.Tool&version=5.8.1"
#tool "dotnet:?package=coveralls.net&version=4.0.1"
#addin nuget:?package=Newtonsoft.Json
#addin nuget:?package=System.Text.Encodings.Web&version=4.7.1
#tool "nuget:?package=ReportGenerator&version=5.1.19"
#addin Cake.Coveralls&version=1.1.0

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// compile
var compileConfig = Argument("configuration", "Release");

var slnFile = "./Ocelot.sln";

// build artifacts
var artifactsDir = Directory("artifacts");

// unit testing
var artifactsForUnitTestsDir = artifactsDir + Directory("UnitTests");
var unitTestAssemblies = @"./test/Ocelot.UnitTests/Ocelot.UnitTests.csproj";
var minCodeCoverage = 0.80d;
var coverallsRepoToken = "OCELOT_COVERALLS_TOKEN";
var coverallsRepo = "https://coveralls.io/github/ThreeMammals/Ocelot";

// acceptance testing
var artifactsForAcceptanceTestsDir = artifactsDir + Directory("AcceptanceTests");
var acceptanceTestAssemblies = @"./test/Ocelot.AcceptanceTests/Ocelot.AcceptanceTests.csproj";

// integration testing
var artifactsForIntegrationTestsDir = artifactsDir + Directory("IntegrationTests");
var integrationTestAssemblies = @"./test/Ocelot.IntegrationTests/Ocelot.IntegrationTests.csproj";

// benchmark testing
var artifactsForBenchmarkTestsDir = artifactsDir + Directory("BenchmarkTests");
var benchmarkTestAssemblies = @"./test/Ocelot.Benchmarks";

// packaging
var packagesDir = artifactsDir + Directory("Packages");
var releaseNotesFile = packagesDir + File("ReleaseNotes.md");
var artifactsFile = packagesDir + File("artifacts.txt");

// stable releases
var tagsUrl = "https://api.github.com/repos/ThreeMammals/ocelot/releases/tags/";
var nugetFeedStableKey = EnvironmentVariable("OCELOT_NUTGET_API_KEY");
var nugetFeedStableUploadUrl = "https://www.nuget.org/api/v2/package";
var nugetFeedStableSymbolsUploadUrl = "https://www.nuget.org/api/v2/package";

// internal build variables - don't change these.
string committedVersion = "0.0.0-dev";
GitVersion versioning = null;
int releaseId = 0;
string gitHubUsername = "TomPallister";
string gitHubPassword = Environment.GetEnvironmentVariable("OCELOT_GITHUB_API_KEY");

var target = Argument("target", "Default");

Information("target is " + target);
Information("Build configuration is " + compileConfig);	

Task("Default")
	.IsDependentOn("Build");

Task("Build")
	.IsDependentOn("RunTests");

Task("ReleaseNotes")
	.IsDependentOn("CreateReleaseNotes");

Task("RunTests")
	.IsDependentOn("RunUnitTests")
	.IsDependentOn("RunAcceptanceTests")
	.IsDependentOn("RunIntegrationTests");

Task("Release")
	.IsDependentOn("Build")
	.IsDependentOn("CreateArtifacts")
	.IsDependentOn("PublishGitHubRelease")
    .IsDependentOn("PublishToNuget");

Task("Compile")
	.IsDependentOn("Clean")
	.IsDependentOn("Version")
	.Does(() =>
	{	
		var settings = new DotNetBuildSettings
		{
			Configuration = compileConfig,
		};

		DotNetBuild(slnFile, settings);
	});

Task("Clean")
	.Does(() =>
	{
        if (DirectoryExists(artifactsDir))
        {
            DeleteDirectory(artifactsDir, new DeleteDirectorySettings {
				Recursive = true,
				Force = true
			});
        }
        CreateDirectory(artifactsDir);
	});

Task("CreateReleaseNotes")
	.Does(() =>
	{	
		Information("Generating release notes at " + releaseNotesFile);

		// local helper function
		static IEnumerable<string> GitHelper(string command)
		{
			IEnumerable<string> output;
			var exitCode = StartProcess(
				"git",
				new ProcessSettings { Arguments = command, RedirectStandardOutput = true },
				out output);
			if (exitCode != 0)
				throw new Exception("Failed to execute Git command: " + command);
			return output;
		}

		IEnumerable<string> lastReleaseTag = GitHelper("describe --tags --abbrev=0 --exclude net*");
		var lastRelease = lastReleaseTag.First(t => !t.StartsWith("net")); // skip 'net*-vX.Y.Z' tag and take 'major.minor.build'
		Information("Last release tag is " + lastRelease);

		IEnumerable<string> shortlogSummary = GitHelper($"shortlog --no-merges --numbered --summary {lastRelease}..HEAD");
		var summary = shortlogSummary
			.Select(line => line.Split("  ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			.Select(arr => new { commits = int.Parse(arr[0]), author = arr[1] })
			.ToList();

		// Starring aka Release Influencers
		var starring = new List<string>
		{
			string.Empty,
			"### Starring :star: aka Release Influencers :bowtie:",
		};
		foreach (var contributor in summary)
		{
			var stars = string.Join(string.Empty, Enumerable.Repeat(":star:", contributor.commits));
			starring.Add($"{stars}  {contributor.author}");
		}

		// Honoring aka Top Contributors
		const int top3 = 3; // Going to create Top 3
		var releaseNotes = new List<string>
		{
			string.Empty,
			"### Honoring :medal_sports: aka Top Contributors :clap:",
		};
		var topContributors = new List<string>;
		var commitsGrouping = summary
			.GroupBy(x => x.commits)
			.Select(g => new
			{
				commits = g.Key,
				count = g.Count(),
				authors = g.Select(x => x.author).ToList(),
			})
			.OrderByDescending(x => x.commits)
			.ToList();

		// local helpers
		string[] places = new[] { "1st", "2nd", "3rd" };
		static string Plural(int n) => n == 1 ? "" : "s";
		static string Honor(string place, string author, int commits, string suffix = null)
			=> $"{place[0]}<sup>{place[1..]}</sup> :{place}_place_medal: goes to **{author}** for delivering **{commits}** feature{Plural(commits)} {suffix ?? ""}";
		static string HonorForFiles(string place, string author, int commits, int files, string suffix = null)
			=> Honor(place, author, commits, $"in **{files}** file{Plural(files)} changed {suffix ?? ""}");
		static string HonorForInsertions(string place, string author, int commits, int files, int insertions, string suffix = null)
			=> HonorForFiles(place, author, commits, files, $"with **{insertions}** insertion{Plural(insertions)} {suffix ?? ""}");
		static string HonorForDeletions(string place, string author, int commits, int files, int insertions, int deletions)
			=> HonorForInsertions(place, author, commits, files, insertions, $"and **{deletions}** deletion{Plural(deletions)}");

		var statistics = new List<(string Contributor, int Files, int Insertions, int Deletions)>();
		foreach (var group in commitsGrouping)
		{
			if (topContributors.Count >= top3) break;
			if (group.count == 1)
			{
				var place = places[topContributors.Count];
				var author = group.authors.First();
				var honoring = Honor(place, author, group.commits);
				topContributors.Add(honoring);
			}
			else // multiple candidates with the same number of commits, so, group by files changed
			{
				var shortstatRegex = new System.Text.RegularExpressions.Regex(@"^\s*(?'files'\d+)\s+files?\s+changed(?'ins',\s+(?'insertions'\d+)\s+insertions?\(\+\))?(?'del',\s+(?'deletions'\d+)\s+deletions?\(\-\))?\s*$");
				// Collect statistics from git log & shortlog
				foreach (var author in group.authors)
				{
					if (!statistics.Exists(s => s.Contributor == author))
					{
						IEnumerable<string> shortstat = GitHelper($"log --no-merges --author='{author}' --shortstat --pretty=oneline {lastRelease}..HEAD");
						var data = shortstat
							.Where(x => shortstatRegex.IsMatch(x))
							.Select(x => shortstatRegex.Match(x))
							.Select(m => new
							{
								files = int.Parse(m.Groups["files"]?.Value ?? "0"),
								insertions = int.Parse(m.Groups["insertions"]?.Value ?? "0"),
								deletions = int.Parse(m.Groups["deletions"]?.Value ?? "0"),
							})
							.ToList();
						statistics.Add((author, data.Sum(x => x.files), data.Sum(x => x.insertions), data.Sum(x => x.deletions)));
					}
				}
				var filesGrouping = statistics
					.GroupBy(x => x.Files)
					.Select(g => new
					{
						files = g.Key,
						count = g.Count(),
						contributors = g.SelectMany(x => statistics.Where(s => s.Contributor==x.Contributor && s.Files==g.Key)).ToList(),
					})
					.OrderByDescending(x => x.files)
					.ToList();
				foreach (var fGroup in filesGrouping)
				{
					if (topContributors.Count >= top3) break;
					if (fGroup.count == 1)
					{
						var place = places[topContributors.Count];
						var contributor = fGroup.contributors.First();
						var honoring = HonorForFiles(place, contributor.Contributor, group.commits, contributor.Files);
						topContributors.Add(honoring);
					}
					else // multiple candidates with the same number of commits, with the same number of changed files, so, group by additions (insertions)
					{
						var insertionsGrouping = fGroup.contributors
							.GroupBy(x => x.Insertions)
							.Select(g => new
							{
								insertions = g.Key,
								count = g.Count(),
								contributors = g.SelectMany(x => fGroup.contributors.Where(s => s.Contributor == x.Contributor && s.Insertions == g.Key)).ToList(),
							})
							.OrderByDescending(x => x.insertions)
							.ToList();
						foreach (var insGroup in insertionsGrouping)
						{
							if (topContributors.Count >= top3) break;
							if (insGroup.count == 1)
							{
								var place = places[topContributors.Count];
								var contributor = insGroup.contributors.First();
								var honoring = HonorForInsertions(place, contributor.Contributor, group.commits, contributor.Files, contributor.Insertions);
								topContributors.Add(honoring);
							}
							else // multiple candidates with the same number of commits, with the same number of changed files, with the same number of insertions, so, order desc by deletions
							{
								foreach (var contributor in insGroup.contributors.OrderByDescending(x => x.Deletions))
								{
									if (topContributors.Count >= top3) break;
									var place = places[topContributors.Count];
									var honoring = HonorForDeletions(place, contributor.Contributor, group.commits, contributor.Files, contributor.Insertions, contributor.Deletions);
									topContributors.Add(honoring);
								}
							}
						}
					}
				}
			}
		} // END of Top 3
		releaseNotes.AddRange(topContributors);
		releaseNotes.AddRange(starring);
		releaseNotes.Add("");
		releaseNotes.Add("### Features Included in the Release");
		IEnumerable<string> commitsHistory = GitHelper($"log --no-merges --date=format-local:'%A, %B %d at %H:%M' --pretty=format:'<sub>%h by **%aN** on %ad &rarr;</sub>%n%s' {lastRelease}..HEAD");
		releaseNotes.AddRange(commitsHistory);

		EnsureDirectoryExists(packagesDir);
		System.IO.File.WriteAllLines(releaseNotesFile, releaseNotes);

		if (string.IsNullOrEmpty(System.IO.File.ReadAllText(releaseNotesFile)))
		{
			System.IO.File.WriteAllText(releaseNotesFile, "No commits since last release");
		}

		Information("Release notes are >>>\r\n" + System.IO.File.ReadAllText(releaseNotesFile) + "<<<");
	});
	
Task("Version")
	.IsDependentOn("CreateReleaseNotes")
	.Does(() =>
	{
		versioning = GetNuGetVersionForCommit();
		var nugetVersion = versioning.NuGetVersion;
		Information("SemVer version number: " + nugetVersion);

		if (IsRunningOnCircleCI())
		{
			Information("Persisting version number...");
			PersistVersion(committedVersion, nugetVersion);
		}
		else
		{
			Information("We are not running on build server, so we won't persist the version number.");
		}
	});

Task("RunUnitTests")
	.IsDependentOn("Compile")
	.Does(() =>
	{
		var testSettings = new DotNetTestSettings
		{
			Configuration = compileConfig,
			ResultsDirectory = artifactsForUnitTestsDir,
				ArgumentCustomization = args => args
					// this create the code coverage report
					.Append("--collect:\"XPlat Code Coverage\"")
		};

		EnsureDirectoryExists(artifactsForUnitTestsDir);
		DotNetTest(unitTestAssemblies, testSettings);

		var coverageSummaryFile = GetSubDirectories(artifactsForUnitTestsDir).First().CombineWithFilePath(File("coverage.cobertura.xml"));
		Information(coverageSummaryFile);
		Information(artifactsForUnitTestsDir);

		GenerateReport(coverageSummaryFile);
		
		if (IsRunningOnCircleCI() && IsMainOrDevelop())
		{
			var repoToken = EnvironmentVariable(coverallsRepoToken);
			if (string.IsNullOrEmpty(repoToken))
			{
				throw new Exception(string.Format("Coveralls repo token not found. Set environment variable '{0}'", coverallsRepoToken));
			}

			Information(string.Format("Uploading test coverage to {0}", coverallsRepo));
			CoverallsNet(coverageSummaryFile, CoverallsNetReportType.OpenCover, new CoverallsNetSettings()
			{
				RepoToken = repoToken
			});
		}
		else
		{
			Information("We are not running on the build server so we won't publish the coverage report to coveralls.io");
		}

		var sequenceCoverage = XmlPeek(coverageSummaryFile, "//coverage/@line-rate");
		var branchCoverage = XmlPeek(coverageSummaryFile, "//coverage/@line-rate");

		Information("Sequence Coverage: " + sequenceCoverage);
	
		if(double.Parse(sequenceCoverage) < minCodeCoverage)
		{
			var whereToCheck = !IsRunningOnCircleCI() ? coverallsRepo : artifactsForUnitTestsDir;
			throw new Exception(string.Format("Code coverage fell below the threshold of {0}%. You can find the code coverage report at {1}", minCodeCoverage, whereToCheck));
		};
	});

Task("RunAcceptanceTests")
	.IsDependentOn("Compile")
	.Does(() =>
	{
		var settings = new DotNetTestSettings
		{
			Configuration = compileConfig,
			ArgumentCustomization = args => args
				.Append("--no-restore")
				.Append("--no-build")
		};

		EnsureDirectoryExists(artifactsForAcceptanceTestsDir);
		DotNetTest(acceptanceTestAssemblies, settings);
	});

Task("RunIntegrationTests")
	.IsDependentOn("Compile")
	.Does(() =>
	{
		var settings = new DotNetTestSettings
		{
			Configuration = compileConfig,
			ArgumentCustomization = args => args
				.Append("--no-restore")
				.Append("--no-build")
		};

		EnsureDirectoryExists(artifactsForIntegrationTestsDir);
		DotNetTest(integrationTestAssemblies, settings);
	});

Task("CreateArtifacts")
	.IsDependentOn("Compile")
	.Does(() => 
	{
		EnsureDirectoryExists(packagesDir);

		CopyFiles("./src/**/Release/Ocelot.*.nupkg", packagesDir);

		var projectFiles = GetFiles("./src/**/Release/Ocelot.*.nupkg");

		foreach(var projectFile in projectFiles)
		{
			System.IO.File.AppendAllLines(
				artifactsFile,
				new[] { projectFile.GetFilename().FullPath, "ReleaseNotes.md" }
			);
		}

		var artifacts = System.IO.File.ReadAllLines(artifactsFile)
			.Distinct();
		
		foreach(var artifact in artifacts)
		{
			var codePackage = packagesDir + File(artifact);

			Information("Created package " + codePackage);
		}
	});

Task("PublishGitHubRelease")
	.IsDependentOn("CreateArtifacts")
	.Does(() => 
	{
		if (IsRunningOnCircleCI())
		{
			var path = packagesDir.ToString() + @"/**/*";

			CreateGitHubRelease();

			foreach (var file in GetFiles(path))
			{
				UploadFileToGitHubRelease(file);
			}

			CompleteGitHubRelease();
		}
	});

Task("EnsureStableReleaseRequirements")
    .Does(() =>	
    {
		Information("Check if stable release...");

        if (!IsRunningOnCircleCI())
		{
           throw new Exception("Stable release should happen via circleci");
		}

		Information("Release is stable...");
    });

Task("DownloadGitHubReleaseArtifacts")
    .Does(() =>
    {

		try
		{
			// hack to let GitHub catch up, todo - refactor to poll
			System.Threading.Thread.Sleep(5000);

			EnsureDirectoryExists(packagesDir);

			var releaseUrl = tagsUrl + versioning.NuGetVersion;

        	var assets_url = Newtonsoft.Json.Linq.JObject.Parse(GetResource(releaseUrl))
				.Value<string>("assets_url");

			var assets = GetResource(assets_url);

			foreach(var asset in Newtonsoft.Json.JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JArray>(assets))
			{
				var file = packagesDir + File(asset.Value<string>("name"));
				DownloadFile(asset.Value<string>("browser_download_url"), file);
			}
		}
		catch(Exception exception)
		{
			Information("There was an exception " + exception);
			throw;
		}
    });

Task("PublishToNuget")
    .IsDependentOn("DownloadGitHubReleaseArtifacts")
    .Does(() =>
    {
		if (IsRunningOnCircleCI())
		{
			PublishPackages(packagesDir, artifactsFile, nugetFeedStableKey, nugetFeedStableUploadUrl, nugetFeedStableSymbolsUploadUrl);
		}
    });

RunTarget(target);

private void GenerateReport(Cake.Core.IO.FilePath coverageSummaryFile)
{
	var dir = System.IO.Directory.GetCurrentDirectory();
	Information(dir);

	var reportSettings = new ProcessArgumentBuilder();
	reportSettings.Append($"-targetdir:" + $"{dir}/{artifactsForUnitTestsDir}");
	reportSettings.Append($"-reports:" + coverageSummaryFile);

	var toolpath = Context.Tools.Resolve("net7.0/ReportGenerator.dll");
	Information($"Tool Path : {toolpath.ToString()}");

	DotNetExecute(toolpath, reportSettings);
}

/// Gets unique nuget version for this commit
private GitVersion GetNuGetVersionForCommit()
{
    GitVersion(new GitVersionSettings{
        UpdateAssemblyInfo = false,
        OutputType = GitVersionOutput.BuildServer
    });

    return GitVersion(new GitVersionSettings{ OutputType = GitVersionOutput.Json });
}

/// Updates project version in all of our projects
private void PersistVersion(string committedVersion, string newVersion)
{
	Information(string.Format("We'll search all csproj files for {0} and replace with {1}...", committedVersion, newVersion));

	var projectFiles = GetFiles("./**/*.csproj");

	foreach(var projectFile in projectFiles)
	{
		var file = projectFile.ToString();
 
		Information(string.Format("Updating {0}...", file));

		var updatedProjectFile = System.IO.File.ReadAllText(file)
			.Replace(committedVersion, newVersion);

		System.IO.File.WriteAllText(file, updatedProjectFile);
	}
}

/// Publishes code and symbols packages to nuget feed, based on contents of artifacts file
private void PublishPackages(ConvertableDirectoryPath packagesDir, ConvertableFilePath artifactsFile, string feedApiKey, string codeFeedUrl, string symbolFeedUrl)
{
		Information("PublishPackages");
        var artifacts = System.IO.File
            .ReadAllLines(artifactsFile)
			.Distinct();
		
		foreach(var artifact in artifacts)
		{
			if (artifact == "ReleaseNotes.md") 
			{
				continue;
			}

			var codePackage = packagesDir + File(artifact);

			Information("Pushing package " + codePackage);
			
			Information("Calling NuGetPush");

			DotNetNuGetPush(
				codePackage,
				new DotNetNuGetPushSettings {
					ApiKey = feedApiKey,
					Source = codeFeedUrl
				});
		}
}

private void CreateGitHubRelease()
{
	var json = $"{{ \"tag_name\": \"{versioning.NuGetVersion}\", \"target_commitish\": \"main\", \"name\": \"{versioning.NuGetVersion}\", \"body\": \"{ReleaseNotesAsJson()}\", \"draft\": true, \"prerelease\": true }}";
	
	var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

	using(var client = new System.Net.Http.HttpClient())
	{	
			client.DefaultRequestHeaders.Authorization = 
    new System.Net.Http.Headers.AuthenticationHeaderValue(
        "Basic", Convert.ToBase64String(
            System.Text.ASCIIEncoding.ASCII.GetBytes(
               $"{gitHubUsername}:{gitHubPassword}")));

		client.DefaultRequestHeaders.Add("User-Agent", "Ocelot Release");

		var result = client.PostAsync("https://api.github.com/repos/ThreeMammals/Ocelot/releases", content).Result;
		if(result.StatusCode != System.Net.HttpStatusCode.Created) 
		{
			throw new Exception("CreateGitHubRelease result.StatusCode = " + result.StatusCode);
		}
		var returnValue = result.Content.ReadAsStringAsync().Result;
		dynamic test = Newtonsoft.Json.JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(returnValue);
		releaseId = test.id;
	}
}

private string ReleaseNotesAsJson()
{
	return System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(System.IO.File.ReadAllText(releaseNotesFile));
}

private void UploadFileToGitHubRelease(FilePath file)
{
	var data = System.IO.File.ReadAllBytes(file.FullPath);
	var content = new System.Net.Http.ByteArrayContent(data);
	content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

	using(var client = new System.Net.Http.HttpClient())
	{	
			client.DefaultRequestHeaders.Authorization = 
    new System.Net.Http.Headers.AuthenticationHeaderValue(
        "Basic", Convert.ToBase64String(
            System.Text.ASCIIEncoding.ASCII.GetBytes(
               $"{gitHubUsername}:{gitHubPassword}")));

		client.DefaultRequestHeaders.Add("User-Agent", "Ocelot Release");

		var result = client.PostAsync($"https://uploads.github.com/repos/ThreeMammals/Ocelot/releases/{releaseId}/assets?name={file.GetFilename()}", content).Result;
		if(result.StatusCode != System.Net.HttpStatusCode.Created) 
		{
			throw new Exception("UploadFileToGitHubRelease result.StatusCode = " + result.StatusCode);
		}
	}
}

private void CompleteGitHubRelease()
{
	var json = $"{{ \"tag_name\": \"{versioning.NuGetVersion}\", \"target_commitish\": \"main\", \"name\": \"{versioning.NuGetVersion}\", \"body\": \"{ReleaseNotesAsJson()}\", \"draft\": false, \"prerelease\": false }}";
	var request = new System.Net.Http.HttpRequestMessage(new System.Net.Http.HttpMethod("Patch"), $"https://api.github.com/repos/ThreeMammals/Ocelot/releases/{releaseId}");
	request.Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");

	using(var client = new System.Net.Http.HttpClient())
	{	
			client.DefaultRequestHeaders.Authorization = 
    new System.Net.Http.Headers.AuthenticationHeaderValue(
        "Basic", Convert.ToBase64String(
            System.Text.ASCIIEncoding.ASCII.GetBytes(
               $"{gitHubUsername}:{gitHubPassword}")));

		client.DefaultRequestHeaders.Add("User-Agent", "Ocelot Release");

		var result = client.SendAsync(request).Result;
		if(result.StatusCode != System.Net.HttpStatusCode.OK) 
		{
			throw new Exception("CompleteGitHubRelease result.StatusCode = " + result.StatusCode);
		}
	}
}


/// gets the resource from the specified url
private string GetResource(string url)
{
	try
	{
		Information("Getting resource from " + url);

		var assetsRequest = System.Net.WebRequest.CreateHttp(url);
		assetsRequest.Method = "GET";
		assetsRequest.Accept = "application/vnd.github.v3+json";
		assetsRequest.UserAgent = "BuildScript";

		using (var assetsResponse = assetsRequest.GetResponse())
		{
			var assetsStream = assetsResponse.GetResponseStream();
			var assetsReader = new StreamReader(assetsStream);
			var response =  assetsReader.ReadToEnd();

			Information("Response is " + response);
			
			return response;
		}
	}
	catch(Exception exception)
	{
		Information("There was an exception " + exception);
		throw;
	}
}

private bool IsRunningOnCircleCI()
{
    return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CIRCLECI"));
}

private bool IsMainOrDevelop()
{
	var env = Environment.GetEnvironmentVariable("CIRCLE_BRANCH").ToLower();

	if(env == "main") 
	{
		return true;
	}

	if(env == "develop") 
	{
		return true;
	}

    return false;
}
