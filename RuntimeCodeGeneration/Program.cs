using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RuntimeCodeGeneration
{
    public interface IConfig
    {
        public static abstract bool Option1 { get; }
    }

    public readonly struct DefaultConfig: IConfig
    {
        public static bool Option1 => true;
    }

    public static class Consumer<ConfigT> where ConfigT: IConfig
    {
        private static readonly bool OPTION_1 = ConfigT.Option1;

        // For demonstration purposes, just promote it straight to T1
        // The class constructor will be pre-ran to avoid regressing codegen
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static int DoWork()
        {
            return OPTION_1 ? 1 : 0;
        }
    }

    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            // Viewing JIT disassembly:

            // Mac:
            // export DOTNET_JitDisasm="*DoWork*"
            // Windows:
            // $Env:DOTNET_JitDisasm="*DoWork*"

            // Then run the application:
            // dotnet run -c Release

            const string CONFIG_NAME = "GeneratedConfig";

            var option1 = true;

            var code =
            $$"""
            public readonly struct {{CONFIG_NAME}}: RuntimeCodeGeneration.IConfig
            {
                public static bool Option1 => {{option1.ToString().ToLower()}};
            }
            """;

            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            var refs = AppDomain
                .CurrentDomain
                .GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>();

            var compilation = CSharpCompilation.Create(
                assemblyName: "GeneratedConfigAssembly",
                syntaxTrees: [ syntaxTree ],
                references: refs,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            var dllFilePath = $"{AppContext.BaseDirectory}/GeneratedConfig.dll";

            await using (var dllStream = File.Create(dllFilePath))
            {
                var emitResult = compilation.Emit(
                    peStream: dllStream
                );

                if (!emitResult.Success)
                {
                    // Dump diagnostics if compilation failed
                    foreach (var diag in emitResult.Diagnostics)
                    {
                        Console.Error.WriteLine(diag);
                    }

                    return;
                }
            }

            var defaultAssemblyLoadContext = AssemblyLoadContext.Default;

            var configAssembly = defaultAssemblyLoadContext.LoadFromAssemblyPath(dllFilePath);

            Console.WriteLine($"List of assembly load contexts: {string.Join(", ", AssemblyLoadContext.All.Select(x => x.Name))}\n");

            var configType = configAssembly.GetType(CONFIG_NAME)!;

            Console.WriteLine(
            $"""
            Name of generated assembly: {configAssembly.FullName}
            
            Its load context: {AssemblyLoadContext.GetLoadContext(configAssembly)!.Name}
            
            Is it a collectable assembly? {configAssembly.IsCollectible}
            
            It it a dynamic assembly? {configAssembly.IsDynamic}

            """);

            var consumerTypeUnbounded = typeof(Consumer<>);

            RuntimeHelpers.RunClassConstructor(consumerTypeUnbounded.TypeHandle);

            var generatedConsumerType = consumerTypeUnbounded.MakeGenericType(configType);

            RuntimeHelpers.RunClassConstructor(generatedConsumerType.TypeHandle);

            var defaultConsumerType = typeof(Consumer<DefaultConfig>);

            RuntimeHelpers.RunClassConstructor(defaultConsumerType.TypeHandle);

            var doWorkMethod = generatedConsumerType.GetMethod(
                name: nameof(Consumer<DefaultConfig>.DoWork),
                bindingAttr: BindingFlags.Static | BindingFlags.Public
            )!;

            unsafe
            {
                if (true)
                {
                    var doWorkGeneratedFP = (delegate*<int>) doWorkMethod
                        .MethodHandle
                        .GetFunctionPointer();

                    Console.WriteLine($"Result ( Generated ) - {doWorkGeneratedFP()}");
                }

                if (true)
                {
                    var doWorkDefaultFP = (delegate*<int>) typeof(Consumer<DefaultConfig>)
                        .GetMethod(nameof(Consumer<DefaultConfig>.DoWork))!
                        .MethodHandle
                        .GetFunctionPointer();

                    Console.WriteLine($"Result ( Default ) - {doWorkDefaultFP()}");
                }
            }
        }
    }
}