using Ruvents.Build;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

if (args.Length != 5)
{
    await Console.Error.WriteLineAsync("Expected: project-directory root-namespace define-constants components-manifest compile-manifest");
    return 2;
}

var validator = new ValidateRazorCodeBehind
{
    ProjectDirectory = args[0],
    RootNamespace = args[1],
    DefineConstants = args[2],
    Components = (await File.ReadAllLinesAsync(args[3], cancellation.Token)).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray(),
    CompileFiles = (await File.ReadAllLinesAsync(args[4], cancellation.Token)).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray(),
};

return validator.Execute(cancellation.Token) ? 0 : 1;
