#r "Spawn.dll"

using Spawn;

public ProcessUtil.AsyncResult<ProcessUtil.ProcessResult> SpawnProcess(string file, string args = "") {
  return Spawn.ProcessUtil.Spawn(file, args);
}

public void Out(string value) {
  Console.WriteLine(value);
}
