#tool "nuget:?package=xunit.runner.console"

var target = Argument("target", "Default");
var buildDir = Directory("./build");
// this can be changed once we are pushing the nuget packages up
var nugetPackageOutput = buildDir + Directory("packages");
var configuration = Argument("configuration", "Release");

Task("Clean")
    .Does(() =>
{
    CleanDirectory(buildDir);
});

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    NuGetRestore("./Bugsnag.sln");
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
  MSBuild("./Bugsnag.sln", settings =>
    settings
      .WithProperty("BaseOutputPath", $"{MakeAbsolute(buildDir).FullPath}\\")
      .SetVerbosity(Verbosity.Minimal)
      .SetConfiguration(configuration));
});

Task("Test")
  .IsDependentOn("Build")
  .Does(() => {
    var testAssemblies = GetFiles($"{buildDir}/**/*.Tests.dll");
    XUnit2(testAssemblies,
      new XUnit2Settings {
          ArgumentCustomization = args => {
            if (AppVeyor.IsRunningOnAppVeyor)
            {
              args.Append("-appveyor");
            }
            return args;
          }
      });
  });

Task("Pack")
  .IsDependentOn("Test")
  .Does(() =>
{
  MSBuild("./Bugsnag.sln", settings =>
    settings
      .SetVerbosity(Verbosity.Minimal)
      .WithTarget("pack")
      .SetConfiguration(configuration)
      .WithProperty("IncludeSymbols", "true")
      .WithProperty("GenerateDocumentationFile", "true")
      .WithProperty("PackageOutputPath", MakeAbsolute(nugetPackageOutput).FullPath));
});

Task("PopulateExamplePackages")
  .IsDependentOn("Pack")
  .Does(() =>
{
      foreach (var directory in examples)
      {
          CopyDirectory(nugetPackageOutput, directory.Combine(new DirectoryPath("packages")));
      }
});

Task("BuildExamples")
  .IsDependentOn("PopulateExamplePackages")
  .Does(() =>
{
      var failures = examples.AsParallel().Select(e => {
        IEnumerable<string> stdOut;
        IEnumerable<string> errOut;
        var settings = new ProcessSettings { Arguments = "build", WorkingDirectory = e, RedirectStandardOutput = true, RedirectStandardError = true };
        var exitCode = StartProcess("docker-compose", settings, out stdOut, out errOut);
        Information("docker-compose build {0}", e);
        return new { ExitCode = exitCode, StdOutput = stdOut, ErrOutput = errOut, Example = e };
      }).Where(o => o.ExitCode != 0).ToArray();

      foreach (var failure in failures)
      {
        Error(failure.Example);
        foreach (var output in failure.StdOutput)
        {
          Error(output);
        }
        foreach (var output in failure.ErrOutput)
        {
          Error(output);
        }
      }

      if (failures.Any())
      {
        throw new Exception("Failed to build examples");
      }
});

Task("Default")
    .IsDependentOn("Test");

Task("Appveyor")
    .IsDependentOn("Pack");

RunTarget(target);
