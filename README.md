## How to view disassembly

### MacOS

```bash
cd RuntimeCodeGeneration/
export DOTNET_JitDisasm="*DoWork*"
dotnet run -c Release
```

### Windows

```bash
cd RuntimeCodeGeneration/
$Env:DOTNET_JitDisasm="*DoWork*"
dotnet run -c Release
```