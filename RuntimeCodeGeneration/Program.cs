using System.Reflection;
using System.Runtime.CompilerServices;
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

    public static class Consumer<ConfigT> where ConfigT : IConfig
    {
        // For demonstration purposes, just promote it straight to T1
        // The class constructor will be pre-ran to avoid regressing codegen
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static int DoWork()
        {
            return ConfigT.Option1 ? 1 : 0;
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

            var configType = configAssembly.GetType(CONFIG_NAME)!;

            Console.WriteLine(
            $"""
            Name of assembly: {configAssembly.FullName}
            
            Is it a collectable assembly? {configAssembly.IsCollectible}
            
            It it a dynamic assembly? {configAssembly.IsDynamic}
            """);

            var consumerType = typeof(Consumer<>).MakeGenericType(configType);

            RuntimeHelpers.RunClassConstructor(consumerType.TypeHandle);

            var doWorkMethod = consumerType.GetMethod(
                name: nameof(Consumer<DefaultConfig>.DoWork),
                bindingAttr: BindingFlags.Static | BindingFlags.Public
            )!;

            unsafe
            {
                var doWorkFP = (delegate*<int>) doWorkMethod
                    .MethodHandle
                    .GetFunctionPointer();

                Console.WriteLine($"Result ( Generated ) - {doWorkFP()}");
            }

            Console.WriteLine($"Result ( Default ) - {Consumer<DefaultConfig>.DoWork()}");
        }
    }
}