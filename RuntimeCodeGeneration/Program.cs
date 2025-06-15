using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

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
        // The class construct will be pre-ran to avoid regressing codegen
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

            var option1 = true;

            var code =
            $$"""
            public readonly struct Config: IConfig
            {
                public static bool Option1 => {{option1.ToString().ToLower()}};
            }
            
            return typeof(Config);
            """;

            var options = ScriptOptions
                .Default
                .AddReferences(typeof(IConfig).Assembly)
                .AddImports("RuntimeCodeGeneration")
                .WithAllowUnsafe(true);

            var configType = await CSharpScript.EvaluateAsync<Type>(
                code: code,
                options: options
            )!;

            var configAssembly = configType.Assembly;

            Console.WriteLine($"Name of assembly: {configAssembly.FullName}");

            Console.WriteLine($"Is it a collectable assembly? {configAssembly.IsCollectible}");

            Console.WriteLine($"It it a dynamic assembly? {configAssembly.IsDynamic}");

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