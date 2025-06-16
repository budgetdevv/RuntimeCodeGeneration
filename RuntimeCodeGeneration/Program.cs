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

        public static abstract bool Option2 { get; }
    }

    public readonly struct DefaultConfig: IConfig
    {
        public static bool Option1 => true;

        public static bool Option2 => true;
    }

    public static class Consumer<ConfigT> where ConfigT: IConfig
    {
        // For demonstration purposes, just promote it straight to T1
        // The class constructor will be pre-ran to avoid regressing codegen
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static int DoWork()
        {
            var isOne = ConfigT.Option1 && ConfigT.Option2;

            return isOne ? 1 : 0;
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

            const string
                ASSEMBLY_NAME = "RuntimeCodeGeneration",
                CONFIG_NAME = "GeneratedConfig";

            var option1 = true;

            var option2 = true;

            var configNamespace = typeof(IConfig).Namespace!;

            var code =
            $$"""
            using {{configNamespace}};
            
            public readonly struct {{CONFIG_NAME}}: {{nameof(IConfig)}}
            {
                public static bool {{nameof(IConfig.Option1)}} => {{option1.ToString().ToLower()}};
                
                public static bool {{nameof(IConfig.Option2)}} => {{option2.ToString().ToLower()}};
            }
            """;

            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            var assemblyReferences = AppDomain
                .CurrentDomain
                .GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>();

            var compilation = CSharpCompilation.Create(
                assemblyName: ASSEMBLY_NAME,
                syntaxTrees: [ syntaxTree ],
                references: assemblyReferences,
                options: new CSharpCompilationOptions(
                    outputKind: OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release
                )
            );

            using var memoryStream = new MemoryStream();

            var emitResult = compilation.Emit(memoryStream);

            if (!emitResult.Success)
            {
                // Dump diagnostics if compilation failed
                foreach (var diag in emitResult.Diagnostics)
                {
                    Console.Error.WriteLine(diag);
                }

                return;
            }

            memoryStream.Seek(0, SeekOrigin.Begin);

            var configAssembly = Assembly.Load(memoryStream.ToArray());

            var configType = configAssembly.GetType(CONFIG_NAME);

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