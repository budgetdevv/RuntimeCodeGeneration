using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

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

            const string CONFIG_NAME = "GeneratedConfig";

            var option1 = true;

            var option2 = true;

            var code =
            $$"""
            public readonly struct Config: IConfig
            {
                public static bool Option1 => {{option1.ToString().ToLower()}};
                
                public static bool Option2 => {{option2.ToString().ToLower()}};
            }

            return typeof(Config);
            """;

            var options = ScriptOptions
                .Default
                .AddReferences(typeof(IConfig).Assembly)
                .AddImports("RuntimeCodeGeneration")
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithAllowUnsafe(true);

            var configType = await CSharpScript.EvaluateAsync<Type>(
                code: code,
                options: options
            )!;

            var configAssembly = configType.Assembly;

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