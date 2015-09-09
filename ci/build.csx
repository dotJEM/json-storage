#load "util.csx";


Out(SpawnProcess("test.bat").Result.Output);

SpawnProcess(@"C:\Program Files (x86)\MSBuild\14.0\Bin\MsBuild.exe").Then(result => Out(result.Output));

// using static Spawn.ProcessUtil;
// Spawn("test.bat");
// Console.WriteLine(Spawn.ProcessUtil.Spawn("test.bat").Result.Output);
// ProjectCollection project = new ProjectCollection();
// GlobalProperty.Add("Configuration", "Release");
// BuildRequestData request = new BuildRequestData("DotJEM.Json.Storage.sln", GlobalProperty, null, new string[] { "Build" }, null);
// BuildResult result = BuildManager.DefaultBuildManager.Build(new BuildParameters(pc), request);
// Console.WriteLine(result.ToString());